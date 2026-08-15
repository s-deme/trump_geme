# ADR-0002 Unity製品シェルとuGUI画面境界

- Status: Accepted
- Date: 2026-08-16

## Context

M01で`crazy_eights`の構造化表示契約を追加した。M02ではルールRuntimeを重複実装せず、
Unity 2021.3以上でタイトルから結果、再戦まで遊べる製品縦切り版を作る必要がある。
既存の`tests/TrumpLab.UnityTests/`はパッケージ検証用の一時プロジェクトtemplateであり、
製品Scene、Prefab、Player Settingsを所有する場所にはしない。

次の判断を後続実装より先に固定する。

- 製品UnityプロジェクトとUPM packageの配置関係
- uGUIとUI Toolkitの選択
- Scene、画面、セッション、Runtimeの責務境界
- assembly definitionの依存方向
- 最小解像度、画面比率、M02で対応する入力
- 人間入力とCPU手番の排他、seed再現性、異常時動作
- Edit ModeとPlay Modeの検証境界

## Decision

### プロジェクト配置

製品プロジェクトをリポジトリ内の`Unity/TrumpGameLab/`へ置く。

```text
Unity/TrumpGameLab/
├─ Assets/TrumpLab/Product/
│  ├─ Runtime/
│  ├─ Scenes/
│  ├─ Prefabs/Screens/
│  └─ Tests/{Editor,PlayMode}/
├─ Packages/manifest.json
└─ ProjectSettings/
```

- `Packages/manifest.json`から、manifestの配置を基準に
  `file:../../../Packages/com.trump-game-lab.rules`として既存UPM packageを参照する。
- Runtime sourceを`Assets`へcopyしない。製品層は`TrumpLab.Core` assemblyの公開APIだけを使う。
- `Library/`、`Logs/`、`Temp/`、`UserSettings/`、生成されたIDE projectは既存`.gitignore`対象とする。
- `tests/TrumpLab.UnityTests/`は引き続きUPM package単体検証専用とし、製品Sceneへ依存させない。

製品とpackageを同一リポジトリで原子的に変更でき、Unityがpackageを通常のUPM依存として
compileするため、この配置を採用する。packageの埋込みcopyと別repository化は行わない。

### UI方式と表示基準

M02はuGUIを採用し、`com.unity.ugui`のUnity 2021.3組込み互換版を使用する。

- 画面は`Canvas`、`CanvasScaler`、uGUI componentと画面Prefabで構築する。
- reference resolutionは横向き`1920 x 1080`、scale modeは`Scale With Screen Size`、
  matchは`0.5`とする。
- M02の受入範囲は`1280 x 720`以上の16:9/16:10 windowとする。safe area、超横長、
  高DPI個別調整はM06の品質基準で扱う。
- カードはM02中は文字とsuit/rankを使う簡素なuGUI表現とし、最終artへ依存しない。
- Runtimeの`View()`、`Action.ToString()`、表示済みlabelは解析しない。

UI ToolkitはUXML/USSによる分離に利点がある一方、M02で必要なカードbuttonの動的生成、
legacy controller navigation、Unity 2021.3でのPlay Mode testを増やす。縦切りではuGUIの
成熟したEventSystemとPrefab testabilityを優先する。UI方式を将来変更しても、画面が参照する
`GamePresentation`と`ActionPresentation`の境界は維持する。

### Sceneと画面遷移

製品Sceneは`Bootstrap.unity`の1つとし、Scene内に次を置く。

- product rootと`ProductAppController`
- `ScreenRouter`
- screenを載せる単一Canvas
- `EventSystem`と`StandaloneInputModule`
- modal error panel

タイトル、ゲーム設定、対局、結果はSceneではなくscreen Prefabとし、同時に1つだけactiveにする。

```text
Title ── Play ──> GameSettings ── Start ──> Match ── Terminal ──> Result
  ^                  │ Back                    │ Fault              │
  │                  └─────────────────────────┘                    │
  ├──────────────────────── Return to title <───────────────────────┤
  └──────────────────────── Result: title <─────────────────────────┘

Result ── Rematch ──> Match（同じ設定とseedで新しいIGameを生成）
```

- `TitleScreen`は開始と終了要求だけを通知する。
- `GameSettingsScreen`はM02では2人戦、human player 0、difficulty 1を固定し、seedと
  `wild_rank`を検証済み値として`GameStartRequest`へまとめる。
- `MatchScreen`は渡されたimmutable view modelを描画し、選択されたaction IDを通知する。
- `ResultScreen`はwinner、score、reasonを表示し、再戦とタイトル遷移を通知する。
- screenはRegistry、`IGame.Apply()`、CPU方策、Scene遷移を直接呼ばない。

複数Sceneを使わないことでM02の非同期load、重複EventSystem、session引継ぎを避ける。
将来の起動logoや大規模game selectionで必要になった時だけScene分割を再検討する。

### セッションとRuntime境界

`ProductAppController`がscreen遷移を所有し、対局中だけ`GameSessionController`を所有する。

`GameSessionController`の責務は次に限定する。

- `BuiltInGames.Registry.Create("crazy_eights", 2, seed, options)`による`IGame`生成
- `IGamePresentationProvider`の必須確認とhuman viewer 0の`Present(0)`取得
- 現在snapshotの`ActionPresentation`をaction IDで選び、その`Action`だけを`Apply()`へ渡す
- CPU手番で`ChooseCpuAction(CurrentPlayer, cpuRandom, difficulty: 1)`を呼び、その結果を`Apply()`する
- 各Apply後のsnapshot再取得、terminal検出、結果またはfaultの通知
- session終了時のcoroutine停止と入力無効化

CPU用乱数はCLIとの既存対応に合わせ、対局seedから
`new DeterministicRandom(seed + 99991)`で1回だけ生成してsession内で再利用する。
Unityの`Random`、`System.Random`、時刻による対局内乱数は使わない。M02の再戦は同じrequestを
再生成するため同じ進行を再現する。別seedは設定画面で明示的に入力する。

screenへ渡すview modelはUnity向けの表示文字列、button enabled、並び順へ変換済みでもよいが、
元データは`GamePresentation`と`ActionPresentation`だけとする。UI labelをRuntime Actionへ
逆変換せず、screen buttonはsnapshot内のaction IDを保持する。

### 入力と排他状態

M02はmouse/標準pointerとkeyboardを必須入力にする。

- pointerはuGUI button click、keyboardはTab/Shift+Tabまたは矢印でnavigation、Enter/Spaceでsubmit、
  Escapeで許可されたBack/return操作を行う。
- Unity標準`EventSystem`と`StandaloneInputModule`を使用し、新Input System packageは追加しない。
- screen表示時に決定可能な最初のcontrolへfocusを置き、screen切替時に古い選択をclearする。
- controllerの最終mapping、rebinding、device切断復帰はM06で扱う。

対局loopは次の排他状態を持つ。

| 状態 | 人間Action | CPU coroutine | 遷移 |
|---|---|---|---|
| `Starting` | 無効 | 無効 | 初回snapshot後に手番で分岐 |
| `AwaitingHuman` | 現snapshotだけ有効 | 無効 | click直後に`Applying` |
| `Applying` | 無効 | 無効 | Applyと再描画後に手番で分岐 |
| `WaitingForCpu` | 無効 | delay後に1回だけ有効 | CPU Action取得後に`Applying` |
| `Finished` | 無効 | 無効 | Resultへ通知 |
| `Faulted` | 無効 | 無効 | error表示後に安全なsession終了 |

click handlerは状態とaction IDの存在を再検査してから即座に`Applying`へ遷移する。同一frameの
二重click、古いsnapshotのbutton、CPU待機中のclickは無視する。`Apply()`がActionを拒否した場合や
providerが得られない場合は推測して継続せず`Faulted`へ入り、error内容を表示してタイトルへ戻れる。

CPU待機演出はscaled timeに依存しない短いcoroutine delayとし、screen終了・再戦・タイトル遷移で
必ずcancelする。CPUは`ChooseCpuAction`以外から手札や山札へアクセスしない。

### assembly definition境界

| Assembly | 配置 | 参照 | 責務 |
|---|---|---|---|
| `TrumpLab.Product` | `Assets/TrumpLab/Product/Runtime/` | `TrumpLab.Core`、uGUI | app、session、screen、presenter |
| `TrumpLab.Product.EditorTests` | `Assets/TrumpLab/Product/Tests/Editor/` | Product、Core、TestAssemblies | presenter、router、Prefab構造のEdit Mode test |
| `TrumpLab.Product.PlayModeTests` | `Assets/TrumpLab/Product/Tests/PlayMode/` | Product、Core、TestAssemblies | screen遷移、人間/CPU 1局完走 |

- `TrumpLab.Core`からProduct assemblyへの参照を追加しない。
- Product assemblyから具体型`CrazyEightsGame`へcastせず、`IGame`、
  `IGamePresentationProvider`、Registryだけを使う。
- Editor APIはEditorTestsに閉じ、Product runtime assemblyへ入れない。
- test helperはRuntimeの非公開状態をreflectionで読まず、公開snapshotと画面出力を検証する。

### 検証境界

M02-T02以降で次を自動化する。

- Edit Mode：screen router、表示変換、Action ID保持、設定validation、fault変換
- Play Mode：Title→Settings→Match→Result、再戦、タイトル復帰、二重入力lock、CPU完走
- Runtime契約：既存.NET/Unity package test、全登録ゲーム、migration verification

SceneとPrefabはUnity Editorで生成・保存し、YAMLを手編集して複雑なobject referenceを推測しない。
Editorを利用できない環境ではcompile可能なscript・asmdef・manifestを先に用意できるが、Scene/Prefabを
含むタスクの完了判定は利用可能なEditorでの生成または検証結果が必要になる。

## Consequences

- 製品Sceneと設定をtest templateから分離しながら、Runtime sourceは1か所に保てる。
- M01のviewer境界と合法手対応がそのままUIのsecurity/input境界になる。
- 単一SceneによりM02の画面遷移とPlay Mode testが小さくなる。
- uGUIとlegacy inputによりUnity 2021.3での縦切りを早く安定させられる。
- M06でgamepad、新Input System、safe areaを扱う際は入力adapterまたはUI navigationを追加する必要がある。
- 同じseedの再戦は同じ局になる。ランダムな「次の局」は保存・リプレイ用seed方針と合わせてM03以降で設計する。
- Unity Editorがない環境ではScene/Prefab serializationとPlay Mode実行を完了できないため、該当タスクで
  Editor利用可否を明示して完了判定する。
