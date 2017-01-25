$compileModuleFunc = "New-TestDllModule"

$references = @(
    'System.Management.Automation',
    'System.Management.Automation.Language',
    'System',
    'System.Collection'
)

$assetPath = Join-Path $PSScriptRoot 'assets'

function New-TestDllModule
{
    param([string] $TestDrive, [string] $ModuleName)

    $dllDirPath = Join-Path $TestDrive $ModuleName
    $dllPath = Join-Path -Path $dllDirPath -ChildPath "$ModuleName.dll"
    
    if (Test-Path $dllPath)
    {
        return
    }

    if (-not (Test-Path $dllDirPath))
    {
        New-Item -ItemType Directory $dllDirPath
    }

    $csSourcePath = Join-Path -Path $assetPath -ChildPath "$ModuleName.cs"

    Add-Type -Path $csSourcePath -OutputAssembly $dllPath # -ReferencedAssemblies $references

    #Write-Host "Created new module $ModuleName at $dllPath" -ForegroundColor Gray
}

$powershellExecutable = Join-Path -Path $PSHOME -ChildPath "powershell.exe"

function Get-ExpressionFromModuleInNewContext
{
    param([string] $TestDrive, [string] $ModuleName, [string[]] $Prelude, [string] $Expression)

    # Let the parent set up the modules
    New-TestDllModule -TestDrive $TestDrive -ModuleName $ModuleName

    # Now tell the child to import and evaluate

    $preludeDefs = $Prelude -join '`n'

    $command = @"
`$env:PSModulePath += ([System.IO.Path]::PathSeparator + '$TestDrive' + [System.IO.Path]::DirectorySeparatorChar)

$preludeDefs

`$sb = [scriptblock]::Create('using module $moduleName')
`$sb.Invoke()

$Expression
"@

    Write-Host $command -ForegroundColor Cyan

    & $powershellExecutable -NoProfile -NonInteractive -OutputFormat XML -Command $command
}

