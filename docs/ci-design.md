# CI Design

## 1. 目的

CI は単に build を通すためではなく、設計境界を壊していないことを継続的に証明するために使う。

## 2. 2026-03-19 時点の前提

GitHub Docs の .NET CI ガイドでは:

- .NET 利用には `actions/setup-dotnet` が推奨
- dependency caching に `packages.lock.json` を使える
- build と test は通常の `dotnet restore`, `dotnet build`, `dotnet test` でよい
- artifact には `actions/upload-artifact@v4` が使える

設計への影響:

- GitHub Actions を標準 CI とする
- action version は docs にある現行例に沿って pin する
- lock file を前提に caching を設計する

## 3. workflow 分割

### `ci.yml`

必須 gate。

トリガ:

- `push`
- `pull_request`

内容:

1. checkout
2. setup .NET
3. restore
4. build
5. format verify
6. analyzer gate
7. unit / integration / architecture tests
8. coverage collection
9. coverage threshold check
10. test results / coverage artifact upload

### `nightly.yml`

任意。

トリガ:

- `schedule`
- `workflow_dispatch`

内容:

- 長めの integration tests
- future compatibility checks
- dependency drift report

### `release-validation.yml`

任意。

トリガ:

- tag
- manual dispatch

内容:

- release build
- packaging smoke test
- manifest validation

## 4. primary CI job

### Runner

- `ubuntu-latest`

理由:

- host / domain / application / tests は Linux でも動くように設計すべき
- plugin assembly 自体は Windows 向け要素があっても、コアロジックは OS 非依存で検証できる

### optional secondary job

- `windows-latest`

用途:

- Dalamud / native interop に近い adapter smoke test
- packaging sanity

評価:

v1 では任意、v2 以降で追加検討。

## 5. gate の順番

順番は意図的に固定する。

1. restore
2. build
3. `dotnet format --verify-no-changes`
4. analyzer / warnings as errors
5. fast tests
6. slower integration tests
7. architecture tests
8. coverage gate

理由:

- 明らかな formatting / analyzer 問題を早く落とす
- 重い test は後ろに寄せる

## 6. coverage 実行

2026-03-19 時点の Microsoft Learn では、Microsoft.Testing.Platform の code coverage と `coverlet.MTP` が現行の選択肢。

設計方針:

- 第一候補: `coverlet.MTP`
- 出力形式: `cobertura`
- artifact として保存
- PR gate では threshold 失敗時に即 fail

これは現時点の公式 docs を踏まえた推奨。

## 7. artifact

保存対象:

- test result
- coverage report
- format report
- analyzer report

保持:

- PR: 短期
- main/nightly: やや長め

## 8. キャッシュ

GitHub Docs では `setup-dotnet` に cache 機能があり、`cache-dependency-path` に `packages.lock.json` を使える。

方針:

- lock file 必須
- NuGet cache を有効化

## 9. fail-fast ポリシー

### fail-fast にするもの

- build failure
- format failure
- analyzer failure
- architecture failure
- coverage threshold failure

### artifact を残してから落とすもの

- integration test failure
- host schema failure

## 10. branch protection 前提

最低限 required にする check:

- `ci / build-and-test`
- `ci / architecture`
- `ci / coverage`

もし workflow を分けないなら、単一 workflow 内でも check name を安定させる。

## 11. 将来の拡張

- reusable workflow 化
- codeql
- dependency review
- package vulnerability scan

ただし v1 の必須ではない。

## 12. 推奨 job 断面

### Job A: `quality`

- format
- analyzer
- architecture tests

### Job B: `test`

- unit tests
- integration tests
- coverage

### Job C: `package-smoke`

- plugin packaging smoke
- host startup smoke

`package-smoke` は最初は optional でよい。

## 13. 結論

CI は 1 本の巨大 workflow より、`quality` と `test` を少なくとも論理分離した方が見通しがよい。

## Sources

- https://docs.github.com/actions/automating-builds-and-tests/building-and-testing-net
- https://docs.github.com/en/actions/reference/workflows-and-actions
- https://docs.github.com/actions/using-workflows/storing-workflow-data-as-artifacts
- https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-code-coverage
- https://learn.microsoft.com/en-us/dotnet/core/tools/global-json
