# ADR-0003: プラグインと外部ホスト間の内部ブリッジに Named Pipe を採用する

## Status

Accepted

## Context

プラグインと外部ホストの間にはローカル限定の双方向通信が必要である。候補は `Named Pipe`、`localhost HTTP`、`WebSocket`。

## Decision

内部ブリッジの第一候補を `Windows Named Pipe` とする。

## Consequences

良い点:

- ローカル限定を表現しやすい
- ポート競合を避けやすい
- Windows デスクトップ用途と相性が良い

悪い点:

- 開発中の可観測性は HTTP より下がる
- 将来クロスプラットフォーム化する場合は差し替え検討が必要

## Rejected Alternatives

- `localhost HTTP`
  - 可観測性は高いが、待受公開の誤設定やポート管理負荷が増える
- `WebSocket`
  - 将来性はあるが、この段階では複雑さが先行する
