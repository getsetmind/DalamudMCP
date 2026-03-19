# ADR-0007: 高カバレッジと CI gate を必須にする

## Status

Accepted

## Context

`DalamudMCP` は policy と境界制御が重要で、機能より事故防止が優先される。allowlist、deny list、profile、UI 観測、MCP 公開面は回帰に弱い。

## Decision

- unit / integration / architecture tests を必須にする
- overall coverage gate を設ける
- new / changed code に厳しい coverage を要求する
- formatter / analyzer / tests を merge gate にする

## Consequences

良い点:

- 境界破壊を早期検知できる
- refactor しやすい
- 設定 UI と policy の回帰を抑えられる

悪い点:

- 初速は落ちる
- fixture と test infrastructure の整備コストが必要
