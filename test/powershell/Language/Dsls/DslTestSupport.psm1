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
    $csSourcePath = Join-Path (Join-Path $PSScriptRoot "assets") "$ModuleName.cs"

    <#
    $references = @(
        "System.Management.Automation",
        "System.Management.Automation.Language"
    )
    #>

    Add-Type -Path $csSourcePath -OutputAssembly $dllPath -Ref $references
}

function Get-SystemPathString
{
    param([string] $TestDrive)

    [System.IO.Path]::PathSeparator + $TestDrive + [System.IO.Path]::DirectorySeparatorChar
}
