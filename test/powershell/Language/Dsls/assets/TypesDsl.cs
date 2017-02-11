using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using System.Management.Automation.Internal;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection;
using System;

[Keyword(Body = DynamicKeywordBodyMode.ScriptBlock)]
public class TypeExtension : Keyword
{
    private static MethodInfo GetMethodInfo(string referencedTypeName, string referencedMethodName)
    {
        Type refType = Type.GetType(referencedTypeName, throwOnError: true, ignoreCase: true);
        return refType.GetMethod(referencedMethodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.IgnoreCase);
    }

    // The type to extend
    [KeywordParameter(Position = 0, Mandatory = true)]
    public string ExtendedType { get; set; }

    private TypeData _typeData;

    public override object RuntimeEnterScope(IEnumerable<Tuple<Keyword, object>> parentKeywordSetups)
    {
        _typeData = new TypeData(ExtendedType);
        return _typeData;
    }

    public override object RuntimeLeaveScope(IEnumerable<Tuple<Keyword, object>> parentKeywordSetups, List<object> childResults)
    {
        var errors = new ConcurrentBag<string>();

        PropertyInfo contextProperty = typeof(TypeExtension).GetProperty("Context", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        Console.WriteLine("contextProperty");
        object context = contextProperty.GetValue(this, BindingFlags.NonPublic, null, null, null);
        Console.WriteLine("context");
        PropertyInfo typeTableProperty = context.GetType().GetProperty("TypeTable", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        Console.WriteLine("typeTableProperty");
        object typeTable = typeTableProperty.GetValue(context, BindingFlags.NonPublic, null, null, null);
        Console.WriteLine("typeTable");
        MethodInfo processType = typeTable.GetType().GetMethod("ProcessTypeDataToAdd", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        Console.WriteLine("processType");

        processType.Invoke(typeTable, new object[] { errors, _typeData });

        return errors;
    }
    
    // Add a method to the type
    [Keyword()]
    public class Method : Keyword
    {
        // Parameter sets:
        //   -Name <string> -ScriptMethod <ScriptBlock> -- add a method defined by a scriptblock
        //   -Name <string> -CodeReference <string>     -- add a method aliasing an existing C# method

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

        public override object RuntimeLeaveScope(IEnumerable<Tuple<Keyword, object>> parentKeywordSetups, List<object> childResults)
        {
            TypeData td = null;
            foreach (var result in parentKeywordSetups)
            {
                if (result.Item2 is TypeData)
                {
                    td = (TypeData)result.Item2;
                }
            }

            if (ScriptMethod != null)
            {
                td.Members.Add(Name, new ScriptMethodData(Name, ScriptMethod));
                return null;
            }

            if (!(String.IsNullOrEmpty(CodeReference) || String.IsNullOrEmpty(ReferencedType)))
            {
                td.Members.Add(Name, new CodeMethodData(Name, GetMethodInfo(ReferencedType, CodeReference)));
                return null;
            }

            throw new Exception("Necessary parameters were not provided");
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

        [KeywordParameter(Position = 1)]
        public ScriptBlock ScriptProperty { get; set; }

        [KeywordParameter()]
        public object NoteProperty { get; set; }

        [KeywordParameter()]
        public string CodeReference { get; set; }

        [KeywordParameter()]
        public string ReferencedType { get; set; }

        public override object RuntimeLeaveScope(IEnumerable<Tuple<Keyword, object>> parentKeywordSetups, List<object> childResults)
        {
            TypeData td = null;
            foreach (var result in parentKeywordSetups)
            {
                if (result.Item2 is TypeData)
                {
                    td = (TypeData)result.Item2;
                }
            }

            if (ScriptProperty != null)
            {
                 td.Members.Add(Name, new ScriptPropertyData(Name, ScriptProperty));
                 return null;
            }

            if (NoteProperty != null)
            {
                td.Members.Add(Name, new NotePropertyData(Name, NoteProperty));
                return null;
            }

            if (!String.IsNullOrEmpty(Alias))
            {
                td.Members.Add(Name, new AliasPropertyData(Name, Alias));
                return null;
            }

            if (!(String.IsNullOrEmpty(CodeReference) || String.IsNullOrEmpty(ReferencedType)))
            {
                td.Members.Add(Name, new CodePropertyData(Name, GetMethodInfo(ReferencedType, CodeReference)));
                return null;
            }

            throw new Exception("Not enough parameters provided");
        }
    }
}