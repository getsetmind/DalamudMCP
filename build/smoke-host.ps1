param(
    [string]$PipeName,
    [string]$GameProcessName = 'ffxiv_dx11'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Get-DotNetCommand.ps1')
$dotnet = Get-DotNetCommand -RepositoryRoot $root
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).
    IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$tracePath = Join-Path $env:TEMP 'DalamudMCP.bridge.trace.log'

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

$smokeProject = Join-Path $root 'src\DalamudMCP.Smoke\DalamudMCP.Smoke.csproj'
if (-not (Test-Path $smokeProject)) {
    throw "Smoke project was not found: $smokeProject"
}

Write-Host "Running smoke check against pipe '$PipeName'..."
Write-Host "Current PowerShell admin: $isAdmin"
Remove-Item -Path $tracePath -Force -ErrorAction SilentlyContinue
$buildOutput = Join-Path $env:TEMP ("dalamudmcp-smoke-build-" + [guid]::NewGuid().ToString("N"))
$smokeDll = Join-Path $buildOutput 'DalamudMCP.Smoke.dll'
New-Item -ItemType Directory -Path $buildOutput | Out-Null

& $dotnet build $smokeProject -o $buildOutput
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$stdoutPath = Join-Path $env:TEMP ("dalamudmcp-smoke-stdout-" + [guid]::NewGuid().ToString("N") + ".log")
$stderrPath = Join-Path $env:TEMP ("dalamudmcp-smoke-stderr-" + [guid]::NewGuid().ToString("N") + ".log")

try {
    Write-Host "Launching smoke client..."
    $process = Start-Process `
        -FilePath $dotnet `
        -ArgumentList @($smokeDll, $PipeName) `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -NoNewWindow `
        -PassThru

    if (-not ($process | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue)) {
        Write-Host "Smoke client timed out after 10 seconds. Killing process."
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }

    if (Test-Path $stdoutPath) {
        @(Get-Content -Path $stdoutPath)
    }

    if (Test-Path $stderrPath) {
        $stderrLines = @(Get-Content -Path $stderrPath)
        if ($stderrLines.Count -gt 0) {
            Write-Host "--- stderr ---"
            $stderrLines
        }
    }

    if (Test-Path $tracePath) {
        Write-Host "--- bridge trace ---"
        @(Get-Content -Path $tracePath)
    }

    exit $process.ExitCode
}
finally {
    Remove-Item -Path $stdoutPath -Force -ErrorAction SilentlyContinue
    Remove-Item -Path $stderrPath -Force -ErrorAction SilentlyContinue
    Remove-Item -Path $buildOutput -Recurse -Force -ErrorAction SilentlyContinue
}
