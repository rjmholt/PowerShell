Describe "Basic DSL syntax loading" -Tags "Feature" {
    BeforeAll {
        # Bring in helpers
        Import-Module $PSScriptRoot\DslTestSupport.psm1

        # Compile the DSL to a .dll
        $dllPath = Join-Path $TESTDRIVE "\basicDsl.dll"
        $csSourcePath = Join-Path (Join-Path $PSScriptRoot "assets") "basicDsl.cs"
        CompileDsl -DslSourcePath $csSourcePath -DllOutputPath $dllPath

        # Add the .dll's directory to the PSModulePath temporarily
        $envModulePath = $env:PSModulePath
        $env:PSModulePath += ";$TESTDRIVE\"
    }

    AfterAll {
        $env:PSModulePath = $envModulePath
    }

    It "imports a minimal C# defined DSL into the AST" {
        $err = $null
        $ast = [System.Management.Automation.Language.Parser]::ParseInput("using module BasicDsl", [ref]$null, [ref]$err)

        $err.Count | Should Be 0
    }

    It "contains a DSL keyword in the ASt" {
        $ast = [System.Management.Automation.Language.Parser]::ParseInput("using module BasicDsl", [ref]$null, [ref]$err)

        # TODO: Ensure keyword is present in the AST node defined
    }
}
