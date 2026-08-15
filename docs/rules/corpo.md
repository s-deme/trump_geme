# Corpo検証仕様（Verified）

## 資料・採用範囲

[ゴクラキズム: コルポ（Colpo）](https://gokurakism.com/colpo/)
（3人用の公開完全規則、参照日: 2026-08-15）を候補元索引の個別リンクから直接取得し、
掲載variantを採用する。

| 項目 | 完全規則 | Runtime・判断 |
|---|---|---|
| カード・deal | 2・3を除く44枚、各14枚、余り2枚は伏せて不使用 | A・4～Kの44枚から各14枚。不使用2枚は状態から除外。一致 |
| 分割 | 各自がPoker用5枚を伏せ、残り9枚をtrickへ | `reserve_for_poker`を各自5回行う。一致 |
| bid | dealer左からcolpo（7勝以上）またはpass。最初のcolpoで終了 | `bid` phaseの逐次`colpo`/`pass`とbidder leadが一致 |
| trick | bidderなしならdealer左lead。spade固定trump、must-follow、9trick | `LegalActions()`、`TrickWinner()`、winner leadが一致 |
| colpo精算 | 成功はbidderの勝数、失敗はbidder -7・相手は各勝数 | `FinishDeal()`が一致 |
| 無宣言7勝 | 7勝者だけ5点 | 同じ。一致 |
| Poker条件・役 | 無宣言かつ7勝者なし。straight/flushなし。4kind、full house、3kind、2pair、pair、high | `PokerValue()`が同じcategoryと通常kicker順を比較。一致 |
| Poker tie・得点 | 最強者（2人tieは両者、3人tieは無効）が自分の勝数、0勝なら3点 | `ScorePoker()`が同じwinner集合と得点を適用。一致 |
| showdown公開 | Poker時は3人が伏せた5枚を公開 | 精算時に全15枚を`revealed_poker`へ保存し、全viewerへ同一表示。一致 |
| 終了 | dealer交代、15点以上へ最初に到達した最多点者 | 既定`target_score=15`、instance-local短縮可。一致 |
| 観測・乱数 | showdown前のPoker札、相手手札、余り札は非公開 | View/CPUから隔離し、注入乱数だけを使用。一致 |

手札分割とbidをdealer左からの逐次Actionへ正規化するが、全カード選択とcolpo/passを保持する。
2026-08-15補正前はPoker精算後すぐ次dealへ移って全員公開を観測できなかったため、最新showdownを
`revealed_poker`へ保持した。`SeventhRuleAuditTests`はseed 800～819でPoker到達と3人各5枚の
同一公開を確認し、seed 822で二相手手札の観測同値を確認する。未解決差分はなく`Verified`とする。
