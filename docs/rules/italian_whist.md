# Italian Whist監査記録（Verified）

## 資料・採用範囲

[ゴクラキズム: イタリアン・ホイスト](https://gokurakism.com/italian_whist/)
（公開完全規則、参照日: 2026-08-15）を候補元索引から直接再取得した。3人・6ディールの
紹介variantを照合対象とする。

## 項目別照合

| 項目 | 完全規則 | Runtime・判断 |
|---|---|---|
| カード・配札 | 52枚＋赤黒Joker、各18枚 | 同じ54枚を各18枚。一致 |
| 手札分割 | 各自9枚を後半用に伏せ、1/4は左、2/5は右、3/6は自分へ渡す | `reserve_for_second_half`と`TransferSecondHands()`が一致 |
| trump・lead | 1～3はno-trump、4～6はspade。前半dealer左、後半dealer右lead | `HasTrump`とhalf切替が一致 |
| trick・得点 | 両halfともmust-follow。前半勝数－後半勝数 | `LegalActions()`と`FinishDeal()`が一致 |
| Joker follow | leadと同色Jokerもfollow義務を満たし、同色通常札がなければ強制 | 色別Jokerをfollow候補に含める。一致 |
| Jokerのスート指定 | lead時に後続同色札がなければ2スートから選ぶ。off-color応手でも他札によって2択が残る場合がある | 3枚出揃った後、必要な場合だけ所有者へ`choose_joker_suit`の同色2択を提示。一致 |
| Jokerのrank指定 | 全員のplay後、そのスートで場にない任意rankを指定 | 所有者へ`choose_joker_rank`を提示し、同スートで場にあるrankを除外。一致 |
| session | 合意round数または目標点。紹介例は6round | 既定`deals=6`、instance optionで短縮。一致 |

秘密の後半札と相手手札はView/CPUから隔離する。`NineteenthRuleAuditTests`はseed 1910～1939で
スート2択、使用済みrank除外、CPU合法性、1deal完走を確認し、seed 1941で二つの相手手札を
入れ替えた観測同値性を確認する。未解決差分はないため`Verified`とする。
