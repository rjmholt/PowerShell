using System.Management.Automation.Language;

[PSDsl]
class UseModeDsl
{
    [PSKeyword(Use = PSKeywordUseMode.Required)]
    class RequiredUseKeyword : IPSKeyword
    {
        public RequiredUseKeyword()
        {
            PreParse = null;
            PostParse = (dynamicKeywordStatementAst) => null;
            SemanticCheck = null;
        }

        public Func<DynamicKeyword, ParseError[]> PreParse
        {
            get;
        }

        public Func<DynamicKeywordStatementAst, ParseError[]> PostParse
        {
            get;
        }

        public Func<DynamicKeywordStatementAst, ParseError[]> SemanticCheck
        {
            get;
        }
    }

    [PSKeyword(Use = PSKeywordUseMode.Optional)]
    class OptionalUseKeyword : IPSKeyword
    {
        public OptionalUseKeyword()
        {
            PreParse = null;
            PostParse = (dynamicKeywordStatementAst) => null;
            SemanticCheck = null;
        }

        public Func<DynamicKeyword, ParseError[]> PreParse
        {
            get;
        }

        public Func<DynamicKeywordStatementAst, ParseError[]> PostParse
        {
            get;
        }

        public Func<DynamicKeywordStatementAst, ParseError[]> SemanticCheck
        {
            get;
        }
    }

    [PSKeyword(Use = PSKeywordUseMode.RequiredMany)]
    class RequiredManyUseKeyword : IPSKeyword
    {
        public RequiredManyUseKeyword()
        {
            PreParse = null;
            PostParse = (dynamicKeywordStatementAst) => null;
            SemanticCheck = null;
        }

        public Func<DynamicKeyword, ParseError[]> PreParse
        {
            get;
        }

        public Func<DynamicKeywordStatementAst, ParseError[]> PostParse
        {
            get;
        }

        public Func<DynamicKeywordStatementAst, ParseError[]> SemanticCheck
        {
            get;
        }
    }

    [PSKeyword(Use = PSKeywordUseMode.OptionalMany)]
    class OptionalManyUseKeyword : IPSKeyword
    {
        public OptionalManyUseKeyword()
        {
            PreParse = null;
            PostParse = (dynamicKeywordStatementAst) => null;
            SemanticCheck = null;
        }

        public Func<DynamicKeyword, ParseError[]> PreParse
        {
            get;
        }

        public Func<DynamicKeywordStatementAst, ParseError[]> PostParse
        {
            get;
        }

        public Func<DynamicKeywordStatementAst, ParseError[]> SemanticCheck
        {
            get;
        }
    }
}
