# ADR-0005: Baseline profile と Experimental private profile を分離する

## Status

Accepted

## Context

`DalamudMCP` には、read-only な context bridge と、より攻撃的な UI interaction / operation 要件が混在し得る。

しかし official plugin restrictions を踏まえると、これらを同一 profile で扱うと境界が曖昧になる。

## Decision

- `BaselineProfile`
  - read-only context
  - UI introspection
  - MCP `tools + resources`
- `ExperimentalPrivateProfile`
  - default OFF
  - custom repo / dev plugin 前提
  - one-shot UI interaction のみ検討対象
  - gameplay operation は含めない

## Consequences

良い点:

- 安全な設計と危険な設計を分離できる
- baseline を説明しやすい
- 将来 profile 単位で配布や build を分けやすい

悪い点:

- 機能差分の管理が増える
- ユーザー視点では分かりにくくなる可能性がある

## Sources

- https://dalamud.dev/plugin-publishing/restrictions
- https://dalamud.dev/plugin-publishing/approval-process/
