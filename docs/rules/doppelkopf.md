# Doppelkopf監査記録

状態は`RuleSpecific`。資料は[Pagatの完全規則](https://www.pagat.com/schafk/doko.html)と
[ゴクラキズム解説（全4回の入口）](https://gokurakism.com/doppelkopf_01/)（参照日: 2026-08-15）。
48枚の通常ゲーム、Marriage、Poverty、suit/queen/jack Soloまでを照合対象とした。

Runtimeはheart 10・Q・J・diamondのtrump順、club QのRe陣営、Marriage探索、Poverty 3枚交換、
card pointとSchneider段階を実装している。一方、公開完全規則にあるRe/Kontra等の宣言、Fox、
Charlie、Doppelkopfなどの特殊bonusと宣言倍率がAction/得点にない。これらは採用variantの中核であり、
単なる表示正規化ではないため未解決とする。`TenthRuleAuditTests` seed 1001の完走確認に留め、
`Verified`へ昇格しない。
