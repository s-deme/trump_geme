# M06-T04 localization・アクセシビリティ受入記録

## 受入境界

- 実施日：2026-08-20
- 対象：Windows 11 x64、Unity 6000.3.22f1、Crazy Eights製品縦切り版
- locale：`en-US`、`ja-JP`（英語fallback）
- text scale：`100%`、`125%`、`150%`
- 解像度：`1280x720`、`1280x800`、`1920x1080`、`1920x1200`、`2560x1080`、
  `3440x1440`、`3840x2160`
- font：Windows installed fontをruntime参照し、font fileを同梱しない

この記録は[ADR-0006](../decisions/ADR-0006-product-quality-baseline.md)の`LOC-01`〜`LOC-03`、
`A11Y-01`〜`A11Y-05`と
[ADR-0009](../decisions/ADR-0009-product-localization-accessibility-contract.md)を、生成scene、
自動test、release candidateで再実施する手動matrixへ接続する。

## 番号付き基準と自動証跡

| ID | 自動証跡 | 結果 |
|---|---|---|
| LOC-01 | catalogのkey集合・placeholder parity・英語fallback、全Textのstable分類、raw key / user-facing hard-code scan | Passed（254 key、`ProductLocalizationTests`、`ProductContractTests`） |
| LOC-02 | OS font候補順、全catalog・symbol glyph、候補なし時のeffective英語fallbackと明示warning | Passed（`ProductLocalizationTests`、font欠落PlayMode回帰） |
| LOC-03 | 2 locale x 3 scale x 7解像度の42組で全主要screenと動的状態の文字・bounds検査 | Passed（42 / 42、focused contract 18 / 18） |
| A11Y-01 | semantic palette全roleのWCAG relative luminanceと`4.5:1` / `3:1` threshold | Passed（`ProductAccessibilityTests`） |
| A11Y-02 | suit、合法Action、tutorial期待、focus、勝敗、errorのtext / symbol / outline契約 | Passed（generated UI contract、Product presenter tests） |
| A11Y-03 | 全Selectableのlocale別label、visible focus、active controlだけの視覚順navigation | Passed（modal / dropdown / scroll PlayMode回帰を含む） |
| A11Y-04 | Match / tutorial中のApply後もsession、archive、snapshot、手番、input waitが不変 | Passed（Match / tutorial settings PlayMode回帰） |
| A11Y-05 | reference resolutionで全hit領域`44 x 44 px`以上、Reduced時motion / flash停止 | Passed（generated UI contract、presentation tests） |

## 互換性証跡

| 項目 | 合格条件 | 結果 |
|---|---|---|
| settings | `settings.v1`のversion、field順、round-trip、破損保全を変更しない | Passed（codec / store契約と既存回帰を維持） |
| rule / seed | locale、font、theme、scale、motionでAction列・結果・seed再現性が変わらない | Passed（262 / 262、presentation速度不変性回帰） |
| save / replay | session、replay、tutorial progressへUI設定を混ぜない | Passed（Product PlayMode 16 / 16） |
| assembly | ProductだけがUnity UI / OS fontを参照し、UPM RuntimeはUnityEngine非依存 | Passed（両migration検証） |
| asset / license | repositoryへttf / otf、外部localization package、第三者assetを追加しない | Passed（repository-owned code / uGUI primitiveのみ） |

## 実行結果

| 検証 | 結果 |
|---|---|
| generator compile / generated asset validation | Passed（生成成功、focused contract 18 / 18） |
| Product Unity EditMode | Passed（119 / 119、skip 0） |
| Product Unity PlayMode | Passed（16 / 16、skip 0） |
| Unity Standard EditMode | Passed（260 / 260） |
| `dotnet build TrumpGameLab.sln -m:1` | Passed（warning 0、error 0） |
| `dotnet test tests/TrumpLab.Tests` | Passed（262 / 262、skip 0） |
| Bash / PowerShell migration | Passed / Passed |
| `git diff --check`、Markdown link、Unity meta GUID | Passed（最終台帳更新後に再確認） |

## Release candidateで再実施する手動matrix

Windows 11 x64 Playerでbuild hash、OS build、Windows表示scale、解像度、locale、text scale、
input device、font名、実施者、実施日を記録する。Title、Game Settings、Product Settings全page、
Session Library、Matchのhuman / CPU / help / tutorial、Replay、How to play最長page、Win / Loss Result、
Error modalを巡回し、次を確認する。

- 実fontでtofu、意味を失うtruncate、不自然な日本語改行、重なり、viewport外がない。
- Windows表示scale`100%`、`150%`、`200%`と代表的な16:10、21:9、4Kでsafe frameを守る。
- mouse、keyboard、Xbox / XInput gamepadでfocus outline、視覚順navigation、意味のあるlabelを保つ。
- high contrastでも通常文字、active control、focusを区別でき、suit・合法性・勝敗・errorが色だけに
  依存しない。
- 進行中の同じMatch / tutorialからlocale、`150%`、high contrast、reduced motionをApplyして戻り、
  盤面と入力待ちを失わない。Player再起動後も設定を保持し、Reset後もsession / progressを残す。

Windows native screen reader連携とfull WCAG conformanceは正式対応に含めない。この制限はM06-T06の
release candidate checklistとSteam提出checklistへ引き継ぐ。
