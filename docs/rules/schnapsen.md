# シュナプセン検証仕様

## 出典と採用variant

[DRS tournament Schnapsen rules](https://schnapsen.realtype.at/index.php?page=rules-english)
（参照日: 2026-08-15）を採用する。2人・20枚・66 card points・7 game pointsの標準Schnapsenであり、
24枚のSixty-Six variantとDix de Dernierは採用しない。DRSのサイト本文は監査時に502だったため、
参照URLと取得日、検索取得済みのルール要約を記録し、再取得可能時に本文照合を再実施する。

## 項目別照合

| 項目 | 資料 | Runtime照合 | 判断 |
|---|---|---|---|
| 札・配札 | A/10/K/Q/J、各5、表trump | `StartHand()` | 一致 |
| talon open | follow不要、勝者から補充 | `TalonOpen`/`DrawAfterTrick()` | 一致 |
| close後 | follow・可能なら上位・void時trump | `StrictPlay` | 一致 |
| marriage/J交換/close | KQ 20/40、勝trick後J交換、lead時close | 専用Action | 一致 |
| 66と最終trick | 得点直後にcheck out、未check-outの最終trick勝者は1 game point | `claim_66`/`last_check`/`settle_last_trick` | 一致（修正） |
| match | 7 game pointsをゼロへ減算 | 先に7点を加算する等価正規化 | 一致 |

## 正規化・観測境界・修正・試験

最終trick後のcheck-out機会を`last_check` phaseへ明示し、`claim_66`または`settle_last_trick`を列挙する。
後者は常に1 game pointであり、10 card-point bonusを加えない。相手手札とstock順はView・CPU入力から除外する。
`FourthRuleAuditTests`はseed 403の完走・合法CPU、公開trump/close入口、相手手札とstockの観測同値、
最後の2札で76点・未check-outは7点目へ1点だけ加算される境界を確認する。未解決差分はなく`Verified`へ昇格する。
