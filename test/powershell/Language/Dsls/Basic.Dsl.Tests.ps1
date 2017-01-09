using module ".\TestUtils.psm1" # Change to something that works

Describe "Basic DSL syntax loading" -Tags "Feature" {
    BeforeAll {
        # Bring in helpers
        Import-Module $PSScriptRoot\TestUtils.psm1

        # Compile the DSL to a .dll
        $dllPath = "TestDrive:\basicDsl.dll"
        $csSourcePath = Join-Path (Join-Path $PSScriptRoot "assets") "basicDsl.cs"
        Compile-Dsl $csSourcePath $dllPath

        # Add the .dll's directory to the PSModulePath temporarily
        $envModulePath = $env:PSModulePath
        $env:PSModulePath += ";TestDrive:\"
    }

    AfterAll {
        $env:PSModulePath = $envModulePath
    }

    It "imports a minimal C# defined DSL" {
        try {
            using module BasicDsl
            throw "Execution Succeeded"
        }
        catch {
            $_.FullyQualifiedErrorId | Should Be "Execution Succeeded"
        }
    }
}
