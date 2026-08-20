# ADR-0009 製品localization・アクセシビリティ契約

- Status: Accepted
- Date: 2026-08-16

## Context

M06-T03までに製品設定v1へ`locale`、`text_scale_percent`、`high_contrast`、
`reduced_motion`を保存できるようになったが、画面は英語の直接記述とUnityのbuilt-in fontに依存し、
これらの設定を操作・適用するUIを持たない。M06-T04では[ADR-0006](ADR-0006-product-quality-baseline.md)の
`LOC-01`〜`LOC-03`と`A11Y-01`〜`A11Y-05`を、既存game・CLI・seed・保存契約を変えずに満たす必要がある。

外部font、翻訳package、画像assetは追加しない。Windows 11同梱fontをruntimeで参照し、製品固有の
catalog、theme、focus表示、layout検査はProduct assemblyだけに置く。

## Decision

### Catalogと文字列境界

- 全user-facing文字列はProduct側のstable keyで識別し、`en-US`と`ja-JP`を同一key集合で持つ。
- 可変値は位置付きの型付き引数でformatし、両localeのplaceholder集合を一致させる。数値と保存時刻は
  意味が変わらないinvariant値を渡し、語順だけをcatalog側で決める。
- key欠落、空値、placeholder不一致、raw key表示を検証時に失敗させる。解決不能時の表示fallbackは
  `en-US`とし、keyそのものを画面へ出さない。
- Coreのgame ID、Action ID、phase、result reason、difficulty ID、tutorial keyは変更せず、Product側で
  catalog keyへ対応付ける。internal exceptionと診断詳細はlogへ残せるが、画面へ生の
  `exception.Message`や未変換の内部IDを表示しない。
- prefab/sceneの初期文字列もcatalogの英語値から生成する。静的文字、動的template、locale非依存の
  user入力を生成componentで分類し、未分類の`Text`を契約違反とする。

### Font選択とfallback

- font fileはrepositoryやbuildへ同梱・再配布しない。Windowsのinstalled fontをruntime APIで検出する。
- 日本語候補は`Yu Gothic UI`、`Meiryo UI`、`Yu Gothic`、`Meiryo`の順、英語fallbackは
  Windows UI fontまたはUnity built-in fontとする。
- 選んだfontで両catalog、card suit、focus、結果、errorに使う全文字のglyphを検査する。
- `ja-JP`の候補または必要glyphが得られない場合、保存済み設定を変更せずeffective localeだけを
  `en-US`へfallbackし、英語の明示warningを表示する。tofuを表示したり起動時に設定fileを
  暗黙更新したりしない。
- font hostは注入可能な境界とし、候補なし、glyph欠落、英語fallbackをdevice非依存testで再現する。

### 設定の適用

- Product SettingsへAccessibility pageを設け、`ja-JP`/`en-US`、`100%`/`125%`/`150%`、
  high contrast、reduced motionを編集する。
- 既存`settings.v1`のversion、field順、atomic更新、破損保全を変更しない。
- Load成功、明示Apply成功、Reset成功の後だけ、locale、font、文字倍率、theme、motionを同じUI更新で
  適用する。保存失敗時は現在の設定と表示を変えない。
- 文字倍率は生成時のimmutableなbase font sizeから毎回計算し、倍率を往復しても累積誤差を作らない。
- MatchまたはtutorialからSettingsを開く場合は、Action commit前のCPU待機だけを停止して同じsessionを
  保持する。戻ると同じsnapshot、Action列、手番、入力待ちへ戻り、CPU処理を高々1回だけ再開する。
  presentation中の設定遷移は受け付けない。

### Theme、非color情報、focus

- UI色はsemantic roleを持つnormal/high-contrast paletteから適用する。WCAG relative luminanceで
  通常文字`4.5:1`以上、大きな文字・active control・focus indicator`3:1`以上を自動計算する。
- suit、合法Action、tutorial期待Action、focus、勝敗、errorは色だけに依存せず、文字、symbol、
  outlineのいずれかを常に併記する。
- 全`Selectable`はstableなlocale別labelと非colorのfocus outlineを持つ。native Windows screen reader
  連携は主張しないが、labelの完全性を検査可能にする。
- navigationはactiveかつinteractableなcontrolだけを視覚上の位置から明示接続し、hidden・disabledな
  controlへ移動しない。screen、modal、device復帰後のvisible focusを1件維持する。
- reference resolutionで全操作対象を`44 x 44 px`以上とする。1秒に3回を超える点滅は実装せず、
  reduced motionでは非本質的なscale、fade、flashを停止する。

### Layout

- 全製品screen、modal、feedbackを中央の`16:9` safe frameへ配置する。16:10や21:9では余白だけを
  拡張し、主要操作をsafe frame外へ動かさない。
- text scaleでfontを縮め直すBest Fitは使わない。長文はwrap、十分なRect、必要なScrollRectでreflowする。
- 7解像度、2 locale、3文字倍率の42組について、文字切れ、意味を失うtruncate、重なり、viewport外、
  0 pixel control、44 pixel未満の操作対象を自動検出する。Windows DPI、実font、日本語改行の人間確認は
  再現可能なPlayer matrixへ分離する。

## Compatibility

- `crazy_eights`、`IGame`、Action ID、CLI、seed、CPU観測、session/replay/tutorial progressを変更しない。
- locale、font、theme、text scale、motionをrule stateやarchiveへ入れない。
- Product assemblyだけがUnity UIとOS font APIを参照し、UPM RuntimeへUnityEngine依存を追加しない。
- 外部font、翻訳、package、assetの同梱が必要になった場合だけ、費用・契約・license判断の前に停止する。

## Verification

- catalog key・placeholder完全性、英語fallback、font候補・glyph、未分類TextをEditor testで検査する。
- palette contrast、非color token、focus outline、label、navigation、hit領域、safe frameをEditor testで検査する。
- 42組のlayout matrixと、進行中Match/tutorialから設定を変更して状態不変で戻るflowを自動testする。
- Windows 11 Playerで実font、tofu、日本語改行、DPI、mouse・keyboard・XInput、設定再起動保持を
  手動matrixとして再確認し、release candidateではM06-T06 checklistから再実施する。

## Consequences

- 翻訳、font、themeの問題をCoreや保存形式へ伝播させず、missing key・glyph・contrastを機械的に検出できる。
- Windows標準fontが利用不能でもdataを失わず英語で操作を続行できる。
- native Windows screen readerとfull WCAG conformanceは対象外のままだが、desktop向けsubsetの
  focus、label、contrast、文字倍率、motion軽減を再現可能な品質gateとして持てる。
