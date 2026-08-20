# ADR-0006 製品品質基準とWindows対象環境

- Status: Accepted
- Date: 2026-08-16

## Context

M05までに`crazy_eights`の構造化表示、Unity製品縦切り版、保存・リプレイ、CPU難易度、
チュートリアルを実装した。M06ではこの縦切り版を配布候補として評価できる状態へ引き上げるため、
後続実装より先に次を固定する必要がある。

- 正式サポートするWindows、最小環境、画面範囲
- mouse、keyboard、gamepadと再割り当ての範囲
- 設定、音、演出、日本語・英語、アクセシビリティ、異常復旧の合否条件
- 起動、frame、応答、保存、memory、長時間実行の性能予算
- releaseを止める不具合重大度と検証証跡

Windows 10は2025年10月14日にMicrosoftの通常サポートを終了した。一方、Unity 6 Playerは
Windows 10以降でも動作し得るため、engineの動作範囲と本製品が保証する範囲を分ける。
ユーザー判断により、Windows 11 x64を正式サポート、Windows 10をbest-effort・非保証とする。

## Decision

### 対象環境

| ID | 項目 | 基準 | 合否証跡 |
|---|---|---|---|
| ENV-01 | 正式OS | Microsoftがservice中のWindows 11 x64。M06の最小基準は24H2（build 26100）以降、native実行 | release候補を更新済みWindows 11 x64で起動し、全手動受入を完走する |
| ENV-02 | 非保証OS | Windows 10 22H2 x64はbest-effort。利用可能ならsmoke testするが、Windows 10固有不具合はrelease blockerにしない | 実施有無と結果を既知の制限へ記録し、正式対応と表記しない |
| ENV-03 | CPU・memory | Windows 11互換x64 CPU、2 core以上、1 GHz以上、system memory 4 GiB以上 | 最小profileまたは同等に制限した環境でPERF項目を測定する |
| ENV-04 | graphics | DirectX 12対応かつWDDM 2.0 driverを持つGPU。Playerの描画APIはDirect3D 11、vendorがservice中のdriver | release buildがD3D11で起動し、graphics error、欠落shader、表示崩れがない |
| ENV-05 | storage・network | product、log、保存のため1 GiBの空き容量。通常対局、tutorial、保存、replayはaccountとnetwork接続を要求しない | offlineで起動から終了まで完走し、保存・再開できる |
| ENV-06 | build基準 | Windows Standalone x86_64。製品projectは保存済みUnity Editor versionでbuildし、使用packageをlock fileで固定する | build reportへEditor、package lock、target、hashを記録する |

Windows 11のservice対象が変わった場合、M07の候補build作成時にMicrosoft lifecycleを再確認する。
24H2がservice外なら、動作範囲を下げずに最小versionをservice中のreleaseへ進め、本文と提出要件を
同じcommitで更新する。

UPM Runtime packageは引き続きUnity 2021.3以上、`.NET Standard 2.1`、UnityEngine非依存を守る。
製品projectだけで利用する入力・表示packageをCoreやpackage Runtimeへ伝播させない。

### 画面と表示

reference resolutionは[ADR-0002](ADR-0002-unity-product-shell.md)どおり`1920 x 1080`、
既定windowは`1280 x 720`とする。

| ID | 基準 | 合格条件 |
|---|---|---|
| DSP-01 | 解像度matrix | `1280x720`、`1280x800`、`1920x1080`、`1920x1200`、`2560x1080`、`3440x1440`、`3840x2160`のwindowまたは同等Game Viewで全主要screenを確認する |
| DSP-02 | scaleとsafe frame | Windows表示scale `100%`、`150%`、`200%`でcritical control、focus、modal、card、説明文がviewport外へ出ない。21:9では主要操作を中央16:9 safe frame内に置き、余白だけを拡張する |
| DSP-03 | layout | 文字切れ、重なり、意図しないscroll、0 pixelのcontrol、操作不能なbuttonが0件。windowを最小値未満へ縮めても最後に有効だった設定へ戻せる |
| DSP-04 | frame presentation | 60 HzでVSyncする`60 fps`を既定とし、focus喪失中は入力を適用しない。windowedとborderless fullscreenから安全に復帰できる |

exclusive fullscreen、HDR、portrait、touch、Steam Deck固有layoutはM06の正式範囲外とする。
unsupportedなaspectでもcrashや保存破損を起こしてはならず、中央safe frameまたはletterboxで操作を残す。

### 入力と設定

製品projectはUnity Input Systemを採用し、保存済みEditorに対するreleased packageを
`Packages/manifest.json`と`packages-lock.json`で固定する。Input Systemは製品assemblyだけから参照し、
ルールRuntime、Action ID、seed、保存・replay形式へ入力device情報を混ぜない。

| ID | 基準 | 合格条件 |
|---|---|---|
| INP-01 | mouse | 左clickだけでTitleから1局完走、結果、再戦、保存一覧、replay、tutorial、設定、終了へ到達できる |
| INP-02 | keyboard | keyboardだけで全screenを操作できる。既定は矢印/WASDまたはTabで移動、Enter/Spaceで決定、Escapeで戻る、F1でhelp |
| INP-03 | gamepad | Unity `Gamepad` layoutを使い、Xbox/XInput controllerを基準deviceとする。D-pad/left stickで移動、southで決定、eastで戻る、northでhelpを実行し、1局とtutorialを完走できる |
| INP-04 | focus | screen、modal、device切替後にvisible focusが必ず1つある。disabled・hidden controlへfocusせず、同一入力を二重適用しない |
| INP-05 | rebinding | navigation 4方向、決定、戻る、helpをkeyboardとgamepadごとに再割り当てできる。重複を拒否し、決定・戻るを未割当にはできず、cancelと既定値復元ができる |
| INP-06 | device変化 | gamepad切断から1秒以内にkeyboard/mouseで操作を継続でき、再接続後は再起動なしでgamepadへ戻れる。切断中のActionを適用しない |
| CFG-01 | 永続設定 | display mode、resolution、VSync、master/music/SFX音量、演出速度、入力binding、locale、text scale、high contrast、reduced motionをversion付き設定へatomic保存する |
| CFG-02 | 安全な復旧 | 設定の欠落、破損、未知versionでは安全な既定値で起動し、元fileを黙って上書き・削除しない。Resetはsession、replay、tutorial進捗を消さない |

既定値はwindowed `1280x720`、VSync on、各音量`80/60/80`、演出速度`Normal`、
text scale `100%`、high contrast off、reduced motion offとする。localeはWindows UI cultureが
日本語なら`ja-JP`、それ以外は`en-US`とし、いつでも明示変更できる。

DualShock、DualSense、generic joystickの直接接続はInput Systemが`Gamepad`として正規化できる範囲の
best-effortとし、正式な手動受入deviceには数えない。Steam Inputによるdevice変換とglyph切替はM07で扱う。

### 音、演出、localization、アクセシビリティ

外部素材の購入・制作委託は行わない。M06の視覚素材はuGUIとrepository内で作成したprimitive、
音はrepository内で生成・所有する短いclipを使う。Windows同梱fontをruntime参照し、font fileを
再配布しない。外部assetまたはfontの同梱が必要になった場合は、そのlicense判断前に停止する。

| ID | 基準 | 合格条件 |
|---|---|---|
| AV-01 | 操作feedback | navigation、決定、拒否、card play、draw、wild suit確定、CPU手番、勝利、敗北、errorに視覚変化があり、主要状態は音でも区別できる |
| AV-02 | 音量 | master/music/SFXを`0`から`100`まで変更でき、`0`は完全mute。設定変更を1秒以内に既存AudioSourceへ反映し、再起動後も保持する |
| AV-03 | 演出 | `Reduced`、`Normal`、`Fast`を選べる。Reducedは非本質的motion・flashを停止し、どの速度でも入力lock、Action順、結果は同一になる |
| LOC-01 | 対応言語 | 全user-facing文字列にstable key、`ja-JP`と`en-US`の値、英語fallbackがある。catalog間の欠落key、raw key表示、未許可hard-coded文言は0件 |
| LOC-02 | 日本語表示 | Windows 11標準の日本語font候補を検出し、全catalog文字にmissing glyphがない。候補が得られない場合は英語fallbackと明示errorを使い、tofuを表示しない |
| LOC-03 | 文字layout | DSP matrixの両localeとtext scale `100%`、`125%`、`150%`で意味を失うtruncate、重なり、操作不能が0件 |
| A11Y-01 | contrast | 通常文字は背景と`4.5:1`以上、大きな文字とfocus indicator・active controlは`3:1`以上。palette値を自動計算し閾値未満を失敗にする |
| A11Y-02 | 非color情報 | suit、合法性、tutorial期待Action、focus、勝敗、errorを色だけで区別せず、文字・symbol・outlineのいずれかを併記する |
| A11Y-03 | focusと読み順 | keyboard/gamepadの移動順が視覚順と一致し、全interactive controlにlocale別の意味のあるlabelがある |
| A11Y-04 | motion・文字 | text scale、high contrast、reduced motionを対局中でも変更でき、盤面状態や入力待ちを失わない |
| A11Y-05 | 操作対象・点滅 | interactive controlのhit領域はreference resolutionで`44x44 px`以上。1秒間に3回を超える点滅を使わず、Reducedでは非本質的な点滅を0にする |

Windows Playerに対するOS native screen reader連携は正式対応に含めない。Unity 6の
`AssistiveSupport`がscreen readerを公開するplatformはAndroidとiOSであり、Windowsは対象外のためである。
本製品はfull WCAG conformanceを主張せず、上記の検証可能なdesktop subsetを品質gateとする。
この制限はM06の既知の問題とSteam提出checklistへ明記する。

### 異常復旧

| ID | 基準 | 合格条件 |
|---|---|---|
| REL-01 | 保存・設定破損 | 破損、途中書込み、未知version、改ざんを明示errorで拒否し、正常な既存fileを上書き・削除しない |
| REL-02 | 想定外終了 | 初期dealと各Action適用後のatomic checkpointだけを復元し、部分Actionや二重Actionを生成しない |
| REL-03 | device・例外 | gamepadまたはaudio出力deviceの切断・再接続でcrashせず、状態を失わない。user操作で回復可能な失敗はlocale化したerrorと安全な戻り先を示し、未処理例外、無限待機、入力lock残留は0件 |
| REL-04 | offline | network、Steam client、外部accountがなくてもM06の全機能を利用できる。外部送信を行わない |

### 性能予算

性能測定はWindows x86_64のnon-Development release Player、D3D11、60 Hz、VSync onで行う。
最小profileを利用できない場合はCPU・memoryを同等以下へ制限したprofileを使い、その差をreportへ記録する。
最初の1回をwarm-upとして除外し、時刻だけに依存する不安定なassertは通常のunit testへ混ぜない。

Unityがnon-Development Playerでmanaged allocation counterを公開しないため、`PERF-07`のallocationだけは
[ADR-0010](ADR-0010-product-quality-probe-contract.md)に従い、同じWindows targetのDevelopment Playerで
測る。frame、起動、保存、memory、soakの値は引き続きrelease Playerだけを合否に使う。

| ID | 測定 | 合格予算 |
|---|---|---|
| PERF-01 | process開始からTitleが入力可能になるまで | 3回すべて`5.0秒以下` |
| PERF-02 | Title、Settings、Match、How to play、Resultを各1分操作したCPU frame time | 各screenでp95 `16.67 ms以下`、p99 `33.33 ms以下`、明示的load以外の最大`100 ms未満` |
| PERF-03 | 入力eventからfocus・label・screen・cardが更新されるまで100操作 | p95 `100 ms以下`、最大`200 ms以下` |
| PERF-04 | CPU方策の純計算時間。3難易度、固定100 state、演出delayを除外 | 各難易度p95 `50 ms以下`、最大`100 ms以下` |
| PERF-05 | atomic save、resume、replay checkpoint、100 slot一覧 | 各操作p95 `250 ms以下`、最大`500 ms以下` |
| PERF-05B | [ADR-0003](ADR-0003-save-replay-contract.md)上限の1 MiBまたは10,000 Action archive | save・encode `1秒以下`、load・全replay `2秒以下`。処理中は明示的なinput lockまたは進捗を表示し、OSから無応答と判定されない |
| PERF-06 | 5分warm-up後のprivate bytes（PSAPI `PrivateUsage`）と60分soak中のpeak | baseline `512 MiB以下`、peak `768 MiB以下`、warm baselineからの増加`64 MiB以下` |
| PERF-07 | idle中のmanaged allocationとAction適用100回 | idle steady-state `0 B/frame`、1 Action p95 `256 KiB以下`、GC起因frame stall `50 ms未満` |
| PERF-08 | 固定seedの自動100局と実時間60分の操作soak | 両方で未処理例外、deadlock、入力二重適用、保存破損、増え続けるGameObject・coroutineが0件 |

`0.35秒`のCPU待機、tutorial説明待ち、選択した演出時間は意図したpresentation delayであり、
PERF-03とPERF-04から除外する。ただし待機中にinputを誤適用せず、Reduced/Fast設定へ即時追従する。

### 品質gateと証跡

| 重大度 | 定義 | M06完了時の扱い |
|---|---|---|
| S0 Blocker | 起動不能、data loss、秘密情報露出、rule/seed破壊、release不能 | 0件必須 |
| S1 Critical | 必須flow完走不能、正式入力・localeが利用不能、crash・hang | 0件必須 |
| S2 Major | 主要品質低下だが明示的な回避策で完走可能 | 該当する番号付き基準を破らない場合だけ、回避策・owner・M07判断を記録して許容 |
| S3 Minor | cosmeticまたは限定的で完走と理解を妨げない | 重大度、再現手順、回避策を記録する |

M06-T02からT05は、実装した番号付き基準を自動testまたは再現可能なmanual matrixへ結び付ける。
M06-T05は性能値とsoak結果を`docs/product/reports/`へ保存する。M06-T06は全IDの証跡、既知の問題、
Windows 10 smokeの実施有無をrelease candidate checklistで照合する。合否欄が未記入、測定環境が不明、
S0/S1が残存、または番号付き基準が失敗中ならM06を`Done`にしない。

### 互換性境界

- `crazy_eights`のgame ID、`IGame`、Action ID、CLI引数・出力、seed再現性を変更しない。
- settingsはsession archive、replay、tutorial progressと別のversion付きproduct fileにする。
- 演出速度、locale、音量、入力bindingはrule state、CPU観測、replay Action列へ入れない。
- 保存形式を変える必要が生じた場合はADR-0003のmigration規則に従い、破壊的変更では停止する。
- RuntimeはUnityEngine、Input System、候補台帳、具体game UIへ依存しない。

## Consequences

- Windows 11 x64で再現可能なrelease gateを持ち、Windows 10固有問題を正式品質保証から分離できる。
- M06後続タスクは番号付き基準へtestとreportを対応させるため、単なる見た目の調整で完了扱いにできない。
- Input System、locale catalog、設定store、performance probe、soak harnessの製品側実装が必要になる。
- 日本語fontを再配布せずWindows同梱fontへ依存する。対象環境でglyph検査を必須にし、失敗時は
  tofuではなく英語fallbackとerrorを使う。
- native Windows screen readerは未対応として公開するが、focus、label、非color情報、text scale、
  contrast、motion軽減を自動・手動の両方で固定する。
- 外部素材を採用する場合は本ADRを黙って逸脱せず、費用・契約・license判断の前に停止する。

## References

- [Microsoft: Windows 10 support ended on October 14, 2025](https://support.microsoft.com/en-US/Windows/Deployment/Updates-Lifecycle/windows-10-support-has-ended-on-october-14-2025)
- [Microsoft: Windows 11 requirements](https://learn.microsoft.com/en-us/windows/whats-new/windows-11-requirements)
- [Unity: System requirements for Unity 6](https://docs.unity3d.com/6000.0/Documentation/Manual/system-requirements.html)
- [Unity: Input System](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.inputsystem.html)
- [Unity: AssistiveSupport](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Accessibility.AssistiveSupport.html)
- [W3C: Understanding Success Criterion 1.4.3, Contrast (Minimum)](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum)
- [W3C: Understanding Success Criterion 1.4.1, Use of Color](https://www.w3.org/WAI/WCAG22/Understanding/use-of-color)
