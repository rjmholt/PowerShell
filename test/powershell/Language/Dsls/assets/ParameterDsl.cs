using System.Management.Automation;

enum KeywordParameterType
{
    Type1,
    Type2,
}

[Keyword()]
public class ParameterKeyword : Keyword
{
    public ParameterKeyword()
    {
        PostParse = (dynamicKeywordStatementAst) => {
            //TODO: Add testable, parameter-based functionality here
            return null;
        };
    }

    [KeywordParameter()]
    string NamedParameter
    {
        get; set;
    }

    [KeywordParameter(Position = 0)]
    string PositionalParameter
    {
        get; set;
    }

    [KeywordParameter(Mandatory = true)]
    string MandatoryNamedParamter
    {
        get; set;
    }

    [KeywordParameter(Position = 1, Mandatory = true)]
    string MandatoryPositionalParameter
    {
        get; set;
    }

    [KeywordParameter()]
    int IntParameter
    {
        get; set;
    }

    [KeywordParameter()]
    KeywordParameterType CustomTypeParameter
    {
        get; set;
    }
}