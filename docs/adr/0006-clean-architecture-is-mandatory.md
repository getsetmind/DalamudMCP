# ADR-0006: Clean Architecture を必須にする

## Status

Accepted

## Context

Dalamud 依存と MCP 依存は、放置するとすぐに全体へ漏れる。特に plugin project にロジックが集まると、native UI、transport、policy が癒着して、テスト不能になる。

## Decision

- `Domain` / `Application` / `Infrastructure` / `Plugin` / `Host` を分ける
- dependency rule を architecture tests で強制する
- `Plugin` と `Host` は delivery mechanism として扱う

## Consequences

良い点:

- テストしやすい
- MCP 側と Dalamud 側を別々に進化させやすい
- UI introspection や policy を内側に保てる

悪い点:

- 初期の project 数が増える
- 小規模実装でも ceremony が増える
