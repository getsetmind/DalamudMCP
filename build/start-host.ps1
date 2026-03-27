param(
    [string]$PipeName,
    [int]$Port = 38473
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Get-DotNetCommand.ps1')
$dotnet = Get-DotNetCommand -RepositoryRoot $root
$cliProject = Join-Path $root 'src\DalamudMCP.Cli\DalamudMCP.Cli.csproj'
if (-not (Test-Path $cliProject)) {
    throw "CLI project was not found: $cliProject"
}

$arguments = @('run', '--project', $cliProject, '--', 'serve', 'http', '--port', $Port.ToString())
if (-not [string]::IsNullOrWhiteSpace($PipeName)) {
    $arguments += @('--pipe', $PipeName)
}

Write-Host "Starting local MCP HTTP server on port $Port..."
if ([string]::IsNullOrWhiteSpace($PipeName)) {
    Write-Host "Using plugin auto-discovery."
}
else {
    Write-Host "Using explicit pipe '$PipeName'."
}

& $dotnet @arguments
exit $LASTEXITCODE
