using System.Management.Automation.Language;

[Keyword(Body = KeywordBodyMode.ScriptBlock)]
public class ScopedTypeKeyword : Keyword
{
    public ScopedTypeKeyword()
    {
    }

    public enum ScopedParameterType
    {
        Type1,
        Type2
    }

    [Keyword()]
    public class InnerScopedTypeKeyword : Keyword
    {
        public InnerScopedTypeKeyword()
        {
        }

        [KeywordParameter()]
        public ScopedParameterType InnerScopedParameter
        {
            get;
            set;
        }
    }
}