param(
    [string]$Solution = 'DalamudMCP.slnx',
    [switch]$NoRestore,
    [string]$Profile = 'Built-in: Reformat & Apply Syntax Style'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot

if (-not $NoRestore) {
    & (Join-Path $PSScriptRoot 'restore.ps1') -Solution $Solution
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

. (Join-Path $PSScriptRoot 'Invoke-JetBrainsTool.ps1')
Invoke-JetBrainsTool -RepositoryRoot $root -Arguments @(
    'cleanupcode',
    $Solution,
    "--profile=$Profile"
)
