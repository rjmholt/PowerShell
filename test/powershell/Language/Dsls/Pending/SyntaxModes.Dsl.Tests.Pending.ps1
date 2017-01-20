<#
Tests usage of keyword syntax mode specifications
#>

# TODO: Decide how and whether parse modes should fail

Import-Module $PSScriptRoot\DslTestSupport.psm1

Describe "DSL keyword Name/Body mode functionality" -Tags "CI" {
    $testCases = @(
        @{ attr = "Body"; value = "Command"; error = "success"; body = "{0}Dsl {{ {0}Keyword }}"; condition = "no body" }
        @{ attr = "Body"; value = "Command"; error = ""; body = "{0}Dsl {{ {0}Keyword {{  }} }}"; condition = "ScriptBlock body" }
        @{ attr = "Body"; value = "ScriptBlock"; error = "success"; body = "{0}Dsl {{ {0}Keyword {{ }} }}"; condition = "ScriptBlock body" }
        @{ attr = "Body"; value = "ScriptBlock"; error = "fails"; body = "{0}Dsl {{ {0}Keyword }}"; condition = "no body" }
        @{ attr = "Body"; value = "ScriptBlock"; error = "fails"; body = "{0}Dsl {{ {0}Keyword {{ x = 5 }} }}"; condition = "Hashtable body" }
        @{ attr = "Body"; value = "Hashtable"; error = "success"; body = "{0}Dsl {{ {0}Keyword {{ x = 5 }} }}"; condition = "Hashtable body" }
        @{ attr = "Body"; value = "Hashtable"; error = "fails"; body = "{0}Dsl {{ {0}Keyword }}"; condition = "no body" }
        @{ attr = "Body"; value = "Hashtable"; error = "fails"; body = "{0}Dsl {{ {0}Keyword {{ }} }}"; condition = "ScriptBlock body" }

        @{ attr = "Use"; value = "Required"; error = "success"; body = "{0}Dsl {{ {0}Keyword }}"; condition = "single keyword use" }
        @{ attr = "Use"; value = "Required"; error = "fails"; body = "{0}Dsl {{ }}"; condition = "no keyword use" }
        @{ attr = "Use"; value = "Required"; error = "fails"; body = "{0}Dsl {{ {0}Keyword; {0}Keyword }}"; condition = "double keyword use" }
        @{ attr = "Use"; value = "Optional"; error = "success"; body = "{0}Dsl {{ }}"; condition = "no keyword use" }
        @{ attr = "Use"; value = "Optional"; error = "success"; body = "{0}Dsl {{ {0}Keyword }}"; condition = "single keyword use" }
        @{ attr = "Use"; value = "Optional"; error = "fails"; body = "{0}Dsl {{ {0}Keyword; {0}Keyword }}"; condition = "double keyword use" }
        @{ attr = "Use"; value = "RequiredMany"; error = "success"; body = "{0}Dsl {{ {0}Keyword }}"; condition = "single keyword use" }
        @{ attr = "Use"; value = "RequiredMany"; error = "success"; body = "{0}Dsl {{ {0}Keyword; {0}Keyword }}"; condition = "double keyword use" }
        @{ attr = "Use"; value = "RequiredMany"; error = "fails"; body = "{0}Dsl {{ }}"; condition = "no keyword use" }
        @{ attr = "Use"; value = "OptionalMany"; error = "success"; body = "{0}Dsl {{ }}"; condition = "no keyword use" }
        @{ attr = "Use"; value = "OptionalMany"; error = "success"; body = "{0}Dsl {{ {0}Keyword }}"; condition = "single keyword use" }
        @{ attr = "Use"; value = "OptionalMany"; error = "success"; body = "{0}Dsl {{ {0}Keyword; {0}Keyword }}"; condition = "double keyword use" }
    )

    BeforeAll {
        $envModulePath = $env:PSModulePath
        $env:PSModulePath += Get-SystemPathString -TestDrive $TestDrive

        $context = [powershell]::Create()
    }

    AfterAll {
        $context.Dispose()
        $env:PSModulePath = $envModulePath
    }

    BeforeEach {
        $context.AddScript($body -f "$($attr)Mode").Invoke()
    }

    AfterEach {
        $context.Streams.ClearStreams()
    }

    It "<attr>Mode produces <error> with <condition>" -TestCases $testCases {
        param($attr, $value, $error, $body, $condition)

        New-TestDllModule -TestDrive $TestDrive -ModuleName "$($attr)ModeDsl"

        if ($error -eq "success")
        {
            $context.HadErrors | Should Be $false
        }
        else
        {
            $context.Streams.Error.FullyQualifiedErrorId -join "," | Should Be $error
        }

        $errorExpected = ($error -ne "succeeds")

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
