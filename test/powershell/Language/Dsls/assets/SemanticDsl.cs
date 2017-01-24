using System.Management.Automation.Language;

static class Helper
{
    private static ScriptPosition s_emptyPosition;
    private static IScriptExtent s_emptyExtent;
    public static IScriptPosition EmptyPosition
    {
        get
        {
            return s_emptyPosition ??
                (s_emptyPosition = new ScriptPosition("", 0, 0, "", ""));
        }
    }
    public static IScriptExtent EmptyExtent
    {
        get
        {
            return s_emptyExtent ??
                (s_emptyExtent = new ScriptExtent(EmptyPosition, EmptyPosition));
        }
    }
}

[Keyword(Body = DynamicKeywordBodyMode.ScriptBlock)]
public class SimpleSemanticKeyword : Keyword
{
    public SimpleSemanticKeyword()
    {
    }

    [Keyword()]
    public class SimplePreParseKeyword : Keyword
    {
        public SimplePreParseKeyword()
        {
            PreParse = (dynamicKeyword) => {
                var error = new ParseError(Helper.EmptyExtent, "SuccessfulPreParse", "Successful PreParse action");
                return new [] { error };
            };
        }
    }

    [Keyword()]
    public class SimplePostParseKeyword : Keyword
    {
        public SimplePostParseKeyword()
        {
            PostParse = (dynamicKeywordStatementAst) => {
                var error = new ParseError(Helper.EmptyExtent, "SuccessfulPostParse", "Successful PostParse action");
                return new [] { error };
            };
        }
    }

    [Keyword()]
    public class SimpleSemanticCheckKeyword : Keyword
    {
        public SimpleSemanticCheckKeyword()
        {
            SemanticCheck = (dynamicKeywordStatementAst) => {
                var error = new ParseError(Helper.EmptyExtent, "SuccessfulSemanticAction", "Successful SemanticCheck action");
                return new [] { error };
            };
        }
    }
}

[Keyword(Body = DynamicKeywordBodyMode.ScriptBlock)]
public class AstManipulationSemanticKeyword : Keyword
{
    public AstManipulationSemanticKeyword()
    {
    }

    // This keyword actually just manipulates the DynamicKeyword data structure,
    // rather than the AST
    [Keyword()]
    public class AstManipulationPreParseKeyword : Keyword
    {
        public AstManipulationPreParseKeyword()
        {
            PreParse = (dynamicKeyword) => {
                var dkProperty = new DynamicKeywordProperty()
                {
                    Name = "TestKeywordProperty"
                };
                dynamicKeyword.Properties.Add(dkProperty.Name, dkProperty);
                return null;
            };
        }
    }

    [Keyword(Body = DynamicKeywordBodyMode.ScriptBlock)]
    public class AstManipulationPostParseKeyword : Keyword
    {
        public AstManipulationPostParseKeyword()
        {
            PostParse = (dynamicKeywordStatementAst) => {
                var arg = dynamicKeywordStatementAst.Find(ast => {
                    var expAst = ast as StringConstantExpressionAst;
                    if (expAst != null)
                    {
                        return expAst.Value == "PostParseTest";
                    }
                    return false;
                }, true);

                var strAst = arg as StringConstantExpressionAst;
                if (strAst == null)
                {
                    throw new NullReferenceException("strAst should not be null in PostParse test");
                }

                var error = new ParseError(Helper.EmptyExtent, strAst.Value, "Inner expression: " + strAst.Value);
                return new [] { error };
            };
        }
    }

    [Keyword(Body = DynamicKeywordBodyMode.ScriptBlock)]
    public class AstManipulationSemanticCheckKeyword : Keyword
    {
        public AstManipulationSemanticCheckKeyword()
        {
            SemanticCheck = (dynamicKeywordStatementAst) => {
                var arg = dynamicKeywordStatementAst.Find(ast => {
                    var expAst = ast as StringConstantExpressionAst;
                    if (expAst != null)
                    {
                        return expAst.Value == "SemanticTest";
                    }
                    return false;
                }, true);

                var strAst = arg as StringConstantExpressionAst;
                if (strAst == null)
                {
                    throw new NullReferenceException("strAst should not be null in SemanticCheck test");
                }

                var error = new ParseError(Helper.EmptyExtent, strAst.Value, "Inner expression: " + strAst.Value);
                return new [] { error };
            };
        }
    }
}