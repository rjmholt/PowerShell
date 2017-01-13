using System.Management.Automation.Language;

[PSDsl]
class NestedDsl
{
    [PSKeyword(Body = PSKeywordBodyMode.ScriptBlock, Use = PSKeywordUseMode.Optional)]
    class NestedKeyword1 : IPSKeyword
    {
        public NestedKeyword1()
        {
            PreParse = null;
            PostParse = (dynamicKeywordStatementAst) => null;
            SemanticCheck = null;
        }

        public Func<DynamicKeyword, ParseError[]> PreParse
        {
            get;
        }

        public Func<DynamicKeywordStatementAst, ParseError[]> PostParse
        {
            get;
        }

        public Func<DynamicKeywordStatementAst, ParseError[]> SemanticCheck
        {
            get;
        }

        [PSKeyword(Body = PSKeywordBodyMode.ScriptBlock, Use = PSKeywordUseMode.Optional)]
        class NestedKeyword1_1 : IPSKeyword
        {
            public NestedKeyword1_1()
            {
                PreParse = null;
                PostParse = (dynamicKeywordStatementAst) => null;
                SemanticCheck = null;
            }

            public Func<DynamicKeyword, ParseError[]> PreParse
            {
                get;
            }

            public Func<DynamicKeywordStatementAst, ParseError[]> PostParse
            {
                get;
            }

            public Func<DynamicKeywordStatementAst, ParseError[]> SemanticCheck
            {
                get;
            }

            [PSKeyword(Use = PSKeywordUseMode.Optional)]
            class NestedKeyword1_1_1 : IPSKeyword
            {
                public NestedKeyword1_1_1()
                {
                    PreParse = null;
                    PostParse = (dynamicKeywordStatementAst) => null;
                    SemanticCheck = null;
                }

                public Func<DynamicKeyword, ParseError[]> PreParse
                {
                    get;
                }

                public Func<DynamicKeywordStatementAst, ParseError[]> PostParse
                {
                    get;
                }

                public Func<DynamicKeywordStatementAst, ParseError[]> SemanticCheck
                {
                    get;
                }
            }
        }

        [PSKeyword(Use = PSKeywordUseMode.Optional)]
        class NestedKeyword1_2 : IPSKeyword
        {
            public NestedKeyword1_2()
            {
                PreParse = null;
                PostParse = (dynamicKeywordStatementAst) => null;
                SemanticCheck = null;
            }

            public Func<DynamicKeyword, ParseError[]> PreParse
            {
                get;
            }

            public Func<DynamicKeywordStatementAst, ParseError[]> PostParse
            {
                get;
            }

            public Func<DynamicKeywordStatementAst, ParseError[]> SemanticCheck
            {
                get;
            }
        }
    }

    [PSKeyword(Body = PSKeywordBodyMode.ScriptBlock, Use = PSKeywordUseMode.Optional)]
    class NestedKeyword2 : IPSKeyword
    {
        public NestedKeyword2()
        {
            PreParse = null;
            PostParse = (dynamicKeywordStatementAst) => null;
            SemanticCheck = null;
        }

        public Func<DynamicKeyword, ParseError[]> PreParse
        {
            get;
        }

        public Func<DynamicKeywordStatementAst, ParseError[]> PostParse
        {
            get;
        }

        public Func<DynamicKeywordStatementAst, ParseError[]> SemanticCheck
        {
            get;
        }

        [PSKeyword(Use = PSKeywordUseMode.Optional)]
        class NestedKeyword2_1 : IPSKeyword
        {
            public NestedKeyword2_1()
            {
                PreParse = null;
                PostParse = (dynamicKeywordStatementAst) => null;
                SemanticCheck = null;
            }

            public Func<DynamicKeyword, ParseError[]> PreParse
            {
                get;
            }

            public Func<DynamicKeywordStatementAst, ParseError[]> PostParse
            {
                get;
            }

            public Func<DynamicKeywordStatementAst, ParseError[]> SemanticCheck
            {
                get;
            }
        }

        [PSKeyword(Body = PSKeywordBodyMode.ScriptBlock, Use = PSKeywordUse.Optional)]
        class NestedKeyword2_2 : IPSKeyword
        {
            public NestedKeyword2_2()
            {
                PreParse = null;
                PostParse = (dynamicKeywordStatementAst) => null;
                SemanticCheck = null;
            }

            public Func<DynamicKeyword, ParseError[]> PreParse
            {
                get;
            }

            public Func<DynamicKeywordStatementAst, ParseError[]> PostParse
            {
                get;
            }

            public Func<DynamicKeywordStatementAst, ParseError[]> SemanticCheck
            {
                get;
            }

            [PSKeyword(Body = PSKeywordBodyMode.ScriptBlock, Use = PSKeywordUseMode.Optional)]
            class NestedKeyword2_2_1 : IPSKeyword
            {
                public NestedKeyword2_2_1()
                {
                    PreParse = null;
                    PostParse = (dynamicKeywordStatementAst) => null;
                    SemanticCheck = null;
                }

                public Func<DynamicKeyword, ParseError[]> PreParse
                {
                    get;
                }

                public Func<DynamicKeywordStatementAst, ParseError[]> PostParse
                {
                    get;
                }

                public Func<DynamicKeywordStatementAst, ParseError[]> SemanticCheck
                {
                    get;
                }

                [PSKeyword(Use = PSKeywordUseMode.Optional)]
                class NestedKeyword2_2_1_1 : IPSKeyword
                {
                    public NestedKeyword2_2_1_1()
                    {
                        PreParse = null;
                        PostParse = (dynamicKeywordStatementAst) => null;
                        SemanticCheck = null;
                    }

                    public Func<DynamicKeyword, ParseError[]> PreParse
                    {
                        get;
                    }

                    public Func<DynamicKeywordStatementAst, ParseError[]> PostParse
                    {
                        get;
                    }

                    public Func<DynamicKeywordStatementAst, ParseError[]> SemanticCheck
                    {
                        get;
                    }
                }
            }
        }
    }
}
