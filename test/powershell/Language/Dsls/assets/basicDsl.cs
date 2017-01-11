using System.Management.Automation.Language;

namespace Tests.PowerShell.Dsl
{
    [PSDsl]
    public class BasicDsl
    {
        [PSKeyword]
        public class BasicKeyword : IPSKeyword
        {
            public BasicKeyword()
            {
                PreParse = null;
                // Minimal type-compliant PostParse
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
}
