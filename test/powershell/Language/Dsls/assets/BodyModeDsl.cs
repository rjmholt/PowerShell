using System.Management.Automation.Language;

[Keyword(Body = DynamicKeywordBodyMode.Command)]
class CommandBodyKeyword : Keyword
{
}

[Keyword(Body = DynamicKeywordBodyMode.ScriptBlock)]
class ScriptBlockBodyKeyword : Keyword
{
}

[Keyword(Body = DynamicKeywordBodyMode.Hashtable)]
class HashtableBodyKeyword : Keyword
{
}