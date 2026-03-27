Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-JetBrainsTool {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    . (Join-Path $PSScriptRoot 'Get-DotNetCommand.ps1')
    $dotnet = Get-DotNetCommand -RepositoryRoot $RepositoryRoot
    $dotnetRoot = Split-Path -Parent $dotnet
    $globalJsonPath = Join-Path $RepositoryRoot 'global.json'
    $jbArguments = [System.Collections.Generic.List[string]]::new()
    $jbArguments.AddRange($Arguments)
    $sdkVersion = $null

    if (Test-Path $globalJsonPath) {
        $globalJson = Get-Content $globalJsonPath | ConvertFrom-Json
        $sdkVersion = $globalJson.sdk.version
        if (-not [string]::IsNullOrWhiteSpace($sdkVersion)) {
            $jbArguments.Add("--DotNetCore=$dotnet")
        }
    }

    Push-Location $RepositoryRoot
    try {
        $previousDotNetRoot = $env:DOTNET_ROOT
        $previousMsBuildSDKsPath = $env:MSBuildSDKsPath
        $previousSdkResolverCliDir = $env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR
        $env:DOTNET_ROOT = $dotnetRoot

        if (-not [string]::IsNullOrWhiteSpace($sdkVersion)) {
            $env:MSBuildSDKsPath = Join-Path $dotnetRoot "sdk\$sdkVersion\Sdks"
            $env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = $dotnetRoot
        }

        & $dotnet tool run jb @jbArguments
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }
    finally {
        $env:DOTNET_ROOT = $previousDotNetRoot
        $env:MSBuildSDKsPath = $previousMsBuildSDKsPath
        $env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = $previousSdkResolverCliDir
        Pop-Location
    }
}
