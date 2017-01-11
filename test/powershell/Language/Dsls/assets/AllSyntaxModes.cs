using System.Management.Automation.Language;

[PSDsl]
class AllSyntaxModesDsl
{
    [PSKeyword(Name = PSKeywordNameMode.Required, Body = PSKeywordBodyMode.Hashtable, Use = PSKeywordUseMode.OptionalMany)]
    class AllSyntaxModesKeyword : IPSKeyword
    {
        public AllSyntaxModesKeyword()
        {
            PreParse = null;
            PostParse = (dynamicKeywordStatementAst) => null;
            SemanticCheck = null;
        }

        public override Func<DynamicKeyword, ParseError[]>PreParse
        {
            get;
        }

        public override Func<DynamicKeywordStatementAst, ParseError[]>PostParse
        {
            get;
        }

        public override Func<DynamicKeywordStatementAst, ParseError[]>SemanticCheck
        {
            get;
        }
    }
}
