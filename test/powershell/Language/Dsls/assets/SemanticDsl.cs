using System.Management.Automation.Language;

[PSDsl]
class SemanticDsl
{
    [PSKeyword(Body = PSKeywordBodyMode.ScripBlock)]
    class SemanticKeyword : IPSKeyword
    {
        public SemanticKeyword()
        {
            PreParse = (dynamicKeyword) => {
                
            };

            PostParse = (dynamicKeywordStatementAst) => {

            };

            SemanticCheck = (dynamicKeywordStatementAst) => {

            };
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
    }
}