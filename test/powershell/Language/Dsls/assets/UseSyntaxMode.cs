using System.Management.Automation.Language;

[PSDsl]
class UseSyntaxModeDsl
{
    [PSKeyword(Use = PSKeywordUseMode.OptionalMany)]
    class UseSyntaxModeKeyword : IPSKeyword
    {
        public UseSyntaxModeKeywordName()
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
