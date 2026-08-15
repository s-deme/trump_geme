# Three Tricks監査記録

状態は`Verified`。資料は[ゴクラキズムの完全規則](https://gokurakism.com/threetricks/)
（参照日: 2026-08-15）。掲載された4人・4ラウンド規則を全体として採用する。

| 項目 | 完全規則 | `ThreeTricksGame` |
|---|---|---|
| deck・trick | Jokerなし52枚、各13枚、A high、must-follow、no-trump | `MultiRoundTrickGame`のfollow制約とlead suit最高札判定を使用 |
| 1ラウンド得点 | 0=-5、1=1、2=4、3=9、4以上=-獲得数 | `t==0 ? -5 : t<=3 ? t*t : -t` |
| session | dealerを交代して4ラウンド、合計最高点 | `RoundNo>=4`で終了し、合計最高点者（同点併記） |

`EighthRuleAuditTests`はseed 804/880で完走し、全52trick・208手を独立に勝者集計して4ラウンドの
得点式と最終scoreを照合する。seed 1004ではviewer以外の2手札交換後もView・合法手・CPUが同値である。
未解決差分はない。
