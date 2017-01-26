using System.Management.Automation;
using System.Management.Automation.Language;

[Keyword(Body = DynamicKeywordBodyMode.ScriptBlock)]
public class TypeExtension : Keyword
{
    [KeywordParameter(Position = 0, Mandatory = true)]
    public Type ExtendedType
    { get; set; }

    [Keyword()]
    public class Method : Keyword
    {
        [KeywordParameter(Position = 0, Mandatory = true)]
        public string Name { get; set; }

        [KeywordParameter(Position = 1)]
        public ScriptBlock MethodBody { get; set; }

        // String should represent the delegate to be referenced
        [KeywordParameter()]
        public string CodeReference { get; set; }
    }

    [Keyword()]
    public class Property : Keyword
    {
        [KeywordParameter(Position = 0, Mandatory = true)]
        public string Name { get; set; }

        [KeywordParameter()]
        public string Alias { get; set; }

        [KeywordParameter(Positon = 1)]
        public ScriptBlock Getter { get; set; }
    }
}