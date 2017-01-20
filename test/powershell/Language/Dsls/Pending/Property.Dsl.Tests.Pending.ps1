<#
Tests DynamicKeyword Property and Parameter functionality
#>
Import-Module $PSScriptRoot\DslTestSupport.psm1

Describe "DSL keyword property handling" -Tags "CI" {

}

Describe "DSL keyword parameter handling" -Tags "CI" {
    BeforeAll {
        $envModulePath = $env:PSModulePath
        $env:PSModulePath += Get-SystemPathString -TestDrive $TestDrive

        $dslName = "ParameterDsl"
        $keywordName = "ParameterKeyword"

        New-TestDllModule -TestDrive $TestDrive -ModuleName $dslName
    }

    AfterAll {
        $env:PSModulePath = $envModulePath
    }

    It "employs a positional keyword parameter" -Pending {
        # TODO: Work out how positional parameters would be declared
    }

    It "employs a named keyword parameter" {
        $parameterContext = [powershell]::Create()
        $parameterContext.AddScript("using module $dslName").Invoke()

        $parameterName = "NamedParameter"

        $parameterContext.AddScript(@"
$dslName
{
    $keywordName -$parameterName 'Foo'
}
"@).Invoke()

        $parameterContext.HadErrors | Should Be $false
    }

    It "employs a keyword argument of custom type" {
        $parameterContext = [powershell]::Create()
        $parameterContext.AddScript("using module $dslName").Invoke()

        $customTypeParameterName = "keywordCustomTypeParameter"
        $customTypeName = "KeywordParameterType"
        $customTypeValue = "Type1"

        $parameterContext.AddScript(@"
$dslName
{
    $keywordName -$customTypeParameterName [$customTypeName]::$customTypeValue
}
"@).Invoke()

        $parameterContext.HadErrors | Should Be $false
    }
}
