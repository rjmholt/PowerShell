using System.Management.Automation.Language;

[Keyword(Use = DynamicKeywordUseMode.Required)]
class RequiredUseKeyword : Keyword
{
}

[Keyword(Use = DynamicKeywordUseMode.Optional)]
class OptionalUseKeyword : Keyword
{
}

[Keyword(Use = DynamicKeywordUseMode.RequiredMany)]
class RequiredManyUseKeyword : Keyword
{
}

[Keyword(Use = DynamicKeywordUseMode.OptionalMany)]
class OptionalManyUseKeyword : Keyword
{
}