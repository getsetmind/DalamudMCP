# Platform Strategy

## 1. 先に明確にしておく制約

`DalamudMCP` は名前の通り Dalamud に依存するため、ゲーム実行とプラグイン動作の現実的な前提は Windows です。

したがって、「いろいろなプラットフォームに対応」は次の 2 つに分けて考える必要があります。

1. OS としての対応
2. MCP client / host ecosystem としての対応

この区別をしないと、実現不能なクロスプラットフォーム要求と、実現可能な MCP 互換性の話が混ざります。

## 2. 何が本当に多プラットフォーム化できるか

### できないもの

- Dalamud plugin 本体の macOS / Linux 対応
- FFXIV クライアント非依存の完全クロス OS 実行

### できるもの

- 複数 MCP client から接続しやすい server surface を作る
- local と remote の両接続形態を設計する
- Claude Desktop のような local client と、web 系の remote connector client の両方に向けた transport を切る

## 3. 対応対象の整理

### Profile A: Local Desktop Client

対象:

- Claude Desktop のような local MCP client
- 開発用 client
- Inspector

接続形態:

- `stdio`

要件:

- クライアントが host を subprocess 起動できる
- host が Windows 上で plugin に接続できる

評価:
最優先。

### Profile B: Local Multi-Client Gateway

対象:

- 同一 Windows PC 上で複数 client から使いたいケース

接続形態:

- `stdio` を維持しつつ、必要ならローカル HTTP gateway を別プロセスに追加

評価:
後回し。初期版では必須ではない。

### Profile C: Remote / Web Client

対象:

- web ベースの AI client
- remote connector を持つ client
- 別デバイスからの利用

接続形態:

- `Streamable HTTP`

要件:

- 認証
- `Origin` 検証
- localhost bind か、明示的に公開するなら reverse proxy 前提
- session 管理
- tool 単位の権限管理

評価:
将来対応。初期版では設計のみ確保。

## 4. 推奨する層構造

1. `DalamudMCP.Plugin`
   - Windows / Dalamud 専用
2. `DalamudMCP.Bridge`
   - plugin と host の内部契約
3. `DalamudMCP.Host.Local`
   - `stdio` 提供
4. `DalamudMCP.Host.Remote`
   - `Streamable HTTP` 提供

この分解にしておくと、`Host.Remote` を後から追加しても plugin 側を大きく変えずに済む。

加えて profile を 2 つに分ける。

- `BaselineProfile`
  - read-only context
  - UI introspection
- `ExperimentalPrivateProfile`
  - one-shot UI interaction のみ
  - gameplay operation は除外

## 5. transport 戦略

### v1

- plugin <-> host: `Named Pipe`
- host <-> local client: `stdio`

### v2

- plugin <-> host: `Named Pipe`
- host <-> local client: `stdio`
- host <-> remote client: `Streamable HTTP`

## 6. 配布戦略

### v1

- Windows ローカル利用前提
- dev plugin または custom repo 想定
- registry 依存なし

### v2

- remote host を別配布
- 公開するなら registry metadata を検討

## 7. この戦略の意味

この設計だと、OS の意味では Windows 制約を受け入れつつ、MCP ecosystem の意味では広い client 互換性を狙えます。

つまり、無理に「plugin をクロスプラットフォーム化」するのではなく、「MCP surface を多プラットフォーム化」する方針です。

これは本件で現実的かつ破綻しにくい設計です。

## Sources

- https://modelcontextprotocol.io/specification/2025-11-25/basic/transports
- https://modelcontextprotocol.io/docs/develop/connect-local-servers
- https://modelcontextprotocol.io/docs/develop/connect-remote-servers
