# 製品ロードマップ

## 現在地

- 現在のマイルストーン：`M06 製品品質`
- 現在のタスク：`M06-T03`（`Ready`）
- 参照ゲーム：`crazy_eights`
- 最終更新日：2026-08-16

`crazy_eights`は構造化表示とUnity縦切り版の参照実装に使用する。92ゲームの最終的な
製品収録順を決めるものではなく、既存のCLI例、秘密情報、山札、捨札、特殊rank選択を
小さな画面で確認できるため最初の統合対象とする。

## マイルストーン

| ID | 状態 | マイルストーン | 完了状態 | 依存 |
|---|---|---|---|---|
| M01 | Done | [構造化表示契約](milestones/M01-structured-presentation.md) | UIが表示文字列を解析せず、Crazy Eightsを安全に描画・操作できる | なし |
| M02 | Done | [Unity縦切り版](milestones/M02-unity-vertical-slice.md) | Crazy Eightsを人間対CPUで起動から結果まで遊べる | M01 |
| M03 | Done | [セーブ・リプレイ](milestones/M03-save-and-replay.md) | 中断再開と決定的な行動再生ができる | M02 |
| M04 | Done | [CPU難易度](milestones/M04-cpu-difficulty.md) | 弱・標準・強を観測可能情報と固定seedで検証できる | M03 |
| M05 | Done | [チュートリアル](milestones/M05-tutorial.md) | 初見プレイヤーがゲーム内説明だけで1局を完走できる | M04 |
| M06 | In Progress | [製品品質](milestones/M06-product-quality.md) | 設定、入力、音、演出、多言語化、アクセシビリティを備える | M05 |
| M07 | Backlog | [Steam対応](milestones/M07-steam-release.md) | 配布ビルド、実績、製品設定、リリース確認を完了する | M06 |
| M08 | Backlog | [対戦機能](milestones/M08-multiplayer.md) | 必要性を再評価し、採用時はローカル対戦から段階導入する | M07 |

## 優先順位の原則

1. 1本のゲームを製品品質まで縦に通してから対象ゲームを増やす。
2. RuntimeはUnity表示層から独立させ、UnityEngine依存を追加しない。
3. CLI、ゲームID、seed再現性、非公開情報境界を維持する。
4. セーブとリプレイは同じ状態表現と行動履歴を共有する。
5. オンライン対戦は切断復帰、権威状態、不正対策の設計が必要なため最後に判断する。

## マイルストーン開始条件

- 直前マイルストーンが`Done`である。
- スコープ、対象外、完了条件、検証方法が個別文書に記載されている。
- M01～M08の個別文書は事前に用意し、完了時に次の計画をその場で自動生成しない。
- 個別文書でDecision Gateと明記した未確定事項だけ、実装前にユーザーへ確認する。
- 開始時に最初のタスクだけを`Ready`とし、他は依存関係に従って`Backlog`とする。
- 最終タスク完了時は現在のマイルストーンを`Done`、次のマイルストーンと最初のタスクを
  `Ready`へ変更する。通常の1タスク依頼とGoalは次マイルストーンの実装前に停止する。
