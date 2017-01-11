using System.Management.Automation.Language;

[PSDsl]
class NameSyntaxModeDsl
{
    [PSKeyword(Name = PSKeywordNameMode.Required)]
    class NameSyntaxModeKeyword : IPSKeyword
    {
        public NameSyntaxModeKeywordName()
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
