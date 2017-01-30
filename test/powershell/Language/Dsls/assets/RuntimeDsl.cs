using System.Management.Automation.Language;
using System.Collections.ObjectModel;

[Keyword()]
public class SimpleRuntimeKeyword : Keyword
{
    public SimpleRuntimeKeyword()
    {
        RuntimeCall = TestExecution;
    }

    private static ICollection<PSObject> TestExecution(DynamicKeywordStatementAst statementAst)
    {
        throw new Exception("SimpleRuntimeKeywordTest");
    }
}