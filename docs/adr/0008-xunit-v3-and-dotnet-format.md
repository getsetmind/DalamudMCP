# ADR-0008: xUnit v3 と dotnet format を標準採用する

## Status

Accepted

## Context

2026-03-19 時点で、xUnit.net v3 は正式系として利用可能であり、Microsoft Testing Platform とも整合する。`dotnet format` も公式の .NET CLI formatter として `.editorconfig` に基づく整形および CI 検証を提供する。

## Decision

- test framework は xUnit.net v3
- formatter は `dotnet format`
- lint / static analysis は built-in .NET analyzers + `.editorconfig` + xUnit analyzers

## Consequences

良い点:

- .NET 標準ワークフローと整合しやすい
- CI 組み込みが単純
- test lint を含めて一貫させやすい

悪い点:

- analyzer ルールの厳格化に追従コストがある
- SDK / package version pinning を怠ると揺れやすい

## Sources

- https://xunit.net/docs/getting-started/v3/getting-started
- https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format
- https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview
