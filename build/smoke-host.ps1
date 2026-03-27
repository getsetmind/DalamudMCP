param(
    [string]$PipeName
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

$baseArguments = @('run', '--project', $cliProject, '--')
if (-not [string]::IsNullOrWhiteSpace($PipeName)) {
    $baseArguments += @('--pipe', $PipeName)
}

$commands = @(
    @('session', 'status', '--json'),
    @('player', 'context', '--json')
)

foreach ($command in $commands) {
    Write-Host "Running smoke command: $($command -join ' ')"
    & $dotnet @baseArguments @command
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
