function CompileDsl
{
    param([string] $DslSourcePath, [string] $DllOutputPath)

    # Fetch source
    $dslSource = Get-Content -Path $DslSourcePath

    # Set up compiler
    $compilerParams = [System.CodeDom.Compiler.CompilerParameters]::new()
    $compilerParams.GenerateExecutable = $false
    $compilerParams.OutputAssembly = $DllOutputPath

    # Perform compilation
    $csharpProvider = [System.CodeDom.Compiler.CodeDomProvider]::CreateProvider("CSharp")
    $csharpProvider.CompileAssemblyFromSource($compilerParams, $dslSource)
}
