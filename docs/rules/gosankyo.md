# 御三卿 検証仕様

## 出典と採用variant

統一的な公式ルールブックは確認できない。ゲームを掲載・説明するゴクラクテンの
[御三卿ルール](https://gokurakism.com/gosankyo/)を信頼できる採用資料とし、同サイトの
[トランプゲーム一覧](https://gokurakism.com/trump_matome/)で作品と必要なsuit識別カードを相互確認した。
参照日は2026年8月15日。採用variantは、裏面でsuitだけが分かるカードを使う1人用・3仮想席版である。

## 項目別照合

| 項目 | 資料 | 採用仕様とRuntime照合 |
|---|---|---|
| 人数 | 人間1人＋仮想相手2人 | Registryは1人、内部は3 seatを保持する。 |
| 使用カード | 2～5を除く36枚、bid 4/5/6/7、lead suitカード | deckは36枚。bidとlead suitカードはenum/actionへ置換する。 |
| 配札 | 左・右・自分の順で各12枚 | `StartRound()`が同順に12枚ずつ配る。 |
| 開始状態 | 右の最上札を伏せlead、相手札はsuit別に分ける | `reveal_lead`前はrankを伏せ、`View`は相手suit枚数だけを出す。 |
| 全フェーズ | 未使用4～7を選ぶ予想、12 trick、連続4 round | `bid`、`reveal_lead`/`reveal_follow`、`play`、`EndRound`で表現する。 |
| 合法手 | no-trump must-follow、voidなら任意。自分が勝てば任意lead | `LegalActions()`が自己手札のfollowを強制する。 |
| 特殊札・例外 | 相手勝利時はlead cardでsuitを抽選し、そのsuitから伏せ札を1枚出す | 注入rngでsuitと同suit札を選ぶ。資料上rankは選べないため、観測可能な選択を失わない。 |
| 勝敗 | bidちょうどなら次round、4～7を連続4回成功で勝ち | `four exact bids` / `exact bid failed`で対応する。 |
| 得点 | 資料は勝敗のみ | `IGame`用に連続成功round数0～4をscoreとするローカル正規化。 |
| 終了条件 | 失敗又は4回成功 | 同じ。 |
| ローカルルール | suit識別カードが必要 | suit情報を`View`へ公開し、カード背面・物理抽選をstateとrngへ置換する。 |

## CLI/Unityへの正規化

背面のsuit情報は公開状態、rankは非公開状態として分離する。相手の伏せ札を手で選ぶ操作は、
同一suit内ではrankを識別できないためdeterministic rngで選ぶ。人間が選ぶbid・自手札playは
すべて`LegalActions()`に残る。

## 実装・テスト・差分

資料との差分はない。`InitialRuleAuditTests.GosankyoKeepsHiddenOpponentRanksObservationallyEquivalent`
はseed 96で相手の非公開rankだけを差し替えてView/CPU/action集合が不変なことと終了を検証し、
既存`CoreContractTests.GosankyoOffersEachExactBidOnlyOnce`は未使用bid制約を検証する。

未解決差分はない。
