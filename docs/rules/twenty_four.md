# 24監査記録

## 判定

`RuleSpecific`を維持する。参照日は2026-08-15。

## 直接確認した完全規則

- [Pagat: Twenty-Four](https://www.pagat.com/adders/24.html)

## 採用範囲と一致点

現RuntimeはAceを1、2～10を額面とする40枚の中央stockから4枚を公開し、4数を各1回、加減乗除と
括弧だけで厳密に24へできるか完全探索する。2～8人を逐次手番へ正規化し、正しい`claim_24`、
全員の正しい`no_solution`に1点、誤った`claim_24`に-1点を与え、5点先取またはstock切れで終える。

## 不一致項目と保留理由

Pagatの2人基本形は各自20枚の私有stackから2枚ずつ出し、正解時は相手が4枚を引き取って
手札をなくした側が勝つため、中央stock得点戦とは異なる。3人以上向けのPagat得点variantでも、
誤った`no solution`宣言後に解答したplayerは2点だが、現Runtimeは1点である。4人版のslap/bluff
選択も実装しない。人数・勝敗・得点に差が残るため昇格しない。

## 検証

`EighteenthRuleAuditTests` seed 1802と`CoreContractTests`で固定seed完走、3・3・8・8の解、
不可能例、公開4数だけを用いる合法CPUを確認した。2人stack戦、誤宣言後2点、4人bluffが未解決である。
