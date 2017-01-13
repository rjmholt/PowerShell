using System.Management.Automation.Language;

[PSDsl]
class ScopedTypeDsl
{
    [PSKeyword(Body = PSKeywordBodyMode.ScriptBlock)]
    class ScopedTypeKeyword : IPSKeyword
    {
        public ScopedTypeKeyword()
        {
            PreParse = null;
            PostParse = (dynamicKeywordStatementAst) => null;
            SemanticCheck = null;
        }

        Func<DynamicKeyword, ParseError[]> PreParse
        {
            get;
        }

        Func<DynamicKeywordStatementAst, ParseError[]> PostParse
        {
            get;
        }

        Func<DynamicKeywordStatementAst, ParseError[]> SemanticCheck
        {
            get;
        }

        enum ScopedParameterType
        {
            Type1,
            Type2
        }

        [PSKeyword]
        class InnerScopedTypeKeyword : IPSKeyword
        {
            public InnerScopedTypeKeyword()
            {
                PreParse = null;
                PostParse = (dynamicKeywordStatementAst) => null;
                SemanticCheck = null;
            }

            Func<DynamicKeyword, ParseError[]> PreParse
            {
                get;
            }

            Func<DynamicKeywordStatementAst, ParseError[]> PostParse
            {
                get;
            }

            Func<DynamicKeywordStatementAst, ParseError[]> SemanticCheck
            {
                get;
            }

            [PSKeywordParameter]
            ScopedParameterType InnerScopedParameter
            {
                get;
                set;
            }
        }
    }
}