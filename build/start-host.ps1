param(
    [string]$PipeName,
    [string]$GameProcessName = 'ffxiv_dx11',
    [int]$PageSize = 50
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Get-DotNetCommand.ps1')
$dotnet = Get-DotNetCommand -RepositoryRoot $root

if ([string]::IsNullOrWhiteSpace($PipeName)) {
    $gameProcesses = @(Get-Process -Name $GameProcessName -ErrorAction SilentlyContinue)
    if ($gameProcesses.Count -eq 0) {
        throw "Process '$GameProcessName' was not found. Pass -PipeName explicitly or start the game first."
    }

    if ($gameProcesses.Count -gt 1) {
        throw "Multiple '$GameProcessName' processes were found. Pass -PipeName explicitly."
    }

    $PipeName = "DalamudMCP.$($gameProcesses[0].Id)"
}

$hostDll = Join-Path $root 'src\DalamudMCP.Host\bin\Debug\net10.0\DalamudMCP.Host.dll'
if (-not (Test-Path $hostDll)) {
    throw "Host binary was not found: $hostDll. Build the solution first."
}

Write-Host "Starting DalamudMCP.Host with pipe '$PipeName'..."
& $dotnet $hostDll --pipe-name $PipeName --page-size $PageSize
exit $LASTEXITCODE
