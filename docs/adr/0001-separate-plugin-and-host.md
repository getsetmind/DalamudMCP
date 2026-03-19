# ADR-0001: Dalamud プラグインと MCP ホストを分離する

## Status

Accepted

## Context

MCP の標準 transport である `stdio` は、クライアントがサーバーを subprocess として起動するモデルを前提とする。一方、Dalamud プラグインはゲームプロセス内にロードされる。

この差により、Dalamud プラグインをそのまま MCP サーバー本体として設計すると、標準 transport との整合性、責務分離、クラッシュ耐性が悪化する。

## Decision

- Dalamud プラグインは game context provider と local bridge に限定する
- MCP server 機能は別プロセスのローカルホストに持たせる
- 両者の間はローカル IPC で接続する

## Consequences

良い点:

- `stdio` 型 MCP client に適合しやすい
- ゲームプロセス内ロジックを小さく維持できる
- ログ、再接続、セッション管理を host 側へ隔離できる

悪い点:

- 配布物と運用が複数要素になる
- IPC 契約の設計が必要

## Rejected Alternatives

- プラグイン単体で `Streamable HTTP` サーバーを持つ
- MCP を使わず独自 API のみ公開する
