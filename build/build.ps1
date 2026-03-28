param(
    [string]$Solution = 'DalamudMCP.slnx',
    [string]$DalamudHome,
    [switch]$NoRestore = $true
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
    $arguments = @('build', $Solution)
    if ($NoRestore) {
        $arguments += '--no-restore'
    }

    & $dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
    Restore-DalamudHome -Scope $dalamudScope
}
