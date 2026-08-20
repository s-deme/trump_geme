# M06 製品品質

## 状態

- マイルストーン：`In Progress`
- 次のタスク：`M06-T05`
- 参照ゲーム：`crazy_eights`
- 依存：`M05`

## 目的

縦切り版を配布候補として評価できる品質へ引き上げる。入力、設定、音、演出、多言語化、
アクセシビリティ、性能、異常系を個別の完了条件で固定する。

## 対象

- 解像度、表示、音量、演出速度、入力設定
- キーボード、マウス、主要ゲームパッド
- カード操作、CPU手番、勝敗の音と視覚フィードバック
- 日本語・英語の文字列分離とフォールバック
- 色だけに依存しない表示、文字サイズ、動きの軽減
- ロード時間、メモリ、フレーム時間、長時間対局の基準
- 例外、保存失敗、入力デバイス切断時の復帰

## 対象外

- Steam固有SDKとストア設定
- オンライン対戦
- 92ゲームへの横展開
- 最終マーケティング素材

## タスク

| ID | 状態 | 内容 | 依存 | 完了条件 |
|---|---|---|---|---|
| M06-T01 | Done | 製品品質基準、対象環境、性能予算を定義する | M05 | 各品質項目に検証可能な合否条件がある |
| M06-T02 | Done | 表示・音量・演出・入力設定と永続化を実装する | T01 | 再起動後も設定が保持され、安全な既定値へ戻せる |
| M06-T03 | Done | 音、画面遷移、カード操作、結果演出を仕上げる | T02 | 主要操作の状態変化を視覚と音で識別できる |
| M06-T04 | Done | 日本語・英語とアクセシビリティ設定を実装する | T03 | 文字切れ、未翻訳キー、色だけの情報伝達を検出できる |
| M06-T05 | Ready | 異常系、性能、長時間実行の試験と修正を行う | T04 | 品質基準を満たし、既知の制限を記録している |
| M06-T06 | Backlog | リリース候補チェックと通常の必須検証を行う | T05 | 完了条件を満たす再現可能な候補ビルドがある |

## 完了条件

- 対象環境、入力方式、解像度で起動から終了まで操作できる。
- 日本語・英語、音量、演出速度、アクセシビリティ設定が機能する。
- 保存破損、デバイス切断、想定外終了から安全に復帰できる。
- 性能予算と長時間安定性の試験が合格する。
- 既知の問題が重大度と回避策付きで記録される。
- 通常の必須検証、Unity Standard、必要な手動受入確認が成功する。

## 品質基準

[ADR-0006 製品品質基準とWindows対象環境](../decisions/ADR-0006-product-quality-baseline.md)で
対象OS、最小環境、解像度・入力matrix、日本語・英語、desktop accessibility subset、異常復旧、
性能予算、重大度gateを決定した。

設定v1の形式、atomic更新、破損原本の保全、Input System専用化とstable binding slotは
[ADR-0007 製品設定と入力契約](../decisions/ADR-0007-product-settings-and-input-contract.md)に従う。

音源、semantic cue、visual feedback、演出速度とrule非侵入の境界は
[ADR-0008 製品音響・演出feedback契約](../decisions/ADR-0008-product-av-feedback-contract.md)に従う。
[M06-T03 音響・演出受入記録](../reports/M06-T03-av-evidence.md)へ`AV-01`〜`AV-03`の
自動証跡とrelease candidateで再実施する実device matrixを記録する。

日英catalog、Windows installed font、semantic palette、safe frame、対局中の設定往復は
[ADR-0009 製品localization・アクセシビリティ契約](../decisions/ADR-0009-product-localization-accessibility-contract.md)
に従う。[M06-T04 localization・アクセシビリティ受入記録](../reports/M06-T04-localization-accessibility-evidence.md)
へ`LOC-01`〜`LOC-03`と`A11Y-01`〜`A11Y-05`の証跡を記録する。

- Windows 11 x64を正式サポートし、Windows 10はbest-effort・非保証とする。
- 外部素材を購入・委託せず、repository内で所有するprimitiveと音、Windows同梱fontを使う。
- native Windows screen reader対応は主張せず、既知の制限として公開する。

外部素材やfontの同梱、対応環境に費用・契約・追加license判断が生じる場合だけ、実行前に
ユーザー確認を行う。

## 停止条件

- 有償素材、外部制作、追加ライセンスの購入が必要な場合
- 対応OS、最小環境、言語範囲を決めないと実装が分岐する場合
- 品質基準を満たすためにルールや保存形式の破壊的変更が必要な場合

## 次への遷移

`M06-T06`完了時にM06を`Done`、M07と`M07-T01`を`Ready`へ変更する。通常の1タスク依頼と
M06を対象にしたGoalはM07を実装せず、状態更新後に停止する。
