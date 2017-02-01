using System;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Collections;
using System.Collections.ObjectModel;

[Keyword()]
public class SimpleRuntimeKeyword : Keyword
{
    public SimpleRuntimeKeyword()
    {
        RuntimeCall = TestExecution;
    }

    private static object TestExecution(DynamicKeywordStatementAst statementAst)
    {
        throw new Exception("Evil");
    }
}

[Keyword()]
public class ParameterizedRuntimeKeyword : Keyword
{
    public ParameterizedRuntimeKeyword()
    {
        RuntimeCall = (keywordAst) => {
            bool expectingArgument = false;
            foreach (var cmdElement in keywordAst.CommandElements)
            {
                if (expectingArgument)
                {
                    var argument = cmdElement as ExpressionAst;
                    return GetExpressionValue(argument);
                }

                var parameter = cmdElement as CommandParameterAst;
                if (parameter == null || parameter.ParameterName != "Greeting")
                {
                    continue;
                }

                if (parameter.Argument == null)
                {
                    expectingArgument = true;
                    continue;
                }

                return GetExpressionValue(parameter.Argument);
            }

            return null;
        };
    }

    private string GetExpressionValue(ExpressionAst expr)
    {
        if (expr == null)
        {
            return null;
        }

        var exprStr = expr as StringConstantExpressionAst;
        if (exprStr != null)
        {
            return exprStr.Value;
        }

        throw new Exception("Bad expression type: " + expr.GetType());
    }

    [KeywordParameter(Position = 0)]
    public string Greeting { get; set; }
}

[Keyword(Body = DynamicKeywordBodyMode.ScriptBlock)]
public class OuterRuntimeKeyword : Keyword
{
    public OuterRuntimeKeyword()
    {
        RuntimeCall = (keywordAst) => {
            return "I'm outside";
        };
    }

    [Keyword()]
    public class InnerRuntimeKeyword : Keyword
    {
        public InnerRuntimeKeyword()
        {
            RuntimeCall = (keywordAst) => {
                return "I'm inside";
            };
        }
    }
}