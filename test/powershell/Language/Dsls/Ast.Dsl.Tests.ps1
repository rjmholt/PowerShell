<#
Tests DynamicKeyword addition to AST structure
#>
using module .\DslTestSupport.psm1

Describe "Basic DSL syntax loading into AST" -Tags "CI" {
    BeforeAll {
        $dslName = "BasicDsl"
        $keywordName = "BasicKeyword"

        New-TestDllModule -TestDrive $TESTDRIVE -ModuleName $dslName

        $envModulePath = $env:PSModulePath
        $env:PSModulePath += Get-SystemPathString -TestDrive $TESTDRIVE

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
        $dynamicKeywordStmt = $ast.Find({
            $args[0] -is [System.Management.Automation.Language.DynamicKeywordStatementAst] -and ($args[0].Keyword.Keyword -eq $dslName)
        }, $true)

        $dynamicKeywordStmt | Should Not Be $null
    }

    It "contains a DSL keyword in the AST" {
        $topDynamicKeyword = $ast.Find({ $args[0] -is [System.Management.Automation.Language.DynamicKeywordStatementAst] }, $true)

        $keywordStmtAst = $topDynamicKeyword.BodyExpression.Find({
            $args[0] -is [System.Management.Automation.Language.DynamicKeywordStatementAst] -and ($args[0].Keyword.Keyword -eq $keywordName)
        }, $true)

        $keywordStmtAst | Should Not Be $null
    }
}

Describe "More complex nested keyword structure loading into AST" -Tags "CI" {

}
