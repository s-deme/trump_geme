# Triple Crown監査記録

状態は`Verified`。資料は[ゴクラキズムの完全規則](https://gokurakism.com/game_141018_1/)
（参照日: 2026-08-15）。4人・15点先取規則を採用する。

| 項目 | 完全規則 | `TripleCrownGame` |
|---|---|---|
| trick | 52枚、A high、must-follow。Double Stakes以外no-trump | 一致 |
| High / Low | AS保持者は5勝以上で2点、2D保持者は0勝で3点 | 各保持者だけに`your_role`を表示して同じ精算 |
| Team | 他2人はHigh不足`max(0,5-H)`とLow勝数をそれぞれ加点 | 一致 |
| Double Stakes | 両札保持者がHigh/Lowを秘密記録しtrump指定。どちらか達成で5、失敗は宣言側不足×2を他3人へ | 選択を`choose_double`、公開trumpと秘密宣言へ分離して同じ精算 |
| 終了 | 合計15点を得たplayerが勝利 | 既定`target_score=15`へ修正 |

従来の明示`deals`はCLI互換の短縮sessionとして残し、指定時だけ局数終了を優先する。
`NinthRuleAuditTests`はseed 905/950で役を手札から独立判定して全13trickと精算を再計算し、seed 951で
目標点session、seed 964で相手2手札の観測同値を確認する。未解決差分はない。
