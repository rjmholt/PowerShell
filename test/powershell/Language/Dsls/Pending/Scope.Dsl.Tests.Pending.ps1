<#
Tests scoping in DynamicKeyword contexts
#>
Import-Module $PSScriptRoot\DslTestSupport.psm1

Describe "DynamicKeyword scoping rules" -Tags "CI" {
    Context "has nested keywords" {
        BeforeAll {
            $envModulePath = $env:PSModulePath
            $env:PSModulePath += Get-SystemPathString -TestDrive $TestDrive

            $dslName = "NestedDsl"

            New-TestDllModule -TestDrive $TestDrive -ModuleName $dslName
        }

        AfterAll {
            $env:PSModulePath = $env:PSModulePath
        }

        BeforeEach {
            $context = [powershell]::Create()
            $context.AddScript("using module $dslName").Invoke()
        }

        AfterEach {
            $context.Dispose()
        }

        It "does not resolve inner keyword in outer scope" {
            $context.AddScript("NestedDsl { NestedKeyword1_1 { } }").Invoke()
            $context.HadErrors | Should Be $true
        }

        It "successfully resolves inner keyword in inner scope" {
            $context.AddScript("NestedDsl { NestedKeyword1 { NestedKeyword1_1 { } } }").Invoked()
            $context.HadErrors | Should Be $false
        }
    }

    Context "has types that are properly scoped" {
        BeforeAll {
            $envModulePath = $env:PSModulePath
            $env:PSModulePath += Get-SystemPathString -TestDrive $TestDrive

            $dslName = "ScopedTypeDsl"

            New-TestDllModule -TestDrive $TestDrive -ModuleName $dslName
        }

        BeforeEach {
            $context = [powershell]::Create()
            $context.AddScript("using module $dslName").Invoke()
        }

        AfterEach {
            $context.Dispose()
        }

        It "does not resolve inner type in outer scope" {
            $context.AddScript('ScopedTypeDsl { $x = [ScopedParameterType]::Type1; ScopedTypeKeyword { } }').Invoke()
            $context.HadErrors | Should Be $true
        }

        It "successfully resolves inner type in inner scope" {
            $context.AddScript('ScopedTypeDsl { ScopedTypeKeyword { $x = [ScopedParameterType]::Type1 } }').Invoke()
            $context.HadErrors | Should Be $false
        }

        It "resolves parameter type implicitly" {
            $context.AddScript('ScopedTypeDsl { ScopedTypeKeyword { InnerScopedTypeKeyword -InnerScopedParameter Type1 } }').Invoke()
            $context.HadErrors | Should Be $false
        }
    }
}
