# ブリスコラ検証仕様

## 出典と採用variant

統一競技規則の公式発行元は確認できないため、John McLeod編の
[Pagat: Briscola](https://www.pagat.com/aceten/briscola.html)を信頼できる採用資料とした。
参照日は2026年8月15日、採用variantは同ページの通常2人・イタリアン40枚・1 deal完結版である。
高札を引き直す、累積120点、Scoperta、Briscolone等のvariationは採用しない。

## 項目別照合

| 項目 | 資料 | 採用仕様とRuntime照合 |
|---|---|---|
| 人数 | 2人 | Registryと`BriscolaGame`は2人専用。 |
| 使用カード | 52枚から8・9・10・Jokerを除く40枚 | A・2～7・J・Q・Kの各suitを生成する。 |
| 配札 | 各3枚、次の1枚を表向きbriscola（trump）、残りをstock | constructorが各3枚を配り、stock先頭を公開trumpとして保持する。 |
| 開始状態 | non-dealerがlead | P0=non-dealerとしてP0開始へ正規化する。 |
| 全フェーズ | lead／response、trick判定、勝者先行の2枚補充、stock後の残手札play、採点 | `play`、trick解決、winner→loserの`Pop(stock)`、終了採点で遷移する。 |
| 合法手 | responseにもfollow／trump義務はなく任意の手札1枚 | `LegalActions()`は常に全手札を返す。 |
| 特殊札・例外 | 同suitはA・3・K・Q・J・7…の強さ、off-suit trumpが勝つ。stock最後の公開briscolaはそのtrickの敗者が取る | `Strength`、`Beats()`、stock末尾のwinner→loser補充順で対応する。 |
| 勝敗 | カード点の多い側、60-60はdraw | `Result()`が最大点者をwinnerにする。 |
| 得点 | A=11、3=10、K=4、Q=3、J=2、総120点 | `Points`とcaptured札から同じ値を集計する。 |
| 終了条件 | 20 trick、両手札が尽きた時 | 最終trick後の空手札で終了する。 |
| ローカルルール | 複数deal・特殊な高札引き直しはvariation | 採用しない。追加optionは持たない。 |

## CLI/Unityへの正規化

初dealer決めだけをP0/P1の対称なラベルへ正規化し、P0をnon-dealerに固定する。物理的な獲得札の裏向き山は
得点以外の後続選択に影響しないため`captured`へ集約する。一方で任意response、公開trump、補充順は保持し、
選択肢を失わない。

## 実装・テスト・差分

`BriscolaGame`は採用variantとの差分なし。`ThirdRuleAuditTests.BriscolaFixedSeedUsesTrumpDrawOrderAndScoresAllOneHundredTwentyPoints`
はseed 301で20 trick・120点・follow不要を確認する。同
`BriscolaViewAndCpuIgnoreTheOtherPlayersHand`は相手手札を変えてもP0のView、合法手、CPU選択が観測同値であることを確認する。

未解決差分はない。
