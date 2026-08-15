# 2人用大富豪検証仕様

## 出典と採用variant

統一競技団体の公式規則は確認できない。考案者を明記し、使用札・革命・山札補充・得点を連続して記す
ゴクラクテンの[大富豪 2人用ルール](https://gokurakism.com/daifugo2/)を信頼できる採用資料とした。
参照日は2026年8月15日、採用variantは同資料の38枚・16枚配り・30点目標版である。

## 項目別照合

| 項目 | 資料 | 採用仕様とRuntime照合 |
|---|---|---|
| 人数 | 2人 | Registryと`DaifugoTwoGame`は2人専用。 |
| 使用カード | 3～6を除く36枚と識別可能なJoker 2枚、計38枚 | 7～A・2の各4suitと`J0`／`J1`を生成する。 |
| 配札 | 4枚ずつ4回、各16枚。残6枚はstock | `StartDeal()`が同じblock dealとstockを作る。 |
| 開始状態 | 任意に先手を決める | 初dealはP0、次dealは交代へ決定論的に正規化する。 |
| 全フェーズ | lead／応答、pass時の場捨て・stock補充、上がり採点、次deal | `play`と`pass`、`StartDeal()`でこの遷移を表す。 |
| 合法手 | 単札、同rank2～4枚、3枚以上runを、同型・同枚数でより強く出す | `Combinations()`と`CanBeat()`が全合法組を列挙する。runはsuitを問わない。 |
| 特殊札・例外 | 色付きJoker＞色なしJoker＞2…7。Jokerは組／runの代用、2の上へはrun不可。4枚組（Joker可）で革命、Jokerは革命後も最強 | `J1`／`J0`、run生成範囲、代用、`revolution`と`ComboStrength()`に対応する。 |
| 勝敗 | 手札を先に尽くした側 | 空手札で直ちにそのdealの勝者とする。 |
| 得点 | 相手残り手札枚数を勝者へ加算 | `scores[player] += hands[other].Count`で加算する。 |
| 終了条件 | あらかじめ決めた30点、50点等へ到達 | 既定`target_score=30`に達したら終了する。 |
| ローカルルール | 目標点は事前合意 | `target_score`をinstance optionとし、既定30。 |

## CLI/Unityへの正規化

任意の先手決めは初dealをP0として固定し、以後は交代する決定論的手順にした。pass時に場札を物理的に
捨てる操作は後続の選択へ影響しないため状態外へ除去する一方、Jokerの代用を含む全組合せ、pass、目標点は
保持する。`target_score`はローカル合意を各game instanceへ閉じ込め、グローバル状態を持たない。

## 実装・テスト・差分

`DaifugoTwoGame`は採用variantとの差分なし。`SecondRuleAuditTests.DaifugoFixedSeedCompletesAScoredMatchAndFourOfAKindStartsRevolution`
はseed 216/217で得点終了と4枚組革命を検証する。既存
`CoreContractTests.TwoPlayerDaifugoDealsSixteenAndDrawsAfterPass`は配札とpass補充を補完する。同
`DaifugoViewAndCpuIgnoreTheOtherPlayersHiddenHand`が相手手札の観測同値を確認する。

未解決差分はない。
