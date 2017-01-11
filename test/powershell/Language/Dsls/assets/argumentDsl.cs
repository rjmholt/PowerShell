using System.Management.Automation;

namespace Tests.PowerShell.Dsl
{
    [PSDsl]
    public class ArgumentDsl
    {
        enum KeywordArgumentType
        {
            Type1,
            Type2,
        }

        [PSKeyword]
        public class ArgumentKeyword : IPSKeyword
        {
            public ArgumentKeyword()
            {
                PreParse = null;
                PostParse = (dynamicKeywordStatementAst) => {
                    //TODO: Add testable, parameter-based functionality here
                    
                    return new ParseError[0];
                };
                SemanticCheck = null;
            }

            [PSKeywordArgument(ParameterName = "NamedParameter")]
            KeywordArgumentType keywordNamedArgument;

            [PSKeywordArgument]
            int keywordFirstPositionalArgument;

            [PSKeywordArgument]
            string keywordSecondPositionalArgument;

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
