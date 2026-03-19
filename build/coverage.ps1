param(
    [string]$Solution = 'DalamudMCP.sln',
    [string]$OutputDirectory = 'artifacts/coverage',
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Get-DotNetCommand.ps1')
$dotnet = Get-DotNetCommand -RepositoryRoot $root
Push-Location $root
try {
    & $dotnet restore $Solution
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    $resolvedOutputDirectory = Join-Path $root $OutputDirectory
    if (Test-Path $resolvedOutputDirectory) {
        Remove-Item -Path $resolvedOutputDirectory -Recurse -Force
    }

    New-Item -ItemType Directory -Path $resolvedOutputDirectory -Force | Out-Null

    $projects = Get-ChildItem -Path (Join-Path $root 'tests') -Filter '*.csproj' -Recurse |
        Sort-Object FullName

    foreach ($project in $projects) {
        [xml]$projectXml = Get-Content -Path $project.FullName
        $isTestProject = @($projectXml.Project.PropertyGroup.IsDalamudMcpTestProject) -contains 'true'
        if (-not $isTestProject) {
            continue
        }

        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project.FullName)
        $projectOutputDirectory = Join-Path $resolvedOutputDirectory $projectName
        New-Item -ItemType Directory -Path $projectOutputDirectory -Force | Out-Null

        Push-Location $projectOutputDirectory
        try {
            $arguments = @(
                'test',
                $project.FullName
            )

            if ($NoBuild) {
                $arguments += '--no-build'
            }

            $arguments += @(
                '--coverlet',
                '--coverlet-output-format',
                'cobertura'
            )

            & $dotnet @arguments
            if ($LASTEXITCODE -ne 0) {
                exit $LASTEXITCODE
            }
        }
        finally {
            Pop-Location
        }
    }
}
finally {
    Pop-Location
}
