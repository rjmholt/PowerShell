using System.Management.Automation;

namespace Tests.PowerShell.Dsl
{
    [PSDsl]
    public class ParameterDsl
    {
        enum KeywordParameterType
        {
            Type1,
            Type2,
        }

        [PSKeyword]
        public class ParameterKeyword : IPSKeyword
        {
            public ParameterKeyword()
            {
                PreParse = null;
                PostParse = (dynamicKeywordStatementAst) => {
                    //TODO: Add testable, parameter-based functionality here
                    
                    return new ParseError[0];
                };
                SemanticCheck = null;
            }

            [PSKeywordParameter]
            KeywordParameterType keywordCustomPositionalParameter;

            [PSKeywordParameter]
            int keywordIntPositionalParameter;

            [PSKeywordParameter]
            string keywordStringPositionalParameter;

            [PSKeywordParameter(ParameterName = "NamedParameter")]
            string keywordStringNamedParameter;

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
