param(
    [string]$Solution = 'DalamudMCP.slnx',
    [switch]$Fix,
    [switch]$NoRestore,
    [ValidateSet('rider', 'dotnet')]
    [string]$Engine = 'rider'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot

if ($Engine -eq 'rider') {
    if (-not $Fix) {
        Write-Host 'Rider CleanupCode does not support verify-no-changes. Falling back to dotnet format for verification.'
        $Engine = 'dotnet'
    }
    else {
        & (Join-Path $PSScriptRoot 'format-rider.ps1') -Solution $Solution -NoRestore:$NoRestore
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }

        return
    }
}

. (Join-Path $PSScriptRoot 'Get-DotNetCommand.ps1')
$dotnet = Get-DotNetCommand -RepositoryRoot $root
Push-Location $root
try {
    $arguments = @('format', $Solution)
    if (-not $Fix) {
        $arguments += '--verify-no-changes'
    }

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
}
