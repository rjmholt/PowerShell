using System;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Collections.ObjectModel;

[Keyword()]
public class SimpleRuntimeKeyword : Keyword
{
    public SimpleRuntimeKeyword()
    {
        RuntimeCall = TestExecution;
    }

    private static Collection<object> TestExecution(DynamicKeywordStatementAst statementAst)
    {
        Console.WriteLine("Hello Friends!");
        return null;
    }
}