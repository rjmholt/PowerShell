using System.Management.Automation // ?

namespace Tests.Mock.PowerShell.Dsl
{
    [PSDsl]
    public class BasicDsl
    {
        [PSKeyword]
        public class BasicKeyword : IPSKeyword
        {
            SyntaxKeyword()
            {
                PreParseAction = null;
                PostParseAction = null;
                SemanticCheck = null;
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
}
