# M06-T06 release candidate checklist

## 候補identity

| 項目 | 値 |
|---|---|
| source commit | `3ffd2d5` (`Add product quality and soak verification`) |
| build日 | 2026-08-21 |
| Unity | 6000.3.22f1 |
| target | Windows Standalone x86_64 / non-Development / D3D11 / VSync on |
| scene | `Assets/TrumpLab/Product/Scenes/Bootstrap.unity`のみ |
| release executable | `TestResults/ProductQuality/WindowsRelease/TrumpGameLab.exe` |
| release executable SHA-256 | `8da50e79b68e979c53cafb639961ca6c34d39bfed8e87b91cf9caf5c12c081fe` |
| release build tree SHA-256 | `6ddc547d7d5c7e8b9332f5b8021b511f9abd78a218140b5188f1a341663c2a51` |
| build report size | 93,155,030 bytes |
| `Packages/manifest.json` SHA-256 | `93b828f11c37f29135790fa29e54b4a88816c811ba832cb019e5ccf5a70eeab8` |
| `Packages/packages-lock.json` SHA-256 | `860cdccdcc49ff9a1a443207d24cfbeb3f59df1326e39c4baff21e54123eaf03` |
| `ProjectVersion.txt` SHA-256 | `4c5c83b324e4beafbe03132a68b0e9d87e93d6a11e687f6cb5c0fc514219e154` |
| `Bootstrap.unity` SHA-256 | `93e4a11381fb17ecf9b971490ec75a7c483414c6aca4bf8c6eb871c47c131e02` |

候補は`scripts/run-product-quality.ps1 -Mode Quick`のclean outputからbuildし、同じbuildを
`-Mode Full -SkipBuild`で検証した。release tree hashは
[M06-T05品質記録](M06-T05-quality-evidence.md)の正式Fullと一致する。buildはリポジトリ外の
`TestResults/`にあり、Gitや公開配布物には含めない。

## 自動検証の固定値

| 検証 | 結果 |
|---|---|
| Windows quality probe Full | Passed（startup 3 / 3、5 screen、100局、60分42,400 Action、allocation） |
| Product Unity EditMode / PlayMode | Passed（124 / 124、17 / 17） |
| Unity Standard EditMode | Passed（260 / 260） |
| .NET build / test | Passed（warning 0、error 0、262 / 262） |
| Bash / PowerShell migration | Passed / Passed |
| Markdown link / Unity meta GUID / missing script | Passed（0 missing / 0 duplicate / 0 missing） |

### 実候補Title smoke

2026-08-21に同一SHA-256のrelease executableを`1280x720`、windowed、D3D11で起動した。
windowは1296x759（frame込み）で応答し、日本語Title / subtitle / 5 controlにtofu・truncate・
重なりはなく、初期focusはTutorial buttonの青いoutlineとして表示された。Player logの
`Error` / `Exception` / missing shader / failedにhitは0件だった。window close requestで通常終了した。

hostにはFosi Audio SK02、NVIDIA HDMI、Realtek、Steam Streamingなどのaudio endpointが
Present / OKで存在した。一方、Xbox / XInput / Gamepadと判定できるPnP deviceは0件で、
実gamepad受入はこの環境で実施できない。このsmokeはTitleの実描画だけを証明し、
下記の全screen、物理入力、主観的audioを代替しない。

## 番号付き基準の照合

`Manual Pending`はbatchやsynthetic deviceでPassedと代替できない。合否欄が残る間は
M06-T06とM06を`Done`にしない。

| ID | 自動証跡 | 候補の現状 |
|---|---|---|
| ENV-01 | Windows 11 build 26200でFull / Unity全件Passed | **Manual Pending**：同じ候補で起動から終了まで手動完走 |
| ENV-02 | 正式gate対象外 | Not run：Windows 10環境なし、best-effort・非保証として既知制限に記録 |
| ENV-03 | 2 logical CPU affinity、private bytes / frame予算Passed | Passed |
| ENV-04 | D3D11固定、5 screen frame予算Passed、実Title smoke / log 0 | **Manual Pending**：残るscreenのgraphics error、missing shader、表示崩れ0を目視確認 |
| ENV-05 | Product Runtimeのnetwork依存0、offline quality Player Passed | **Manual Pending**：networkを切った実候補で保存・再開・replayまで完走 |
| ENV-06 | Unity / target / scene / package lock / hashを上表に固定 | Passed |
| DSP-01 | 7解像度 x 2 locale x 3 text scaleの42組contract Passed | **Manual Pending**：主要screenの実Player巡回 |
| DSP-02 | 16:9 safe frame、21:9 bounds contract Passed | **Manual Pending**：Windows表示scale 100 / 150 / 200%の目視確認 |
| DSP-03 | overflow / overlap / hit target / minimum window guard回帰Passed | **Manual Pending**：最小未満resize→最後の有効解像度への復帰 |
| DSP-04 | VSync 1、focus / display guard回帰Passed | **Manual Pending**：windowed / borderless往復、focus喪失中のAction 0 |
| INP-01 | pointer PlayMode flow Passed | **Manual Pending**：実mouseだけで通常局・tutorial・replay・設定・終了 |
| INP-02 | keyboard Input System / focus / modal回帰Passed | **Manual Pending**：実keyboardだけで全screenと1局完走 |
| INP-03 | synthetic `Gamepad` action / focus / tutorial回帰Passed | **Manual Pending**：実物Xbox / XInputで全screenと1局・tutorial完走 |
| INP-04 | modal / dropdown / scroll / routeのvisible unique focus Passed、実Title初期focus表示 | **Manual Pending**：3 device切替と同一入力の二重適用0 |
| INP-05 | stable binding、duplicate / empty拒否、cancel / reset / persistence Passed | **Manual Pending**：keyboardとXInputの7 commandを実rebind |
| INP-06 | synthetic disconnect / reconnect、rebind中disconnect Passed | **Manual Pending**：実XInput抜き差しと1秒以内のkeyboard / mouse復帰 |
| CFG-01 | `settings.v1`の全field atomic round-trip Passed | **Manual Pending**：Apply→再起動後の実UI保持 |
| CFG-02 | missing / corrupt / unknown / explicit Resetとsession / progress不変Passed | **Manual Pending**：Reset後に保存局・replay・tutorial進捗を目視確認 |
| AV-01 | 10 semantic cueのvisual / PCM / exactly-once回帰Passed | **Manual Pending**：スピーカーでvisible / audible / 相互聞き分け |
| AV-02 | master / music / SFX 0〜100、Apply / Reset / Load、audio reconfigure Passed | **Manual Pending**：実出力で0 / 50 / 100と1秒以内反映、抜き差し復帰 |
| AV-03 | Reduced / Normal / FastのAction列・結果・input lock不変Passed | **Manual Pending**：各速度の視覚差、Reducedの非本質motion 0 |
| LOC-01 | 254 key、ja / en parity、fallback / raw key contract Passed | **Manual Pending**：主要flowの未翻訳 / raw key 0を目視確認 |
| LOC-02 | Windows font candidate / glyph / English fallback回帰Passed、実Titleのtofu 0 | **Manual Pending**：残る全flowと長文の日本語改行 |
| LOC-03 | 42組layout contract Passed | **Manual Pending**：ja / en x 100 / 125 / 150%の主要flow目視 |
| A11Y-01 | palette全roleの4.5:1 / 3:1自動計算Passed | **Manual Pending**：high contrastで文字 / active / focusを識別 |
| A11Y-02 | suit / legal / expected / focus / outcome / errorのnon-color contract Passed | **Manual Pending**：主要flowで色以外の識別手段を目視 |
| A11Y-03 | locale別label / visual-order navigation / focus scope Passed | **Manual Pending**：実keyboard / XInputの読み順とvisible focus |
| A11Y-04 | 対局中設定往復のsession / archive / turn不変Passed | **Manual Pending**：同局でlocale / 150% / high / reducedをApplyし復帰 |
| A11Y-05 | 44x44 hit area、flash / Reduced policy contract Passed | **Manual Pending**：実scaleで操作可能、3 Hz超の点滅0 |
| REL-01 | strict codec / atomic store / 破損原本保全Passed | Passed |
| REL-02 | autosave interruptionで最終成功checkpointのみ復元Passed | Passed |
| REL-03 | synthetic input / audio reconfigure、error復旧、Full log / lock 0 | **Manual Pending**：実gamepad / audio device抜き差し |
| REL-04 | network / Steam API非依存、offline quality Player Passed | **Manual Pending**：実際にnetworkを切った全flow |
| PERF-01〜08 | [M06-T05品質記録](M06-T05-quality-evidence.md)のformal Full | Passed |

## 手動受入記録欄

次の全欄を実候補で埋める。実施者の主観を要する聞き分け、日本語改行、
物理device切断は自動値で代替しない。

| 項目 | 記録 |
|---|---|
| 実施者 / 実施日 | **Pending** |
| Windows edition / build / 表示scale | **Pending** |
| GPU / driver / 解像度 / refresh rate | **Pending** |
| mouse / keyboard / Xbox or XInput型番 | **Pending** |
| audio出力device / driver | **Pending** |
| 実際の日本語font名 / English font名 | **Pending** |
| candidate executable SHA-256一致 | **Pending** |

### 手動flow

| ケース | 実施内容 | 結果 |
|---|---|---|
| RC-M01 mouse / offline | networkを切り、mouseだけでTitle→設定→1局→Result→再戦→保存一覧→再開→replay→tutorial→終了 | **Pending** |
| RC-M02 keyboard | 矢印 / WASD / Tab、Enter / Space、Escape、F1だけで全screenと1局完走 | **Pending** |
| RC-M03 XInput | D-pad / left stick、south / east / northで全screen、1局、tutorialを完走 | **Pending** |
| RC-M04 rebind / disconnect | keyboard / gamepad各7 commandをrebind、duplicate拒否、cancel / reset、待機中抜き差しと復帰 | **Pending** |
| RC-M05 display | 7解像度、16:10 / 21:9 / 4K代表、OS scale 100 / 150 / 200%、windowed / borderless、最小未満resize | **Pending** |
| RC-M06 locale / accessibility | ja / en x 100 / 125 / 150%、high contrast、reduced motionを主要flowと対局中往復で確認 | **Pending** |
| RC-M07 audio / feedback | 10 cueのvisible / audible / 聞き分け、master / music / SFX 0 / 50 / 100、audio抜き差し | **Pending** |
| RC-M08 settings / recovery | Apply→再起動保持、Resetでsession / replay / progress不変、保存・再開・replay | **Pending** |

## 現在の判定

- 自動gate：Passed
- 実候補Title smoke：Passed（日本語glyph / visible focus / log 0）
- S0 / S1 / S2の既知問題：0件
- Windows 10 smoke：Not run（利用可能環境なし、best-effortのためrelease blockerではない）
- 実device / 実表示 / 実audio受入：**Pending**
- M06-T06 / M06：**In Progress**

RC-M01〜M08がすべてPassedになるまでM06を完了しない。
