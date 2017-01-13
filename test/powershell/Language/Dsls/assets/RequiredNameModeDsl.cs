using System.Management.Automation.Language;

[PSDsl]
class RequiredModeDsl
{
    [PSKeyword(Name = PSKeywordNameMode.Required)
    class RequiredModeKeyword : IPSKeyword
    {
        public RequiredModeKeyword()
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
