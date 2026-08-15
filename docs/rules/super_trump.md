# スーパートランプ検証仕様

## 出典と採用variant

配布元の公式ルールブックは確認できなかった。原紹介先を明示し、カード・2段階補充・得点を記す
ゴクラクテンの[スーパートランプ ルール](https://gokurakism.com/about_supertramp/)を信頼できる採用資料とした。
参照日は2026年8月15日、採用variantは同資料の2人・52枚・第1/第2ステージ各13 trick版である。

## 項目別照合

| 項目 | 資料 | 採用仕様とRuntime照合 |
|---|---|---|
| 人数 | 2人 | Registryと`SuperTrumpGame`は2人専用。 |
| 使用カード | Jokerを除く52枚、A強・2弱 | `Cards.StandardDeck()`を使用し、`Strength()`でAを最大にする。 |
| 配札 | dealer決定後、各13枚、残26枚をstock | constructorが各13枚を配る。 |
| 開始状態 | non-dealerがsuit trump、dealerが2～Aのsuper rankを選び、表札を1枚出す。non-dealerがlead | P0=dealer、P1=non-dealerとして`choose_trump`、`choose_super`、`faceUp`、P1 leadを実装する。 |
| 全フェーズ | 宣言、第1ステージ13 trickと補充、第2ステージ13 trick、得点決定 | `choose_trump`、`choose_super`、`play`、`stage`で遷移する。 |
| 合法手 | 通常のsuit must-follow | `EffectiveSuit()`でsuper rankをtrump suitとして扱い、follow可能ならそれだけを列挙する。 |
| 特殊札・例外 | super rankは全trumpより強く、全suitで同強さ、2枚なら先出し勝ち。super rankは通常suitでなくtrumpとしてfollow | `TrickWinner()`と`EffectiveSuit()`がこの例外を実装する。 |
| 勝敗 | 39点中20点以上の側 | 全点39で同点なし、最大scoreをwinnerとする。 |
| 得点 | 第1ステージは1 trick=1、第2ステージは1 trick=2 | `ResolveTrick()`がstage別に加算する。 |
| 終了条件 | 第2ステージの手札13枚を尽くす | 26 trick後に終了する。 |
| ローカルルール | 記載なし | dealer決めをP0/P1へ固定し、追加optionは持たない。 |

## CLI/Unityへの正規化

外部のdealer決めはP0=dealerへ固定しただけであり、P番号を交換すれば同じ選択空間である。rankのAは
CLI内部値`1`、表示は`A`として正規化する。表札、宣言、trickは公開し、各自の手札と伏せ補充札はViewに
出さないため、物理カードの公開／非公開を失わない。

## 実装・テスト・差分

`SuperTrumpGame`は資料との差分なし。`SecondRuleAuditTests.SuperTrumpFixedSeedUsesBothStagesAndTreatsSuperRankAsTrumpForFollowing`
はseed 214/215でsuper rankのmust-follow例外、第1・第2ステージ、54遷移、39点を確認する。同
`SuperTrumpViewAndCpuIgnoreTheDealersHiddenHand`はdealer手札を変えてもnon-dealerのView・合法手・CPU選択が
同じことを確認する。

未解決差分はない。
