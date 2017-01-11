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

        public override Func<DynamicKeyword, ParseError[]> PreParse
        {
            get;
        }

        public override Func<DynamicKeywordStatementAst, ParseError[]> PostParse
        {
            get;
        }

        public override Func<DynamicKeywordStatementAst, ParseError[]> SemanticCheck
        {
            get;
        }
    }
}
