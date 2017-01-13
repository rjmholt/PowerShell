<#
Tests usage of keyword syntax mode specifications
#>

using module .\DslTestSupport.psm1

Describe "DSL keyword Name/Body mode functionality" -Tags "CI" {
    $testCases = @(
        @{ attr = "Name"; value = "NoName"; success = "succeeds"; body = "{0}Dsl {{ {0}Keyword }}"; condition = 'no name' }
        @{ attr = "Name"; value = "NoName"; success = "fails"; body = "{0}Dsl {{ {0}Keyword -Name 'Foo' }}"; condition = 'name' }
        @{ attr = "Name"; value = "Required"; success = "succeeds"; body = "{0}Dsl {{ {0}Keyword -Name 'Foo' }}"; condition = "name" }
        @{ attr = "Name"; value = "Required"; success = "fails"; body = "{0}Dsl {{ {0}Keyword }}"; condition = "no name" }
        @{ attr = "Name"; value = "Optional"; success = "succeeds"; body = "{0}Dsl {{ {0}Keyword }}"; condition = "no name" }
        @{ attr = "Name"; value = "Optional"; success = "succeeds"; body = "{0}Dsl {{ {0}Keyword -Name 'Foo' }}"; condition = "name"}

        @{ attr = "Body"; value = "Command"; success = "succeeds"; body = "{0}Dsl {{ {0}Keyword }}"; condition = "no body" }
        @{ attr = "Body"; value = "Command"; success = "fails"; body = "{0}Dsl {{ {0}Keyword {{  }} }}"; condition = "ScriptBlock body" }
        @{ attr = "Body"; value = "ScriptBlock"; success = "succeeds"; body = "{0}Dsl {{ {0}Keyword {{ }} }}"; condition = "ScriptBlock body" }
        @{ attr = "Body"; value = "ScriptBlock"; success = "fails"; body = "{0}Dsl {{ {0}Keyword }}"; condition = "no body" }
        @{ attr = "Body"; value = "ScriptBlock"; success = "fails"; body = "{0}Dsl {{ {0}Keyword {{ x = 5 }} }}"; condition = "Hashtable body" }
        @{ attr = "Body"; value = "Hashtable"; success = "succeeds"; body = "{0}Dsl {{ {0}Keyword {{ x = 5 }} }}"; condition = "Hashtable body" }
        @{ attr = "Body"; value = "Hashtable"; success = "fails"; body = "{0}Dsl {{ {0}Keyword }}"; condition = "no body" }
        @{ attr = "Body"; value = "Hashtable"; success = "fails"; body = "{0}Dsl {{ {0}Keyword {{ }} }}"; condition = "ScriptBlock body" }

        @{ attr = "Use"; value = "Required"; success = "succeeds"; body = "{0}Dsl {{ {0}Keyword }}"; condition = "single keyword use" }
        @{ attr = "Use"; value = "Required"; success = "fails"; body = "{0}Dsl {{ }}"; condition = "no keyword use" }
        @{ attr = "Use"; value = "Required"; success = "fails"; body = "{0}Dsl {{ {0}Keyword; {0}Keyword }}"; condition = "double keyword use" }
        @{ attr = "Use"; value = "Optional"; success = "succeeds"; body = "{0}Dsl {{ }}"; condition = "no keyword use" }
        @{ attr = "Use"; value = "Optional"; success = "succeeds"; body = "{0}Dsl {{ {0}Keyword }}"; condition = "single keyword use" }
        @{ attr = "Use"; value = "Optional"; success = "fails"; body = "{0}Dsl {{ {0}Keyword; {0}Keyword }}"; condition = "double keyword use" }
        @{ attr = "Use"; value = "RequiredMany"; success = "succeeds"; body = "{0}Dsl {{ {0}Keyword }}"; condition = "single keyword use" }
        @{ attr = "Use"; value = "RequiredMany"; success = "succeeds"; body = "{0}Dsl {{ {0}Keyword; {0}Keyword }}"; condition = "double keyword use" }
        @{ attr = "Use"; value = "RequiredMany"; success = "fails"; body = "{0}Dsl {{ }}"; condition = "no keyword use" }
        @{ attr = "Use"; value = "OptionalMany"; success = "succeeds"; body = "{0}Dsl {{ }}"; condition = "no keyword use" }
        @{ attr = "Use"; value = "OptionalMany"; success = "succeeds"; body = "{0}Dsl {{ {0}Keyword }}"; condition = "single keyword use" }
        @{ attr = "Use"; value = "OptionalMany"; success = "succeeds"; body = "{0}Dsl {{ {0}Keyword; {0}Keyword }}"; condition = "double keyword use" }
    )

    BeforeAll {
        $envModulePath = $env:PSModulePath
        $env:PSModulePath += Get-SystemPathString -TestDrive $TestDrive
    }

    AfterAll {
        $env:PSModulePath = $envModulePath
    }

    BeforeEach {
        $context = [powershell]::Create()
        $context.AddScript($body -f "$($attr)Mode").Invoke()
    }

    AfterEach {
        $context.Dispose()
    }

    It "<attr>Mode <success> with <condition>" -TestCases $testCases {
        param($attr, $value, $success, $body, $condition)

        New-TestDllModule -TestDrive $TestDrive -ModuleName "$($attr)ModeDsl"

        $errorExpected = ($success -ne "succeeds")

        $context.HadErrors | Should Be $errorExpected
    }
}

Describe "Mixed use SyntaxMode semantics" {
    BeforeAll {
        $envModulePath = $env:PSModulePath
        $env:PSModulePath += Get-SystemPathString -TestDrive $TestDrive

        $dslName = "AllSyntaxModesDsl"
    }

    AfterAll {
        $env:PSModulePath = $envModulePath
    }

    BeforeEach {
        $context = [powershell]::Create()
        $context.AddScript("using module $dslName")
    }

    AfterEach {
        $context.Dispose()
    }

    It "successfully parses all syntax modes in the same DSL" {
        $context.AddScript("$dslName { AllSyntaxModesKeyword -Name 'Foo' {  }; AllSyntaxModesKeyword -Name 'Bar' { } }").Invoke()
        $context.HadErrors | Should Be $false
    }
}
