# ADR-0005 Crazy Eightsチュートリアルの学習順序とUX契約

- Status: Accepted
- Date: 2026-08-16

## Context

M05では初見プレイヤーが外部説明なしでCrazy Eightsの基本操作を学び、案内付きの1局を
完走できるようにする。通常対局の`IGame`、合法手、非公開情報境界を変更せず、固定seedで
自動検証できるチュートリアル契約が必要である。

実装前に次を固定する。

- 初回導線と再表示導線
- play、draw、wild suit指定、勝利条件を学ぶ順序と完了判定
- 固定シナリオと通常ルールの境界
- 想定外入力、途中終了、再実行時の扱い
- 通常対局の文脈ヘルプとルール画面
- 入力、focus、非公開情報、文言の検証境界
- 外部ユーザーテストなしで行う「初見相当」受入の意味

## Decision

### 導線と画面責務

初回起動でチュートリアル未完了なら、Titleの最初のfocusと主ボタンを`Tutorial`にする。
通常の`Play`は常に選べ、チュートリアルを強制しない。完了後もTitleとGame Settingsの
`How to play`から再実行できる。

製品層へ次の表示責務を追加する。

- `HowToPlayScreen`：目的、合法なplay、任意draw、8、上がり、得点をページ順に表示する。
- `TutorialOverlay`：通常のMatch表示上へ現在の目標、進捗、短い理由、終了操作を表示する。
- `ContextHelpPanel`：通常対局中に現在phase、top/called suit、合法Actionの意味を表示する。

ルール画面と文脈ヘルプは読み取り専用とし、`IGame`を生成・変更しない。Tutorial overlayだけが
tutorial controllerへaction IDを通知し、画面自身は`Apply()`を呼ばない。

### 学習順序と完了判定

チュートリアルは2人Crazy Eights、人間player 0、CPU player 1、wild rank 8、Standard CPUの
1局で構成する。各学習目標は説明を表示しただけではなく、次の観測可能なeventで完了とする。

| 順序 | 学習目標 | プレイヤーが行うこと | 完了判定 |
|---:|---|---|---|
| 1 | 目的と盤面 | 自分の手札、相手枚数、stock、discard top、手番を確認する | introを開き、最初の盤面説明を次へ進めた |
| 2 | 同suit／同rankのplay | 強調されたnon-wild cardをplayする | 選んだ合法Actionが`play`または`play_last_card`で、直前topとsuitまたはrankが一致した |
| 3 | drawと手番終了 | 強調された`draw`を選ぶ | `draw`が適用され、手札が1枚増え、次playerへ手番が移った |
| 4 | 8とsuit指定 | 8をplayし、Actionに含まれる指定suitを選ぶ | wild rankのplay Actionが適用され、公開field `called_suit`が指定値になった |
| 5 | 合法手の読み方 | 残りの案内Actionを順に選んで対局を進める | 各checkpointの期待Actionがその時点の`Presentation.Actions`に存在し、適用された |
| 6 | 上がりと結果 | 最後のcardをplayして結果を見る | terminal presentationのwinnerがplayer 0、reasonが`empty hand`で、結果説明を確認した |

進捗表示は`Step n / 6`、1文の目標、1文の理由を基本とする。既に達成済みのstepへ戻して
二重加算せず、終了resultを受け取る前に完了状態を保存しない。

### 決定的シナリオ

専用deck、game clone、private field変更は採用しない。通常の
`BuiltInGames.Registry.Create("crazy_eights", ...)`と`SessionRecorder`を使用し、次を持つ
immutableな`TutorialDefinition`を製品層に置く。

- 安定IDとdefinition version（最初は`crazy_eights_basic_v1`）
- 固定game seed、wild rank、difficulty
- 全Actionの期待traceと、学習stepに対応するcheckpoint
- 各checkpointのinstruction keyと期待する公開event

traceは各時点の`ActionPresentation.Id`ではなく、canonicalな`Action`値で保持する。表示時に現在の
`Presentation.Actions`から同値ActionのIDを解決し、存在しない、複数一致、CPU選択不一致、terminal
不一致なら安全に停止する。Action IDはsnapshotごとに再生成され得るため永続契約にしない。

CPU checkpointは0.35秒の既存待機後に`SessionRecorder.ApplyCpuAction()`を呼び、記録Actionが
期待traceと一致することを確認する。人間checkpointでは期待Actionだけを強調する。別の合法Actionを
選んだ場合はゲームへ適用せず、「このstepでは何を確かめるか」と期待Actionの理由を表示する。
非合法Action、古いsnapshot ID、二重入力は従来どおり拒否する。このUI gateは
`LegalActions()`の集合を変更せず、通常ルール用の`Apply()`へ特別分岐を追加しない。

traceは1局を連続して完走し、途中の自動skipや盤面置換を行わない。M05-T03で固定seedを選び、
全checkpoint、全Action、最終resultを.NETとUnityのfixtureへ固定する。rules versionまたは方策変更で
traceが変わる場合は、session互換性とdefinition versionを同時に判断する。

### 案内と通常対局ヘルプ

通常対局でも全合法Actionを有効なbuttonとして表示し、各buttonへ次の短い説明を付ける。

- play：topと一致したsuitまたはrank、wildなら指定後のsuit
- draw：play可能でも1枚引いて手番を終えられること
- choose starter suit：最初の8が指定するsuit
- pass：stockと再利用可能なdiscardがなくplayもないこと

合法理由は`GamePresentation`、`ActionPresentation`、公開fieldだけから構築する。相手のcard identity、
stock順、将来のCPU Actionを説明や強調へ使用しない。色だけに依存せず、button label、枠、説明文の
少なくとも2つで強調する。

`HowToPlayScreen`のページ順は「目的」→「場札と合法play」→「draw」→「8とcalled suit」→
「上がりと得点」とする。Matchから開いた場合は現在phaseに対応するページを最初に表示し、閉じたら
同じsnapshotへ戻る。Help表示中は人間入力とCPU coroutineを停止し、閉じた後に1回だけ再開する。

### 文言と入力

M05のsource文言は英語1言語とするが、instruction、button、rule pageには安定したtext keyと
型付き引数を与え、画面scriptへ長文を散在させない。翻訳資産とlocale選択はM06へ送る。

mouse/pointerに加え、Tab/Shift+Tabまたは矢印、Enter/Space、Escapeで完走できるようにする。
step変更時はinstruction見出し、その後に期待Actionをfocus順へ置く。自動focusでActionを実行せず、
screen reader固有APIや新Input System packageはM06の入力・アクセシビリティ範囲とする。

### 完了状態

tutorial session自体はSaved sessions一覧へ自動保存せず、中断時は次回最初から再実行する。
完了時だけ製品層の`IProductProgressStore`へ次を保存する。

- progress format version
- tutorial definition IDとversion
- completed flag

production実装は`Application.persistentDataPath/TrumpGameLab/`配下の小さなversion付きfileを使い、
一時fileを検証してから置換する。testはmemory storeを注入し、Unity `PlayerPrefs`やglobal staticへ
状態を残さない。未知version、破損、不完全書込みは未完了として安全に案内するが、壊れたfileを
黙って上書き・削除しない。明示的な再実行は完了flagを消さず、同じtutorialを開始する。

### 受入基準

外部ユーザー、翻訳、費用をM05完了条件にはしない。「初見相当」は履歴のないmemory progress storeと
既定focusから開始する自動受入scenarioを意味し、実ユーザーの理解率を主張しない。

Play Mode受入は最低限次を固定する。

1. 未完了状態のTitleでTutorialが最初のfocusになり、pointerとkeyboardの双方で開始できる。
2. 6 stepを順に進み、想定外Action、古いID、二重入力でgame stateが変わらない。
3. play、draw、wild/called suit、terminal resultを実際のRuntime eventで完了する。
4. help中はCPUが進まず、終了、scene破棄、error時に待機coroutineがcancelされる。
5. 完了記録後の再起動相当では通常Playを主focusにし、How to playから再実行できる。
6. viewer 0向けpresentation以外のcard identityが案内、log、保存へ出ない。

Edit Modeではdefinition traceの完全一致、text keyの重複・欠落、focus順、progress fileの
round-tripと破損拒否を検証する。M05-T06で外部説明を参照しない受入手順と、実ユーザーテスト未実施
という制約を完了記録へ残す。

## Consequences

- 通常のCrazy Eightsと同じRuntime・合法手・seedを使い、tutorial専用rule forkを避けられる。
- 固定traceにより学習eventと最終結果を再現できる一方、rules/CPU変更時はdefinition versionと
  fixtureを意図的に更新する必要がある。
- 想定外の合法手を適用しないため自由練習ではないが、短い1局で必須eventを確実に体験できる。
- tutorial sessionの中断再開は提供しない。保存形式を通常sessionと混在させず、完了flagだけを
  小さく安全に保持する。
- 英語sourceと自動の初見相当受入はM05の再現可能な基準になるが、翻訳品質、screen reader、
  実ユーザー理解率はM06以降で別に評価する。
