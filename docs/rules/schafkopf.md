# Schafkopf検証仕様

状態は`Verified`。参照日は2026-08-15。[ゴクラキズム: Bavarian Schafkopf](https://gokurakism.com/schafkopf/)の32枚版を採用する。

- Partner、Wenz、Suit Solo、各Tout、Sieを、より高いcontractへ上げられる勝ち抜きauctionで選ぶ。全passは再配布する。
- Partnerでは非trump Aをcallし、called Aceのlead／follow制約を守る。Q/Jとcontract別trump順を用いる。
- Stoss、Gegenstoss、Supra、Resupraを最大16倍までAction化し、61点、Schneider、Schwarz、soloゼロ和で精算する。

`TwentyFirstRuleAuditTests`はauction raiseとStoss倍率を、固定seed監査は全契約とsessionを確認する。未解決差分はない。
