param(
    [string]$Project = '',
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Project)) {
    $root = Split-Path -Parent $PSScriptRoot
    $errors = [System.Collections.Generic.List[string]]::new()

    $requiredFiles = @(
        'DalamudMCP.slnx',
        'DalamudMCP.CI.slnx',
        'README.md',
        '.github/workflows/ci.yml'
    )

    foreach ($requiredFile in $requiredFiles) {
        $requiredPath = Join-Path $root $requiredFile
        if (-not (Test-Path $requiredPath)) {
            $errors.Add("Required repository file is missing: $requiredFile")
        }
    }

    $readmePath = Join-Path $root 'README.md'
    if (Test-Path $readmePath) {
        $readmeContent = Get-Content $readmePath -Raw
        if ($readmeContent -match '\./docs/') {
            $errors.Add('README.md still references docs/, but docs/ is not part of the published repository.')
        }
    }

    $pluginReadmePath = Join-Path $root 'plugin/README.md'
    if (Test-Path $pluginReadmePath) {
        $errors.Add('plugin/README.md is stale in the current line and should not be shipped.')
    }

    $ciPath = Join-Path $root '.github/workflows/ci.yml'
    if (Test-Path $ciPath) {
        $ciContent = Get-Content $ciPath -Raw
        if ($ciContent -match 'working-directory:\s*DalamudMCP' -or
            $ciContent -match 'DalamudMCP/global\.json' -or
            $ciContent -match "DalamudMCP/\*\*")
        {
            $errors.Add('The CI workflow still assumes the old nested repository path.')
        }
    }

    if ($errors.Count -gt 0) {
        foreach ($error in $errors) {
            Write-Error $error
        }

        exit 1
    }

    Write-Host 'Repository architecture checks passed.'
    exit 0
}

$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Get-DotNetCommand.ps1')
$dotnet = Get-DotNetCommand -RepositoryRoot $root

Push-Location $root
try {
    $arguments = @('test', '--project', $Project)
    if ($NoBuild) {
        $arguments += '--no-build'
    }

    & $dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}
