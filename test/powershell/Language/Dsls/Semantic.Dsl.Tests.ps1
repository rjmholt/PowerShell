<#
Tests functionality of semantic features in DynamicKeywords
#>

import module $PSScriptRoot\DslTestSupport.psm1

Describe "Execution of semantic actions" -Tags "CI" {
    $testCases = @(
        @{phase = "PreParse"; keyword = "SimplePreParseKeyword"; errorId = "SuccessfulPreParse"},
        @{phase = "PostParse"; keyword = "SimplePostParseKeyword"; errorId = "SuccessfulPostParse"},
        @{phase = "SemanticCheck"; keyword = "SimpleSemanticCheckKeyword"; errorId = "SuccessfulSemanticCheck"}
    )

    BeforeAll {
        $savedModulePath = $env:PSModulePath
        $env:PSModulePath += Get-TestDrivePathString -TestDrive $TestDrive

        $moduleName = "SemanticDsl"
        $outerKeyword = "SimpleSemanticKeyword"

        New-TestDllModule -TestDrive $TestDrive -ModuleName $moduleName

        $context = [powershell]::Create()
    }

    AfterAll {
        $context.Dispose()
    }

    BeforeEach {
        $context.Streams.ClearStreams()
    }

    It "throws a simple error in <phase> phase" -TestCases $testCases {
        try
        {
            $context.AddScript("$outerKeyword { $keyword }")
        }
        catch
        {
            $_.FullyQualifiedErrorId | Should Be $errorId
        }
    }
}

Describe "Manipulation of AST/DynamicKeyword with semantic actions" -Tags "CI" {
    $testCases = @(
        @{ action = "PostParse"; keyword = "AstManipulationPostParseKeyword"; expected = "PostParseTest" },
        @{ action = "SemanticCheck"; keyword = "AstManipulationSemanticKeyword"; expected = "SemanticTest" },
    )

    BeforeAll {
        $savedModulePath = $env:PSModulePath
        $env:PSModulePath += Get-TestDrivePathString -TestDrive $TestDrive

        $moduleName = "SemanticDsl"
        $outerKeyword = "AstManipulationSemanticKeyword"

        New-TestDllModule -TestDrive $TestDrive -ModuleName $moduleName

        $context = [powershell]::Create()
    }

    AfterAll {
        $context.Dispose()
    }

    BeforeEach {
        $context.Streams.ClearStreams()
    }

    It "Finds a string in the inner scriptblock and throws it in a ParseError using <action>" -TestCases $testCases {
        try
        {
            $context.AddScript("$outerKeyword { $keyword { $expected } }").Invoke()
        }
        catch
        {
            $_.FullyQualifiedErrorId | Should Be $expected
        }
    }

    It "Adds a new property to the dynamic keyword using PreParse action" {
        $addedProperty = "TestKeywordProperty"
        $keyword = "AstManipulationPreParseKeyword"
        $context.AddScript("$outerKeyword { $keyword }").Invoke()
        $kwObj = $context.AddScript("[System.Management.Automation.Language.DynamicKeyword]::GetKeyword('$keyword')").Invoke()
        $kwObj.Properties.$addedProperty | Should BeOfType [System.Management.Automation.Language.DynamicKeywordProperty]
    }
}