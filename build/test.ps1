param(
    [string]$Solution = 'DalamudMCP.slnx',
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Get-DotNetCommand.ps1')
$dotnet = Get-DotNetCommand -RepositoryRoot $root
Push-Location $root
try {
    $extension = [System.IO.Path]::GetExtension($Solution)
    if ($extension -eq '.csproj') {
        $testProjects = @((Resolve-Path $Solution).Path)
    }
    else {
        $testProjects = Get-ChildItem -Path (Join-Path $root 'tests') -Filter '*.csproj' -Recurse |
            Sort-Object FullName |
            ForEach-Object FullName
    }

    foreach ($testProject in $testProjects) {
        $arguments = @('test', '--project', $testProject)
        if ($NoBuild) {
            $arguments += '--no-build'
        }

        & $dotnet @arguments
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }
}
finally {
    Pop-Location
}
