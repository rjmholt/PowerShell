using System.Management.Automation.Language;

[Keyword(Body = DynamicKeywordBodyMode.ScriptBlock)]
class NestedDsl
{
    [Keyword(Body = DynamicKeywordBodyMode.ScriptBlock)]
    class NestedKeyword1 : Keyword
    {
        [Keyword(Body = DynamicKeywordBodyMode.ScriptBlock)]
        class NestedKeyword1_1 : Keyword
        {
            [Keyword()]
            class NestedKeyword1_1_1 : Keyword
            {
            }
        }

        [Keyword(Use = PSKeywordUseMode.Optional)]
        class NestedKeyword1_2 : Keyword
        {
        }
    }

    [Keyword(Body = DynamicKeywordBodyMode.ScriptBlock)]
    class NestedKeyword2 : Keyword
    {
        [Keyword()]
        class NestedKeyword2_1 : Keyword
        {
        }

        [Keyword(Body = DynamicKeywordBodyMode.ScriptBlock)]
        class NestedKeyword2_2 : Keyword
        {
            [Keyword(Body = DynamicKeywordBodyMode.ScriptBlock)]
            class NestedKeyword2_2_1 : Keyword
            {
                [Keyword()]
                class NestedKeyword2_2_1_1 : Keyword
                {
                }
            }
        }
    }
}
