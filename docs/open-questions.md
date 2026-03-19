# Open Questions

## 高優先度

1. `player context` に含める項目の境界をどこで切るか
2. `party context` で他プレイヤー情報をどこまで出すか
3. inventory は raw item list を返すか、要約だけ返すか
4. UI 状態をどこまで正式 capability に含めるか
5. 監査ログを常時保存するか、セッション中のみ保持するか
6. remote profile を v2 で本当に開くか、それとも local 専用を維持するか
7. UI introspection の allowlist にどの addon を入れるか
8. `ExperimentalPrivateProfile` を本当に持つか
9. 設定 UI のプリセットを何段階にするか
10. coverage gate を solution 全体 90/85 にするか、さらに上げるか
11. CI を single workflow にするか quality/test 分離にするか
12. baseline / experimental を最終的に別 assembly に分けるか
13. addon metadata を v1 から code-defined に固定するか

## 中優先度

1. `resources` を採用するか、当面 `tools` のみに寄せるか
2. snapshot を pull-only にするか、限定的 push を許すか
3. custom repository 配布を前提にするか、dev plugin から始めるか
4. プラグイン UI に接続状態をどう見せるか
5. `get_player_context` の position を coarse からさらに丸めるか
6. `summaryText` を返すか、structured only にするか
7. UI string table の raw / decoded を両方返すか
8. shallow tree の深さを 2 か 3 にするか
9. 設定変更を即時反映にするか `Apply` 制にするか
10. architecture test を reflection ベースで書くか、専用ライブラリを使うか
11. `windows-latest` の smoke job を v1 から必須にするか
12. addon metadata をコード定義にするか設定データ化するか
13. blocked項目をUI一覧に見せるか完全非表示にするか

## 判断基準

- Dalamud ガイドラインから逸脱しないか
- 外部から見た責務境界が明快か
- ゲームプロセス内の負荷を増やしすぎないか
- ユーザーが何を公開しているか理解できるか
