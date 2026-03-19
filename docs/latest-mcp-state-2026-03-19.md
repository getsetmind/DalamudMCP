# Latest MCP State as of 2026-03-19

この文書は、2026-03-19 時点での公式 MCP 一次情報を、`DalamudMCP` の設計判断に必要な範囲に絞って要約したものです。

## 1. 現在の基準仕様

- 参照すべき仕様リビジョンは `2025-11-25`
- `latest` の lifecycle ページも `2025-11-25` へ解決される
- 初期化フェーズでは capability negotiation と protocol version negotiation が必須

設計への影響:

- `DalamudMCP.Host` は必ず `initialize` を正しく処理する
- server info、capabilities、version を明示的に返す
- プロトコル版の後方互換を甘く見ない

## 2. 現在の標準 transport

MCP が定義する標準 transport は次の 2 つ。

1. `stdio`
2. `Streamable HTTP`

公式仕様では、clients SHOULD support `stdio` whenever possible とされている。

設計への影響:

- `DalamudMCP.Host` の第一 transport は `stdio`
- 将来の多クライアント対応や web 系クライアント対応のために `Streamable HTTP` を第二 transport として設計余地を残す
- `DalamudMCP.Plugin` 自身は MCP transport を持たない

## 3. Streamable HTTP の注意点

2025-11-25 仕様では、従来の `HTTP+SSE` は `Streamable HTTP` に置き換えられている。

重要点:

- 単一の MCP endpoint が `POST` と `GET` を扱う
- `MCP-Session-Id` を使うセッション管理がある
- `MCP-Protocol-Version` ヘッダを subsequent requests に付与する
- `Origin` 検証が必須
- ローカル実行では `localhost` bind が推奨
- すべての接続に適切な認証実装が推奨

設計への影響:

- remote gateway を作る場合、HTTP 実装は後付けではなく最初から dedicated component に切る
- plugin 内に HTTP 待受を入れる案はさらに不適切
- remote profile は認証なしでは出荷対象にしない

## 4. JSON Schema 方針

2025-11-25 仕様では、schema dialect の default は `JSON Schema 2020-12`。

設計への影響:

- tool `inputSchema` と `outputSchema` は 2020-12 を前提に設計する
- draft-07 に寄せた古いテンプレートは採用しない
- internal contract と MCP schema は分けるが、外向け schema は 2020-12 で統一する

## 5. MCP の基本プリミティブ

設計原則では、MCP の基礎プリミティブは次の通り。

- `resources`
- `tools`
- `prompts`
- `tasks`

ただし `tasks` は 2025-11-25 で experimental。

設計への影響:

- `DalamudMCP` 初期版は `resources` と `tools` を中心にする
- `prompts` は補助的に後回し
- `tasks` は初期版では使わない

## 6. Tools / Resources / Prompts の役割分担

### Tools

- model-controlled
- human in the loop が推奨
- 明示的な tool surface と schema が必要

### Resources

- application-driven
- 文脈データの参照向き
- `subscribe` と `listChanged` は optional

### Prompts

- user-controlled
- UI から明示選択されるテンプレート向き

設計への影響:

- `get_player_context` は tool として提供できる
- 同じ内容を `ffxiv://player/context` のような resource としても提供できる
- ゲーム内の読み取り文脈は resource に相性がよい
- 操作系を今後もし作るなら tool だが、当面は対象外

## 7. SDK の現状

2026-03-19 時点の公式 SDK tier では、以下が Tier 1:

- TypeScript
- Python
- C#
- Go

公式 C# SDK は .NET 向け MCP client/server 実装を提供する。

設計への影響:

- `DalamudMCP.Host` は C# を第一候補にする
- 理由は Dalamud 側も .NET 系であり、契約型や validation 戦略を共有しやすいから
- Python/TypeScript はプロトタイピングには向くが、本件の本命にはしない

これは公式 SDK tier と Dalamud の .NET 文脈を踏まえた推奨であり、ソースからの推論を含む。

## 8. 開発ツールの現状

公式の `MCP Inspector` は、ローカル server の接続、resources / prompts / tools の確認、通知確認に使える。

設計への影響:

- 実装フェーズでは Inspector を最初の検証クライアントにする
- Claude Desktop 固有設定の前に Inspector で protocol surface を検証する

## 9. Registry の扱い

公式 MCP Registry は preview。

設計への影響:

- 初期版の設計では registry 依存を持ち込まない
- remote 配布を考える段階で `server.json` を検討する

## 10. 結論

`DalamudMCP` の現時点の最適方針は次の通り。

1. `DalamudMCP.Plugin` は local bridge に限定
2. `DalamudMCP.Host` は C# で実装する前提で設計
3. local client 向けは `stdio` を第一にする
4. remote client 向けは将来 `Streamable HTTP` gateway を追加する
5. schema は `JSON Schema 2020-12` を前提にする
6. 初期版は `tools + resources` のみを主戦場にする

## Sources

- https://modelcontextprotocol.io/specification/2025-11-25/basic/lifecycle
- https://modelcontextprotocol.io/specification/2025-11-25/basic/transports
- https://modelcontextprotocol.io/specification/2025-11-25/changelog
- https://modelcontextprotocol.io/specification/2025-11-25/basic
- https://modelcontextprotocol.io/docs/sdk
- https://modelcontextprotocol.io/community/sdk-tiers
- https://modelcontextprotocol.io/docs/tools/inspector
- https://modelcontextprotocol.io/community/design-principles
- https://modelcontextprotocol.io/registry/about
