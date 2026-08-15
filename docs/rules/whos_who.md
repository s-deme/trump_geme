# WHO’S WHO 検証仕様

## 出典と採用variant

作者の[David Parlett: Who's Who](https://www.parlettgames.uk/oricards/whoswho.html)（参照日: 2026-08-15）に従い、3人・5〜Aの40枚＋Joker2枚・100点先取を採用する。Joker分配でsoloistと秘密partnerを決める。

## 項目別照合

| 項目 | 資料 | Runtime照合 | 判断 |
|---|---|---|---|
| 札・配札 | 42枚を14枚ずつ | `StartDeal()` | 一致 |
| 隠れ役 | Joker2枚の保持者、またはJokerなしがsoloist | `initialJokers`/`soloist` | 一致 |
| 通常trick | follow、lead suitのsecond-highが勝つ | `SecondHighWinner()` | 一致 |
| Joker | naturalがあればlead不可、出たJokerの保持者/soloistがwinner指定 | `LegalActions()`/`assign_trick` | 一致 |
| 得点 | 10+soloistのtrickを成功ならsoloist、失敗なら各partnerへ | `FinishDeal()` | 一致 |

## 観測境界・試験・結論

Joker分配とpartnerは本人に必要な範囲だけViewへ出し、相手手札・相手役職は非公開にする。`FifthRuleAuditTests`はseed 502完走、14枚配札とJoker lead境界、二つの相手手札を交換した観測同値性を確認する。実装修正・未解決差分はなく`Verified`へ昇格する。
