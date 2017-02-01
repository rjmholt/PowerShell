<#
Tests functionality of semantic features in DynamicKeywords
#>

Import-Module $PSScriptRoot\DslTestSupport.psm1

Describe "Execution of semantic actions" -Tags "CI" {
    BeforeAll {
        $testCases = @(
            @{phase = "PreParse"; keyword = "SimplePreParseKeyword"; errorId = "SuccessfulPreParse"},
            @{phase = "PostParse"; keyword = "SimplePostParseKeyword"; errorId = "SuccessfulPostParse"},
            @{phase = "SemanticCheck"; keyword = "SimpleSemanticCheckKeyword"; errorId = "SuccessfulSemanticCheck"}
        )


        $savedModulePath = $env:PSModulePath
        $env:PSModulePath += Get-TestDrivePathString -TestDrive $TestDrive

        $moduleName = "SemanticDsl"
        $outerKeyword = "SimpleSemanticKeyword"

        $sb = {
            $null = [scriptblock]::Create("using module SemanticDsl").Invoke()

            $errs = @{}

            $tests = $args[0] -split ";"
            foreach ($test in $tests)
            {

                $items = $test -split ","
                $phase = $items[0]
                $keyword = $items[1]

                try
                {
                    [scriptblock]::Create("SimpleSemanticKeyword { $keyword }").Invoke()
                }
                catch
                {
                    $errs[$phase] = $_.Exception.InnerException.Errors[0].ErrorId
                }
            }
            $errs
        }

        $testData = ($testCases | ForEach-Object { $_.phase,$_.keyword,$_.errorId -join "," }) -join ";"
        $errs = Get-ScriptBlockResultInNewProcess -TestDrive $TestDrive -ModuleNames $moduleName -Command $sb -Arguments $testData
    }

    AfterAll {
    }

    It "<keyword> throws <errorId> in <phase> phase" -TestCases $testCases {
        param($phase, $keyword, $errorId)

        $errs.$phase | Should Be $errorId
    }
}

Describe "Manipulation of AST/DynamicKeyword with semantic actions" -Tags "CI" {
    BeforeAll {
        $testCases = @(
            @{ action = "PostParse"; keyword = "AstManipulationPostParseKeyword"; expected = "PostParseTest" },
            @{ action = "SemanticCheck"; keyword = "AstManipulationSemanticKeyword"; expected = "SemanticTest" }
        )


        $savedModulePath = $env:PSModulePath
        $env:PSModulePath += Get-TestDrivePathString -TestDrive $TestDrive

        $moduleName = "SemanticDsl"

        $sb = {
            $null = [scriptblock]::Create("using module SemanticDsl").Invoke()

            $errs = @{}

            $tests = $args[0] -split ";"
            foreach ($test in $tests)
            {

                $items = $test -split ","
                $action = $items[0]
                $keyword = $items[1]

                try
                {
                    [scriptblock]::Create("AstManipulationSemanticKeyword { $keyword { $expected } }").Invoke()
                }
                catch
                {
                    $errs[$action] = $_.Exception.InnerException.Errors[0].ErrorId
                }
            }
            $errs
        }

        $testData = ($testCases | ForEach-Object { $_.action,$_.keyword,$_.expected -join "," }) -join ";"
        $errs = Get-ScriptBlockResultInNewProcess -TestDrive $TestDrive -ModuleNames $moduleName -Command $sb -Arguments $testData
    }

    AfterAll {
        $env:PSModulePath = $savedModulePath
    }

    It "Finds a string in the inner scriptblock and throws it in a ParseError using <action>" -TestCases $testCases {
        param($action, $keyword, $expected)
        $errs.$action | Should Be $expected
    }

    It "Adds a new property to the dynamic keyword using PreParse action" {
        $preParseSb = {
            $null = [scriptblock]::Create("using module SemanticDsl").Invoke()

            $bindingFlags = [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Instance
            $keywordProperty = [System.Management.Automation.Language.DynamicKeywordStatementAst].GetProperty("Keyword", $bindingFlags)

            $ast = [scriptblock]::Create("AstManipulationSemanticKeyword { AstManipulationPreParseKeyword }").Ast
            $subAst = $ast.Find({
                $args[0] -is [System.Management.Automation.Language.DynamicKeywordStatementAst] -and
                $keywordProperty.GetValue($args[0]).Keyword -eq "AstManipulationPreParseKeyword"
            }, $true)

            $keywordProperty.GetValue($subAst)
        }

        $keyword = Get-ScriptBlockResultInNewProcess -TestDrive $TestDrive -ModuleNames $moduleName -Command $preParseSb
        $keyword.Properties.Contains("TestKeywordProperty") | Should Be $true
    }
}