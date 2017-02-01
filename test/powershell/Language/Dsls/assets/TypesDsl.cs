using System.Management.Automation;
using System.Management.Automation.Language;

[Keyword(Body = DynamicKeywordBodyMode.ScriptBlock)]
public class TypeExtension : Keyword
{
    // The type to extend
    [KeywordParameter(Position = 0, Mandatory = true)]
    public Type ExtendedType
    { get; set; }

    // Add a method to the type
    [Keyword()]
    public class Method : Keyword
    {
        // Parameter sets:
        //   -Name <string> -ScriptMethod <ScriptBlock> -- add a method defined by a scriptblock
        //   -Name <string> -CodeReference <string>     -- add a method aliasing an existing C# method

        public Method()
        {
            SemanticCheck = CheckParameters;
        }

        [KeywordParameter(Position = 0, Mandatory = true)]
        public string Name { get; set; }

        [KeywordParameter(Position = 1)]
        public ScriptBlock ScriptMethod { get; set; }

        // String should represent the delegate to be referenced
        [KeywordParameter()]
        public string CodeReference { get; set; }

        // String to represent the type containing the CodeReference delegate
        [KeywordParameter()]
        public string ReferencedType { get; set; }

        private static ParseError[] CheckParameters(DynamicKeywordStatementAst kwStmt)
        {
            var errors = new List<ParseError>();

            DynamicKeyword keyword = kwStmt.Keyword;

            if (keyword.Properties.Contains("ScriptMethod"))
            {
                if (keyword.Properties.Contains("CodeReference")))
                {
                    return new [] { new ParseError(kwStmt.Extent, "NonAllowedParameter", "The CodeReference parameter is not in the ScriptMethod set") };
                }
            }
            else if (keyword.Properties.Contains("CodeReference"))
            {
                if (keyword.Properties.Contains("ScriptMethod"))
                {
                    return new [] { new ParseError(kwStmt.Extent, "NonAllowedParameter", "The ScriptMethod parameter is not in the CodeReference set") };
                }

                string codeRef = keyword.Properties["CodeReference"];
                Type referencedType = Type.GetType()
            }
            else
            {
                return new [] { new ParseError(kwStmt.Extent, "MissingParameter", "Either ScriptMethod or CodeReference is required") };
            }

            return null;
        }

        private static void AddScriptMethod(Type extendedType, string methodName, ScriptBlock methodBody)
        {

        }
    }

    // Add a property to the type
    [Keyword()]
    public class Property : Keyword
    {
        // Parameter sets:
        //   -Name <string> -Alias <string>               -- add a property aliasing an existing property
        //   -Name <string> -ScriptProperty <scriptblock> -- add a property with a ScriptBlock getter
        //   -Name <string> -NoteProperty <object>        -- add a note property
        //   -Name <string> -CodeReference <string>       -- add a property aliasing an existing C# method

        [KeywordParameter(Position = 0, Mandatory = true)]
        public string Name { get; set; }

        [KeywordParameter()]
        public string Alias { get; set; }

        [KeywordParameter(Positon = 1)]
        public ScriptBlock ScriptProperty { get; set; }

        [KeywordParameter()]
        public object NoteProperty { get; set; }

        [KeywordParameter()]
        public string CodeReference { get; set; }
    }
}