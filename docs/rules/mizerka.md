# ミゼルカ検証仕様

## 出典と採用variant

[Pagat: Mizerka](https://www.pagat.com/quotawhist/mizerka.html)（参照日: 2026-08-15）の3人・52枚版を採用する。4 trump/NT/Mizerkaを各人一回ずつ選ぶ18 deal、6枚時点でcontractを選び13枚とtalonへ配り切る方式である。

## 項目別照合

| 項目 | 資料 | Runtime照合 | 判断 |
|---|---|---|---|
| deal境界 | 6枚でcontract選択後、各13枚＋talon13枚 | `StartDeal()`/`choose_contract` | 一致 |
| exchange | chooser、右手、dealerが残talonまで任意交換 | `discard_for_exchange`/`finish_exchange` | 一致 |
| trick | chooser lead、followのみ | `LegalActions()`/`TrickWinner()` | 一致 |
| contract回数 | 6 contractを各人一回、18 deal | `usedContracts`/`dealsPlayed` | 一致 |
| quota得点 | normal 7/5/1、M 1/5/7 | `FinishDeal()` | 一致 |

## 観測境界・試験・結論

交換を1枚ずつのActionに分解し、手札・talon順をView/CPUから隔離する。`FifthRuleAuditTests`はseed 505完走、6→13枚とtalon 6→13の境界、M contract、二つの相手手札を交換した観測同値性を確認する。実装修正・未解決差分はなく`Verified`へ昇格する。
