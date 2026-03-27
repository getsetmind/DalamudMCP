param(
    [string]$Solution = 'DalamudMCP.slnx',
    [string]$Output = 'artifacts/inspectcode/report.xml',
    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$outputPath = Join-Path $root $Output
$outputDirectory = Split-Path -Parent $outputPath

if (-not (Test-Path $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

if (-not $NoRestore) {
    & (Join-Path $PSScriptRoot 'restore.ps1') -Solution $Solution
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

. (Join-Path $PSScriptRoot 'Invoke-JetBrainsTool.ps1')
Invoke-JetBrainsTool -RepositoryRoot $root -Arguments @(
    'inspectcode',
    $Solution,
    "--output=$outputPath"
)
