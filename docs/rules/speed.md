# Speed／Spit検証仕様

状態は`Verified`。参照日は2026-08-15。[Bicycle: Spit](https://bicyclecards.com/how-to-play/spit)と[Pagat: Spit / Speed](https://www.pagat.com/patience/spit.html)を採用する。

- 各playerが独立した52枚deck、4枚layout、reserveを持ち、中央2 pileの上下1 rankへ同時に出す。
- 同時入力は両playerの選択を同じ公開状態で受けるrace windowへ正規化し、同じpileの競合優先はseed初期値から毎window交替する。
- 両者とも出せないとき、各中央starterはその所有者のreserveだけから補充する。片側枯渇時に相手reserveを流用しない。

`TwentyFirstRuleAuditTests`は片側枯渇境界を、固定seed監査は競合・決定性・完走を確認する。未解決差分はない。
