$compileModuleFunc = "New-TestDllModule"

$references = @(
    'System.Management.Automation',
    'System',
    'System.Collections'
)

$assetPath = Join-Path $PSScriptRoot 'assets'

$powershellExecutable = Join-Path -Path $PSHOME -ChildPath "powershell.exe"

function Get-TestDrivePathString
{
    param([string] $TestDrive)
    [System.IO.Path]::PathSeparator + $TestDrive + [System.IO.Path]::DirectorySeparatorChar
}

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

    Add-Type -Path $csSourcePath -OutputAssembly $dllPath -ReferencedAssemblies $references
}

function Get-ScriptBlockResultInNewProcess
{
    param ([string] $TestDrive, [string[]] $ModuleNames, [scriptblock] $Command, [object[]] $Arguments)

    foreach ($moduleName in $ModuleNames)
    {
        New-TestDllModule -TestDrive $TestDrive -ModuleName $moduleName
    }

    $result = & $powershellExecutable -NoProfile -NonInteractive -OutputFormat XML -Command $Command -args $Arguments *>&1

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

    if ($result -is [psobject])
    {
        return $result
    }

    if ($result -eq $null)
    {
        return $null
    }

    throw [System.Exception] ("Bad powershell result: " + $result.GetType())
}

function Get-ExpressionFromModuleInNewProcess
{
    param([string] $TestDrive, [string[]] $ModuleNames, [string[]] $Prelude, [string] $Expression)

    # Let the parent set up the modules
    foreach ($moduleName in $ModuleNames)
    {
        New-TestDllModule -TestDrive $TestDrive -ModuleName $ModuleName
    }

    # Tell the child to import and evaluate

    $preludeDefs = $Prelude -join '`n'

    # Put `using module $moduleName` on new lines
    $moduleImports = "@'`n" + (( $ModuleNames | ForEach-Object { "using module " + $_ }) -join "`n") + "`n'@"

    $command = @"
$preludeDefs

`$sb = [scriptblock]::Create($moduleImports)
`$null = `$sb.Invoke()

$Expression
"@

    $result = & $powershellExecutable -NoProfile -NonInteractive -OutputFormat XML -Command $command *>&1

    # Search for the psobject to return

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

    if ($result -is [psobject])
    {
        return $result
    }

    if ($result -eq $null)
    {
        return $null
    }

    throw [System.Exception] ("Bad powershell result: " + $result.GetType())
}

