using System.Management.Automation.Language;

namespace Tests.PowerShell.Dsl
{
    [PSDsl]
    public class BasicDsl
    {
        [PSKeyword(Body = PSKeywordBodyMode.ScriptBlock)]
        public class BasicKeyword : IPSKeyword
        {
            public BasicKeyword()
            {
                PreParse = null;
                // Minimal type-compliant PostParse
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
}
