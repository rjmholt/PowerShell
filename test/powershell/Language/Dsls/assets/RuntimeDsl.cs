using System;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

[Keyword()]
public class SimpleRuntimeKeyword : Keyword
{
    public SimpleRuntimeKeyword()
    {
        RuntimeEnterScopeCall = TestExecution;
    }

    private static object TestExecution(Keyword thisKeyword, IEnumerable<Tuple<Keyword, object>> keywordStack)
    {
        throw new Exception("Evil");
    }
}

[Keyword()]
public class ParameterizedRuntimeKeyword : Keyword
{
    public ParameterizedRuntimeKeyword()
    {
        RuntimeLeaveScopeCall = (thisKeyword, keywordStack, childResults) => {
            return "Your greeting was: " + Greeting;
        };
    }

    [KeywordParameter(Position = 0)]
    public string Greeting { get; set; }
}

[Keyword(Body = DynamicKeywordBodyMode.ScriptBlock)]
public class OuterRuntimeKeyword : Keyword
{
    public OuterRuntimeKeyword()
    {
        RuntimeLeaveScopeCall = DeliverResults;

        List<object> DeliverResults(Keyword thisKeyword, IEnumerable<Tuple<Keyword, object>> keywordStack, List<object> childResults)
        {
            var results = new List<object>{ "I'm outside" };
            foreach (var result in childResults)
            {
                results.Add(result);
            }
            return results;
        }
    }

    [Keyword()]
    public class InnerRuntimeKeyword : Keyword
    {
        public InnerRuntimeKeyword()
        {
            RuntimeLeaveScopeCall = (thisKeyword, keywordStack, childResults) => {
                return "I'm inside";
            };
        }
    }
}

public class MyData
{
    public int Count { get; }
    public string Name { get; }

    public MyData(int count, string name)
    {
        Count = count;
        Name = name;
    }
}

[Keyword()]
public class CustomTypeKeyword : Keyword
{
    public CustomTypeKeyword()
    {
        DataNum = 7;
        DataString = "Hello";

        RuntimeLeaveScopeCall = (thisKeyword, keywordStack, childResults) => {
            var keyword = thisKeyword as CustomTypeKeyword;
            if (keyword == null)
            {
                throw new ArgumentOutOfRangeException("Must call keyword on itself");
            }
            return new MyData(keyword.DataNum, keyword.DataString);
        };
    }

    [KeywordParameter()]
    public int DataNum { get; set; }

    [KeywordParameter()]
    public string DataString { get; set; }
}