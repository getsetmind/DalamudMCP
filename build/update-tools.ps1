param(
    [string]$Readme = 'README.md',
    [switch]$Check
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$operationRoot = Join-Path $root 'src\DalamudMCP.Plugin\Operations'
$readmePath = Join-Path $root $Readme
$startMarker = '<!-- generated-tools:start -->'
$endMarker = '<!-- generated-tools:end -->'
$pattern = '\[Operation\(\s*"(?<operation>[^"]+)"[\s\S]*?\[McpTool\("(?<tool>[^"]+)"\)\]'

$entries = foreach ($file in Get-ChildItem -LiteralPath $operationRoot -Filter '*.cs' -File) {
    $content = [IO.File]::ReadAllText($file.FullName)
    foreach ($match in [regex]::Matches($content, $pattern)) {
        [pscustomobject]@{
            Tool = $match.Groups['tool'].Value
            Operation = $match.Groups['operation'].Value
            Source = $file.Name
        }
    }
}

$entries = @($entries | Sort-Object Tool)
if ($entries.Count -eq 0) {
    throw "No MCP tools were found under '$operationRoot'."
}

$duplicateTools = @($entries | Group-Object Tool | Where-Object Count -gt 1)
if ($duplicateTools.Count -gt 0) {
    throw "Duplicate MCP tool names: $($duplicateTools.Name -join ', ')"
}

$lines = @(
    $startMarker
    '| MCP tool | Operation ID | Source |'
    '| --- | --- | --- |'
)
$lines += $entries | ForEach-Object {
    "| ``$($_.Tool)`` | ``$($_.Operation)`` | [$($_.Source)](./src/DalamudMCP.Plugin/Operations/$($_.Source)) |"
}
$lines += $endMarker

$readmeContent = [IO.File]::ReadAllText($readmePath)
$newLine = if ($readmeContent.Contains("`r`n")) { "`r`n" } else { "`n" }
$generatedBlock = $lines -join $newLine
$markerPattern = [regex]::Escape($startMarker) + '[\s\S]*?' + [regex]::Escape($endMarker)

if (-not [regex]::IsMatch($readmeContent, $markerPattern)) {
    throw "README markers '$startMarker' and '$endMarker' were not found."
}

$updatedContent = [regex]::Replace($readmeContent, $markerPattern, [Text.RegularExpressions.MatchEvaluator]{ param($match) $generatedBlock }, 1)
if ($Check) {
    if (-not [string]::Equals($readmeContent, $updatedContent, [StringComparison]::Ordinal)) {
        throw 'README tool catalog is stale. Run .\build\update-tools.ps1.'
    }

    Write-Host "README tool catalog is current ($($entries.Count) tools)."
    exit 0
}

$utf8WithoutBom = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText($readmePath, $updatedContent, $utf8WithoutBom)
Write-Host "Updated README tool catalog ($($entries.Count) tools)."
