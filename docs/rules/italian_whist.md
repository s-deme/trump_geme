# Italian Whist監査記録（RuleSpecific）

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
| Jokerのスート指定 | lead時に後続同色札がなければ2スートから選ぶ。off-color応手でも他札によって2択が残る場合がある | `EffectiveSuit()`がheart/clubまたはspadeへ自動決定し、選択Actionがない。**未解決差分** |
| Jokerのrank指定 | 全員のplay後、そのスートで場にない任意rankを指定 | `EffectiveStrength()`が最強の未出rankを自動選択し、選択Actionがない。**未解決差分** |
| session | 合意round数または目標点。紹介例は6round | 既定`deals=6`、instance optionで短縮。一致 |

秘密の後半札と相手手札はView/CPUから隔離し、seed 701で完走・CPU合法性を確認済みである。
ただしJokerのスート・rank指定は勝者を変え得る意思決定であり、決定論的自動選択は入力順への
正規化ではなく選択肢の喪失になる。完全規則を取得できたため旧「原典未取得」は撤回するが、
この2 Actionと対応テストを実装するまで`RuleSpecific`を維持する。
