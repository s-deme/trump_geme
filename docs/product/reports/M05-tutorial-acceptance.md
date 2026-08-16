# M05 Crazy Eightsチュートリアル受入記録

## 受入境界

- 実施日：2026-08-16
- 対象：2人Crazy Eights、human player 0、CPU player 1
- tutorial definition：`crazy_eights_basic_v1` version `1`
- game seed：`29`
- wild rank：`8`
- CPU difficulty：`Standard`（安定ID `1`）
- 初見相当：履歴のないmemory progress storeとTitleの既定focusから開始する自動scenario
- 入力：pointer clickとEventSystem Submit（keyboard／controllerの決定操作相当）

この受入は外部説明を参照せず、保存済みsceneから製品画面を操作する。実ユーザーの理解率を
測定するものではなく、再現可能な導線・操作・観測eventを合格基準とする。

## 自動受入結果

`pwsh ./scripts/run-product-unity-tests.ps1 -UnityPath <Unity.exe>`で次を検証した。

| 項目 | 自動検証 | 結果 |
|---|---|---|
| 未完了時の入口 | Titleの`Tutorial`が優先focusで、通常`Play`も利用可能 | Pass |
| 基本操作 | pointerとSubmitを交互に使い、play、draw、8のcalled suit、上がりを実Runtime eventで完了 | Pass |
| 6 step | `Step 1 / 6`から`Step 6 / 6`まで固定trace 17 Actionを順に進行 | Pass |
| 想定外入力 | 別の合法Action、古いID、二重入力を拒否し、turnとAction履歴を変更しない | Pass |
| CPU待機 | tutorial中のHelpでCPUが進まず、閉じた後のExitで待機coroutineをcancel | Pass |
| 結果説明 | winner、score、`empty hand`、turn数を確認してからだけ完了を保存 | Pass |
| 完了状態 | version付きprogressをatomic保存し、Titleの優先focusを`Play`へ変更 | Pass |
| 再実行 | TitleとGame Settingsの`How to play`から明示的に再開始できる | Pass |
| 保存境界 | tutorial対局をSaved sessionsへ保存せず、progressにID、version、flagだけを保存 | Pass |
| 破損保護 | 破損・未知形式を完了扱いせず、再完了時にも元fileを上書き・削除しない | Pass |
| 非公開情報 | viewer 0のstructured presentationだけを画面へ渡し、CPU手札identityやstock順を案内へ使わない | Pass |

主要fixtureは次のとおり。

- `ProductFlowTests.TutorialCompletesWithPointerAndSubmitThenCanBeRestarted`
- `ProductFlowTests.TutorialHelpPausesCpuAndExitCancelsThePendingAction`
- `ProductContractTests.TutorialRejectsUnexpectedAndStaleActionsThenCompletes`
- `ProductContractTests.ProductProgressRoundTripsAndRefusesToOverwriteCorruption`
- `StructuredPresentationContractTests.CrazyEightsPresentationDoesNotLeakOpponentCards`

## M05完了時の検証

| 検証 | 結果 |
|---|---|
| `dotnet build TrumpGameLab.sln -m:1` | Pass、警告0、エラー0 |
| `dotnet test tests/TrumpLab.Tests` | Pass、262/262 |
| `./scripts/verify-migration.sh` | Pass |
| `pwsh ./scripts/verify-migration.ps1` | Pass |
| Product Unity EditMode | Pass、17/17 |
| Product Unity PlayMode | Pass、4/4 |
| Unity Standard EditMode | Pass、260/260 |

## 既知の学習上の制約

- 外部ユーザーテストは未実施であり、初見プレイヤーの理解率・完了率は評価していない。
- source文言は英語のみ。翻訳、locale切替、文章校正はM06の範囲とする。
- pointer、従来Inputのnavigation、Submit、Escapeを対象とし、screen reader固有APIと新Input Systemは未対応。
- 固定traceで必須eventを確実に体験する形式であり、自由練習や全合法手の網羅ではない。
- tutorialはCrazy Eightsだけを対象とし、他の登録ゲームへ一般化していない。
- definitionのrulesまたはCPU方策を変更するときは、versionと17 Action fixtureを意図的に再評価する必要がある。
