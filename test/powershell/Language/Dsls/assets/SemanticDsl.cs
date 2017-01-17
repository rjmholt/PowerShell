using System.Management.Automation.Language;

[Keyword(Body = KeywordBodyMode.ScripBlock)]
class SemanticKeyword : Keyword
{
    public SemanticKeyword()
    {
        // TODO: Specify simple, testable semantic behaviors

        PreParse = (dynamicKeyword) => {
            
        };

        PostParse = (dynamicKeywordStatementAst) => {

        };

        SemanticCheck = (dynamicKeywordStatementAst) => {

        };
    }
}