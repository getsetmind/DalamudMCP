# Quality Tooling

## 1. 目的

formatter、linter、analyzer、coverage を後付けにせず、最初から build pipeline に組み込む。

## 2. Formatter

2026-03-19 時点の Microsoft Learn では、`dotnet format` は `.editorconfig` に従ってコード整形を行い、`--verify-no-changes` で差分検出ができる。

採用:

- formatter: `dotnet format`
- source of truth: `.editorconfig`

CI:

```text
dotnet format --verify-no-changes
```

補足:

- `dotnet format style`
- `dotnet format analyzers`

の粒度でも使えるが、基本 gate は solution 全体の `--verify-no-changes` とする。

## 3. Linter / Analyzer

### ベース

.NET SDK には Roslyn analyzers が含まれる。公式 docs でも .NET 5 以降は code analysis が既定で有効。

採用:

- built-in .NET analyzers
- `.editorconfig` による IDE / style / naming 設定
- `EnforceCodeStyleInBuild = true`
- nullable enabled
- warnings as errors

### テスト用

- xUnit analyzers

## 4. 推奨 build 設定

方針:

- SDK version は `global.json` で固定する
- `AnalysisLevel` は意図的に固定する
- `latest` 追従で CI が突然壊れないようにする

推奨例:

- `AnalysisLevel = latest-recommended`
- 必要な厳格 rule は個別に error へ昇格

これは Microsoft docs にある `latest-Recommended` の compound value を使う方針に基づく。

## 5. Coverage Tool

Microsoft docs では、Microsoft.Testing.Platform code coverage と `coverlet.MTP` の両方が現行手段として案内されている。

採用方針:

- まずは `coverlet.MTP` を第一候補
- 必要なら Microsoft code coverage に切り替え可能な構成にする

理由:

- Cobertura 等の出力が扱いやすい
- CI 連携が単純

これは現行 docs を見たうえでの推奨であり、最終採用は実装時に再確認する。

## 6. 最低限の gate

- build succeeds
- tests pass
- coverage meets threshold
- `dotnet format --verify-no-changes`
- analyzer warnings are zero

## 7. `.editorconfig` 原則

- naming rules を明示
- file-scoped namespace を基本
- nullable を前提にした書き方へ寄せる
- explicit accessibility を要求
- using 整列規則を固定

## 8. suppressions

抑制は最後の手段。

ルール:

- pragma 乱用禁止
- global suppression には理由必須
- `.editorconfig` の `none` は issue 参照つきで残す

## 9. 結論

formatter と linter は開発者の善意に依存させず、CI gate として強制する。

## Sources

- https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format
- https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview
- https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/code-style-rule-options
- https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-code-coverage
- https://xunit.net/docs/getting-started/v3/getting-started
- https://xunit.net/releases/analyzers/1.24.0
