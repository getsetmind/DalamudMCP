param(
    [string]$Solution = 'DalamudMCP.slnx',
    [string]$DalamudHome,
    [switch]$SkipRestore,
    [switch]$SkipInspect
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$toolManifestPath = Join-Path $root '.config\dotnet-tools.json'

if (-not $SkipRestore) {
    & (Join-Path $PSScriptRoot 'restore.ps1') -Solution $Solution -DalamudHome $DalamudHome
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

& (Join-Path $PSScriptRoot 'format.ps1') -Solution $Solution -NoRestore
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if (-not $SkipInspect -and -not (Test-Path $toolManifestPath)) {
    Write-Host 'Skipping inspect-rider because .config/dotnet-tools.json is not present.'
    $SkipInspect = $true
}

if (-not $SkipInspect) {
    & (Join-Path $PSScriptRoot 'inspect-rider.ps1') -Solution $Solution -NoRestore
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

& (Join-Path $PSScriptRoot 'build.ps1') -Solution $Solution -DalamudHome $DalamudHome -NoRestore
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& (Join-Path $PSScriptRoot 'test.ps1') -Solution $Solution -DalamudHome $DalamudHome
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& (Join-Path $PSScriptRoot 'architecture.ps1')
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
