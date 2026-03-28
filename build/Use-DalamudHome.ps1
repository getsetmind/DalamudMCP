function Resolve-DalamudHome {
    param(
        [string]$DalamudHome
    )

    Set-StrictMode -Version Latest

    $candidate = $DalamudHome
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        $candidate = $env:DALAMUD_HOME
    }

    if ([string]::IsNullOrWhiteSpace($candidate)) {
        if ($IsWindows) {
            $candidate = Join-Path $env:APPDATA 'XIVLauncher\addon\Hooks\dev'
        }
        elseif ($IsLinux) {
            $candidate = Join-Path $env:HOME '.xlcore/dalamud/Hooks/dev'
        }
        elseif ($IsMacOS) {
            $candidate = Join-Path $env:HOME 'Library/Application Support/XIV on Mac/dalamud/Hooks/dev'
        }
    }

    if ([string]::IsNullOrWhiteSpace($candidate)) {
        return $null
    }

    $resolved = [System.IO.Path]::GetFullPath($candidate)
    if (-not (Test-Path (Join-Path $resolved 'Dalamud.dll'))) {
        return $null
    }

    return $resolved
}

function Use-DalamudHome {
    param(
        [string]$DalamudHome,
        [switch]$Require
    )

    Set-StrictMode -Version Latest

    $resolved = Resolve-DalamudHome -DalamudHome $DalamudHome
    if ($Require -and [string]::IsNullOrWhiteSpace($resolved)) {
        throw 'Dalamud reference assemblies were not found. Set DALAMUD_HOME or pass -DalamudHome to a Hooks/dev directory that contains Dalamud.dll.'
    }

    $scope = [pscustomobject]@{
        PreviousValue = $env:DALAMUD_HOME
        ResolvedPath  = $resolved
    }

    if (-not [string]::IsNullOrWhiteSpace($resolved)) {
        $env:DALAMUD_HOME = $resolved
    }

    return $scope
}

function Restore-DalamudHome {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Scope
    )

    Set-StrictMode -Version Latest

    if ($null -eq $Scope.PreviousValue) {
        Remove-Item Env:DALAMUD_HOME -ErrorAction SilentlyContinue
        return
    }

    $env:DALAMUD_HOME = [string]$Scope.PreviousValue
}
