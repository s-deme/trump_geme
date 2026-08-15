# ゴニンカン検証仕様

状態は`Verified`。参照日は2026-08-15。五所川原商工会議所の[基本ルール](https://www.gocci.or.jp/goninkan/rules/rules.html)、[注意事項](https://www.gocci.or.jp/goninkan/rules/rules2.html)、[点数表](https://www.gocci.or.jp/goninkan/rules/haitenhyo.html)を採用する。

- 49枚＋Joker、関係2対無関係3、反時計回りを、関係者が対面関係になる`playOrder`へ正規化する。
- 二重関は2席先の関係者が伏せ札を交換する。第2・第3戦は関係者の2枚提示と決め役のtrump選択をAction化する。
- 9/8/9枚境界、スコンク、じゅうろく、逆じゅうろく、外しと公式配点をround差分へ反映する。

`TwentyFirstRuleAuditTests`は二重関交換後も全員10枚であることを、固定seed監査は10 roundと特殊宣言を確認する。競技時間は再現可能な固定sessionへ正規化し、未解決差分はない。
