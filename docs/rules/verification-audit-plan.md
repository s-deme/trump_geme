# Verified監査計画

## 状態と昇格規律

取消し完了時点では`Verified`を`trump_crew`、`baohuang`、`napoleon`の3件へ戻した。
第3単位の個別監査後、現在の`Verified`はこれらと`card_capture`、`scoundrel`、`gosankyo`、
`german_whist`、`gin_rummy`、`sono`、`crisp`、`cribbage`、`super_trump`、`daifugo_two`、
`briscola`、`bohemian_schneider`、`durak`、`officer_skat`の17件であり、残る75件は`RuleSpecific`である。専用状態機械を
持つことは外部ルールの正式照合を意味しない。
各ゲームは、個別照合書、資料URL・参照日・採用variant、項目別照合表、正規化記録、実装修正、
固定seed試験（秘密情報があれば観測同値試験）をそろえ、全検証後にだけ単独で昇格できる。

各単位の報告には、昇格ID、資料、実装修正、追加テスト、未解決差分を必ず記録する。根拠が
不足したIDはこの計画を消化しても`RuleSpecific`のままにする。第1単位の結果は各個別照合書に
記録し、未解決差分はない。

## 第3単位の結果

| 区分 | ゲームID | 出典 | 実装修正 | 追加テスト | 未解決差分 |
|---|---|---|---|---|---|
| 昇格 | `briscola` | [Pagat](https://www.pagat.com/aceten/briscola.html) | なし | 全フェーズ、120点、任意の非follow、観測同値 | なし |
| 昇格 | `bohemian_schneider` | [CardRules+](https://cardrulesplus.com/games/bohemian-schneider/) | 同点再配り時のcarryを廃止 | 全フェーズ、tier、同スート直上例外、観測同値 | なし |
| 維持 | `piquet` | [Pagat](https://www.pagat.com/notrump/piquet.html) | younger交換下限と同点延長を補正 | 交換下限、6/8 deal同点処理 | Carte Blanche、宣言・sinkingの選択、Repique/Piqueの時機 |
| 昇格 | `durak` | [Pagat](https://www.pagat.com/beating/podkidnoy_durak.html) | 表向きtrumpをViewへ公開 | 攻防、終了、切り札例外、観測同値 | なし（trump 6交換なしvariantを採用） |
| 昇格 | `officer_skat` | [KDE LSkat manual](https://docs.kde.org/stable_kf6/de/lskat/lskat/lskat.pdf) | なし | 全フェーズ、120点、J trump例外、観測同値 | Grand/Ramsch/Null/Kontra/Reは採用外 |

## 監査単位

| 単位 | 対象ゲームID | 状態 |
|---:|---|---|
| 01 | `card_capture`, `scoundrel`, `gosankyo`, `german_whist`, `gin_rummy` | 完了（5件昇格） |
| 02 | `sono`, `crisp`, `cribbage`, `super_trump`, `daifugo_two` | 完了（5件昇格） |
| 03 | `briscola`, `bohemian_schneider`, `piquet`, `durak`, `officer_skat` | 完了（4件昇格、`piquet`はRuleSpecific維持） |
| 04 | `klaberjass`, `norwegian_whist`, `schnapsen`, `goldmine`, `knave` | 未着手 |
| 05 | `hamlet`, `whos_who`, `farbwechsel`, `sheriff`, `mizerka` | 未着手 |
| 06 | `ninety_nine`, `five_hundred`, `skat`, `gooseberry_fool`, `ulti` | 未着手 |
| 07 | `italian_whist`, `minimo`, `kaedama_trick`, `trick_of_the_dead`, `corpo` | 未着手 |
| 08 | `tanuki`, `multi_stack`, `dubito`, `three_tricks`, `mini_misere` | 未着手 |
| 09 | `agony_aunt`, `collusion`, `confirmation`, `big_two`, `triple_crown` | 未着手 |
| 10 | `doppelkopf`, `guillotine`, `sasaki_44a`, `schafkopf`, `the_trick` | 未着手 |
| 11 | `truf`, `pass_cut_run`, `finesse`, `yaniv`, `wuxing_xiangke` | 未着手 |
| 12 | `schmear`, `briscola_chiamata`, `briscola_bugiarda`, `goninkan`, `portland` | 未着手 |
| 13 | `toepen`, `war`, `blackjack`, `crazy_eights`, `go_fish` | 未着手 |
| 14 | `old_maid`, `speed`, `gops`, `spite_and_malice`, `casino` | 未着手 |
| 15 | `golf`, `sevens`, `concentration`, `cheat`, `page_one` | 未着手 |
| 16 | `seven_bridge`, `rummy_500`, `canasta`, `pinochle`, `hearts` | 未着手 |
| 17 | `spades`, `euchre`, `oh_hell`, `texas_holdem`, `five_card_draw` | 未着手 |
| 18 | `baccarat`, `twenty_four`, `black_lady`, `four_tricks` | 未着手 |

この並びは台帳の候補順を保ち、1単位は最大5ゲームである。監査結果によって実装差分が大きい
場合は、その単位内であっても未解決IDを残し、別のIDだけを昇格させない。
