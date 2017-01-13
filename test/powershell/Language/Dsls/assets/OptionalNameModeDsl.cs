using System.Management.Automation.Language;

[PSDsl]
class OptionalModeDsl
{
    [PSKeyword(Name = PSKeywordNameMode.Optional)
    class OptionalModeKeyword : IPSKeyword
    {
        public OptionalModeKeyword()
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
