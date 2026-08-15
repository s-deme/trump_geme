# Gin Rummy 検証仕様

## 出典と採用variant

Gin Rummyには世界共通の公式規則がないため、標準版を体系化した
[Pagat: Gin Rummy](https://www.pagat.com/rummy/ginrummy.html)を信頼できる主資料とする。
参照日は2026年8月15日。採用variantは2人・52枚・10枚配札・knock 10以下、Gin bonus 20、
undercut bonus 10、100点到達時のgame bonusとline bonusを使う同ページの標準版である。
同ページのOklahoma Gin、25点bonus、3/4人variantは採用しない。deal担当は同ページが認める
交互dealer variantを採用する。

## 項目別照合

| 項目 | 資料 | 採用仕様とRuntime照合 |
|---|---|---|
| 人数 | 2人 | Registryは2人専用。 |
| 使用カード | Jokerなし52枚、A=1、絵札=10 | `CardPoints`と標準deckが対応する。 |
| 配札 | 各10枚、21枚目をupcard、残りstock | `StartRound()`が同じ枚数と順で配る。 |
| 開始状態 | non-dealerがupcardをtake/pass、拒否時dealerもtake/pass、両者passならnon-dealerがstock draw | `initial_offer`、`dealer_offer`、`initial_stock`で全遷移を明示する。 |
| 全フェーズ | draw、discard、knockの反復 | phase stateと`Apply()`だけが順序を変更する。 |
| 合法手 | stock/discard draw、drawしたupcardの即時捨て禁止、discard後deadwood 10以下でknock | `lastDrawn`と`BestMelds`で制限し、discard/knockを列挙する。 |
| 特殊札・例外 | set 3/4、同suit連番3以上、A low、Gin時layoff不可、通常knock時のみ相手layoff、stock残2で無得点redeal | meld solver、`MinimumAfterLayoff`、`stock.Count<=2`で対応する。 |
| 勝敗 | 累積100点到達 | `target_score`既定100で終了する。 |
| 得点 | 通常差、Gin=20+相手deadwood、undercut=10+差、game bonus 100/ shutout 200、各勝hand line 20 | `ScoreKnock`と`FinishMatch`が同じ式を適用する。 |
| 終了条件 | 100点到達又はstock枯渇handのredeal | 前者はterminal、後者は同じmatchを次dealへ進める。 |
| ローカルルール | `target_score`、`knock_limit`はCLI練習用option | 既定100/10は採用資料どおり。optionは生成時だけに閉じ、global状態を持たない。 |

## CLI/Unityへの正規化

ノック時の並べ方とlayoffは`BestMelds`の全組合せ探索で評価する。これは同じ合法結果を完全探索する
正規化であり、meldの人間による並べ順を失わせない。伏せstockと相手手札はviewer/CPUへ渡さず、
dealer交替は採用variantどおりinstance内のdeal境界で自動化する。

## 実装・テスト・差分

監査でGin/undercutを25点としていたこと、game bonusとline bonusを未加算だったことを発見し、
標準資料の20/10/100・200/20へ修正した。
`InitialRuleAuditTests.GinRummyUsesClassicGinAndMatchBonuses`はseed 1の固定手札でGin 97点から
shutout/game/line bonus込み317点を検証する。`GinRummyCpuAndViewIgnoreTheOtherPlayersPrivateHand`は
相手手札だけが異なる観測同値のView、合法手、CPU actionを検証し、既存
`CoreContractTests.GinRummyStartsWithTheUpcardOffer`と`GinRummyMeldSolverFindsSetAndRun`が開始・meldを検証する。

未解決差分はない。
