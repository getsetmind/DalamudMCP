param(
    [string]$Solution = 'DalamudMCP.sln',
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Get-DotNetCommand.ps1')
$dotnet = Get-DotNetCommand -RepositoryRoot $root
Push-Location $root
try {
    $arguments = @('test')
    if ([System.IO.Path]::GetExtension($Solution) -eq '.sln') {
        $arguments += @('--solution', $Solution)
    }
    else {
        $arguments += $Solution
    }

    if ($NoBuild) {
        $arguments += '--no-build'
    }

    & $dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}
