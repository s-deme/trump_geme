# クリベッジ検証仕様

## 出典と採用variant

世界共通の単一公式規則は確認できないため、標準2人用six-card Cribbageを詳細に掲載する
[Pagat: Six-card Cribbage](https://www.pagat.com/adders/crib6.html)を信頼できる採用資料とした。
参照日は2026年8月15日、採用variantは同ページの2人・52枚・6枚配り・121点版であり、
Muggins、skunk、91点などの任意variationは採用しない。

## 項目別照合

| 項目 | 資料 | 採用仕様とRuntime照合 |
|---|---|---|
| 人数 | 基本2人 | Registryと`CribbageGame`は2人専用。 |
| 使用カード | 標準52枚、A低・K高 | `Cards.StandardDeck()`を使用する。 |
| 配札 | dealerが各6枚。両者が2枚ずつcribへ伏せて捨てる | `discard_two`を両者に1回ずつ求め、dealerの`crib`に4枚を置く。 |
| 開始状態 | 初dealerはcutで決め、dealは交代。non-dealerが先にdiscard／peg | 初dealerのみP0へ正規化し、以後`dealer`を交代する。starter cutは注入rngで決定する。 |
| 全フェーズ | discard、starter公開（Jならheels）、31までのpegging、non-dealer手札、dealer手札、cribのshow、次deal | `discard`、`pegging`、原子的な`show`採点、次`StartDeal()`で遷移する。 |
| 合法手 | discardは手札から任意2枚。peggingは合計31以下の任意1枚、なければgo | `LegalActions()`が全2枚組／全31以下札、又は`go`を列挙する。 |
| 特殊札・例外 | 15・31、連続ペア、連続run、go/last、starter Jの2点、nobs、cribのflushは5枚同suitのみ | `PeggingScore()`、`HandScore()`、`BeginPegging()`で実装する。 |
| 勝敗 | 121点以上に最初に達した側 | 到達した採点直後に勝者を確定する。 |
| 得点 | peggingとshowの15、pair、run、flush、nobs等を即時加算 | `scores`へ同じ値を累積する。 |
| 終了条件 | 121点以上 | `targetScore`既定121で終了する。 |
| ローカルルール | Muggins、skunk等は任意 | これらは不採用。`target_score`だけはテスト／短縮対戦用のinstance optionで、既定ルールは121。 |

## CLI/Unityへの正規化

cut、peg board、口頭の得点宣言は決定論的rngと数値scoreへ正規化した。showは全札が尽きた後に選択を
含まないため1遷移で採点するが、discard・peg・goの選択はすべて残る。初dealerの物理cutはP番号の固定へ
正規化し、Mugginsなど任意variationは既定仕様に混ぜない。

## 実装・テスト・差分

`CribbageGame`は採用variantとの差分なし。`SecondRuleAuditTests.CribbageFixedSeedCoversDiscardPeggingShowAndCribFlushException`
はseed 213でdiscardから終了までを通し、通常手札の4枚flushとcribで無得点となる例外を検証する。既存
`CoreContractTests.CribbageScoresTheTwentyNineHandAndRunsPeggingPhases`は29点手とpegging runを補完する。同
`CribbageViewAndCpuIgnoreTheOtherPlayersHiddenHand`が相手手札の観測同値を確認する。

未解決差分はない。
