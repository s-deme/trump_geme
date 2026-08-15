# Guillotine監査記録

状態は`Verified`。資料は[ゴクラキズムの完全規則](https://gokurakism.com/about_guillotine/)
（参照日: 2026-08-15）。4人・24局compendiumを全体として採用する。

| 項目 | 完全規則 | `GuillotineGame` |
|---|---|---|
| deck/session | 7～Aの32枚、A>10>K>Q>J>9>8>7。各dealerが6契約を1回、計24局 | 一致 |
| Royalty/Queens/Spades/Parliament/Guillotine | no-trump must-follow。KH、QS、Q、spade、trick、最初/最後を表どおり加減 | `ContractPenalty()`の5分岐と一致 |
| Domino | dealerが任意札。異suit同rankまたは同suit隣接。2枚目以降のAで、その時点から出せる札をすべて連続play | A連続中は合法配置が尽きるまで`finish_ace_run`を返さないよう修正 |
| Domino得点/勝敗 | 先着-30、2着-10、24局合計最少 | 一致 |

`TenthRuleAuditTests`はseed 1002、1010～1020で24局完走と非opening Aの強制連続境界を確認し、
seed 1040で相手2手札の観測同値を確認する。未解決差分はない。
