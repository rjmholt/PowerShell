<#
Tests usage of keyword syntax mode specifications
#>

using module .\DslTestSupport.psm1

Describe "DSL keyword name mode attributes" -Tags "CI" {
    BeforeAll {
        $envModulePath = $env:PSModulePath
        $env:PSModulePath += Get-SystemPathString -TestDrive $TestDrive

        $dslName = "NameSyntaxModeDsl"
        $keywordName = "NameSyntaxModeKeyword"

        New-TestDllModule -TestDrive $TestDrive -ModuleName $dslName
    }

    AfterAll {
        $env:PSModulePath = $envModulePath
    }

    Context "NameMode is NoName" {
        It "succeeds when no name given" {

        }

        It "fails when name is given" {
        }
    }

    Context "NameMode is Required" {
        It "succeeds when a name is provided" {

        }

        It "fails when no name is provided" {
        
        }
    }

    Context "NameMode is Optional" {
        It "succeeds when a name is provided and uses that name" {

        }

        It "succeeds when no name is provided and uses the default" {

        }
    }
}

Describe "DSL keyword body mode attributes" -Tags "CI" {
    BeforeAll {

    }

    AfterAll {

    }

    Context "BodyMode is Command" {
        It "succeeds when keyword is used alone" {

        }

        It "fails when an argument is given to the keyword" {

        }

        It "parses the scriptblock as an argument, not the body" {

        }
    }

    Context "BodyMode is ScriptBlock" {
        It "successfully parses interior of scriptblock body" {

        }

        It "fails when no body is provided" {

        }
    }

    Context "BodyMode is Hashtable" {
        It "successfully parses interior of scriptblock body" {

        }

        It "fails when no body is provided" {

        }
    }
}

Describe "DSL keyword use mode attributes" -Tags "CI" {
    BeforeAll {

    }

    AfterAll {

    }

    Context "UseMode is Required" {
        It "has Required by default" {

        }

        It "fails to parse when keyword is not used" {

        }

        It "fails to parse when keyword is used more than once" {

        }

        It "parses when keyword is used exactly once" {

        }
    }

    Context "UseMode is Optional" {
        It "parses when keyword is not used" {

        }

        It "parses when keyword is used once" {

        }

        It "fails to parse when keyword is used multiple times" {

        }
    }

    Context "UseMode is RequiredMany" {
        It "parses when keyword is used once" {

        }

        It "parses when keyword is used many times" {

        }

        It "fails to parse when keyword is not used" {

        }
    }

    Context "UseMode is OptionalMany" {
        It "parses when keyword is not used" {

        }

        It "parses when keyword is used many times" {

        }
    }
}

Describe "Mixed use SyntaxMode semantics" {

}
