# ADR-0004 Crazy Eights CPU難易度と評価契約

- Status: Accepted
- Date: 2026-08-16

## Context

M04ではCrazy Eightsへ弱・標準・強のCPUを追加し、Unityから選択できるようにする。
`IGame.ChooseCpuAction(player, rng, difficulty)`とCLIの`--difficulty`は既に公開されているが、
正式対応値は`1`だけである。既存の値`1`の選択結果、CLI既定値、保存済みsessionの再生を壊さず、
強さの差を観測不能情報なしで再現可能に説明する契約が必要になる。

次を実装前に固定する。

- 安定difficulty ID、表示順、ゲーム別対応範囲
- 既存difficulty 1とCLI・sessionの互換性
- 方策が参照できる観測と乱数消費
- 弱・標準・強の意図と強度合格基準
- 自己対戦の固定seed、席順、公平なpolicy乱数
- 1手と評価suiteの性能予算

## Decision

### ID、表示名、対応範囲

安定IDとkeyを次のように定義する。IDは保存形式とCLIへ記録される値であり、後から並べ替えない。

| ID | key | 英語表示 | 意味 |
|---:|---|---|---|
| 1 | `standard` | Standard | M03以前のCrazy Eights方策と完全に同じ |
| 2 | `easy` | Easy | 合法手から注入乱数で一様選択する弱い方策 |
| 3 | `hard` | Hard | 観測可能状態を評価するbounded heuristic |

`1`をEasyへ読み替える案は既存CLIとsessionの意味を変えるため採用しない。製品表示順は
Easy、Standard、Hard、すなわちID順ではなく`2, 1, 3`とする。

Runtimeへimmutableなdifficulty descriptorとcatalogueを置き、`GameInfo`は対応IDを公開する。
既存gameの既定対応は`1`だけ、`crazy_eights`は`1, 2, 3`とする。CLI、Simulator、Unity、
SessionRecorderはgame IDとdescriptorで入力を検証し、非対応IDを黙ってfallbackしない。
全92ゲームの`ChooseCpuAction`を一括変更せず、直接呼出しの既存挙動は維持する。

CLIの既定値は`1`のままとする。`simulate crazy_eights`と`play crazy_eights`では`1`～`3`を
指定できるが、他gameまたは非対応gameを含む`compare`では`1`以外を明示エラーにする。
公開game ID、引数名、既定出力は変更しない。

### difficulty 1とsession互換性

Standardは現在の`CrazyEightsGame.ChooseCpuAction`の分岐、legal action順、tie-break、乱数消費数を
完全に保存する。固定stateと同じpolicy seedに対するActionがM04前後で一致しなければ回帰とする。

既存のformat/rules version 1かつdifficulty 1のarchiveは同じAction列を再生できるため、
M04でversionを上げない。EasyとHardも既存`difficulty`fieldへIDを保存する。採用後に各方策の
Actionまたは乱数消費を変える場合は、ADR-0003に従ってrules version判断を行う。

### 観測境界

方策はゲームobject全体ではなく、Crazy Eightsが現在player向けに構築したimmutableな
CPU observationと`LegalActions(player)`だけを受け取る。observationに含めてよい値は次に限る。

- 自分の手札のcard identityと枚数
- 公開された捨札top、called suit、phase、turn count
- stock枚数、各playerの手札枚数
- 現在player、player数、wild rank
- 現在の全合法Action

相手手札のcard identity、stock順、捨札topより下の順序、将来の乱数状態は含めない。
相手手札とstockのcardを同数のまま交換した観測同値state、およびstock順だけを変えたstateで、
同じdifficultyとpolicy seedが同じActionを返すことを全3難易度で検証する。

Easyのランダム選択とHardの同点解消は、引数の`DeterministicRandom`だけを使う。
UnityEngine.Random、System.Random、時刻、process依存hashは使わない。Standardの既存乱数消費は
増やさない。合法手の値と安定順を保持し、すべての方策がその集合の要素だけを返す。

### 方策の意図

- Easy：starter suitを含む全合法手から一様選択する。play可能でもdrawを選び得る。
- Standard：現在と同じくplayをdrawより優先し、non-wildと手札内で多いsuitを優先する。
- Hard：即時勝利、手札削減、高penalty cardの処理、wild保持、called suit後の自分の残り枚数、
  公開された相手手札枚数をscore化する。秘密cardの推定、山札の先読み、game cloneは行わない。

Hardは合法Actionごとの固定整数scoreを1回計算するone-ply未満のheuristicとし、探索木や
Monte Carlo rolloutを追加しない。score係数とtie-breakはsource上の名前付き定数にし、
fixture testと評価reportから変更を検出できるようにする。

### 強度評価

専用evaluatorは2人Crazy Eightsで次の隣接pairを比較する。

1. Standard対Easy
2. Hard対Standard

各pairはgame seed `44000`～`44199`の200件を使い、各seedでdifficultyの席を交換した2局を行う。
pair当たり400局、合計800局である。勝ちは1点、drawは双方0.5点とし、失敗またはturn limit超過は
合格にしない。各方策は席と独立した専用`DeterministicRandom`を持ち、席交換後も同じ方策へ
同じpolicy seed系列を割り当てる。これにより一方の乱数消費が相手の系列をずらさない。

強い側のscore率が各pairで`53%以上`なら強度差を合格とする。これは一般プレイヤー母集団への
統計的勝率主張ではなく、固定corpusに対する決定的な回帰基準である。reportにはpair、seed範囲、
席別勝敗、draw、平均turn、失敗、score率、経過時間を含め、同じ入力の2回実行で完全一致させる。

### 性能予算

方策は手札枚数`H`と合法Action数`A`に対して`O(H + A)`、追加領域`O(A)`以内とする。
referenceの.NET 8 Release実行ではwarm-up後の固定observation corpusで、Hardの1手をp95 `5 ms`以下、
最大`25 ms`以下とする。800局の評価suiteは`15秒`を製品目標、Debug/共有CIの自動回帰hard limitを
`30秒`とする。reportは実測値とbudget判定を持ち、超過時はtestを失敗させる。

Unityの`0.35秒`思考待機は演出であり計算予算に含めない。CPU coroutineは画面遷移、session終了、
再開時に従来どおりcancelし、Hard計算をbackground threadや外部serviceへ移さない。

### 検証境界

- descriptor、game別対応、未知ID拒否、CLI既定値1を契約testで固定する。
- 全難易度について全固定seedで合法性、同一seed決定性、観測同値を検証する。
- difficulty 1のM04前fixture Actionを保存し、完全一致を確認する。
- evaluatorを`BroadSimulation`として.NETとUnity Standardへ含める。
- Unity Play Modeで選択IDがnew sessionとarchiveへ入り、再開後も同じIDでCPU Actionを再生する。

## Consequences

- 数値順と表示順が異なるが、既存difficulty 1と保存済みsessionを壊さず3段階を追加できる。
- game別capabilityにより、Crazy Eightsだけの実装を全game対応のように誤表示しない。
- observation DTOと観測同値testにより、Hardが強さのために秘密情報へ触れることを防げる。
- 固定800局は強度の回帰を再現できる一方、あらゆる相手に対する勝率保証ではない。
- rolloutを採用しないため最高強度には限界があるが、Unity main threadの予算とseed再現性を守れる。
- 係数変更は強度、性能、session replayを同時に再検証する必要がある。
