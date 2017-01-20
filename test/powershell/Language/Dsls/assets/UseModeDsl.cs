using System.Management.Automation.Language;

[Keyword(Use = DynamicKeywordUseMode.Required)]
public class classRequiredUseKeyword : Keyword
{
}

[Keyword(Use = DynamicKeywordUseMode.Optional)]
public class classOptionalUseKeyword : Keyword
{
}

[Keyword(Use = DynamicKeywordUseMode.RequiredMany)]
public class classRequiredManyUseKeyword : Keyword
{
}

[Keyword(Use = DynamicKeywordUseMode.OptionalMany)]
public class classOptionalManyUseKeyword : Keyword
{
}