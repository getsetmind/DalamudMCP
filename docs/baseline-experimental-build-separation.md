# Baseline / Experimental Build Separation

## 1. 目的

`ExperimentalPrivateProfile` が `BaselineProfile` を侵食しないよう、将来の build 分離方針を定義する。

## 2. 方針

最初は単一 solution でもよいが、論理的には最初から分離可能な構造にする。

## 3. 将来の分離案

### 案 A

- 1 repository
- 2 host modes
- 1 plugin assembly

評価:
最も簡単だが、境界侵食リスクが高い。

### 案 B

- 1 repository
- 2 plugin assemblies
- 2 host assemblies

評価:
有力。

### 案 C

- repository 自体を分ける

評価:
最も強い分離だが運用コストが高い。

## 4. 現時点の推奨

将来は案 B を目指す。

- `DalamudMCP.Baseline.Plugin`
- `DalamudMCP.Experimental.Plugin`
- `DalamudMCP.Baseline.Host`
- `DalamudMCP.Experimental.Host`

共通ロジックは:

- `Domain`
- `Application`
- `Contracts`
- `Infrastructure.Core`

に残す。

## 5. 分離時のルール

- experimental capability は baseline assembly にリンクしない
- settings UI でも baseline と experimental を別画面にする
- package / artifact 名も分ける
- CI job も分ける

## 6. 結論

experimental を同じ build に入れ続けると、長期的には境界が緩む。分離前提で設計しておくべき。
