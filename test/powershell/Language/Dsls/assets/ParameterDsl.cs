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

            /*
            TODO: Figure out how (if?) positional parameters should be done...

            [PSKeywordParameter]
            int keywordIntPositionalParameter;

            [PSKeywordParameter]
            string keywordStringPositionalParameter;
            */

            [PSKeywordParameter]
            KeywordParameterType keywordCustomTypeParameter;

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
