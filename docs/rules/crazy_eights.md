# Crazy Eights検証仕様

状態は`Verified`。参照日は2026-08-15。採用variantは[Pagat: Crazy Eights](https://www.pagat.com/eights/crazy8s.html)の基本版である。

- 52枚を2人なら各7枚、3～5人なら各5枚配る。starterが8ならdealerが指定suitを選ぶ。
- topと同suit／同rankまたは8をplayし、8では次suitを明示する。drawは1枚で手番終了とする。
- stock切れではtopを残してdiscardを注入rngで再構成し、最後の1枚を合法にplayした時点で上がる。
- 残札は8=50、絵札と10=10、A=1、他は額面で得点化する。

`TwentiethRuleAuditTests`は配札、任意draw、starter 8のsuit選択と固定seed完走を確認する。`wild_rank`は生成時だけのローカルoptionで、採用範囲に未解決差分はない。
