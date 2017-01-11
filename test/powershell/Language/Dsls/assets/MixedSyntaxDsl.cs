using System.Management.Automation;

namespace Tests.PowerShell.Dsl
{
    [PSDsl]
    public class MixedSyntaxDsl
    {
        [PSKeyword(Name = PSKeywordNameMode.Required)]
        public class MixedSyntaxNameRequiredKeyword : IPSKeyword
        {
            public MixedSyntaxNameRequiredKeyword()
            {
                PreParse = null;
                PostParse = null;
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

        [PSKeyword(Body = PSKeywordBodyMode.Hashtable]
        public class MixedSyntaxBodyHashtableKeyword : IPSKeyword
        {
            public MixedSyntaxBodyHashtableKeyword()
            {
                PreParse = null;
                PostParse = null;
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

        [PSKeyword(Use = PSKeywordUseMode.RequiredMany)]
        public class MixedSyntaxUseRequiredManyKeyword : IPSKeyword
        {
            public MixedSyntaxUseRequiredManyKeyword()
            {
                PreParse = null;
                PostParse = null;
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

        [PSKeyword(Name = PSKeywordNameMode.Optional, Body = PSKeywordBodyMode.ScriptBlock, Use = PSKeywordUseMode.OptionalMany)]
        public class MixedSyntaxAllOptionsKeyword : IPSKeyword
        {
            public MixedSyntaxAllOptionsKeyword()
            {
                PreParse = null;
                PostParse = null;
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
