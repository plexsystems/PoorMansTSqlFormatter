/*
Poor Man's T-SQL Formatter - a small free Transact-SQL formatting 
library for .Net 2.0 and JS, written in C#. 
Copyright (C) 2011-2017 Tao Klerks

Additional Contributors:
 * Timothy Klenke, 2012

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PoorMansTSqlFormatterLib.Interfaces;
using PoorMansTSqlFormatterLib.ParseStructure;

namespace PoorMansTSqlFormatterLib.Formatters
{
    public class TSqlStandardFormatter : ISqlTreeFormatter
    {
        public TSqlStandardFormatter() : this(new TSqlStandardFormatterOptions()) { }
        
        public TSqlStandardFormatter(TSqlStandardFormatterOptions options)
        {
            if (options == null)
                throw new ArgumentNullException("options");

            Options = options;

            if (options.KeywordStandardization)
                KeywordMapping = StandardKeywordRemapping.Instance;
            ErrorOutputPrefix = MessagingConstants.FormatErrorDefaultMessage + Environment.NewLine;
        }
        public bool Debug { get; set; } // New
        [Obsolete("Use the constructor with the TSqlStandardFormatterOptions parameter")]
        public TSqlStandardFormatter(string indentString, int spacesPerTab, int maxLineWidth, bool expandCommaLists, bool trailingCommas, bool spaceAfterExpandedComma, bool expandBooleanExpressions, bool expandCaseStatements, bool expandBetweenConditions, bool breakJoinOnSections, bool uppercaseKeywords, bool htmlColoring, bool keywordStandardization)
        {
            Options = new TSqlStandardFormatterOptions
                {
                    IndentString = indentString,
                    SpacesPerTab = spacesPerTab,
                    MaxLineWidth = maxLineWidth,
                    ExpandCommaLists = expandCommaLists,
                    TrailingCommas = trailingCommas,
                    SpaceAfterExpandedComma = spaceAfterExpandedComma,
                    ExpandBooleanExpressions = expandBooleanExpressions,
                    ExpandBetweenConditions = expandBetweenConditions,
                    ExpandCaseStatements = expandCaseStatements,
                    UppercaseKeywords = uppercaseKeywords,
                    BreakJoinOnSections = breakJoinOnSections,
                    HTMLColoring = htmlColoring,
                    KeywordStandardization = keywordStandardization
                };

            if (keywordStandardization)
                KeywordMapping = StandardKeywordRemapping.Instance;
            ErrorOutputPrefix = MessagingConstants.FormatErrorDefaultMessage + Environment.NewLine;
        }
        
        public TSqlStandardFormatterOptions Options { get; private set; }

        public IDictionary<string, string> KeywordMapping = new Dictionary<string, string>();

        [Obsolete("Use Options.IndentString instead")]
        public string IndentString { get { return Options.IndentString; } set { Options.IndentString = value; } }
        [Obsolete("Use Options.SpacesPerTab instead")]
        public int SpacesPerTab { get { return Options.SpacesPerTab; } set { Options.SpacesPerTab = value; } }
        [Obsolete("Use Options.MaxLineWidth instead")]
        public int MaxLineWidth { get { return Options.MaxLineWidth; } set { Options.MaxLineWidth = value; } }
        [Obsolete("Use Options.ExpandCommaLists instead")]
        public bool ExpandCommaLists { get { return Options.ExpandCommaLists; } set { Options.ExpandCommaLists = value; } }
        [Obsolete("Use Options.TrailingCommas instead")]
        public bool TrailingCommas { get { return Options.TrailingCommas; } set { Options.TrailingCommas = value; } }
        [Obsolete("Use Options.SpaceAfterExpandedComma instead")]
        public bool SpaceAfterExpandedComma { get { return Options.SpaceAfterExpandedComma; } set { Options.SpaceAfterExpandedComma = value; } }
        [Obsolete("Use Options.ExpandBooleanExpressions instead")]
        public bool ExpandBooleanExpressions { get { return Options.ExpandBooleanExpressions; } set { Options.ExpandBooleanExpressions = value; } }
        [Obsolete("Use Options.ExpandBetweenConditions instead")]
        public bool ExpandCaseStatements { get { return Options.ExpandCaseStatements; } set { Options.ExpandCaseStatements = value; } }
        [Obsolete("Use Options.ExpandCaseStatements instead")]
        public bool ExpandBetweenConditions { get { return Options.ExpandBetweenConditions; } set { Options.ExpandBetweenConditions = value; } }
        [Obsolete("Use Options.UppercaseKeywords instead")]
        public bool UppercaseKeywords { get { return Options.UppercaseKeywords; } set { Options.UppercaseKeywords = value; } }
        [Obsolete("Use Options.BreakJoinOnSections instead")]
        public bool BreakJoinOnSections { get { return Options.BreakJoinOnSections; } set { Options.BreakJoinOnSections = value; } }
        [Obsolete("Use Options.HTMLColoring instead")]
        public bool HTMLColoring { get { return Options.HTMLColoring; } set { Options.HTMLColoring = value; } }

        public bool HTMLFormatted { get { return Options.HTMLColoring; } }
        public string ErrorOutputPrefix { get; set; }

        public string FormatSQLTree(Node sqlTreeDoc)
        {
            //thread-safe - each call to FormatSQLTree() gets its own independent state object
            TSqlStandardFormattingState state = new TSqlStandardFormattingState(Options.HTMLColoring, Options.IndentString, Options.SpacesPerTab, Options.MaxLineWidth, 0);
            
            PlexFormatter.PreProcessSQLTree(ref sqlTreeDoc); // New
            
            if (sqlTreeDoc.Name == SqlStructureConstants.ENAME_SQL_ROOT && sqlTreeDoc.GetAttributeValue(SqlStructureConstants.ANAME_ERRORFOUND) == "1")
                state.AddOutputContent(ErrorOutputPrefix);

            ProcessSqlNodeList(sqlTreeDoc.Children, state);

            if (state.StatementEndCharacterExpected && state.CurrentLineLength > 0) // New
            {
              state.AddOutputContent(";");
              state.StatementEndCharacterExpected = false;
            }

            WhiteSpace_BreakAsExpected(state);

            //someone forgot to close a "[noformat]" or "[minify]" region... we'll assume that's ok
            if (state.SpecialRegionActive == SpecialRegionType.NoFormat)
            {
                Node skippedXml = NodeExtensions.ExtractStructureBetween(state.RegionStartNode, sqlTreeDoc);
                TSqlIdentityFormatter tempFormatter = new TSqlIdentityFormatter(Options.HTMLColoring);
                state.AddOutputContentRaw(tempFormatter.FormatSQLTree(skippedXml));
            }
            else if (state.SpecialRegionActive == SpecialRegionType.Minify)
            {
                Node skippedXml = NodeExtensions.ExtractStructureBetween(state.RegionStartNode, sqlTreeDoc);
                TSqlObfuscatingFormatter tempFormatter = new TSqlObfuscatingFormatter();
                if (HTMLFormatted)
                    state.AddOutputContentRaw(Utils.HtmlEncode(tempFormatter.FormatSQLTree(skippedXml)));
                else
                    state.AddOutputContentRaw(tempFormatter.FormatSQLTree(skippedXml));
            }
            return state.DumpOutput();
        }

        private void ProcessSqlNodeList(IEnumerable<Node> rootList, TSqlStandardFormattingState state)
        {
            var myList = new List<Node>(rootList); // New
            foreach (Node contentElement in myList)
            {
              ProcessSqlNode(contentElement, state);
              if (state.SiblingsProcessed) // New
              {
                state.SiblingsProcessed = false;
                break;
              }
            }
        }

        private void ProcessSqlNode(Node contentElement, TSqlStandardFormattingState state)
        {
            int initialIndent = state.IndentLevel;

            if (contentElement.GetAttributeValue(SqlStructureConstants.ANAME_HASERROR) == "1")
                state.OpenClass(SqlHtmlConstants.CLASS_ERRORHIGHLIGHT);
            bool addOutputSpace = true; // New
            switch (contentElement.Name)
            {
              case SqlStructureConstants.ENAME_SQL_STATEMENT:
                WhiteSpace_SeparateStatements(contentElement, state);
                state.ResetKeywords();
                ProcessSqlNodeList(contentElement.Children, state);
                state.StatementBreakExpected = true;
                break;

              case SqlStructureConstants.ENAME_SQL_CLAUSE:
                state.UnIndentInitialBreak = true;
                state.StatementEndCharacterExpected = true; // New
                ProcessSqlNodeList(contentElement.Children, state.IncrementIndent());
                state.DecrementIndent();
                if (Options.NewClauseLineBreaks > 0)
                  state.BreakExpected = true;
                if (Options.NewClauseLineBreaks > 1)
                  state.AdditionalBreaksExpected = Options.NewClauseLineBreaks - 1;
                break;

              case SqlStructureConstants.ENAME_SET_OPERATOR_CLAUSE:
                state.DecrementIndent();
                state.WhiteSpace_BreakToNextLine(); //this is the one already recommended by the start of the clause
                state.WhiteSpace_BreakToNextLine(); //this is the one we additionally want to apply
                ProcessSqlNodeList(contentElement.Children, state.IncrementIndent());
                state.BreakExpected = true;
                state.AdditionalBreaksExpected = 1;
                break;

              case SqlStructureConstants.ENAME_BATCH_SEPARATOR:
                //newline regardless of whether previous element recommended a break or not.
                if (state.StatementEndCharacterExpected && state.CurrentLineLength > 0) // New
                {
                  state.AddOutputContent(";");
                  state.StatementEndCharacterExpected = false;
                }

                state.WhiteSpace_BreakToNextLine();
                ProcessSqlNodeList(contentElement.Children, state);
                state.BreakExpected = true;
                break;

              case SqlStructureConstants.ENAME_DDL_PROCEDURAL_BLOCK:
              case SqlStructureConstants.ENAME_DDL_OTHER_BLOCK:
              case SqlStructureConstants.ENAME_DDL_DECLARE_BLOCK:
                ProcessSqlNodeList(contentElement.Children, state); // New
                break;
              case SqlStructureConstants.ENAME_CURSOR_DECLARATION:
              case SqlStructureConstants.ENAME_BEGIN_TRANSACTION:
              case SqlStructureConstants.ENAME_SAVE_TRANSACTION:
              case SqlStructureConstants.ENAME_COMMIT_TRANSACTION:
              case SqlStructureConstants.ENAME_ROLLBACK_TRANSACTION:
              case SqlStructureConstants.ENAME_CONTAINER_OPEN:
              case SqlStructureConstants.ENAME_CONTAINER_CLOSE:
                if (contentElement.Parent.Name == SqlStructureConstants.ENAME_CASE_STATEMENT) // New
                {
                  state.DecrementIndent();
                  ProcessSqlNodeList(contentElement.Children, state);
                  state.IncrementIndent();
                }
                else
                {
                  if (contentElement.Parent.Name == SqlStructureConstants.ENAME_DDL_WITH_CLAUSE)
                  {
                    
                  }
                  ProcessSqlNodeList(contentElement.Children, state);
                }

                break;

              case SqlStructureConstants.ENAME_WHILE_LOOP:
              case SqlStructureConstants.ENAME_IF_STATEMENT:
              case SqlStructureConstants.ENAME_SELECTIONTARGET:
              case SqlStructureConstants.ENAME_CONTAINER_GENERALCONTENT:
              case SqlStructureConstants.ENAME_CTE_WITH_CLAUSE:
              case SqlStructureConstants.ENAME_PERMISSIONS_BLOCK:
              case SqlStructureConstants.ENAME_PERMISSIONS_DETAIL:
              case SqlStructureConstants.ENAME_MERGE_CLAUSE:
              case SqlStructureConstants.ENAME_MERGE_TARGET:
                ProcessSqlNodeList(contentElement.Children, state);
                break;

              case SqlStructureConstants.ENAME_CASE_INPUT:
              case SqlStructureConstants.ENAME_BOOLEAN_EXPRESSION:
              case SqlStructureConstants.ENAME_BETWEEN_LOWERBOUND:
              case SqlStructureConstants.ENAME_BETWEEN_UPPERBOUND:
                WhiteSpace_SeparateWords(state, contentElement); // New
                ProcessSqlNodeList(contentElement.Children, state);
                break;

              case SqlStructureConstants.ENAME_CONTAINER_SINGLESTATEMENT:
              case SqlStructureConstants.ENAME_CONTAINER_MULTISTATEMENT:
              case SqlStructureConstants.ENAME_MERGE_ACTION:

                bool singleStatementIsIf = false;
                foreach (Node statement in contentElement.ChildrenByName(SqlStructureConstants.ENAME_SQL_STATEMENT))
                foreach (Node clause in statement.ChildrenByName(SqlStructureConstants.ENAME_SQL_CLAUSE))
                foreach (Node ifStatement in clause.ChildrenByName(SqlStructureConstants.ENAME_IF_STATEMENT))
                  singleStatementIsIf = true;

                if (singleStatementIsIf && contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_ELSE_CLAUSE))
                {
                  //artificially decrement indent and skip new statement break for "ELSE IF" constructs
                  state.DecrementIndent();
                }
                else
                {
                  state.BreakExpected = true;
                }

                ProcessSqlNodeList(contentElement.Children, state);
                if (singleStatementIsIf && contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_ELSE_CLAUSE))
                {
                  //bring indent back to symmetrical level
                  state.IncrementIndent();
                }

                state.StatementBreakExpected =
                  false; //the responsibility for breaking will be with the OUTER statement; there should be no consequence propagating out from statements in this container;
                state.UnIndentInitialBreak =
                  false; //if there was no word spacing after the last content statement's clause starter, doesn't mean the unIndent should propagate to the following content!
                break;

              case SqlStructureConstants.ENAME_PERMISSIONS_TARGET:
              case SqlStructureConstants.ENAME_PERMISSIONS_RECIPIENT:
              case SqlStructureConstants.ENAME_DDL_WITH_CLAUSE:
              case SqlStructureConstants.ENAME_MERGE_CONDITION:
              case SqlStructureConstants.ENAME_MERGE_THEN:
                if (false)
                {
                  state.BreakExpected = true;
                  state.UnIndentInitialBreak = true;
                  ProcessSqlNodeList(contentElement.Children, state.IncrementIndent());
                  state.DecrementIndent();
                }
                else
                {
                  if (contentElement.Name.Equals(SqlStructureConstants.ENAME_DDL_WITH_CLAUSE))
                  {
                    state.DecrementIndent();
                    ProcessSqlNodeList(contentElement.Children, state);
                    state.IncrementIndent();
                  }
                  else
                  {
                    state.BreakExpected = true;
                    state.UnIndentInitialBreak = true;
                    ProcessSqlNodeList(contentElement.Children, state.IncrementIndent());
                    state.DecrementIndent();
                  }
                }

                break;

                case SqlStructureConstants.ENAME_JOIN_ON_SECTION:
                    if (Options.BreakJoinOnSections)
                        state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_OPEN), state);
                    // if (Options.BreakJoinOnSections) // New
                    //    state.IncrementIndent();
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_GENERALCONTENT), state);
                    // if (Options.BreakJoinOnSections) // New
                    //     state.DecrementIndent();
                    break;

                case SqlStructureConstants.ENAME_CTE_ALIAS:
                    state.UnIndentInitialBreak = true;
                    ProcessSqlNodeList(contentElement.Children, state);
                    break;

                case SqlStructureConstants.ENAME_ELSE_CLAUSE:
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_OPEN), state.DecrementIndent());
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_SINGLESTATEMENT), state.IncrementIndent());
                    break;

                case SqlStructureConstants.ENAME_DDL_AS_BLOCK:
                case SqlStructureConstants.ENAME_CURSOR_FOR_BLOCK:
                    state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_OPEN), state.DecrementIndent());
                    state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_GENERALCONTENT), state);
                    state.IncrementIndent();
                    break;

                case SqlStructureConstants.ENAME_TRIGGER_CONDITION:
                    state.DecrementIndent();
                    state.WhiteSpace_BreakToNextLine();
                    ProcessSqlNodeList(contentElement.Children, state.IncrementIndent());
                    break;

                case SqlStructureConstants.ENAME_CURSOR_FOR_OPTIONS:
                case SqlStructureConstants.ENAME_CTE_AS_BLOCK:
                    state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_OPEN), state.DecrementIndent());
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_GENERALCONTENT), state.IncrementIndent());
                    break;

                case SqlStructureConstants.ENAME_DDL_RETURNS:
                case SqlStructureConstants.ENAME_MERGE_USING:
                case SqlStructureConstants.ENAME_MERGE_WHEN:
                    state.BreakExpected = true;
                    state.UnIndentInitialBreak = true;
                    ProcessSqlNodeList(contentElement.Children, state);
                    break;

                case SqlStructureConstants.ENAME_BETWEEN_CONDITION:
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_OPEN), state);
                    state.IncrementIndent();
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_BETWEEN_LOWERBOUND), state.IncrementIndent());
                    if (Options.ExpandBetweenConditions)
                        state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_CLOSE), state.DecrementIndent());
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_BETWEEN_UPPERBOUND), state.IncrementIndent());
                    state.DecrementIndent();
                    state.DecrementIndent();
                    break;

                case SqlStructureConstants.ENAME_DDLDETAIL_PARENS:
                case SqlStructureConstants.ENAME_FUNCTION_PARENS:
                    //simply process sub-nodes - don't add space or expect any linebreaks (but respect linebreaks if necessary)
                    state.WordSeparatorExpected = false;
                    //Determine if Break after Paren 
                    Should_Break_After_Paren(contentElement, state); // New
                    WhiteSpace_BreakAsExpected(state);
                    state.AddOutputContent(FormatOperator("("), SqlHtmlConstants.CLASS_OPERATOR);
                    ProcessSqlNodeList(contentElement.Children, state.IncrementIndent());
                    state.DecrementIndent();
                    WhiteSpace_BreakAsExpected(state);
                    state.AddOutputContent(FormatOperator(")"), SqlHtmlConstants.CLASS_OPERATOR);
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_DDL_PARENS:
                case SqlStructureConstants.ENAME_EXPRESSION_PARENS:
                case SqlStructureConstants.ENAME_SELECTIONTARGET_PARENS:
                case SqlStructureConstants.ENAME_IN_PARENS:
                  if (false)
                  {
                    // Original Code
                    WhiteSpace_SeparateWords(state, contentElement, false);
                    if (contentElement.Name.Equals(SqlStructureConstants.ENAME_EXPRESSION_PARENS) ||
                        contentElement.Name.Equals(SqlStructureConstants.ENAME_IN_PARENS))
                      state.IncrementIndent();
                    state.AddOutputContent(FormatOperator("("), SqlHtmlConstants.CLASS_OPERATOR);
                    TSqlStandardFormattingState innerState = new TSqlStandardFormattingState(state);
                    ProcessSqlNodeList(contentElement.Children, innerState);
                    //if there was a linebreak in the parens content, or if it wanted one to follow, then put linebreaks before and after.
                    if (innerState.BreakExpected || innerState.OutputContainsLineBreak)
                    {
                      if (!innerState.StartsWithBreak)
                        state.WhiteSpace_BreakToNextLine();
                      state.Assimilate(innerState);
                      state.WhiteSpace_BreakToNextLine();
                    }
                    else
                    {
                      state.Assimilate(innerState);
                    }

                    state.AddOutputContent(FormatOperator(")"), SqlHtmlConstants.CLASS_OPERATOR);
                    if (contentElement.Name.Equals(SqlStructureConstants.ENAME_EXPRESSION_PARENS) ||
                        contentElement.Name.Equals(SqlStructureConstants.ENAME_IN_PARENS))
                      state.DecrementIndent();
                    state.WordSeparatorExpected = true;
                    break;
                  }
                  else
                  {
                    WhiteSpace_SeparateWords(state, contentElement, false);
                    bool topClause = false;
                    bool indentAfter = false;
                    var recentKey = state.GetRecentKeyword() ?? string.Empty;
                    var initialOption = Options.ExpandCommaLists;
                    if (contentElement.Name.Equals(SqlStructureConstants.ENAME_DDL_PARENS)) // New begin
                    {
                      switch (recentKey)
                      {
                        case "TABLE":
                          indentAfter = true;
                          state.AddOutputLineBreak();
                          // if not in a declare statement
                          switch (contentElement.Parent.Name)
                          {
                            case SqlStructureConstants.ENAME_DDL_DECLARE_BLOCK:
                              state.DecrementIndent();
                              break;
                            default:
                              state.DecrementIndent();
                              break;
                          }

                          state.Indent(state.IndentLevel);
                          break;
                        case "VALUES":
                          bool multirowInsert = contentElement.Parent
                            .ChildrenByName(SqlStructureConstants.ENAME_DDL_PARENS).Count() > 1;
                          if (multirowInsert)
                          {
                            Options.ExpandCommaLists = false;
                            indentAfter = false;
                            state.AddOutputLineBreak();
                            state.Indent(state.IndentLevel);
                          }
                          else
                          {
                            Options.ExpandCommaLists = true;
                            indentAfter = true;
                            state.AddOutputLineBreak();
                            state.DecrementIndent();
                            state.Indent(state.IndentLevel);
                          }

                          break;
                        

                        default:
                          indentAfter = true;
                          state.AddOutputLineBreak();
                          state.DecrementIndent();
                          state.Indent(state.IndentLevel);
                          break;
                      }
                    }
                    else
                    {
                      switch (recentKey)
                      {
                        case "TOP":
                          topClause = true;
                          break;
                        case "INSERT":
                        case "VALUES":
                          indentAfter = true;
                          state.AddOutputLineBreak();
                          state.DecrementIndent();
                          state.Indent(state.IndentLevel);
                          break;
                        case "IF":
                          indentAfter = true;
                          state.AddOutputLineBreak();
                          state.DecrementIndent();
                          state.Indent(state.IndentLevel);
                          break;
                        case "AND":
                          state.AddOutputLineBreak();
                          state.Indent(state.IndentLevel);
                          indentAfter = true;
                          break;
                        case "OR":
                          state.AddOutputLineBreak();
                          state.Indent(state.IndentLevel);
                          indentAfter = true;
                          break;
                        default:
                          switch (contentElement.Parent.Name)
                          {
                            case SqlStructureConstants.ENAME_SQL_CLAUSE:
                              switch (contentElement.Name)
                              {
                                case SqlStructureConstants.ENAME_EXPRESSION_PARENS:
                                  state.AddOutputLineBreak();
                                  state.Indent(state.IndentLevel);
                                  indentAfter = true;
                                  break;
                                default:
                                  state.AddOutputLineBreak();
                                  state.Indent(state.IndentLevel);
                                  indentAfter = true;
                                  break;
                              }

                              break;
                            case SqlStructureConstants.ENAME_EXPRESSION_PARENS:
                              break;
                            case SqlStructureConstants.ENAME_DDL_PARENS:
                              switch (contentElement.Name)
                              {
                                case SqlStructureConstants.ENAME_EXPRESSION_PARENS:
                                  state.AddOutputLineBreak();
                                  state.Indent(state.IndentLevel);
                                  indentAfter = true;
                                  break;
                                default:
                                  state.AddOutputLineBreak();
                                  state.Indent(state.IndentLevel);
                                  indentAfter = true;
                                  break;
                              }
                              break;
                            case SqlStructureConstants.ENAME_CONTAINER_GENERALCONTENT:
                              if (contentElement.Parent.Parent.Name == SqlStructureConstants.ENAME_DDL_WITH_CLAUSE)
                              {
                                state.AddOutputLineBreak();
                                state.Indent(state.IndentLevel);
                                indentAfter = true;
                                break;
                              }
                              break;
                            default:
                              state.AddOutputLineBreak();
                              state.Indent(state.IndentLevel);
                              break;
                          }

                          break;
                      }
                    } // New end

                    // Open parenthesis
                    state.AddOutputContent(FormatOperator("("), SqlHtmlConstants.CLASS_OPERATOR);
                    TSqlStandardFormattingState innerState = new TSqlStandardFormattingState(state);
                    if (indentAfter) // New
                    {
                      innerState.IncrementIndent();
                      innerState.BreakExpected = true;
                    }

                    ProcessSqlNodeList(contentElement.Children, innerState);
                    //if there was a linebreak in the parens content, or if it wanted one to follow, then put linebreaks before and after.
                    if (innerState.BreakExpected || innerState.OutputContainsLineBreak)
                    {
                      if (contentElement.Name.Equals(SqlStructureConstants.ENAME_DDL_PARENS) || contentElement.Name.Equals(SqlStructureConstants.ENAME_EXPRESSION_PARENS)) // New
                      {
                        if (!innerState.StartsWithBreak)
                        {
                          state.AddOutputLineBreak();
                          state.BreakExpected = false;
                          state.SourceBreakPending = false;
                          state.WordSeparatorExpected = false;
                        }

                        state.Indent(state.IndentLevel);
                        state.Assimilate(innerState);
                        state.AddOutputLineBreak();
                        state.Indent(state.IndentLevel);
                      }
                      else
                      {
                        if (!innerState.StartsWithBreak)
                          state.WhiteSpace_BreakToNextLine();
                        state.Assimilate(innerState);
                        state.WhiteSpace_BreakToNextLine();
                      }
                    }
                    else
                    {
                      state.Assimilate(innerState);
                    }

                    // Close parenthesis 
                    state.AddOutputContent(FormatOperator(")"), SqlHtmlConstants.CLASS_OPERATOR);
                    // if (recentKey == "WITH")
                    //{
                    //  state.AddOutputLineBreak();
                    //  state.Indent(state.IndentLevel);
                    //}
                    state.WordSeparatorExpected = true;
                    if (indentAfter && initialIndent != state.IndentLevel) // New
                    {
                      state.IncrementIndent();
                    }

                    Options.ExpandCommaLists = initialOption;
                    break;
                  }
                case SqlStructureConstants.ENAME_BEGIN_END_BLOCK:
                case SqlStructureConstants.ENAME_TRY_BLOCK:
                case SqlStructureConstants.ENAME_CATCH_BLOCK:
                    if (contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_SQL_CLAUSE)
                        && contentElement.Parent.Parent.Name.Equals(SqlStructureConstants.ENAME_SQL_STATEMENT)
                        && contentElement.Parent.Parent.Parent.Name.Equals(SqlStructureConstants.ENAME_CONTAINER_SINGLESTATEMENT)
                        )
                      state.DecrementIndent();
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_OPEN), state);
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_MULTISTATEMENT), state);
                    state.DecrementIndent();
                    state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_CLOSE), state);
                    state.IncrementIndent();
                    if (contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_SQL_CLAUSE)
                        && contentElement.Parent.Parent.Name.Equals(SqlStructureConstants.ENAME_SQL_STATEMENT)
                        && contentElement.Parent.Parent.Parent.Name.Equals(SqlStructureConstants.ENAME_CONTAINER_SINGLESTATEMENT)
                        )
                      state.IncrementIndent();
                    break;

                case SqlStructureConstants.ENAME_CASE_STATEMENT:
                    switch (contentElement.Parent.Name) // New
                    {
                      case SqlStructureConstants.ENAME_FUNCTION_PARENS:
                      case SqlStructureConstants.ENAME_CONTAINER_GENERALCONTENT:
                        state.BreakExpected = true;
                        WhiteSpace_BreakAsExpected(state);
                        break;
                    }
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_OPEN), state);
                    state.IncrementIndent();
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CASE_INPUT), state);
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CASE_WHEN), state);
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CASE_ELSE), state);
                    if (Options.ExpandCaseStatements)
                        state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_CLOSE), state);
                    switch (contentElement.Parent.Name) // New
                    {
                      case SqlStructureConstants.ENAME_FUNCTION_PARENS:
                        state.BreakExpected = true;
                        break;
                    }
                    state.DecrementIndent();
                    break;

                case SqlStructureConstants.ENAME_CASE_WHEN:
                case SqlStructureConstants.ENAME_CASE_THEN:
                case SqlStructureConstants.ENAME_CASE_ELSE:
                    if (Options.ExpandCaseStatements) // New
                    {
                      switch (contentElement.Name)
                      {
                        case SqlStructureConstants.ENAME_CASE_THEN:
                          state.BreakExpected = false;
                          break;
                        default:
                          state.BreakExpected = true;
                          break;
                      }
                    }
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_OPEN), state);
                    if (contentElement.Name == SqlStructureConstants.ENAME_CASE_THEN)
                    {
                      state.BreakExpected = false;
                    }
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_GENERALCONTENT), state.IncrementIndent());
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CASE_THEN), state.DecrementIndent()); // New
                    break;

                case SqlStructureConstants.ENAME_AND_OPERATOR:
                case SqlStructureConstants.ENAME_OR_OPERATOR:
                    if (Options.ExpandBooleanExpressions)
                        state.BreakExpected = true;
                    ProcessSqlNode(contentElement.ChildByName(SqlStructureConstants.ENAME_OTHERKEYWORD), state);
                    break;

                case SqlStructureConstants.ENAME_COMMENT_MULTILINE:
                    if (state.SpecialRegionActive == SpecialRegionType.NoFormat && contentElement.TextValue.ToUpperInvariant().Contains("[/NOFORMAT]"))
                    {
                        Node skippedXml = NodeExtensions.ExtractStructureBetween(state.RegionStartNode, contentElement);
                        if (skippedXml != null)
                        {
                            TSqlIdentityFormatter tempFormatter = new TSqlIdentityFormatter(Options.HTMLColoring);
                            state.AddOutputContentRaw(tempFormatter.FormatSQLTree(skippedXml));
                            state.WordSeparatorExpected = false;
                            state.BreakExpected = false;
                        }
                        state.SpecialRegionActive = null;
                        state.RegionStartNode = null;
                    }
                    else if (state.SpecialRegionActive == SpecialRegionType.Minify && contentElement.TextValue.ToUpperInvariant().Contains("[/MINIFY]"))
                    {
                        Node skippedXml = NodeExtensions.ExtractStructureBetween(state.RegionStartNode, contentElement);
                        if (skippedXml != null)
                        {
                            TSqlObfuscatingFormatter tempFormatter = new TSqlObfuscatingFormatter();
                            if (HTMLFormatted)
                                state.AddOutputContentRaw(Utils.HtmlEncode(tempFormatter.FormatSQLTree(skippedXml)));
                            else
                                state.AddOutputContentRaw(tempFormatter.FormatSQLTree(skippedXml));
                            state.WordSeparatorExpected = false;
                            state.BreakExpected = false;
                        }
                        state.SpecialRegionActive = null;
                        state.RegionStartNode = null;
                    }

                    WhiteSpace_SeparateComment(contentElement, state);
                    state.AddOutputContent("/*" + contentElement.TextValue + "*/", SqlHtmlConstants.CLASS_COMMENT);
                    if (contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_SQL_STATEMENT)
                        || (contentElement.NextSibling() != null
                            && contentElement.NextSibling().Name.Equals(SqlStructureConstants.ENAME_WHITESPACE)
                            && NodeImpl.BreakMatch.IsMatch(contentElement.NextSibling().TextValue) // New
                            )
                        )
                        //if this block comment is at the start or end of a statement, or if it was followed by a 
                        // linebreak before any following content, then break here.
                        state.BreakExpected = true;
                    else
                    {
                        state.WordSeparatorExpected = true;
                    }

                    if (state.SpecialRegionActive == null && contentElement.TextValue.ToUpperInvariant().Contains("[NOFORMAT]"))
                    {
                        //state.AddOutputLineBreak();
                        state.SpecialRegionActive = SpecialRegionType.NoFormat;
                        state.RegionStartNode = contentElement;
                    }
                    else if (state.SpecialRegionActive == null && contentElement.TextValue.ToUpperInvariant().Contains("[MINIFY]"))
                    {
                        //state.AddOutputLineBreak();
                        state.SpecialRegionActive = SpecialRegionType.Minify;
                        state.RegionStartNode = contentElement;
                    }
                    break;

                case SqlStructureConstants.ENAME_COMMENT_SINGLELINE:
                case SqlStructureConstants.ENAME_COMMENT_SINGLELINE_CSTYLE:
                    if (state.SpecialRegionActive == SpecialRegionType.NoFormat && contentElement.TextValue.ToUpperInvariant().Contains("[/NOFORMAT]"))
                    {
                        Node skippedXml = NodeExtensions.ExtractStructureBetween(state.RegionStartNode, contentElement);
                        if (skippedXml != null)
                        {
                            TSqlIdentityFormatter tempFormatter = new TSqlIdentityFormatter(Options.HTMLColoring);
                            state.AddOutputContentRaw(tempFormatter.FormatSQLTree(skippedXml));
                            state.WordSeparatorExpected = false;
                            state.BreakExpected = false;
                        }
                        state.SpecialRegionActive = null;
                        state.RegionStartNode = null;
                    }
                    else if (state.SpecialRegionActive == SpecialRegionType.Minify && contentElement.TextValue.ToUpperInvariant().Contains("[/MINIFY]"))
                    {
                        Node skippedXml = NodeExtensions.ExtractStructureBetween(state.RegionStartNode, contentElement);
                        if (skippedXml != null)
                        {
                            TSqlObfuscatingFormatter tempFormatter = new TSqlObfuscatingFormatter();
                            if (HTMLFormatted)
                                state.AddOutputContentRaw(Utils.HtmlEncode(tempFormatter.FormatSQLTree(skippedXml)));
                            else
                                state.AddOutputContentRaw(tempFormatter.FormatSQLTree(skippedXml));
                            state.WordSeparatorExpected = false;
                            state.BreakExpected = false;
                        }
                        state.SpecialRegionActive = null;
                        state.RegionStartNode = null;
                    }

                    WhiteSpace_SeparateComment(contentElement, state);
                    string formattedOutput = (contentElement.Name == SqlStructureConstants.ENAME_COMMENT_SINGLELINE ? "--" : "//") // New begin
                      + ((contentElement.TextValue.StartsWith(" ") || contentElement.TextValue.StartsWith("-")) ? string.Empty : " ")
                      + contentElement.TextValue.Replace("\r", "").Replace("\n", "");
                    switch (contentElement.Parent.Name)
                    {
                      case SqlStructureConstants.ENAME_DDL_PARENS:
                        var prevsNode = contentElement.PreviousSibling();
                        if (prevsNode.Name == SqlStructureConstants.ENAME_WHITESPACE && prevsNode.TextValue.Contains(Environment.NewLine))
                        {
                          state.Indent(1);
                        }
                        state.AddOutputContent(formattedOutput, SqlHtmlConstants.CLASS_COMMENT);
                        state.BreakExpected = true;
                        break;
                      case SqlStructureConstants.ENAME_SQL_CLAUSE:
                        switch (state.GetRecentKeyword())
                        {
                          case "INSERT":
                          case "UPDATE":
                          case "DELETE":
                            state.AddOutputContent(formattedOutput, SqlHtmlConstants.CLASS_COMMENT);
                            break;
                          default:
                            state.AddOutputContent(formattedOutput, SqlHtmlConstants.CLASS_COMMENT);
                            state.BreakExpected = true;
                            state.SourceBreakPending = true;
                            break;
                        }
                        break;
                      default:
                        state.AddOutputContent(formattedOutput, SqlHtmlConstants.CLASS_COMMENT);
                        state.BreakExpected = true;
                        state.SourceBreakPending = true;
                        break;
                    } // New end

                    if (state.SpecialRegionActive == null && contentElement.TextValue.ToUpperInvariant().Contains("[NOFORMAT]"))
                    {
                        state.AddOutputLineBreak();
                        state.SpecialRegionActive = SpecialRegionType.NoFormat;
                        state.RegionStartNode = contentElement;
                    }
                    else if (state.SpecialRegionActive == null && contentElement.TextValue.ToUpperInvariant().Contains("[MINIFY]"))
                    {
                        state.AddOutputLineBreak();
                        state.SpecialRegionActive = SpecialRegionType.Minify;
                        state.RegionStartNode = contentElement;
                    }
                    break;

                case SqlStructureConstants.ENAME_STRING:
                case SqlStructureConstants.ENAME_NSTRING:
                    WhiteSpace_SeparateWords(state, contentElement); // New
                    string outValue = null;
                    if (contentElement.Name.Equals(SqlStructureConstants.ENAME_NSTRING))
                        outValue = "N'" + contentElement.TextValue.Replace("'", "''") + "'";
                    else
                        outValue = "'" + contentElement.TextValue.Replace("'", "''") + "'";
                    state.AddOutputContent(outValue, SqlHtmlConstants.CLASS_STRING);
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_BRACKET_QUOTED_NAME:
                    WhiteSpace_SeparateWords(state, contentElement); // New
                    // data types
                    var formattedDataType = FormatDataTypeKeyword(contentElement);
                    state.AddOutputContent(formattedDataType);
                    //state.AddOutputContent("[" + contentElement.TextValue.Replace("]", "]]") + "]");
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_QUOTED_STRING:
                    WhiteSpace_SeparateWords(state, contentElement); // New
                    state.AddOutputContent("\"" + contentElement.TextValue.Replace("\"", "\"\"") + "\"");
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_COMMA:
                    //comma always ignores requested word spacing
                    if (Options.TrailingCommas)
                    {
                        WhiteSpace_BreakAsExpected(state);
                        state.AddOutputContent(FormatOperator(","), SqlHtmlConstants.CLASS_OPERATOR);

                        if ((Options.ExpandCommaLists
                          && !(contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_DDLDETAIL_PARENS) // New
                            || contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_FUNCTION_PARENS)
                            || contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_IN_PARENS)
                            )
                          )
                        || (Options.ExpandInLists
                          && contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_IN_PARENS)
                          )
                        )
                        {
                          if (state.GetRecentKeyword() != "VALUES")
                          {
                            state.BreakExpected = true;
                          }
                        }
                        else
                            state.WordSeparatorExpected = true;
                    }
                    else
                    {
                        if ((Options.ExpandCommaLists
                        && !(contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_DDLDETAIL_PARENS) // New
                          || contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_FUNCTION_PARENS)
                          || contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_IN_PARENS)
                          )
                        )
                        || (Options.ExpandInLists
                          && contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_IN_PARENS)
                          )
                        )
                        {
                            state.WhiteSpace_BreakToNextLine();
                            state.AddOutputContent(FormatOperator(","), SqlHtmlConstants.CLASS_OPERATOR);
                            if (Options.SpaceAfterExpandedComma)
                                state.WordSeparatorExpected = true;
                        }
                        else
                        {
                            WhiteSpace_BreakAsExpected(state);
                            state.AddOutputContent(FormatOperator(","), SqlHtmlConstants.CLASS_OPERATOR);
                            state.WordSeparatorExpected = true;
                        }

                    }
                    break;

                case SqlStructureConstants.ENAME_PERIOD:
                case SqlStructureConstants.ENAME_SEMICOLON:
                case SqlStructureConstants.ENAME_SCOPERESOLUTIONOPERATOR:
                    //always ignores requested word spacing, and doesn't request a following space either.
                    state.WordSeparatorExpected = false;
                    WhiteSpace_BreakAsExpected(state);
                    state.AddOutputContent(FormatOperator(contentElement.TextValue), SqlHtmlConstants.CLASS_OPERATOR); // New
                    if (contentElement.Name.Equals(SqlStructureConstants.ENAME_SEMICOLON))
                    {
                      state.StatementEndCharacterExpected = false;
                    }
                    break;

                case SqlStructureConstants.ENAME_ASTERISK:
                case SqlStructureConstants.ENAME_EQUALSSIGN:
                case SqlStructureConstants.ENAME_ALPHAOPERATOR:
                case SqlStructureConstants.ENAME_OTHEROPERATOR:
                    if (contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_BOOLEAN_EXPRESSION) && contentElement.TextValue.Equals("EXISTS", StringComparison.CurrentCultureIgnoreCase) // New begin
                     && contentElement.Parent.ChildrenByName(SqlStructureConstants.ENAME_EXPRESSION_PARENS) == null)
                    {
                      WhiteSpace_SeparateWords(state, contentElement);
                      state.AddOutputContent(FormatOperator(contentElement.TextValue), SqlHtmlConstants.CLASS_OPERATOR);
                      state.WordSeparatorExpected = false;
                      state.AddOutputLineBreak();
                      state.Indent(state.IndentLevel);
                    }
                    else
                    {
                      WhiteSpace_SeparateWords(state, contentElement);
                      state.AddOutputContent(FormatOperator(contentElement.TextValue), SqlHtmlConstants.CLASS_OPERATOR);
                      if (!contentElement.TextValue.Equals("-"))
                        state.WordSeparatorExpected = true;
                    } // New end
                    break;

                case SqlStructureConstants.ENAME_COMPOUNDKEYWORD:
                    WhiteSpace_SeparateWords(state, contentElement); // New
                    state.SetRecentKeyword(contentElement.GetAttributeValue(SqlStructureConstants.ANAME_SIMPLETEXT));
                    state.AddOutputContent(FormatKeyword(contentElement.GetAttributeValue(SqlStructureConstants.ANAME_SIMPLETEXT)), SqlHtmlConstants.CLASS_KEYWORD);
                    state.WordSeparatorExpected = true;
                    ProcessSqlNodeList(contentElement.ChildrenByNames(SqlStructureConstants.ENAMELIST_COMMENT), state.IncrementIndent());
                    state.DecrementIndent();
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_OTHERKEYWORD:
                case SqlStructureConstants.ENAME_DATATYPE_KEYWORD:
                  FormatKeyword(contentElement, state);
                  break;

                case SqlStructureConstants.ENAME_PSEUDONAME:
                    WhiteSpace_SeparateWords(state, contentElement); // New
                    state.AddOutputContent(FormatKeyword(contentElement.TextValue), SqlHtmlConstants.CLASS_KEYWORD);
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_FUNCTION_KEYWORD:
                    WhiteSpace_SeparateWords(state, contentElement); // New
                    state.SetRecentKeyword(contentElement.TextValue);
                    state.AddOutputContent(contentElement.TextValue, SqlHtmlConstants.CLASS_FUNCTION);
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_OTHERNODE: // New
                    // if first paramter line break and indent
                    switch (state.GetRecentKeyword())
                    {
                      case "TOP":
                        if (contentElement.TextValue.Contains("@"))
                        {
                          state.AddOutputLineBreak();
                          state.Indent(state.IndentLevel);
                          state.SetRecentKeyword("");
                          state.WordSeparatorExpected = false;
                        }
                        break;
                      case "EXECUTE":
                      case "EXEC":
                        int spaceCount = 0;
                        foreach (var childnode in contentElement.Parent.Children)
                        {
                          if (childnode.Name.Equals(SqlStructureConstants.ENAME_WHITESPACE))
                          {
                            spaceCount++;
                          }
                          if (spaceCount == 2)
                          {
                            if (childnode.Equals(contentElement.PreviousSibling()))
                            {
                              state.AddOutputLineBreak();
                              state.Indent(state.IndentLevel);
                              addOutputSpace = false;
                            }
                            break;
                          }
                        }
                        break;
                    }
                    WhiteSpace_SeparateWords(state, contentElement, addOutputSpace);
                    state.AddOutputContent(contentElement.TextValue);
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_MONETARY_VALUE:
                case SqlStructureConstants.ENAME_LABEL:
                    WhiteSpace_SeparateWords(state, contentElement); // New
                    state.AddOutputContent(contentElement.TextValue);
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_NUMBER_VALUE:
                    WhiteSpace_SeparateWords(state, contentElement); // New
                    state.AddOutputContent(contentElement.TextValue.ToLowerInvariant());
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_BINARY_VALUE:
                    WhiteSpace_SeparateWords(state, contentElement); // New
                    state.AddOutputContent("0x");
                    state.AddOutputContent(contentElement.TextValue.Substring(2).ToUpperInvariant());
                    state.WordSeparatorExpected = true;
                    break;

               case SqlStructureConstants.ENAME_WHITESPACE: // New
                    //take note if it's a line-breaking space, but don't DO anything here
                    if (NodeImpl.BreakMatch.IsMatch(contentElement.TextValue))
                      state.SourceBreakPending = true;
                    if (contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_DDL_PARENS)
                        && (contentElement.NextSibling() == null))
                    {
                      state.SourceBreakPending = false;
                    }
                    else if (contentElement.PreviousSibling() != null)
                    {
                      if (contentElement.PreviousSibling().Name.Equals(SqlStructureConstants.ENAME_COMMENT_SINGLELINE) && state.SourceBreakPending)
                      {
                        state.AddOutputLineBreak();
                      }
                    }
                    break;
                default:
                    throw new Exception("Unrecognized element in SQL Xml!");
            }

            if (contentElement.GetAttributeValue(SqlStructureConstants.ANAME_HASERROR) == "1")
                state.CloseClass();

            if (initialIndent != state.IndentLevel)
                throw new Exception("Messed up the indenting!! Check code/stack or panic!");
            if (this.Debug)
            {
              System.Diagnostics.Debug.WriteLine(" ");
              System.Diagnostics.Debug.WriteLine("'" + state.DumpOutput() + "'");
            }
        }

        private string FormatDataTypeKeyword(Node contentElement)
        {
          if (contentElement.Name == SqlStructureConstants.ENAME_BRACKET_QUOTED_NAME)
          {
            bool isDataType = new[]
              {
                "bigint", 
                "binary", 
                "bit",
                "char",
                "date",
                "datetime",
                "datetime2",
                "datetimeoffset",
                "decimal",
                "float",
                "image",
                "int",
                "money",
                "nchar",
                "ntext",
                "numeric",
                "nvarchar",
                "real",
                "rowversion",
                "smalldatetime",
                "smallint",
                "smallmoney",
                "sql_variant",
                "text",
                "time",
                "timestamp",
                "tinyint",
                "uniqueidentifier",
                "varbinary",
                "varchar",
                "xml"
              }.Contains(contentElement.TextValue.ToLowerInvariant()); // Not using a comparer here because of compatibility with the JS library.

            if (isDataType)
            {
              return contentElement.TextValue.ToUpperInvariant();
            }
          }

          return $"[{contentElement.TextValue.Replace("]", "]]")}]";
        }

        private void FormatKeyword(Node contentElement, TSqlStandardFormattingState state)
        {
          if (false)
          {
            // This is the original code
            // WhiteSpace_SeparateWords(state);
            WhiteSpace_SeparateWords(state, contentElement); // New
            state.SetRecentKeyword(contentElement.TextValue);
            state.AddOutputContent(FormatKeyword(contentElement.TextValue), SqlHtmlConstants.CLASS_KEYWORD);
            state.WordSeparatorExpected = true;
            // break;
          }
          else
          {
            if (contentElement.TextValue != "WITH") 
            {
              // GZ -The line below to keeps the values from being bumped up right next to it's predecessor.
              // Adding special rule for this keyword (for now) because we want to move this keyword to a newline after closing parenthesis
              WhiteSpace_SeparateWords(state, contentElement); 
            }
            else
            {
              state.AddOutputLineBreak();
              state.Indent(state.IndentLevel);
            }

            // state.SetRecentKeyword(contentElement.TextValue); // New
            state.AddOutputContent(FormatKeyword(contentElement.TextValue), SqlHtmlConstants.CLASS_KEYWORD); // New begin
            state.WordSeparatorExpected = true;
            if (contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_DDL_DECLARE_BLOCK) &&
                contentElement.TextValue.Equals("DECLARE", StringComparison.CurrentCultureIgnoreCase))
            {
              //Confirm this is not a Table variable otherwise linebreak and indent
              var isTable = contentElement.Parent.ChildrenByName(SqlStructureConstants.ENAME_DDL_PARENS).Count() != 0;
              if (!isTable)
              {
                state.AddOutputLineBreak();
                state.Indent(state.IndentLevel);
                state.WordSeparatorExpected = false;
              }
            }
            else
            {
              switch (contentElement.Parent.Name)
              {
                case SqlStructureConstants.ENAME_CONTAINER_CLOSE:
                  state.StatementEndCharacterExpected = true;
                  break;
                case SqlStructureConstants.ENAME_EXPRESSION_PARENS:
                case SqlStructureConstants.ENAME_SQL_CLAUSE:
                  switch (contentElement.TextValue.ToUpper())
                  {
                    case "SELECT":
                      if (!contentElement.Parent.Children.Where(x =>
                        x.Name.Equals(SqlStructureConstants.ENAME_OTHERKEYWORD) &&
                        (x.TextValue.Equals("TOP",
                           StringComparison.InvariantCultureIgnoreCase) ||
                         x.TextValue.Equals("DISTINCT",
                           StringComparison.InvariantCultureIgnoreCase))).Any())
                      {
                        state.AddOutputLineBreak();
                        state.Indent(state.IndentLevel);
                        state.WordSeparatorExpected = false;
                      }

                      break;
                    case "OUTPUT":
                      switch (state.GetRecentKeyword())
                      {
                        case "EXEC":
                        case "EXECUTE":
                          break;
                        default:
                          state.AddOutputLineBreak();
                          state.Indent(state.IndentLevel);
                          state.WordSeparatorExpected = false;
                          break;
                      }

                      break;
                    case "BY":
                      state.AddOutputLineBreak();
                      bool nestedBy = false;
                      switch (state.GetRecentKeyword())
                      {
                        case "ORDER":
                          nestedBy = true;
                          state.Indent(state.IndentLevel);
                          break;
                        default:
                          state.IncrementIndent();
                          state.Indent(state.IndentLevel);
                          break;
                      }

                      state.WordSeparatorExpected = false;
                      ProcessSqlNodeList(contentElement.RestOfSiblings(), state);
                      if (!nestedBy)
                      {
                        state.DecrementIndent();
                      }

                      state.SiblingsProcessed = true;
                      break;
                    case "TOP":
                      state.WordSeparatorExpected = false;
                      break;
                    case "USE":
                      state.StatementBreakExpected = true;
                      break;
                    case "GO":
                      state.StatementBreakExpected = false;
                      state.AddOutputLineBreak();
                      state.Indent(state.IndentLevel);
                      state.WordSeparatorExpected = false;
                      break;
                    case "DISTINCT":
                      state.AddOutputLineBreak();
                      state.Indent(state.IndentLevel);
                      state.WordSeparatorExpected = false;
                      break;
                    case "SET":
                      switch (state.GetRecentKeyword())
                      {
                        case "UPDATE":
                          state.AddOutputLineBreak();
                          state.Indent(state.IndentLevel);
                          state.WordSeparatorExpected = false;
                          break;
                        default:
                          state.StatementBreakExpected = true;
                          break;
                      }

                      break;
                  }

                  break;
              }
            }

            state.SetRecentKeyword(contentElement.TextValue); // New end
            return;
          }
        }


        private string FormatKeyword(string keyword)
        {
            string outputKeyword;
            if (!KeywordMapping.TryGetValue(keyword.ToUpperInvariant(), out outputKeyword))
                outputKeyword = keyword;

            if (Options.UppercaseKeywords)
                return outputKeyword.ToUpperInvariant();
            else
                return outputKeyword.ToLowerInvariant();
        }

        private string FormatOperator(string operatorValue)
        {
            if (Options.UppercaseKeywords)
                return operatorValue.ToUpperInvariant();
            else
                return operatorValue.ToLowerInvariant();
        }

        private void WhiteSpace_SeparateStatements(Node contentElement, TSqlStandardFormattingState state)
        {
          if (state.StatementBreakExpected)
          {
            //check whether this is a DECLARE/SET clause with similar precedent, and therefore exempt from double-linebreak.
            Node thisClauseStarter = FirstSemanticElementChild(contentElement);
            if (!(thisClauseStarter != null
                  && thisClauseStarter.Name.Equals(SqlStructureConstants.ENAME_OTHERKEYWORD)
                  && state.GetRecentKeyword() != null
                  && ((thisClauseStarter.TextValue.ToUpperInvariant().Equals("SET")
                       && state.GetRecentKeyword().Equals("SET")
                      )
                      || (thisClauseStarter.TextValue.ToUpperInvariant().Equals("DECLARE")
                          && state.GetRecentKeyword().Equals("DECLARE")
                      )
                      || (thisClauseStarter.TextValue.ToUpperInvariant().Equals("PRINT")
                          && state.GetRecentKeyword().Equals("PRINT")
                      )
                  )
              ))
            {
              if (state.StatementEndCharacterExpected) // New
              {
                state.AddOutputContent(";");
                state.StatementEndCharacterExpected = false;
              }

              for (int i = Options.NewStatementLineBreaks; i > 0; i--)
                state.AddOutputLineBreak();
            }
            else
            {
              if (state.StatementEndCharacterExpected) // New
              {
                state.AddOutputContent(";");
                state.StatementEndCharacterExpected = false;
              }

              for (int i = Options.NewClauseLineBreaks; i > 0; i--)
                state.AddOutputLineBreak();
            }

            state.Indent(state.IndentLevel);
            state.BreakExpected = false;
            state.AdditionalBreaksExpected = 0;
            state.SourceBreakPending = false;
            state.StatementBreakExpected = false;
            state.WordSeparatorExpected = false;
          }
        }

        private Node FirstSemanticElementChild(Node contentElement)
        {
            Node target = null;
            while (contentElement != null)
            {
                target = contentElement.ChildrenExcludingNames(SqlStructureConstants.ENAMELIST_NONCONTENT).FirstOrDefault();

                if (target != null && SqlStructureConstants.ENAMELIST_NONSEMANTICCONTENT.Contains(target.Name))
                    contentElement = target;
                else
                    contentElement = null;
            }

            return target;
        }

        private void WhiteSpace_SeparateWords(TSqlStandardFormattingState state, Node contentElement, bool addOutputSpace = true) // New
        {
            if (state.BreakExpected || state.AdditionalBreaksExpected > 0)
            {
                bool wasUnIndent = state.UnIndentInitialBreak;
                if (wasUnIndent) state.DecrementIndent();
                WhiteSpace_BreakAsExpected(state);
                if (wasUnIndent) state.IncrementIndent();
            }
            else if (state.WordSeparatorExpected)
            {
                if (addOutputSpace) // New
                {
                  state.AddOutputSpace();
                }
            }
            state.UnIndentInitialBreak = false;
            state.SourceBreakPending = false;
            state.WordSeparatorExpected = false;
        }

        private void WhiteSpace_SeparateComment(Node contentElement, TSqlStandardFormattingState state)
        {
            if (contentElement.Name.Equals(SqlStructureConstants.ENAME_COMMENT_SINGLELINE) && state.SourceBreakPending) // New
            {
              state.BreakExpected = true;
              WhiteSpace_BreakAsExpected(state);
            }
            else if (state.CurrentLineHasContent && state.SourceBreakPending)
            {
                state.BreakExpected = true;
                WhiteSpace_BreakAsExpected(state);
            }
            else if (state.WordSeparatorExpected)
                state.AddOutputSpace();
            state.SourceBreakPending = false;
            state.WordSeparatorExpected = false;
        }

        private void Should_Break_After_Paren(Node contentElement, TSqlStandardFormattingState state)
        {
          var typeOfParen = contentElement.Parent;
          var breakItems = contentElement.ChildrenByNames(new[] { "CaseStatement" });
          if (breakItems.Count() > 0)
          {
            state.BreakExpected = true;
          }
        }

        private void WhiteSpace_BreakAsExpected(TSqlStandardFormattingState state)
        {
            if (state.BreakExpected)
                state.WhiteSpace_BreakToNextLine();
            while (state.AdditionalBreaksExpected > 0)
            {
                state.WhiteSpace_BreakToNextLine();
                state.AdditionalBreaksExpected--;
            }
        }

        internal class TSqlStandardFormattingState : BaseFormatterState
        {
            //normal constructor
            public TSqlStandardFormattingState(bool htmlOutput, string indentString, int spacesPerTab, int maxLineWidth, int initialIndentLevel)
                : base(htmlOutput)
            {
                IndentLevel = initialIndentLevel;
                HtmlOutput = htmlOutput;
                IndentString = indentString;
                MaxLineWidth = maxLineWidth;

                int tabCount = indentString.Split('\t').Length - 1;
                int tabExtraCharacters = tabCount * (spacesPerTab - 1);
                IndentLength = indentString.Length + tabExtraCharacters;
            }

            //special "we want isolated state, but inheriting existing conditions" constructor
            public TSqlStandardFormattingState(TSqlStandardFormattingState sourceState)
                : base(sourceState.HtmlOutput)
            {
                IndentLevel = sourceState.IndentLevel;
                HtmlOutput = sourceState.HtmlOutput;
                IndentString = sourceState.IndentString;
                IndentLength = sourceState.IndentLength;
                MaxLineWidth = sourceState.MaxLineWidth;
                //TODO: find a way out of the cross-dependent wrapping maze...
                //CurrentLineLength = sourceState.CurrentLineLength;
                CurrentLineLength = IndentLevel * IndentLength;
                CurrentLineHasContent = sourceState.CurrentLineHasContent;
            }

            private string IndentString { get; set; }
            private int IndentLength { get; set; }
            private int MaxLineWidth { get; set; }

            public bool StatementBreakExpected { get; set; }
            public bool BreakExpected { get; set; }
            public bool StatementEndCharacterExpected { get; set; }
            public bool WordSeparatorExpected { get; set; }
            public bool SourceBreakPending { get; set; }
            public int AdditionalBreaksExpected { get; set; }

            public bool SiblingsProcessed { get; set; }
            public bool UnIndentInitialBreak { get; set; }
            public int IndentLevel { get; private set; }
            public int CurrentLineLength { get; private set; }
            public bool CurrentLineHasContent { get; private set; }

            public SpecialRegionType? SpecialRegionActive { get; set; }
            public Node RegionStartNode { get; set; }

            private static Regex _startsWithBreakChecker = new Regex(@"^\s*(\r|\n)", RegexOptions.None);
            public bool StartsWithBreak
            {
                get
                {
                    return _startsWithBreakChecker.IsMatch(_outBuilder.ToString());
                }
            }

            public override void AddOutputContent(string content)
            {
                if (SpecialRegionActive == null)
                    AddOutputContent(content, null);
            }

            public override void AddOutputContent(string content, string htmlClassName)
            {
                if (CurrentLineHasContent && (content.Length + CurrentLineLength > MaxLineWidth))
                    WhiteSpace_BreakToNextLine();

                if (SpecialRegionActive == null)
                    base.AddOutputContent(content, htmlClassName);

                CurrentLineHasContent = true;
                CurrentLineLength += content.Length;
            }

            public override void AddOutputLineBreak()
            {
#if DEBUG
                //hints for debugging line-width issues:
                //_outBuilder.Append(" (" + CurrentLineLength.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")");
#endif

                //if linebreaks are added directly in the content (eg in comments or strings), they 
                // won't be accounted for here - that's ok.
                if (SpecialRegionActive == null)
                    base.AddOutputLineBreak();
                CurrentLineLength = 0;
                CurrentLineHasContent = false;
            }

            internal void AddOutputSpace()
            {
                if (SpecialRegionActive == null)
                    _outBuilder.Append(" ");
            }

            public void Indent(int indentLevel)
            {
                for (int i = 0; i < indentLevel; i++)
                {
                    if (SpecialRegionActive == null)
                        base.AddOutputContent(IndentString, ""); //that is, add the indent as HTMLEncoded content if necessary, but no weird linebreak-adding
                    CurrentLineLength += IndentLength;
                }
            }

            internal void WhiteSpace_BreakToNextLine()
            {
                AddOutputLineBreak();
                Indent(IndentLevel);
                BreakExpected = false;
                SourceBreakPending = false;
                WordSeparatorExpected = false;
            }

            public bool OutputContainsLineBreak { get { return NodeImpl.BreakMatch.IsMatch(_outBuilder.ToString()); } }

            public void Assimilate(TSqlStandardFormattingState partialState)
            {
                //TODO: find a way out of the cross-dependent wrapping maze...
                CurrentLineLength = CurrentLineLength + partialState.CurrentLineLength;
                CurrentLineHasContent = CurrentLineHasContent || partialState.CurrentLineHasContent;
                if (SpecialRegionActive == null)
                    _outBuilder.Append(partialState.DumpOutput());
            }


            private Dictionary<int, string> _mostRecentKeywordsAtEachLevel = new Dictionary<int, string>();

            public TSqlStandardFormattingState IncrementIndent()
            {
                IndentLevel++;
                return this;
            }

            public TSqlStandardFormattingState DecrementIndent()
            {
                IndentLevel--;
                return this;
            }

            public void SetRecentKeyword(string ElementName)
            {
                if (!_mostRecentKeywordsAtEachLevel.ContainsKey(IndentLevel))
                    _mostRecentKeywordsAtEachLevel.Add(IndentLevel, ElementName.ToUpperInvariant());
                else
                    _mostRecentKeywordsAtEachLevel[IndentLevel] = ElementName.ToUpperInvariant();
            }

            public string GetRecentKeyword()
            {
                string keywordFound = null;
                int? keywordFoundAt = null;
                foreach (int key in _mostRecentKeywordsAtEachLevel.Keys)
                {
                    if ((keywordFoundAt == null || keywordFoundAt.Value > key) && key >= IndentLevel)
                    {
                        keywordFoundAt = key;
                        keywordFound = _mostRecentKeywordsAtEachLevel[key];
                    }
                }
                return keywordFound;
            }

            public void ResetKeywords()
            {
                List<int> descendentLevelKeys = new List<int>();
                foreach (int key in _mostRecentKeywordsAtEachLevel.Keys)
                    if (key >= IndentLevel)
                        descendentLevelKeys.Add(key);
                foreach (int key in descendentLevelKeys)
                    _mostRecentKeywordsAtEachLevel.Remove(key);
            }
        }

        public enum SpecialRegionType
        {
            NoFormat = 1,
            Minify = 2
        }
    }
}
