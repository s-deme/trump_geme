# Verified監査計画

## 状態と昇格規律

第19～21単位で残る26件を再監査し、個別照合書、採用variant、実装差分、固定seed試験を
そろえた。現在は92件すべて`Verified`、`RuleSpecific` 0件、`Prototype` 0件である。
以下の66件到達までの件数と「維持」は各時点の履歴であり、現行状態はこの最終記録と
`GameCatalogue`、[正式照合記録](candidate-rules.md)を正本とする。

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

第4単位の補強後、`klaberjass`、`goldmine`、`knave`、`norwegian_whist`、`schnapsen`を追加し、現在の`Verified`は22件、
`RuleSpecific`は70件である。第5単位の5件を加えた現時点では`Verified` 27件、`RuleSpecific` 65件である。

第7単位の替え玉トリックを加え、現時点の`Verified`は28件、`RuleSpecific`は64件である。
第6単位のNinety-Nineを作者規則の9ディールsessionまで補強し、現時点の`Verified`は29件、
`RuleSpecific`は63件である。
第7単位の公開完全規則を再取得してMinimo、Trick of the Dead、Corpoを補強し、現時点の
`Verified`は32件、`RuleSpecific`は60件である。Italian Whistは具体的なJoker選択差を残す。
第8単位の公開完全規則を再取得し、Tanuki、Multi Stack、Dubito、Three Tricks、Mini Misereを
補強した現時点では`Verified`は37件、`RuleSpecific`は55件である。
第9単位は作者一次規則と候補元完全規則を再取得し、5件すべてを補強した。現時点では
`Verified`は42件、`RuleSpecific`は50件である。
第10単位はGuillotineとThe Trickを補強し、`Verified`は44件、`RuleSpecific`は48件となった。
Doppelkopf、44A、Schafkopfは個別監査書の具体差により保留する。
第11単位は公開完全規則を再取得し、5件すべての規則差と観測境界を補強した。現時点では
`Verified`は49件、`RuleSpecific`は43件である。
第12単位はPagat、Gokurakism、ゲームファームの完全規則を再取得してSchmear、Briscola
Chiamata、Portlandを補強した。現時点では`Verified`は52件、`RuleSpecific`は40件である。
Briscola BugiardaとGoninkanは完全規則を取得済みだが、個別監査書の中核差により保留する。
第13単位はBicycleの個別完全規則を直接確認し、Go Fishを補強した。
現時点では`Verified`は53件、`RuleSpecific`は39件である。Toepen、War、Blackjack、Crazy Eightsは
完全規則を取得済みだが、個別監査書の中核差により保留する。
第14単位はBicycleとPagatの個別完全規則を直接確認し、Old Maid、GOPS、Spite and Maliceを
補強した。現時点では`Verified`は56件、`RuleSpecific`は36件である。SpeedとCasinoは
個別監査書の中核差により保留する。
第15単位はPagat、Bicycle、トランプスタジアムの完全規則を直接確認し、Golf、Sevens、
Concentration、Page Oneを補強した。現時点では`Verified`は60件、`RuleSpecific`は32件である。
Cheatは任意枚数申告と対応人数の差により保留する。
第16単位は公開完全規則を全件再取得し、Rummy 500のPagat掲載variantを補強した。
現時点では`Verified`は61件、`RuleSpecific`は31件である。残る4件は個別監査書の中核差により保留する。
第17単位はBicycleとPagatの完全規則を直接確認し、EuchreとOh Hellを補強した。
現時点では`Verified`は63件、`RuleSpecific`は29件である。SpadesとPoker 2件は個別監査書の
中核差により保留する。
第18単位はPagat、ゴクラキズム、ゲームファームの完全規則を直接確認し、Baccarat、
Black Lady、Four Tricksを補強した。最終的に`Verified`は66件、`RuleSpecific`は26件である。
Twenty-Fourは2人基本形、誤ったno-solution宣言後の2点、4人bluffとの差により保留する。

## 第3単位の結果

| 区分 | ゲームID | 出典 | 実装修正 | 追加テスト | 未解決差分 |
|---|---|---|---|---|---|
| 昇格 | `briscola` | [Pagat](https://www.pagat.com/aceten/briscola.html) | なし | 全フェーズ、120点、任意の非follow、観測同値 | なし |
| 昇格 | `bohemian_schneider` | [CardRules+](https://cardrulesplus.com/games/bohemian-schneider/) | 同点再配り時のcarryを廃止 | 全フェーズ、tier、同スート直上例外、観測同値 | なし |
| 維持 | `piquet` | [Pagat](https://www.pagat.com/notrump/piquet.html) | younger交換下限と同点延長を補正 | 交換下限、6/8 deal同点処理 | Carte Blanche、宣言・sinkingの選択、Repique/Piqueの時機 |
| 昇格 | `durak` | [Pagat](https://www.pagat.com/beating/podkidnoy_durak.html) | 表向きtrumpをViewへ公開 | 攻防、終了、切り札例外、観測同値 | なし（trump 6交換なしvariantを採用） |
| 昇格 | `officer_skat` | [KDE LSkat manual](https://docs.kde.org/stable_kf6/de/lskat/lskat/lskat.pdf) | なし | 全フェーズ、120点、J trump例外、観測同値 | Grand/Ramsch/Null/Kontra/Reは採用外 |

## 第4単位の結果

| 区分 | ゲームID | 出典 | 実装修正 | 追加テスト | 未解決差分 |
|---|---|---|---|---|---|
| 昇格 | `klaberjass` | [Gokurakism](https://gokurakism.com/klabberjass/)、[Pagat](https://www.pagat.com/jass/klabberjass.html) | 公開meld Action、自然順sequence比較 | seed完走、6→9枚、観測同値、公開/同点meld | なし |
| 昇格 | `norwegian_whist` | [Pagat](https://www.pagat.com/whist/twoplayer.html) | なし | seed完走、53〜54 turn、手札/伏札観測同値 | なし |
| 昇格 | `schnapsen` | [DRS](https://schnapsen.realtype.at/index.php?page=rules-english) | 最終check-out phase、未宣言最終trick=1点 | seed完走、最終2札境界、手札/stock観測同値 | なし（直接再取得時の502は参照可用性の記録） |
| 昇格 | `goldmine` | [Tarte Games](https://tartegames.com/wp-content/uploads/2020/09/Goldmine_EnglishRules.pdf) | 説明の無follow・作者出典を訂正 | seed完走、action順、秘密inspect、indicator、無follow | なし |
| 昇格 | `knave` | [White Knuckle Cards](https://whiteknucklecards.com/games/knaves.html) | 出典を完全規則へ訂正 | seed完走、17枚/trump、J罰点、観測同値 | なし |

## 第5単位の結果

| 区分 | ゲームID | 出典 | 実装修正 | 追加テスト | 未解決差分 |
|---|---|---|---|---|---|
| 昇格 | `hamlet` | [Gokurakism](https://gokurakism.com/hamlet/) | なし | seed、mode境界、二相手手札の観測同値 | なし |
| 昇格 | `whos_who` | [David Parlett](https://www.parlettgames.uk/oricards/whoswho.html) | なし | seed、14枚/Joker境界、二相手手札の観測同値 | なし |
| 昇格 | `farbwechsel` | [Gokurakism](https://gokurakism.com/farbwechsel/) | 11trick後の全bidを公開 | seed、bid公開境界、二相手手札の観測同値 | なし |
| 昇格 | `sheriff` | [Gokurakism](https://gokurakism.com/sherif/) | なし | seed、role/no-trump、Joker敗北、二相手手札の観測同値 | なし |
| 昇格 | `mizerka` | [Pagat](https://www.pagat.com/quotawhist/mizerka.html) | なし | seed、6→13枚/交換境界、二相手手札の観測同値 | なし |

## 第6〜18単位の再確認結果

各IDの資料URL・採用範囲・項目別照合・正規化・固定seedは対応する個別監査書に記録する。
ここまでに個別補強で昇格したIDを除く各IDは、根拠不足またはvariant差を残すため`RuleSpecific`を維持する。

| 単位 | 対象 | 資料・監査書 | 専用固定seed試験 | 未解決項目 |
|---:|---|---|---|---|
| 06 | `ninety_nine`〜`ulti` | 各個別監査書（Pagat/Parlett/ISPA URLを記録） | `SixthRuleAuditTests` seed 601〜617 | `five_hundred`以降のcontract、tie精算 |
| 07 | `italian_whist`〜`corpo` | Gokurakism個別完全規則と各監査書 | `SeventhRuleAuditTests` seed 701〜822 | Italian WhistのJoker suit/rank選択 |
| 08 | `tanuki`〜`mini_misere` | Gokurakism完全規則、Parlett作者規則、各個別監査書 | `EighthRuleAuditTests` seed 801〜1005 | なし |
| 09 | `agony_aunt`〜`triple_crown` | Parlett作者規則、Gokurakism完全規則、各個別監査書 | `NinthRuleAuditTests` seed 901〜964 | なし |
| 10 | `doppelkopf`〜`the_trick` | Gokurakism個別完全規則、Pagat、各個別監査書 | `TenthRuleAuditTests` seed 1001〜1041 | Doppelkopf宣言/bonus、44A終了/赤10交換/方向、Schafkopf auction/Stoss |
| 11 | `truf`〜`wuxing_xiangke` | 各個別監査書 | `EleventhRuleAuditTests` seed 1101〜1184 | なし |
| 12 | `schmear`〜`portland` | 各個別監査書 | `TwelfthRuleAuditTests` seed 1201〜1242 | Bugiarda明示Solo、Goninkan公式関係処理・宣言・配点 |
| 13 | `toepen`〜`go_fish` | 各個別監査書 | `ThirteenthRuleAuditTests` seed 1301〜1381 | Toepen challenge/fold、War多人数/打切り、Blackjack house rule |
| 14 | `old_maid`〜`casino` | 各個別監査書 | `FourteenthRuleAuditTests` seed 1401〜1441 | Speed同時優先、Casino build体系 |
| 15 | `golf`〜`page_one` | 各個別監査書 | `FifteenthRuleAuditTests` seed 1501〜1583 | Cheatの5枚以上申告・対応人数差 |
| 16 | `seven_bridge`〜`hearts` | 各個別監査書 | `SixteenthRuleAuditTests` seed 1601〜1681 | Seven Bridge meld/session、Canasta上がり、Pinochle交換、Hearts 6人版 |
| 17 | `spades`〜`five_card_draw` | 各個別監査書 | `SeventeenthRuleAuditTests` seed 1701〜1705 | score/side pot差 |
| 18 | `baccarat`〜`four_tricks` | 各個別監査書 | `EighteenthRuleAuditTests` seed 1801〜1804 | house/score/原典差 |

### 第7単位 替え玉トリック補強

| 区分 | ゲームID | 出典 | 実装修正 | 追加テスト | 未解決差分 |
|---|---|---|---|---|---|
| 昇格 | `kaedama_trick` | [Gokurakism](https://gokurakism.com/kaedama/) | なし | seed完走、第一/第二Jokerの役職公開、観測同値 | なし |

### 第7単位 公開完全規則の再取得

候補元索引の個別リンクを再確認し、旧「原典未取得」4件すべてについて公開完全規則の有無を
再判定した。Italian Whist、Minimo、Trick of the Dead、Corpoの全ページを直接取得できた。

| 区分 | ゲームID | 出典 | 実装修正 | 追加テスト | 未解決差分 |
|---|---|---|---|---|---|
| 維持 | `italian_whist` | [Gokurakism](https://gokurakism.com/italian_whist/) | なし | seed完走・合法CPU（既存） | Jokerのスート2択と未出rank指定を自動決定しており、選択Actionがない |
| 昇格 | `minimo` | [Gokurakism](https://gokurakism.com/minimo/) | なし | double時の3精算形、固定seed、観測同値 | なし |
| 昇格 | `trick_of_the_dead` | [Gokurakism](https://gokurakism.com/totd/) | 前半勝者をlead suit限定から全札rank比較へ修正 | off-suit勝利、Zombie選択順、観測同値 | なし |
| 昇格 | `corpo` | [Gokurakism](https://gokurakism.com/colpo/) | Poker全15枚を`revealed_poker`へ保持 | showdown公開、固定seed、観測同値 | なし |

この補強後の4検証はbuild 0 warning/0 error、test 148/148、Bash migration成功、
PowerShell migration成功である。

### 第6単位 Ninety-Nine補強

| 区分 | ゲームID | 出典 | 実装修正 | 追加テスト | 未解決差分 |
|---|---|---|---|---|---|
| 昇格 | `ninety_nine` | [David Parlett](https://www.parlettgames.uk/oricards/ninety9.html)、[Pagat](https://www.pagat.com/exact/99.html) | 既定9deal session、成功者だけの`revealed_bids`、`deals`短縮戦、`target_score`互換分離 | 9deal終了、claim公開/非公開、次trump、premium優先、観測同値、旧option | なし |

採用variantはParlettのJunk the Joker、許容された初回no-trump、9ディールsessionである。
Pagatの100点game×3 rubberは代替variantとして採用せず、既存`target_score`は明示指定時だけ
1ゲーム短縮戦として保持する。

### 第8単位 公開完全規則の再取得

旧記録の「原典未取得」「score/Joker例外根拠不足」を再確認し、5件すべてで採用variantを
最後まで記述する公開規則を直接取得した。Mini Misereは作者David Parlettの人数別完全規則を正本とする。

| 区分 | ゲームID | 出典 | 実装修正 | 追加テスト | 未解決差分 |
|---|---|---|---|---|---|
| 昇格 | `tanuki` | [Gokurakism](https://gokurakism.com/tanuki/) | 局終了時の全役suitを`revealed_roles`へ保持 | 9局、may/must/may、局末公開、観測同値 | なし |
| 昇格 | `multi_stack` | [Gokurakism](https://gokurakism.com/multi_stacks/) | 2人戦のJを同色/色交互の2役交換へ修正 | 人数別J役、固定seed、stock観測同値 | なし |
| 昇格 | `dubito` | [Gokurakism](https://gokurakism.com/dubito/) | なし（標準Dubitoを採用、Subitoは採用外） | 固定seed、stock観測同値 | なし |
| 昇格 | `three_tricks` | [Gokurakism](https://gokurakism.com/threetricks/) | なし | 52trick/208手、4ラウンド得点、観測同値 | なし |
| 昇格 | `mini_misere` | [David Parlett](https://www.parlettgames.uk/oricards/minimis.html) | Lotを各playerの第1打直前へ移動 | 3～6人deck/score、Lot時機、第3手Joker、観測同値 | なし |

この補強後の4検証はbuild 0 warning/0 error、test 164/164、Bash migration成功、
PowerShell migration成功である。

### 第9単位 公開完全規則の再取得

旧記録で未取得としていた作者ページと候補元の個別ページを再確認した。Agony AuntとCollusionは
David Parlettの一次規則、残る3件は採用variantを最後まで記述したGokurakismページを直接照合した。

| 区分 | ゲームID | 出典 | 実装修正 | 追加テスト | 未解決差分 |
|---|---|---|---|---|---|
| 昇格 | `agony_aunt` | [David Parlett](https://www.parlettgames.uk/oricards/agonaunt.html) | なし | 9罰点盤、3目、回復、観測同値 | なし |
| 昇格 | `collusion` | [David Parlett](https://www.parlettgames.uk/oricards/collude.html) | なし | bonus全分岐、100点反転、観測同値 | なし |
| 昇格 | `confirmation` | [Gokurakism](https://gokurakism.com/confirmation/) | なし | 公開保護、残札bid/score、観測同値 | なし |
| 昇格 | `big_two` | [Gokurakism](https://gokurakism.com/dairoji/) | なし（2なしstraightを採用） | 5枚役、3C/反時計回り、罰点、観測同値 | なし |
| 昇格 | `triple_crown` | [Gokurakism](https://gokurakism.com/game_141018_1/) | 既定を15点sessionへ修正、`deals`短縮戦を維持 | 役別score、目標点、観測同値 | なし |

この補強後の4検証はbuild 0 warning/0 error、test 170/170、Bash migration成功、
PowerShell migration成功である。

### 第10単位 完全規則の再取得

| 区分 | ゲームID | 出典 | 実装修正 | 追加テスト | 未解決差分 |
|---|---|---|---|---|---|
| 維持 | `doppelkopf` | [Pagat](https://www.pagat.com/schafk/doko.html) | なし | 固定seed（既存） | Re/Kontra宣言、Fox/Charlie/Doppelkopf bonusと倍率 |
| 昇格 | `guillotine` | [Gokurakism](https://gokurakism.com/about_guillotine/) | DominoのA連続を合法配置が尽きるまで強制 | A強制継続、24局、観測同値 | なし |
| 維持 | `sasaki_44a` | [GIVE-ME-THE-TRICK](https://give-me-the-trick.blogspot.com/2019/02/44a.html) | なし | 固定seed（既存） | session未確定、run時赤10交換なし、play方向差 |
| 維持 | `schafkopf` | [Gokurakism](https://gokurakism.com/schafkopf/) | なし | 固定seed（既存） | 勝ち抜きauctionとStoss系4段宣言なし |
| 昇格 | `the_trick` | [Gokurakism](https://gokurakism.com/thetorite/) | なし | 3/4人quota、残suit、score、背面観測同値 | なし |

この補強後の4検証はbuild 0 warning/0 error、test 173/173、Bash migration成功、
PowerShell migration成功である。

### 第11単位 完全規則の再取得

| 区分 | ゲームID | 出典 | 実装修正 | 追加テスト | 未解決差分 |
|---|---|---|---|---|---|
| 昇格 | `truf` | [Pagat](https://www.pagat.com/exact/truf.html) | 特殊手札再配布、bid札/伏せtrumpの時系列公開、調整後0bid bonus | 得点独立計算、伏せ/公開境界、観測同値 | なし |
| 昇格 | `pass_cut_run` | [Gokurakism](https://gokurakism.com/pcr/) | partnerを常に4番手とする方向計算 | 席順、距離別得点、観測同値 | なし |
| 昇格 | `finesse` | [ゲームファーム](https://gamefarm.jp/rule/finesse.html) | 出典外no-trump/終了条件を除去、last trick加点順を修正 | table lead/refill、得点独立計算、観測同値 | なし |
| 昇格 | `yaniv` | [Gokurakism](https://gokurakism.com/yaniv/)、[Pagat](https://www.pagat.com/draw/yaniv.html) | 直前組draw、set/run順、4人以上2deck、山再利用、全手札公開、次開始者 | deck境界、draw対象、公開、観測同値 | なし |
| 昇格 | `wuxing_xiangke` | [Gokurakism](https://gokurakism.com/gogyo/) | 1人partnerの右方向を修正 | 全50枚と得点独立計算、観測同値 | なし |

この補強後の4検証はbuild 0 warning/0 error、test 179/179、Bash migration成功、
PowerShell migration成功である。

### 第12単位 完全規則の再取得

| 区分 | ゲームID | 出典 | 実装修正 | 追加テスト | 未解決差分 |
|---|---|---|---|---|---|
| 昇格 | `schmear` | [Pagat](https://www.pagat.com/allfours/schmier.html) | 人数別pack、交換3枚上限、Ace call、team Game点、相手team点、21点同着winner | stock数、同dealer再配布、交換上限、call公開、観測同値 | なし |
| 昇格 | `briscola_chiamata` | [Gokurakism](https://gokurakism.com/briscola_chiamata/) | 全員pass時を同dealer再配布へ修正 | must-follow、card point独立精算、観測同値 | なし |
| 維持 | `briscola_bugiarda` | [Gokurakism](https://gokurakism.com/briscola_bugiarda/) | なし | 固定seed（既存） | 最上位の明示Solo bidとno-trump playが未実装 |
| 維持 | `goninkan` | [五所川原商工会議所公式](https://www.gocci.or.jp/goninkan/rules/rules2.html) | なし | 固定seed（既存） | 二重関の伏札交換、席移動、じゅうろく系宣言、公式配点、外部まき役session |
| 昇格 | `portland` | [ゲームファーム](https://gamefarm.jp/rule/portland.html)、[Gokurakism](https://gokurakism.com/portland/) | passをdraw前へ移動、draw後上書き強制、公開table、勝者開始、同点variant | 公開table、action境界、私有deck観測同値 | なし |

### 第13単位 完全規則の再取得

| 区分 | ゲームID | 出典 | 実装修正 | 追加テスト | 未解決差分 |
|---|---|---|---|---|---|
| 維持 | `toepen` | [Pagat](https://www.pagat.com/last/toepen.html) | なし | 固定seed（既存） | 任意playerの交換challenge、任意時機knock、fold済みwinnerのlead移譲 |
| 維持 | `war` | [Bicycle](https://bicyclecards.com/how-to-play/war)、[Pagat](https://www.pagat.com/war/war.html) | なし | 固定seed（既存） | 多人数warへの全員参加、出典外の既定10000 turn終了 |
| 維持 | `blackjack` | [Bicycle](https://bicyclecards.com/how-to-play/blackjack) | なし | 固定seed（既存） | doubleを9～11に限定、split Ace/最大handのhouse rule根拠 |
| 維持 | `crazy_eights` | [Bicycle](https://bicyclecards.com/how-to-play/crazy-eights)、[Pagat](https://www.pagat.com/eights/crazy8s.html) | 全員5枚、任意draw、勝者への残札点を補正 | 初期配札/top、draw、独立得点、観測同値 | Bicycleの全員pass膠着が未規定のためPagatのstock再利用を合成 |
| 昇格 | `go_fish` | [Bicycle](https://bicyclecards.com/how-to-play/go-fish) | 3人配札を5枚から7枚へ修正 | 人数別配札、成功ask連続手番、観測同値 | なし |

### 第14単位 完全規則の再取得

| 区分 | ゲームID | 出典 | 実装修正 | 追加テスト | 未解決差分 |
|---|---|---|---|---|---|
| 昇格 | `old_maid` | [Bicycle](https://bicyclecards.com/how-to-play/old-maid) | なし | odd queen敗者、3人観測同値 | なし |
| 維持 | `speed` | [Bicycle](https://bicyclecards.com/how-to-play/spit)、[Pagat](https://www.pagat.com/patience/spit.html) | なし | 固定seed（既存） | 同時競争を交互優先へ変更、片側reserve枯渇時のstarter補充差 |
| 昇格 | `gops` | [Pagat](https://www.pagat.com/misc/gops.html) | なし | 秘密bid観測同値、score+unclaimed=91 | なし |
| 昇格 | `spite_and_malice` | [Pagat](https://www.pagat.com/patience/spitemal.html) | 開始比較をA lowへ修正し、同rank時に両pay-offを再shuffle | 20 seed開始境界、観測同値 | なし |
| 維持 | `casino` | [Pagat](https://www.pagat.com/fishing/casino.html) | なし | 固定seed（既存） | build所有制約、single build増築、multiple buildが未実装 |

### 第15単位 完全規則の再取得

| 区分 | ゲームID | 出典 | 実装修正 | 追加テスト | 未解決差分 |
|---|---|---|---|---|---|
| 昇格 | `golf` | [Pagat](https://www.pagat.com/draw/golf.html#six) | 捨て札強制交換、dealer交代、山切れ合法手、公開盤面とdraw観測境界 | 強制交換、公開盤面、stock観測同値 | なし |
| 昇格 | `sevens` | [トランプスタジアム](https://playingcards.jp/m/game_rules/modal/sevens_rules_modal.html) | 失格札の孤立区間と失格順順位を実装 | 3回pass、4回目失格、孤立札、観測同値 | なし |
| 昇格 | `concentration` | [Bicycle](https://bicyclecards.com/how-to-play/concentration) | なし | pair得点・再手番、hidden layout観測同値 | なし |
| 維持 | `cheat` | [Pagat](https://www.pagat.com/beating/cheat.html) | なし | 固定seed（既存） | 原典2～10人に対し3～6人、原典任意枚数に対し最大4枚 |
| 昇格 | `page_one` | [Pagat](https://www.pagat.com/inflation/page_one.html)、[Bicycle](https://bicyclecards.com/how-to-play/page-one) | 残札罰点を除き単deal無得点へ補正 | 宣言忘れ5枚罰、観測同値 | なし |

### 第16単位 完全規則の再取得

| 区分 | ゲームID | 出典 | 実装修正 | 追加テスト | 未解決差分 |
|---|---|---|---|---|---|
| 維持 | `seven_bridge` | [Pagat](https://www.pagat.com/rummy/7bridge.html)、[任天堂](https://www.nintendo.com/jp/others/playing_cards/howtoplay/seven_bridge/index.html) | 公開meld owner表示のみ共通補強 | 固定seed（既存） | 7入り2枚meld、再利用回数、200点session、6人拡張 |
| 昇格 | `rummy_500` | [Pagat](https://www.pagat.com/rummy/500rum.html) | low Ace得点、500点同着延長、公開meld owner | A23/QKA/A-set、短縮session、観測同値 | なし |
| 維持 | `canasta` | [Pagat](https://www.pagat.com/rummy/canasta.html)、[Bicycle](https://bicyclecards.com/how-to-play/canasta) | なし | 固定seed（既存） | 初回表札、initial meld選択、partner許可、bonus |
| 維持 | `pinochle` | [Pagat](https://www.pagat.com/marriage/pinmain.html) | なし | 固定seed（既存） | partner pass、Dix、trump上回り義務、score換算 |
| 維持 | `hearts` | [Bicycle](https://bicyclecards.com/how-to-play/hearts)、[Pagat](https://www.pagat.com/reverse/hearts.html) | kitty内2Cを移動せず最低club leadへ修正 | 3人kitty/lead、観測同値 | 6人版がCancellation Heartsと不一致 |

### 第17単位 完全規則の再取得

| 区分 | ゲームID | 出典 | 実装修正 | 追加テスト | 未解決差分 |
|---|---|---|---|---|---|
| 維持 | `spades` | [Bicycle](https://bicyclecards.com/how-to-play/spades)、[Pagat](https://www.pagat.com/auctionwhist/spades.html) | 目標点同点時の延長 | 契約失敗0点、同点延長、観測同値 | Bicycleの個人得点表記とteam合算の関係、PagatのNil・失敗減点 |
| 昇格 | `euchre` | [Bicycle](https://bicyclecards.com/how-to-play/euchre)、[Pagat](https://www.pagat.com/euchre/euchre.html) | 3枚＋2枚packet配札 | 表札、alone march得点、観測同値 | なし |
| 昇格 | `oh_hell` | [Pagat](https://www.pagat.com/exact/ohhell.html) | なし | 人数別round列、dealer hook、普及配点、観測同値 | なし |
| 維持 | `texas_holdem` | [Bicycle](https://bicyclecards.com/how-to-play/texas-holdem-poker)、[Pagat Poker Betting](https://www.pagat.com/poker/rules/betting.html) | なし | 固定seed（既存） | side pot、all-in参加資格、複数hand session |
| 維持 | `five_card_draw` | [Pagat](https://www.pagat.com/poker/variants/5draw.html)、[Pagat Poker Betting](https://www.pagat.com/poker/rules/betting.html) | なし | 固定seed（既存） | all-check redeal、side pot、複数hand session |

### 第18単位 完全規則の再取得

| 区分 | ゲームID | 出典 | 実装修正 | 追加テスト | 未解決差分 |
|---|---|---|---|---|---|
| 昇格 | `baccarat` | [Pagat](https://www.pagat.com/banking/baccarat.html) | 根拠名を8-deckオンラインPunto Bancoへ明確化 | 416枚、natural stand、純損益、shoe観測同値 | なし |
| 維持 | `twenty_four` | [Pagat](https://www.pagat.com/adders/24.html) | なし | 固定seed・solver（既存） | 2人private stack戦、誤no-solution後2点、4人bluff |
| 昇格 | `black_lady` | [ゴクラキズム](https://gokurakism.com/black_lady/)、[ゲームファーム](https://gamefarm.jp/rule/blacklady.html) | 規則上表向きのtable札をViewへ公開 | 5人kitty、clear/carry、観測同値 | なし |
| 昇格 | `four_tricks` | [ゴクラキズム](https://gokurakism.com/fourtricks/) | 根拠名を完全規則ページへ明確化 | 36枚、最終二重、得点表、観測同値 | なし |

### 第19～21単位 残件完了

第19単位はItalian WhistのJoker宣言、Gooseberry Foolのtie精算、Briscola Bugiardaの明示Solo/no-trumpを
補強した。第20単位は9件の中規模差を、`TwentiethRuleAuditTests`の固定seedと個別境界で閉じた。
第21単位は残る14件について契約体系、同時入力、meld/build、継続session、main/side potまで補強し、
`TwentyFirstRuleAuditTests`で個別境界と全件決定性を固定した。各採用variantと出典は個別照合書を正本とし、
26件すべて未解決差分なしで昇格した。

## 必須検証記録（2026-08-15）

各単位の最後に、`dotnet build TrumpGameLab.sln -m:1`、`dotnet test tests/TrumpLab.Tests`、
`./scripts/verify-migration.sh`、`pwsh ./scripts/verify-migration.ps1`をこの順で実行した。
Windows環境ではGit for Windows Bashからshell scriptを起動した。test欄には各単位完了時の
実測総数を記録する。

| 単位 | build | test | Bash migration | PowerShell migration |
|---:|---|---|---|---|
| 04 | 成功 | 142/142 | 成功 | 成功 |
| 05 | 成功 | 142/142 | 成功 | 成功 |
| 06 | 成功 | 142/142 | 成功 | 成功 |
| 07 | 成功 | 142/142 | 成功 | 成功 |
| 08 | 成功 | 142/142 | 成功 | 成功 |
| 09 | 成功 | 142/142 | 成功 | 成功 |
| 10 | 成功 | 142/142 | 成功 | 成功 |
| 11 | 成功 | 142/142 | 成功 | 成功 |
| 12 | 成功 | 183/183 | 成功 | 成功 |
| 13 | 成功 | 187/187 | 成功 | 成功 |
| 14 | 成功 | 191/191 | 成功 | 成功 |
| 15 | 成功 | 196/196 | 成功 | 成功 |
| 16 | 成功 | 199/199 | 成功 | 成功 |
| 17 | 成功 | 203/203 | 成功 | 成功 |
| 18 | 成功 | 207/207 | 成功 | 成功 |
| 19～21 | 成功（0 warning / 0 error） | 234/234 | 成功 | 成功 |

第12単位の補強後に指定順で再実行した実測は、build 0 warning/0 error、test 183/183、
Bash migration成功、PowerShell migration成功である。
第14単位は観測helperの人数指定回帰を初回全testで検出して修正した。再実行の実測は
build 0 warning/0 error、test 191/191、Bash migration成功、PowerShell migration成功である。
第13単位は初回全testでCrazy Eightsの全員pass膠着を検出したため同IDを保留へ戻し、
Pagat再利用variantで完走性を回復した。再実行の実測はbuild 0 warning/0 error、test 187/187、
Bash migration成功、PowerShell migration成功である。

### 第4単位クラバヤス補強（2026-08-15）

`declare_meld`の自動集計を公開比較Actionへ置換し、trick強度とは独立したsequence自然順を修正した。
この補強後にも同じ4検証を実行し、buildは0 warning/0 error、testは143/143、Bash migrationと
PowerShell migrationはいずれも成功と記録する。

### 第4単位ゴールドマイン補強（2026-08-15）

Tarte Gamesの作者PDFを取得して、同名Pagat版が別作品であることを切り分けた。作者版に一致する
無followをGameInfo/台帳へ訂正し、action順・秘密inspect・indicator・金塊得点を固定した。4検証の
最終結果はbuild 0 warning/0 error、test 144/144、Bash migration成功、PowerShell migration成功とする。

### 第4単位ネイブ補強（2026-08-15）

White Knuckle Cardsの完全規則で3人標準版を確定し、出典表記を訂正した。17枚配札・turn-up trump・
J罰点を固定し、4検証の最終結果はbuild 0 warning/0 error、test 145/145、Bash migration成功、
PowerShell migration成功とする。

### 第5単位 Sheriff・Farbwechsel 再照合（2026-08-15）

SheriffはGokurakismの完全規則で21枚、Joker保持者起点の役選択、市長のtrump/no-trump、
Joker敗北、役別得点、8点終了を確認して昇格した。Farbwechselは同サイトの完全規則を直接取得し、
11trick後のbid公開を`revealed_bids`として補正して昇格した。両者とも固定seed・境界・観測同値を
追加確認した。最終4検証はbuild 0 warning/0 error、test 147/147、Bash migration成功、
PowerShell migration成功である。

## 監査単位

| 単位 | 対象ゲームID | 状態 |
|---:|---|---|
| 01 | `card_capture`, `scoundrel`, `gosankyo`, `german_whist`, `gin_rummy` | 完了（5件昇格） |
| 02 | `sono`, `crisp`, `cribbage`, `super_trump`, `daifugo_two` | 完了（5件昇格） |
| 03 | `briscola`, `bohemian_schneider`, `piquet`, `durak`, `officer_skat` | 完了（4件昇格、`piquet`はRuleSpecific維持） |
| 04 | `klaberjass`, `norwegian_whist`, `schnapsen`, `goldmine`, `knave` | 完了（5件昇格） |
| 05 | `hamlet`, `whos_who`, `farbwechsel`, `sheriff`, `mizerka` | 完了（5件昇格） |
| 06 | `ninety_nine`, `five_hundred`, `skat`, `gooseberry_fool`, `ulti` | 完了（`ninety_nine`昇格、4件RuleSpecific維持） |
| 07 | `italian_whist`, `minimo`, `kaedama_trick`, `trick_of_the_dead`, `corpo` | 完了（4件昇格、`italian_whist`はRuleSpecific維持） |
| 08 | `tanuki`, `multi_stack`, `dubito`, `three_tricks`, `mini_misere` | 完了（5件昇格） |
| 09 | `agony_aunt`, `collusion`, `confirmation`, `big_two`, `triple_crown` | 完了（5件昇格） |
| 10 | `doppelkopf`, `guillotine`, `sasaki_44a`, `schafkopf`, `the_trick` | 完了（2件昇格、3件RuleSpecific維持） |
| 11 | `truf`, `pass_cut_run`, `finesse`, `yaniv`, `wuxing_xiangke` | 完了（5件昇格） |
| 12 | `schmear`, `briscola_chiamata`, `briscola_bugiarda`, `goninkan`, `portland` | 完了（3件昇格、`briscola_bugiarda`、`goninkan`はRuleSpecific維持） |
| 13 | `toepen`, `war`, `blackjack`, `crazy_eights`, `go_fish` | 完了（`go_fish`昇格、4件RuleSpecific維持） |
| 14 | `old_maid`, `speed`, `gops`, `spite_and_malice`, `casino` | 完了（3件昇格、`speed`、`casino`はRuleSpecific維持） |
| 15 | `golf`, `sevens`, `concentration`, `cheat`, `page_one` | 完了（4件昇格、`cheat`はRuleSpecific維持） |
| 16 | `seven_bridge`, `rummy_500`, `canasta`, `pinochle`, `hearts` | 完了（`rummy_500`昇格、4件RuleSpecific維持） |
| 17 | `spades`, `euchre`, `oh_hell`, `texas_holdem`, `five_card_draw` | 完了（`euchre`、`oh_hell`昇格、3件RuleSpecific維持） |
| 18 | `baccarat`, `twenty_four`, `black_lady`, `four_tricks` | 完了（3件昇格、`twenty_four`はRuleSpecific維持） |
| 19 | `italian_whist`, `gooseberry_fool`, `briscola_bugiarda` | 完了（3件昇格） |
| 20 | `sasaki_44a`, `toepen`, `war`, `blackjack`, `crazy_eights`, `cheat`, `hearts`, `spades`, `twenty_four` | 完了（9件昇格） |
| 21 | `piquet`, `five_hundred`, `skat`, `ulti`, `doppelkopf`, `schafkopf`, `goninkan`, `speed`, `casino`, `seven_bridge`, `canasta`, `pinochle`, `texas_holdem`, `five_card_draw` | 完了（14件昇格） |

第1～18単位は台帳順・最大5ゲームで行った。第19～21単位は残件を実装規模別に再編し、
短・中・大の3群で横断監査した。
