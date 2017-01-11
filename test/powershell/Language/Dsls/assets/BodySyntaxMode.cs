using System.Management.Automation.Language;

[PSDsl]
class BodySyntaxModeDsl
{
    [PSKeyword(Body = PSKeywordBodyMode.Hashtable)]
    class BodySyntaxModeKeyword : IPSKeyword
    {
        public BodySyntaxModeKeywordName()
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
