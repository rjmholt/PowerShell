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
    }

    public override object RuntimeLeaveScope(IEnumerable<Tuple<Keyword, object>> keywordStack, List<object> childResults)
    {
        throw new Exception("Evil");
    }
}

[Keyword()]
public class ParameterizedRuntimeKeyword : Keyword
{
    public ParameterizedRuntimeKeyword()
    {
    }

    [KeywordParameter(Position = 0)]
    public string Greeting { get; set; }

    public override object RuntimeLeaveScope(IEnumerable<Tuple<Keyword, object>> keywordStack, List<object> childResults)
    {
        return "Greeting: " + Greeting;
    }
}

[Keyword(Body = DynamicKeywordBodyMode.ScriptBlock)]
public class OuterRuntimeKeyword : Keyword
{
    public OuterRuntimeKeyword()
    {
    }

    public override object RuntimeLeaveScope(IEnumerable<Tuple<Keyword, object>> keywordStack, List<object> childResults)
    {
        return DeliverResults(keywordStack, childResults);
    }

    List<object> DeliverResults(IEnumerable<Tuple<Keyword, object>> keywordStack, List<object> childResults)
    {
        var results = new List<object>{ "I'm outside" };
        foreach (var result in childResults)
        {
            results.Add(result);
        }
        return results;
    }

    [Keyword()]
    public class InnerRuntimeKeyword : Keyword
    {
        public InnerRuntimeKeyword()
        {
        }

        public override object RuntimeLeaveScope(IEnumerable<Tuple<Keyword, object>> keywordStack, List<object> childResults)
        {
            return "I'm inside";
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
    }

    public override object RuntimeLeaveScope(IEnumerable<Tuple<Keyword, object>> keywordStack, List<object> childResults)
    {
        return new MyData(DataNum, DataString);
    }

    [KeywordParameter()]
    public int DataNum { get; set; }

    [KeywordParameter()]
    public string DataString { get; set; }
}

[Keyword(Body = DynamicKeywordBodyMode.ScriptBlock)]
public class NestedParamKeyword : Keyword
{
    [Keyword()]
    public class InnerParamKeyword : Keyword
    {
        [KeywordParameter()]
        public InnerType Param { get; set; }

        [KeywordParameter()]
        public string OtherParam { get; set; }
    }

    public enum InnerType
    {
        ValueOne,
        ValueTwo
    }

    [KeywordParameter()]
    public object OuterParam { get; set; }
}