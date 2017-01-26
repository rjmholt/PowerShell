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
        $null = New-Item -ItemType Directory $dllDirPath
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
$preludeDefs

`$sb = [scriptblock]::Create('using module $moduleName')
`$null = `$sb.Invoke()

$Expression
"@

    # Write-Host $command -ForegroundColor Cyan

    $result = & $powershellExecutable -NoProfile -NonInteractive -OutputFormat XML -Command $command *>&1
    #Write-Host $result -ForegroundColor Yellow

    # Now search for the psobject to return

    if ($result -is [System.Object[]])
    {
        foreach ($obj in $result)
        {
            if ($obj -isnot [System.IO.DirectoryInfo] -and $obj -is [psobject])
            {
                return $obj
            }
        }
    }

    if ($result -is [System.IO.DirectoryInfo])
    {
        throw [System.Exception] ("DirectoryInfo: " + $result.ToString())
    }

    if ($result -is [psobject])
    {
        return $result
    }

    throw [System.Exception] ("Bad powershell result: " + $result.GetType())
}

