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

using NUnit.Framework;
using PoorMansTSqlFormatterLib;
using PoorMansTSqlFormatterLib.Formatters;
using PoorMansTSqlFormatterLib.Interfaces;
using PoorMansTSqlFormatterLib.Parsers;
using PoorMansTSqlFormatterLib.ParseStructure;
using PoorMansTSqlFormatterLib.Tokenizers;
using System;
using System.Collections.Generic;

namespace PoorMansTSqlFormatterTests
{
  [TestFixture]
  public class TSqlStandardFormatterTests
  {
    ISqlTokenizer _tokenizer;
    ISqlTokenParser _parser;
    Dictionary<string, TSqlStandardFormatter> _formatters;

    public TSqlStandardFormatterTests()
    {
      _tokenizer = new TSqlStandardTokenizer();
      _parser = new TSqlStandardParser();
      _formatters = new Dictionary<string, TSqlStandardFormatter>(StringComparer.OrdinalIgnoreCase);
    }

    private TSqlStandardFormatter GetFormatter(string configString)
    {
      TSqlStandardFormatter outFormatter;
      if (!_formatters.TryGetValue(configString, out outFormatter))
      {
        //var options = new TSqlStandardFormatterOptions
        //{
        //  KeywordStandardization = true,
        //  IndentString = "\t",
        //  SpacesPerTab = 4,
        //  MaxLineWidth = 999,
        //  NewStatementLineBreaks = 2,
        //  NewClauseLineBreaks = 1,
        //  TrailingCommas = false,
        //  SpaceAfterExpandedComma = false,
        //  ExpandBetweenConditions = true,
        //  ExpandBooleanExpressions = true,
        //  ExpandCaseStatements = true,
        //  ExpandCommaLists = true,
        //  BreakJoinOnSections = false,
        //  UppercaseKeywords = true,
        //  ExpandInLists = true
        //};

        var options = new TSqlStandardFormatterOptions(configString);
        options.IndentString = new String(' ', 2);
        options.TrailingCommas = true;
        options.ExpandInLists = true;
        //options.NewStatementLineBreaks = 2;
        outFormatter = new TSqlStandardFormatter(options);
        _formatters.Add(configString, outFormatter);
      }

      return outFormatter;
    }

    [Test, TestCaseSource(typeof(Utils), "GetInputSqlFileNames")]
    public void StandardFormatReparsingReformatting(string FileName)
    {
      string inputSQL = Utils.GetTestFileContent(FileName, Utils.INPUTSQLFOLDER);
      TSqlStandardFormatter _treeFormatter = GetFormatter("");
      ITokenList tokenized = _tokenizer.TokenizeSQL(inputSQL);
      Node parsed = _parser.ParseSQL(tokenized);
      string outputSQL = _treeFormatter.FormatSQLTree(parsed);

      var inputToSecondPass = outputSQL;
      if (inputToSecondPass.StartsWith(Utils.ERROR_FOUND_WARNING))
        inputToSecondPass = inputToSecondPass.Replace(Utils.ERROR_FOUND_WARNING, "");

      ITokenList tokenizedAgain = _tokenizer.TokenizeSQL(inputToSecondPass);
      Node parsedAgain = _parser.ParseSQL(tokenizedAgain);
      string formattedAgain = _treeFormatter.FormatSQLTree(parsedAgain);

      if (!inputSQL.Contains(Utils.REFORMATTING_INCONSISTENCY_WARNING))
      {
        Assert.AreEqual(outputSQL, formattedAgain, "first-pass formatted vs reformatted");
        Utils.StripWhiteSpaceFromSqlTree(parsed);
        Utils.StripWhiteSpaceFromSqlTree(parsedAgain);
        Assert.AreEqual(parsed.ToXmlDoc().OuterXml.ToUpper(), parsedAgain.ToXmlDoc().OuterXml.ToUpper(), "first parse xml vs reparse xml");
      }
    }

    public IEnumerable<string> GetStandardFormatSqlFileNames()
    {
      Utils.SortFileName = false;
      var results = Utils.FolderFileNameIterator(Utils.GetTestContentFolder("StandardFormatSql"));
      return results;
    }

    [Test, TestCaseSource(typeof(Utils), nameof(Utils.GetInputSqlFileNames))]
    public void StandardFormatExpectedOutput(string fileName)
    {
      //string expectedSql = Utils.GetTestFileContent(fileName, Utils.STANDARDFORMATSQLFOLDER);

      
      string inputSql = Utils.GetTestFileContent(Utils.StripFileConfigString(fileName), Utils.INPUTSQLFOLDER);
      TSqlStandardFormatter treeFormatter = GetFormatter(Utils.GetFileConfigString(fileName));
      treeFormatter.Debug = false;
      ITokenList tokenized = _tokenizer.TokenizeSQL(inputSql);
      Node parsed = _parser.ParseSQL(tokenized);
      string formatted = treeFormatter.FormatSQLTree(parsed);
      // System.Diagnostics.Debug.WriteLine(formatted);
      Utils.WriteTestFileContent($"formatted-{fileName}", Utils.STANDARDFORMATSQLFOLDER, formatted);
      //Assert.AreEqual(expectedSql, formatted);
    }
  }
}
