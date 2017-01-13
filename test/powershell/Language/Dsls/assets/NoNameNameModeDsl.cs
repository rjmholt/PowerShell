using System.Management.Automation.Language;

[PSDsl]
class NoNameModeDsl
{
    [PSKeyword(Name = PSKeywordNameMode.NoName)
    class NoNameModeKeyword : IPSKeyword
    {
        public NoNameModeKeyword()
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
