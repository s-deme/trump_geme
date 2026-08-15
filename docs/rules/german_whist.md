# German Whist 検証仕様

## 出典と採用variant

統一競技団体の公式規則は確認できないため、John McLeod編の
[Pagat: German Whist](https://www.pagat.com/whist/german_whist.html)を主資料とした。
参照日は2026年8月15日。採用variantは同ページの標準2人・52枚・第2フェーズ13 trickだけを
勝敗に使う版である。同ページ記載の「第1フェーズはfollow不要」「全26 trickを数える」variantは
採用しない。

## 項目別照合

| 項目 | 資料 | 採用仕様とRuntime照合 |
|---|---|---|
| 人数 | 2人 | Registryは2人専用。 |
| 使用カード | Jokerなし52枚、A high | `Cards.StandardDeck()`とA=14を使う。 |
| 配札 | 各13枚、残stock最上札を表にしてtrump決定 | constructorが13枚ずつ配り、`faceUp`のsuitを固定trumpにする。 |
| 開始状態 | non-dealerが第1 trickをlead | CLIのP0をnon-dealerに固定して開始する。 |
| 全フェーズ | stockのある13 trickの獲得フェーズ、stockなし13 trickの勝敗フェーズ | `faceUp`の有無でphase 1/2を切替える。 |
| 合法手 | leadは任意、応手はmust-follow、voidなら任意 | `LegalActions()`がそのまま列挙する。 |
| 特殊札・例外 | phase 1の勝者は表札、敗者は伏札を取り、最初のtrumpは変わらない | `Apply()`が同順で補充し、trump fieldを不変にする。 |
| 勝敗 | phase 2の13 trick多数 | `secondPhaseTricks`でwinner/drawを決める。 |
| 得点 | 資料はhand勝敗のみ | `IGame` scoreはphase 2 trick数。 |
| 終了条件 | 26 trick後に両手札0 | 同じ。 |
| ローカルルール | deal担当はhandごとに交替 | 1 `IGame`を1 handへ正規化し、P0開始を固定する。次handのdealer選択は外側sessionで行うため、hand内の選択肢は失われない。 |

## CLI/Unityへの正規化

物理的な伏せstockと手札は内部状態にし、viewerには自手札、公開`faceUp`、公開trickだけを出す。
UIで複数handを連続させる場合は新しい同条件instanceを作る。これは資料のdealer交替を削除するので
はなく、1 handを`IGame`の終了単位にする正規化である。

## 実装・テスト・差分

資料との差分はない。`InitialRuleAuditTests.GermanWhistFixedSeedCompletesBothStagesAndKeepsOpponentHandPrivate`
はseed 97で52 play・第2フェーズ得点・相手手札だけを変えた観測同値View/CPUを検証する。

未解決差分はない。
