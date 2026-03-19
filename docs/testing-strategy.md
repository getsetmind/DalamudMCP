# Testing Strategy

## 1. 方針

`DalamudMCP` の全コードはテスト前提で設計する。

ここでいう「全コード」は、少なくとも全ての production code path が何らかの automated test によって通ることを意味する。単純 DTO や generated code を除き、未テストのロジックを残さない。

## 2. テストピラミッド

### Unit Tests

対象:

- `Domain`
- `Application`
- policy
- mapper
- validation

比率:
最多。

### Integration Tests

対象:

- settings persistence
- named pipe bridge
- MCP host registration
- schema generation

### Adapter Tests

対象:

- Dalamud adapter
- Atk tree reader
- string table decoder

注記:
ネイティブ依存部は fake / snapshot / contract test を組み合わせる。

### Architecture Tests

対象:

- project dependency ルール
- namespace ルール
- `Plugin` / `Host` が `Domain` を汚染していないこと

## 3. テスト分類

### `Domain.Tests`

- 値オブジェクト不変条件
- ポリシー判定
- snapshot 正規化

### `Application.Tests`

- use case
- authorization / allowlist / deny list
- unavailable handling

### `Infrastructure.Tests`

- settings store
- file lock / migration
- bridge serialization

### `Host.Tests`

- MCP initialize
- tools list
- resources list
- schema validation

### `Plugin.Tests`

- composition root の最小確認
- UI state -> use case dispatch の最小確認

### `ArchitectureTests`

- 依存方向
- forbidden reference
- layering 逸脱

## 4. テスト技術選定

2026-03-19 時点では、xUnit.net v3 が正式で、C# / .NET 向けに十分成熟している。xUnit の getting started でも .NET 8 以降を前提にしている。

採用方針:

- テスト framework: xUnit.net v3
- test runner: `dotnet test`
- code coverage: Microsoft.Testing.Platform または coverlet.MTP
- test lint: xUnit analyzers

理由:

- MCP 公式 SDK の C# も Tier 1
- xUnit v3 は .NET 8 以降と相性がよい
- Microsoft Testing Platform の code coverage は現行公式 docs がある

これは 2026-03-19 時点の公式 docs を踏まえた推奨。

## 5. カバレッジ基準

### 全体

- line coverage: 90% 以上
- branch coverage: 85% 以上

### `Domain` と `Application`

- line coverage: 95% 以上
- branch coverage: 90% 以上

### 新規 / 変更コード

- line coverage: 95% 以上
- branch coverage: 90% 以上

## 6. 例外扱い

以下のみ coverage 例外候補。

- generated code
- manifest template
- trivial wiring だけの entrypoint
- test impossible な external glue で、かつ代替 integration test がある箇所

ただし除外は明示リスト化し、理由を書く。

## 7. テストが必須の対象

- allowlist / deny list 評価
- sensitivity 判定
- tool exposure policy
- addon exposure policy
- snapshot stale 判定
- unavailable response
- audit log event
- MCP schema 互換

## 8. 失敗を先に書く

方針:

- バグ修正は再現 test を先に追加
- policy 変更は approval test を先に追加
- schema 変更は contract test を先に追加

## 9. CI での扱い

merge 条件:

- 全 test pass
- coverage gate pass
- architecture test pass
- formatter pass
- analyzer pass

## 10. 結論

`DalamudMCP` では「後で test を足す」は許容しない。設計段階から test seam を作る。
