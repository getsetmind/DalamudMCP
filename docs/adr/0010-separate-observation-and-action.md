# ADR-0010: Observation と Action を別 profile として分離する

## Status

Accepted

## Context

`DalamudMCP` を汎用 observation/action 基盤に拡張する場合、read-only capability と操作 capability を同じ profile に混ぜると境界が曖昧になり、settings UI、host 公開面、audit すべてが複雑化する。

## Decision

- read-only capability は `ObservationProfile` に属する
- 操作 capability は `ActionProfile` に属する
- `ActionProfile` は `experimental-private` 扱いとする
- `ActionProfile` は tool only を基本とする

## Consequences

良い点:

- baseline の read-only client を守りやすい
- action 側の guard と audit を強制しやすい
- 将来 build 分離しやすい

悪い点:

- profile 管理が増える
- settings UI が複雑になる
