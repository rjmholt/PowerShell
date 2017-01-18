using System.Management.Automation.Language;

[Keyword(Body = KeywordBodyMode.ScriptBlock)]
class ScopedTypeKeyword : Keyword
{
    public ScopedTypeKeyword()
    {
    }

    enum ScopedParameterType
    {
        Type1,
        Type2
    }

    [Keyword()]
    class InnerScopedTypeKeyword : Keyword
    {
        public InnerScopedTypeKeyword()
        {
        }

        [KeywordParameter()]
        ScopedParameterType InnerScopedParameter
        {
            get;
            set;
        }
    }
}