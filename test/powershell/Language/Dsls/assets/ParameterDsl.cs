using System.Management.Automation.Language;

public enum KeywordParameterType
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
    public string NamedParameter
    {
        get; set;
    }

    [KeywordParameter(Position = 0)]
    public string PositionalParameter
    {
        get; set;
    }

    [KeywordParameter(Mandatory = true)]
    public string MandatoryNamedParamter
    {
        get; set;
    }

    [KeywordParameter(Position = 1, Mandatory = true)]
    public string MandatoryPositionalParameter
    {
        get; set;
    }

    [KeywordParameter()]
    public int IntParameter
    {
        get; set;
    }

    [KeywordParameter()]
    public KeywordParameterType CustomTypeParameter
    {
        get; set;
    }
}