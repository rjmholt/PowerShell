using System.Management.Automation.Language;

[Keyword()]
public class PropertyKeyword : Keyword
{
    public enum PropertyType
    {
        TypeOne,
        TypeTwo
    }

    [KeywordProperty()]
    public string DefaultProperty { get; set; }

    [KeywordProperty(Mandatory = true)]
    public string MandatoryProperty { get; set; }

    [KeywordProperty()]
    public int IntProperty { get; set; }

    [KeywordProperty()]
    public PropertyType CustomTypeProperty { get; set; }
}