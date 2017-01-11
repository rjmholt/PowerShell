<#
Tests DynamicKeyword addition to AST structure
#>
using module .\DslTestSupport.psm1

function Get-KeywordByName
{
    param([System.Management.Automation.Language.Ast] $Ast, [string] $Name)

    $Ast.Find({
        $args[0] -is [System.Management.Automation.Language.DynamicKeywordStatementAst] -and
        $args[0].Keyword.Keyword -eq $Name
    }, $true)
}

function Get-ChildKeywordByName
{
    param([System.Management.Automation.Language.Ast] $Ast, [string] $ParentName, [string] $ChildName)

    $parentAst = Get-KeywordByName -Ast $Ast -Name $ParentName

    Get-KeywordByName -Ast $parentAst -Name $ChildName
}

Describe "Basic DSL syntax loading into AST" -Tags "CI" {
    BeforeAll {
        $dslName = "BasicDsl"
        $keywordName = "BasicKeyword"

        New-TestDllModule -TestDrive $TestDrive -ModuleName $dslName

        $envModulePath = $env:PSModulePath
        $env:PSModulePath += Get-SystemPathString -TestDrive $TestDrive

        $psBody = @"
using module $dslName

$dslName
{
    $keywordName
}
"@

        $ast = [scriptblock]::Create($psBody).Ast
    }

    AfterAll {
        $env:PSModulePath = $envModulePath
    }

    It "imports a minimal C# defined DSL into the AST" {
        Get-KeywordByName -Ast $ast -Name $dslName | Should Not Be $null
    }

    It "contains the inner DSL keyword in the AST" {
        Get-ChildKeywordByName -Ast $ast -ParentName $dslName -ChildName $keywordName | Should Not Be $null
    }
}

Describe "More complex nested keyword structure loading into AST" -Tags "CI" {
    BeforeAll {
        $dslName = "NestedDsl"

        New-TestDllModule -TestDrive $TestDrive -ModuleName $dslName

        $envModulePath = $env:PSModulePath
        $env:PSModulePath += Get-SystemPathString -TestDrive $TestDrive

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
        $env:PSModulePath = $envModulePath
    }

    It "has the DSL keyword as the top AST node" {
        # TODO: Check assumption that direct parent of DynamicKeywordStatementAst is ScriptBlockAst
        $topKw = $ast.Find({
            $args[0] -is [System.Management.Automation.Language.DynamicKeywordStatementAst] -and
            $args[0].Keyword.Keyword -eq $dslName -and
            $args[0].Parent -is [System.Management.Automation.Language.ScriptBlockAst]
        }, $true)

        $topKw | Should Not Be $null
    }

    It "contains NestedKeyword1 under the top level DSL keyword" {
        Get-ChildKeywordByName -Ast $ast -ParentName $dslName -ChildName "NestedKeyword1" | Should Not Be $null
    }

    It "contains NestedKeyword2 under the top level DSL keyword" {
        Get-ChildKeywordByName -Ast $ast -ParentName $dslName -ChildName "NestedKeyword2" | Should Not Be $null
    }

    It "contains NestedKeyword1_1 under NestedKeyword1" {
        Get-ChildKeywordByName -Ast $ast -ParentName "NestedKeyword1" -ChildName "NestedKeyword1_1" | Should Not Be $null
    }

    It "contains NestedKeyword1_2 under NestedKeyword1" {
        Get-ChildKeywordByName -Ast $ast -ParentName "NestedKeyword1" -ChildName "NestedKeyword1_2" | Should Not Be $null
    }

    It "contains NestedKeyword2_1 under NestedKeyword2" {
        Get-ChildKeywordByName -Ast $ast -ParentName "NestedKeyword2" -ChildName "NestedKeyword2_1" | Should Not Be $null
    }

    It "contains NestedKeyword2_2 under NestedKeyword2" {
        Get-ChildKeywordByName -Ast $ast -ParentName "NestedKeyword2" -ChildName "NestedKeyword2_2" | Should Not Be $null
    }

    It "contains NestedKeyword1_1_1 under NestedKeyword1_1" {
        Get-ChildKeywordByName -Ast $ast -ParentName "NestedKeyword1_1" -ChildName "NestedKeyword1_1_1" | Should Not Be $null
    }

    It "contains NestedKeyword2_2_1 under NestedKeyword2_2" {
        Get-ChildKeywordByName -Ast $ast -ParentName "NestedKeyword2_2" -ChildName "NestedKeyword2_2_1" | Should Not Be $null
    }

    It "contains NestedKeyword2_2_1_1 under NestedKeyword2_2_1" {
        Get-ChildKeywordByName -Ast $ast -ParentName "NestedKeyword2_2_1" -ChildName "NestedKeyword2_2_1_1" | Should Not Be $null
    }
}

Describe "Full DSL example loads into AST" -Tags "CI" {
    # TODO: Test AST of full DSL here
}
