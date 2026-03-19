# Plugin Design Notes

このディレクトリは、将来の `DalamudMCP.Plugin` のための設計領域です。まだ C# 実装は置きません。

## 役割

- Dalamud サービスから現在文脈を取得する
- 外部へ公開してよい情報へ整形する
- ユーザー同意と capability 制御を持つ
- ローカル IPC 経由で外部ホストへデータを渡す

## 将来の想定サブ構成

- `DalamudMCP.Plugin/`
  - プラグイン本体
- `DalamudMCP.Plugin.Ui/`
  - 設定 UI と監査 UI
- `DalamudMCP.Plugin.Contracts/`
  - host との内部契約モデル

ただし実装時も、plugin project 自体にロジックを集めない。中心ロジックは `Domain` / `Application` 側へ置く。

## プラグインが持たない責務

- MCP `stdio` サーバー
- MCP `Streamable HTTP` サーバー
- LLM クライアント固有の接続設定
- 外部公開向けのネットワーク待受
