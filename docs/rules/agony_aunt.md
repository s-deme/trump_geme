# Agony Aunt監査記録

状態は`Verified`。一次資料は作者David Parlettの
[Agony Aunt完全規則](https://www.parlettgames.uk/oricards/agonaunt.html)、補助資料は
[ゴクラキズム](https://gokurakism.com/agonyaunt/)（参照日: 2026-08-15）。作者の4人・17 counter規則を採用する。

| 項目 | 作者規則 | `AgonyAuntGame` |
|---|---|---|
| deck・dump | Joker込み53枚。非Jokerの表札をdumpとし、Jokerは同じrank/suit | 通常札からdumpを除きJokerを加えて各13枚。`Effective()`で同一視 |
| trick | no-trump、must-follow、A high、13trick | 一致 |
| 9罰点 | Joker、4 Queens、dump-suit Q、最終、最多、dump番trick | 3×3盤の該当cellへcounterを置く。最多同点はdump suit枚数、最高dump札で解決 |
| 盤・回復 | 自色3目1列ごと追加1。全勝/全敗は17へ、勝利あり無罰は既失点の半分回復 | 8本のlineと同じ優先順で精算 |
| 終了 | 誰かがcounter 0になった局で終了、残数最多 | 一致（同点併記） |

`NinthRuleAuditTests`はseed 901/910で53枚の扱いから第1局の全罰点、最多tie-break、3目、回復を
独立集計してchip残数を照合する。seed 960では相手2手札を交換してもView・合法手・CPUが同値である。
未解決差分はない。
