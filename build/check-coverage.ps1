param(
    [string]$CoverageDirectory = 'artifacts/coverage',
    [double]$SolutionLineThreshold = 90,
    [double]$SolutionBranchThreshold = 85
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $resolvedCoverageDirectory = Join-Path $root $CoverageDirectory
    if (-not (Test-Path $resolvedCoverageDirectory)) {
        throw "Coverage directory not found: $resolvedCoverageDirectory"
    }

    $projectFiles = @(Get-ChildItem -Path $resolvedCoverageDirectory -Filter 'coverage.cobertura.xml' -Recurse |
        Sort-Object FullName)

    if ($projectFiles.Count -eq 0) {
        throw "No coverage.cobertura.xml files were found under $resolvedCoverageDirectory"
    }

    $projectThresholds = @{}
    $testProjects = Get-ChildItem -Path (Join-Path $root 'tests') -Filter '*.csproj' -Recurse
    foreach ($testProject in $testProjects) {
        [xml]$projectXml = Get-Content -Path $testProject.FullName
        $isTestProject = @($projectXml.Project.PropertyGroup.IsDalamudMcpTestProject) -contains 'true'
        if (-not $isTestProject) {
            continue
        }

        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($testProject.FullName)
        $thresholdValue = @($projectXml.Project.PropertyGroup.CoverageThreshold | Select-Object -First 1)
        if ([string]::IsNullOrWhiteSpace($thresholdValue)) {
            throw "CoverageThreshold is missing for $projectName"
        }

        $projectThresholds[$projectName] = [double]$thresholdValue
    }

    $projectSummaries = @()
    $totalLinesCovered = 0.0
    $totalLinesValid = 0.0
    $totalBranchesCovered = 0.0
    $totalBranchesValid = 0.0

    foreach ($projectFile in $projectFiles) {
        [xml]$coverageXml = Get-Content -Path $projectFile.FullName
        $coverage = $coverageXml.coverage
        $projectName = Split-Path -Leaf (Split-Path -Parent $projectFile.FullName)

        if (-not $projectThresholds.ContainsKey($projectName)) {
            throw "CoverageThreshold was not found for $projectName"
        }

        $linesCovered = [double]$coverage.'lines-covered'
        $linesValid = [double]$coverage.'lines-valid'
        $branchesCovered = [double]$coverage.'branches-covered'
        $branchesValid = [double]$coverage.'branches-valid'
        $lineRate = if ($linesValid -eq 0) { 100.0 } else { ($linesCovered / $linesValid) * 100.0 }
        $branchRate = if ($branchesValid -eq 0) { 100.0 } else { ($branchesCovered / $branchesValid) * 100.0 }
        $projectThreshold = $projectThresholds[$projectName]

        if ($lineRate -lt $projectThreshold -or $branchRate -lt $projectThreshold) {
            throw "$projectName coverage below threshold. Line: $([math]::Round($lineRate, 2))%, Branch: $([math]::Round($branchRate, 2))%, Threshold: $projectThreshold%"
        }

        $totalLinesCovered += $linesCovered
        $totalLinesValid += $linesValid
        $totalBranchesCovered += $branchesCovered
        $totalBranchesValid += $branchesValid

        $projectSummaries += [pscustomobject]@{
            Project = $projectName
            LineCoverage = [math]::Round($lineRate, 2)
            BranchCoverage = [math]::Round($branchRate, 2)
            Threshold = $projectThreshold
        }
    }

    $solutionLineRate = if ($totalLinesValid -eq 0) { 100.0 } else { ($totalLinesCovered / $totalLinesValid) * 100.0 }
    $solutionBranchRate = if ($totalBranchesValid -eq 0) { 100.0 } else { ($totalBranchesCovered / $totalBranchesValid) * 100.0 }

    if ($solutionLineRate -lt $SolutionLineThreshold -or $solutionBranchRate -lt $SolutionBranchThreshold) {
        throw "Solution coverage below threshold. Line: $([math]::Round($solutionLineRate, 2))%, Branch: $([math]::Round($solutionBranchRate, 2))%, Required: $SolutionLineThreshold% / $SolutionBranchThreshold%"
    }

    $projectSummaries | Format-Table -AutoSize | Out-Host
    Write-Host ("Solution coverage: line {0}% / branch {1}%" -f ([math]::Round($solutionLineRate, 2)), ([math]::Round($solutionBranchRate, 2)))
}
finally {
    Pop-Location
}
