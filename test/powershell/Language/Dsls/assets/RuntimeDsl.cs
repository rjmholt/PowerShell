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
        RuntimeCall = TestExecution;
    }

    private static object TestExecution(Keyword thisKeyword, Stack<Keyword> keywordStack)
    {
        throw new Exception("Evil");
    }
}

[Keyword()]
public class ParameterizedRuntimeKeyword : Keyword
{
    public ParameterizedRuntimeKeyword()
    {
        RuntimeCall = (thisKeyword, keywordStack) => {
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
        RuntimeCall = (thisKeyword, keywordStack) => {
            return "I'm outside";
        };
    }

    [Keyword()]
    public class InnerRuntimeKeyword : Keyword
    {
        public InnerRuntimeKeyword()
        {
            RuntimeCall = (thisKeyword, keywordStack) => {
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
        RuntimeCall = (thisKeyword, keywordStack) => {
            return new MyData(7, "Hello");
        };
    }
}