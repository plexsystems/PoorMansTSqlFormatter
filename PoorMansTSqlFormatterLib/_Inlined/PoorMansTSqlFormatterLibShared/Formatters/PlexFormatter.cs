using System;
using System.Collections.Generic;
using System.Text;
using PoorMansTSqlFormatterLib.ParseStructure;
using PoorMansTSqlFormatterLib.Interfaces;
using System.Linq;

namespace PoorMansTSqlFormatterLib.Formatters
{
  public static class PlexFormatter
  {
    private static List<string> sqlClauses = new List<string>()
    {
      SqlStructureConstants.ENAME_SQL_CLAUSE,
      SqlStructureConstants.ENAME_SQL_STATEMENT
    };
    private class ProcessState
    {
      public bool IsTransactionIsolationSet { get; set; }
      public bool ScriptOutsideOfProcedure { get; set; }
    }

    public static void PreProcessSQLTree(ref Node parentNode)
    {
      ProcessState state = new ProcessState();
      var childList = new List<Node>(parentNode.Children);
      PreProcessChildren(ref childList, state);
      //if (state.ScriptOutsideOfProcedure && !state.IsTransactionIsolationSet)
      //{
      //  parentNode.InsertChildAtIndex(CreateIsolationNode(), 0);
      //}
    }
    private static void PreProcessChildren(ref List<Node> children, ProcessState state)
    {
      int index = 0;
      while (index >= 0)
      {
        if (children.Count <= index)
        {
          break;
        }
        var childItem = children[index];
        var childList = new List<Node>(childItem.Children);
        switch (childItem.Parent.Name)
        {
          case SqlStructureConstants.ENAME_SQL_CLAUSE:
            if (childItem.TextValue.Equals("TOP", StringComparison.CurrentCultureIgnoreCase))
            {
              ProcessTopClause(childItem);
            }
            break;
        }

        switch (childItem.Name)
        {
          case SqlStructureConstants.ENAME_IF_STATEMENT:
            ValidateIfReferencesHaveBeginBlock(childItem);
            break;
          case SqlStructureConstants.ENAME_OTHEROPERATOR:
            if (childItem.TextValue.Equals("<>"))
            {
              childItem.TextValue = "!=";
            }
            break;
          case SqlStructureConstants.ENAME_JOIN_ON_SECTION:
            ValidateJoinReferences(childItem);
            break;
          case SqlStructureConstants.ENAME_DDL_PROCEDURAL_BLOCK:
            ValidateTransactionIsolation(childItem);
            break;
          case SqlStructureConstants.ENAME_SQL_STATEMENT:
            if (!state.ScriptOutsideOfProcedure && childItem.Parent.Name.Equals(SqlStructureConstants.ENAME_SQL_ROOT))
            {
              if (childItem.ChildrenByNames(sqlClauses).Any())
              {
                state.ScriptOutsideOfProcedure = true;
              }
            }
            if (state.ScriptOutsideOfProcedure && !state.IsTransactionIsolationSet)
            {
              if (IsTransactionNode(childItem))
              {
                state.IsTransactionIsolationSet = true;
              }
            }
            break;
          default:
            break;
        }
        if (childList.Count > 0)
        {
          PreProcessChildren(ref childList, state);
        }
        index++;
      }
    }
    private static void ValidateIfReferencesHaveBeginBlock(Node topElement)
    {
      var ifSqlClause = topElement.ChildByName(SqlStructureConstants.ENAME_CONTAINER_SINGLESTATEMENT);
      if (ifSqlClause != null)
      {
        var sqlStmnt = ifSqlClause.ChildByName(SqlStructureConstants.ENAME_SQL_STATEMENT);
        if (sqlStmnt != null)
        {
          var sqlClause = sqlStmnt.ChildByName(SqlStructureConstants.ENAME_SQL_CLAUSE);
          //If there is no begin/end block adjust structure so that there is...
          var beginEnd = sqlClause.ChildByName(SqlStructureConstants.ENAME_BEGIN_END_BLOCK);
          if (beginEnd == null)
          {
            Node newStmnt = new NodeImpl()
            {
              Name = SqlStructureConstants.ENAME_SQL_STATEMENT,
              TextValue = string.Empty,
            };
            Node newClause = new NodeImpl()
            {
              Name = SqlStructureConstants.ENAME_SQL_CLAUSE,
              TextValue = string.Empty
            };
            Node beginEndBlock = new NodeImpl()
            {
              Name = SqlStructureConstants.ENAME_BEGIN_END_BLOCK,
              TextValue = ""
            };
            Node containerOpen = new NodeImpl()
            {
              Name = SqlStructureConstants.ENAME_CONTAINER_OPEN,
              TextValue = string.Empty
            };

            Node multiStmnt = new NodeImpl()
            {
              Name = SqlStructureConstants.ENAME_CONTAINER_MULTISTATEMENT,
              TextValue = string.Empty
            };
            Node containerClose = new NodeImpl()
            {
              Name = SqlStructureConstants.ENAME_CONTAINER_CLOSE,
              TextValue = string.Empty
            };

            containerOpen.AddChild(new NodeImpl()
            {
              Name = SqlStructureConstants.ENAME_OTHERKEYWORD,
              TextValue = "BEGIN"
            });

            containerClose.AddChild(new NodeImpl()
            {
              Name = SqlStructureConstants.ENAME_OTHERKEYWORD,
              TextValue = "END"
            });

            newStmnt.AddChild(newClause);
            newClause.AddChild(beginEndBlock);


            beginEndBlock.AddChild(containerOpen);
            beginEndBlock.AddChild(multiStmnt);
            beginEndBlock.AddChild(containerClose);

            sqlStmnt.Parent.InsertChildBefore(newStmnt, sqlStmnt);
            sqlStmnt.Parent.RemoveChild(sqlStmnt);

            multiStmnt.AddChild(sqlStmnt);

          }
        }
      }
    }
    private static void ValidateJoinReferences(Node topElement)
    {
      var joinBody = topElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_GENERALCONTENT).FirstOrDefault();
      Node selector = null;
      Node joinType = null;
      foreach (var joinInfo in topElement.Parent.Children)
      {
        if (joinInfo.Name.Equals(SqlStructureConstants.ENAME_SELECTIONTARGET) && selector == null)
        {
          selector = joinInfo;
        }
        else if (joinInfo.Name.Equals(SqlStructureConstants.ENAME_COMPOUNDKEYWORD) && joinType == null)
        {
          joinType = joinInfo;
        }
      }
      if (selector != null)
      {
        var asClause = selector.Children.Where(x => x.TextValue.Equals("AS")).FirstOrDefault();
        if (asClause != null)
        {
          bool notFound = true;
          Node parentAlias = asClause;
          while (notFound)
          {
            parentAlias = parentAlias.NextSibling();
            if (parentAlias == null)
            {
              break;
            }
            if (parentAlias.Name.Equals(SqlStructureConstants.ENAME_OTHERNODE))
            {
              break;
            }
          }

          if (joinBody != null && parentAlias != null)
          {
            //Validate Join Table Order
            ValidateJoinReferenceRules(joinBody, parentAlias, joinType);
          }
        }
      }
    }

    /// <summary>
    /// left outer join leave alone
    /// right outer join throw error - put warning in sql
    /// inner joins order clauses with parent alias field firest
    /// </summary>
    /// <param name="joinBody"></param>
    /// <param name="parentAlias"></param>
    private static void ValidateJoinReferenceRules(Node joinBody, Node parentAlias, Node joinType)
    {
      bool innerJoin = true;
      bool leftJoin = true;
      foreach (var joinclause in joinType.Children)
      {
        if (joinclause.TextValue.Equals("OUTER"))
        {
          innerJoin = false;
        }
        else if (joinclause.TextValue.Equals("RIGHT"))
        {
          leftJoin = false;
        }
      }
      if (!innerJoin)
      {
        if (!leftJoin)
        {
          Node errorMeswsage = new NodeImpl()
          {
            Name = SqlStructureConstants.ENAME_COMMENT_SINGLELINE,
            TextValue = "WARNING! Right Outer Join Not Permitted"
          };
          joinType.Parent.InsertChildBefore(errorMeswsage, joinType);
        }
      }
      else
      {
        ValidateJoinTableOrder(joinBody, parentAlias);
      }
    }
    private static void ValidateJoinTableOrder(Node joinBody, Node parentAlias)
    {
      List<Node> firstClause = new List<Node>();
      List<Node> secondClause = new List<Node>();
      bool nextClause = false;
      foreach (var clause in joinBody.Children)
      {
        if (!nextClause)
        {
          if (clause.Name.Equals(SqlStructureConstants.ENAME_OTHERNODE))
          {
            firstClause.Add(clause);
          }
          else if (clause.Name.Equals(SqlStructureConstants.ENAME_EQUALSSIGN))
          {
            nextClause = true;
          }
        }
        else
        {
          if (clause.Name.Equals(SqlStructureConstants.ENAME_OTHERNODE))
          {
            secondClause.Add(clause);
          }
          else if (secondClause.Count > 0 && clause.Name.Equals(SqlStructureConstants.ENAME_WHITESPACE))
          {
            nextClause = false;
            //Process Clauses if they both have an alias and value
            if (secondClause.Count == 2 && firstClause.Count == 2)
            {
              //switch Order
              if (!firstClause[0].TextValue.Equals(parentAlias.TextValue))
              {
                var firstAlias = firstClause[0].TextValue;
                var firstValue = firstClause[1].TextValue;
                firstClause[0].TextValue = secondClause[0].TextValue;
                firstClause[1].TextValue = secondClause[1].TextValue;
                secondClause[0].TextValue = firstAlias;
                secondClause[1].TextValue = firstValue;
              }
            }
            firstClause.Clear();
            secondClause.Clear();
          }

        }
      }
    }
    private static void ValidateTransactionIsolation(Node topElement)
    {
      //Check for Procs;
      var ddlBlock = new List<Node>(topElement.ChildrenByName(SqlStructureConstants.ENAME_DDL_AS_BLOCK));
      if (ddlBlock.Count > 0)
      {
        var container = new List<Node>(ddlBlock[0].ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_GENERALCONTENT));
        //Check if a set transaction isolation level exists
        if (container.Count > 0)
        {
          foreach (var sqlStatement in container[0].Children)
          {
            if (IsTransactionNode(sqlStatement))
            {
              return;
            }
          }
          //Not found add it into process
          container[0].InsertChildAtIndex(CreateIsolationNode(), 0);
        }
      }
    }
    private static bool IsTransactionNode(Node transElement)
    {
      foreach (var sqlClause in transElement.ChildrenByName(SqlStructureConstants.ENAME_SQL_CLAUSE))
      {
        int keywordCount = 0;
        foreach (var setClause in sqlClause.Children)
        {
          switch (setClause.TextValue.ToUpper())
          {
            case "SET":
              keywordCount++;
              break;
            case "TRANSACTION":
              keywordCount++;
              break;
            case "ISOLATION":
              keywordCount++;
              break;
            case "LEVEL":
              keywordCount++;
              break;
            case "READ":
              keywordCount++;
              break;
            case "UNCOMMITTED":
              keywordCount++;
              break;
          }
          if (keywordCount == 6)
          {
            return true;
          }
        }
      }
      return false;
    }
    private static Node CreateIsolationNode()
    {
      Node setContainer = new NodeImpl()
      {
        Name = SqlStructureConstants.ENAME_SQL_STATEMENT
      };

      setContainer.InsertChildAtIndex(new NodeImpl()
      {
        Name = SqlStructureConstants.ENAME_SEMICOLON,
        TextValue = ";"
      }, 0);
      setContainer.InsertChildAtIndex(new NodeImpl()
      {
        Name = SqlStructureConstants.ENAME_OTHERKEYWORD,
        TextValue = "UNCOMMITTED"
      }, 0);
      setContainer.InsertChildAtIndex(new NodeImpl()
      {
        Name = SqlStructureConstants.ENAME_OTHERKEYWORD,
        TextValue = "READ"
      }, 0);
      setContainer.InsertChildAtIndex(new NodeImpl()
      {
        Name = SqlStructureConstants.ENAME_OTHERKEYWORD,
        TextValue = "LEVEL"
      }, 0);
      setContainer.InsertChildAtIndex(new NodeImpl()
      {
        Name = SqlStructureConstants.ENAME_OTHERKEYWORD,
        TextValue = "ISOLATION"
      }, 0);
      setContainer.InsertChildAtIndex(new NodeImpl()
      {
        Name = SqlStructureConstants.ENAME_OTHERKEYWORD,
        TextValue = "TRANSACTION"
      }, 0);
      setContainer.InsertChildAtIndex(new NodeImpl()
      {
        Name = SqlStructureConstants.ENAME_OTHERKEYWORD,
        TextValue = "SET"
      }, 0);


      return setContainer;
    }
    private static void ProcessTopClause(Node topElement)
    {
      var siblingsAfterTop = topElement.SiblingsAfterChild(topElement);
      Node numberNode = null;
      Node parenNode = null;
      foreach (var topSibling in siblingsAfterTop)
      {
        if (topSibling.Name.Equals(SqlStructureConstants.ENAME_EXPRESSION_PARENS))
        {
          parenNode = topSibling;
          break;
        }
        if (topSibling.Name.Equals(SqlStructureConstants.ENAME_NUMBER_VALUE))
        {
          numberNode = topSibling;
        }
      }
      if (numberNode != null && parenNode == null)
      {
        Node newChild = new NodeImpl()
        {
          Name = numberNode.Name,
          TextValue = numberNode.TextValue
        };

        numberNode.Name = SqlStructureConstants.ENAME_EXPRESSION_PARENS;
        numberNode.TextValue = string.Empty;
        numberNode.InsertChildAtIndex(newChild, 0);
      }
    }
  }
}
