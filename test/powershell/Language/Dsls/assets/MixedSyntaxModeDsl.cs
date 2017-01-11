using System.Management.Automation;

namespace Tests.PowerShell.Dsl
{
    [PSDsl]
    public class MixedSyntaxModeDsl
    {
        [PSKeyword(Name = PSKeywordNameMode.Optional)]
        public class MixedSyntaxModeNameOptionalKeyword : IPSKeyword
        {
            public MixedSyntaxModeNameOptionalKeyword()
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

        [PSKeyword(Body = PSKeywordBodyMode.ScriptBlock]
        public class MixedSyntaxModeBodyScriptBlockKeyword : IPSKeyword
        {
            public MixedSyntaxModeBodyScriptBlockKeyword()
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
        public class MixedSyntaxModeUseRequiredManyKeyword : IPSKeyword
        {
            public MixedSyntaxModeUseRequiredManyKeyword()
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

        [PSKeyword(Name = PSKeywordNameMode.Optional, Body = PSKeywordBodyMode.Hashtable, Use = PSKeywordUseMode.OptionalMany)]
        public class MixedSyntaxModeAllOptionsKeyword : IPSKeyword
        {
            public MixedSyntaxModeAllOptionsKeyword()
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
