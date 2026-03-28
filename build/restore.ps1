param(
    [string]$Solution = 'DalamudMCP.slnx',
    [string]$DalamudHome
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Get-DotNetCommand.ps1')
. (Join-Path $PSScriptRoot 'Use-DalamudHome.ps1')
$dotnet = Get-DotNetCommand -RepositoryRoot $root
$dalamudScope = Use-DalamudHome -DalamudHome $DalamudHome
Push-Location $root
try {
    & $dotnet restore $Solution
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    if (Test-Path (Join-Path $root '.config\dotnet-tools.json')) {
        & $dotnet tool restore
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }
}
finally {
    Pop-Location
    Restore-DalamudHome -Scope $dalamudScope
}
