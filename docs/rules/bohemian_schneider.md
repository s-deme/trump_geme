# ボヘミアン・シュナイダー検証仕様

## 出典と採用variant

公式の統一ルールブックは確認できない。現行の2人・French 32枚版を詳述する
[CardRules+ Bohemian Schneider](https://cardrulesplus.com/games/bohemian-schneider/)を主資料、
歴史・32枚構成・honor採点を相互確認する
[Bohemian Schneider 概説](https://en.wikipedia.org/wiki/Bohemian_Schneider)を補助資料とした。
参照日は2026年8月15日。採用variantは2人・32枚・6枚手札・trumpなし・同suitの直上札だけで奪える版で、
10-10 honorは得点なしの再deal、目標7 game pointである。

## 項目別照合

| 項目 | 資料 | 採用仕様とRuntime照合 |
|---|---|---|
| 人数 | 2人 | Registryと`BohemianSchneiderGame`は2人専用。 |
| 使用カード | 7～Aの32枚。A・K・Q・J・10の20枚がhonor | 同rank集合、`IsHonor()`で同じ20枚を識別する。 |
| 配札 | 3枚ずつ2 packetで各6枚、残20枚はtalon | `StartDeal()`が同じpacket dealとstockを作る。 |
| 開始状態 | non-dealerがlead、dealは交代 | 初deal P1 lead、以後dealer交代として正規化する。 |
| 全フェーズ | 1枚lead、response、trick、winner先行の2枚補充、honor採点、次deal | `play`、trick解決、winner→loser補充、`FinishDeal()`で遷移する。 |
| 合法手 | lead／responseとも任意の手札札。follow義務なし | `LegalActions()`は全手札を列挙する。 |
| 特殊札・例外 | responseは同suitでちょうど1 rank上の時だけ勝つ。上位すぎても勝たない | `BeatsByOne()`がrank差1だけを許す。 |
| 勝敗 | 11～15 honorでsingle、16～19でSchneider、20でSchwarz。10-10はredeal | tier 1/2/3をwinnerへ加点し、10-10は加点せず`StartDeal()`する。 |
| 得点 | single=1、Schneider=2、Schwarz=3 game point | `gamePoints`へtierを加算する。 |
| 終了条件 | 合意目標へ到達 | 採用目標は既定7、`target_score`でinstanceごとに変更できる。 |
| ローカルルール | 24枚版、must-follow／trump版、目標点はvariation | 32枚・no-follow・no-trumpを固定し、目標点だけoption化する。 |

## CLI/Unityへの正規化

最初のdealer決めをP0/P1へ固定し、交代はRuntimeで管理する。物理的なtrick山はhonor数以外へ影響しないため
状態外に集約するが、任意response、同suit直上の例外、補充順はすべて保持する。目標点はローカル合意なので
`target_score`をinstance optionに閉じ込める。

## 実装・テスト・差分

監査で10-10 honor再dealに前deal倍率を持ち越す差分を見つけ、倍率なしの再dealへ修正した。
`ThirdRuleAuditTests.BohemianSchneiderFixedSeedScoresHonorsAndRedealsATieWithoutCarry`はseed 303/304で
終了採点、10-10再deal、直上rank例外を確認する。同
`BohemianSchneiderViewAndCpuIgnoreTheOtherPlayersHand`は相手手札の観測同値試験である。

未解決差分はない。
