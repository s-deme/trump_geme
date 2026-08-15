# 44A（ササキ）検証仕様

状態は`Verified`。参照日は2026-08-15。[GIVE-ME-THE-TRICKの詳細規則](https://give-me-the-trick.blogspot.com/2019/02/44a.html)と[ゴクラキズム](https://gokurakism.com/44a/)を採用資料とする。

- 48枚、赤10による2対2／1対3、基本組、Triple、44A、Four、赤豚、黒豚、kick/stab、順位別得点とrun/stop倍率を扱う。
- run時はD10とH10の所有者が2枚を交換し、playは時計回りとする。
- 資料が固定しない終了境界は、採用variantとして10点先取の累積sessionに定め、`target_score`で短縮可能とする。

`TwentiethRuleAuditTests`は赤10交換、公開team、run/stop段階と固定seed決定性を確認する。採用variant内に未解決差分はない。
