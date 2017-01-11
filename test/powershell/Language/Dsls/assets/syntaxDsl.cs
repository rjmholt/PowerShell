using System.Management.Automation;

namespace Tests.PowerShell.Dsl
{
    [PSDsl]
    public class SyntaxDsl
    {
        [PSKeyword(Name = PSKeywordNameMode.Required)]
        public class SyntaxNameRequiredKeyword : IPSKeyword
        {
            public SyntaxNameRequiredKeyword()
            {
                PreParse = null;
                PostParse = null;
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

        [PSKeyword(Body = PSKeywordBodyMode.Hashtable]
        public class SyntaxBodyHashtableKeyword : IPSKeyword
        {
            public SyntaxBodyHashtableKeyword()
            {
                PreParse = null;
                PostParse = null;
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

        [PSKeyword(Use = PSKeywordUseMode.RequiredMany)]
        public class SyntaxUseRequiredManyKeyword : IPSKeyword
        {
            public SyntaxUseRequiredManyKeyword()
            {
                PreParse = null;
                PostParse = null;
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

        [PSKeyword(Name = PSKeywordNameMode.Optional, Body = PSKeywordBodyMode.ScriptBlock, Use = PSKeywordUseMode.OptionalMany)]
        public class SyntaxAllOptionsKeyword : IPSKeyword
        {
            public SyntaxAllOptionsKeyword()
            {
                PreParse = null;
                PostParse = null;
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
