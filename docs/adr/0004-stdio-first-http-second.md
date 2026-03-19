# ADR-0004: MCP transport は stdio first、HTTP second とする

## Status

Accepted

## Context

2026-03-19 時点の MCP 公式仕様では、標準 transport は `stdio` と `Streamable HTTP`。また clients SHOULD support `stdio` whenever possible とされている。

`DalamudMCP` はローカル Windows 上の Dalamud plugin と接続する構成が自然であるため、まずは local desktop client との互換が最優先となる。

## Decision

- v1 の MCP surface は `stdio` を第一とする
- `Streamable HTTP` は将来の remote / web client 対応として第二段階で追加する
- plugin は MCP transport を直接持たず、host が transport を提供する

## Consequences

良い点:

- Claude Desktop 系の local 接続に合わせやすい
- 実装とデバッグが単純
- HTTP の認証や Origin 検証を初期スコープから外せる

悪い点:

- ブラウザ系 / remote client には直接つながらない
- 多端末共有には追加コンポーネントが必要

## Sources

- https://modelcontextprotocol.io/specification/2025-11-25/basic/transports
- https://modelcontextprotocol.io/docs/develop/connect-local-servers
- https://modelcontextprotocol.io/docs/develop/connect-remote-servers
