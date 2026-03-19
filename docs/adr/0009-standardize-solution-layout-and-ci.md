# ADR-0009: solution 構成と CI gate を標準化する

## Status

Accepted

## Context

project 構成と CI が曖昧だと、plugin 固有事情に引っ張られて `Plugin.cs` にロジックが集まりやすい。また、quality gate を後で追加すると既存コードの是正コストが跳ね上がる。

## Decision

- root に `src/`, `tests/`, `build/`, `.github/workflows/` を置く
- project を layer ごとに分ける
- GitHub Actions を標準 CI とする
- quality gate と test gate を PR 必須にする

## Consequences

良い点:

- 構成の迷いが減る
- CI 要件が明示される
- scale しても崩れにくい

悪い点:

- 最初の ceremony は増える
- 小変更でも複数 project に触れる可能性がある

## Sources

- https://docs.github.com/actions/automating-builds-and-tests/building-and-testing-net
- https://dalamud.dev/plugin-development/project-layout/
