<#
Tests DynamicKeyword addition to AST structure
#>
using module $PSScriptRoot\DslTestSupport.psm1

# Get the first instance of a DynamicKeyword of a given name within some AST
function Get-KeywordByName
{
    param([System.Management.Automation.Language.Ast] $Ast, [string] $Name)

    $Ast.Find({
        $args[0] -is [System.Management.Automation.Language.DynamicKeywordStatementAst] -and
        $args[0].Keyword.Keyword -eq $Name
    }, $true)
}

function Get-ChildKeywordByNamePath
{
    param([System.Management.Automation.Language.Ast] $Ast, [string[]] $NamePath)

    foreach ($childKwName in $NamePath)
    {
        $Ast = Get-KeywordByName -Ast $Ast -Name $childKwName
    }
    $Ast
}

Describe "Basic DSL syntax loading into AST" -Tags "CI" {
    BeforeAll {
        $dslName = "BasicDsl"

        $savedModulePath = $env:PSModulePath
        $env:PSModulePath += Get-SystemPathString -TestDrive $TestDrive

        New-TestDllModule -TestDrive $TestDrive -ModuleName $dslName
    }

    AfterAll {
        $env:PSModulePath = $savedModulePath
    }

    It "imports a minimal C# defined DSL into the AST" {
        $ast = [scriptblock]::Create("using module $dslName; $dslName").Ast

        $(Get-KeywordByName -Ast $ast -Name $dslName).Keyword.Keyword | Should Be $dslName
    }
}

Describe "More complex nested keyword structure loading into AST" -Tags "CI" {
    $testCases = @(
        @{ keywordToFind = "NestedKeyword1"; pathToKeyword = @() },
        @{ keywordToFind = "NestedKeyword2"; pathToKeyword = @() },
        @{ keywordToFind = "NestedKeyword1_1"; pathToKeyword = @("NestedKeyword1") },
        @{ keywordToFind = "NestedKeyword1_2"; pathToKeyword = @("NestedKeyword1") },
        @{ keywordToFind = "NestedKeyword2_1"; pathToKeyword = @("NestedKeyword2") },
        @{ keywordToFind = "NestedKeyword2_2"; pathToKeyword = @("NestedKeyword2") },
        @{ keywordToFind = "NestedKeyword1_1_1"; pathToKeyword = @("NestedKeyword1", "NestedKeyword1_1") },
        @{ keywordToFind = "NestedKeyword2_2_1"; pathToKeyword = @("NestedKeyword2", "NestedKeyword2_2") },
        @{ keywordToFind = "NestedKeyword2_2_1_1"; pathToKeyword = @("NestedKeyword2", "NestedKeyword2_2", "NestedKeyword2_2_1") }
    )

    BeforeAll {
        $dslName = "NestedDsl"

        $savedModulePath = $env:PSModulePath
        $env:PSModulePath += Get-SystemPathString -TestDrive $TestDrive

        New-TestDllModule -TestDrive $TestDrive -ModuleName $dslName

        $ast = [scriptblock]::Create(@"
$dslName
{
    NestedKeyword1
    {
        NestedKeyword1_1
        {
            NestedKeyword1_1_1
        }

        NestedKeyword1_2
    }

    NestedKeyword2
    {
        NestedKeyword2_1

        NestedKeyword2_2
        {
            NestedKeyword2_2_1
            {
                NestedKeyword2_2_1_1
            }
        }
    }
}
"@).Ast
    }

    AfterAll {
        $env:PSModulePath = $savedModulePath
    }

    It "contains <keywordToFind> under the top level keyword" -TestCases $testCases {
        $kw = Get-ChildKeywordByNamePath -Ast $ast -NamePath $($pathToKeyword + $keywordToFind)
        $kw.Keyword.Keyword | Should Be $keywordToFind
    }
}

Describe "Full DSL example loads into AST" -Tags "CI" {
    # TODO: Test AST of full DSL here
}
