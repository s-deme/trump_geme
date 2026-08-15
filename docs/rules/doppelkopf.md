# Doppelkopf検証仕様

状態は`Verified`。参照日は2026-08-15。[Pagat: Doppelkopf](https://www.pagat.com/schafk/doko.html)と[ゴクラキズム](https://gokurakism.com/doppelkopf_01/)の48枚版を採用する。

- 通常Re陣営、Marriage、Poverty 3枚交換、suit/queen/jack Solo、heart 10・Q・J・diamondのtrump順を扱う。
- Re/Kontra、no90/no60/no30/Schwarzを残手札期限つきActionへ正規化する。
- card points、宣言段階、Fox、Charlie、40点以上のDoppelkopfをdeal履歴から加点し、soloはゼロ和で精算する。

`TwentyFirstRuleAuditTests`は宣言Actionを、固定seed監査は契約、特殊bonus、session決定性を確認する。未解決差分はない。
