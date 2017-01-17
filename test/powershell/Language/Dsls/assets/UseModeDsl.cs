using System.Management.Automation.Language;

[Keyword(Use = KeywordUseMode.Required)]
class RequiredUseKeyword : Keyword
{
}

[Keyword(Use = KeywordUseMode.Optional)]
class OptionalUseKeyword : Keyword
{
}

[Keyword(Use = KeywordUseMode.RequiredMany)]
class RequiredManyUseKeyword : Keyword
{
}

[Keyword(Use = KeywordUseMode.OptionalMany)]
class OptionalManyUseKeyword : Keyword
{
}