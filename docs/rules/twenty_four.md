# 24検証仕様

状態は`Verified`。参照日は2026-08-15。採用variantは[Pagat: Twenty-Four](https://www.pagat.com/adders/24.html)の2人private-stack版と3人以上の得点版である。

- A～10の40枚から4数を公開し、各数を1回ずつ、加減乗除と括弧だけで24を作れるか完全探索する。
- 2人は各20枚のprivate stackから2枚ずつ出し、正答時は相手へ4枚を渡し、自分のstackを空にした側が勝つ。
- 3人以上は正答1点、誤`no_solution`後に他playerが解けば2点、正しい全員`no_solution`は宣言者1点とする。

`TwentiethRuleAuditTests`はprivate stack移動と誤宣言後2点を、既存監査はsolverと秘密境界を確認する。slapは逐次claimへ正規化し、未解決差分はない。
