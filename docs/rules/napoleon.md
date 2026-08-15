# Napoleon（ナポレオン）検証仕様

## 検証状態と資料方針

ゲームID `napoleon` は、日本で地域差が非常に多いゲームである。本基盤では赤桐裕二
『トランプゲーム大全』の基本ルール系を採用し、書籍著者が公開しているGamefarmの解説を
主資料とする。参照日は2026年8月14日である。

- **A** — 赤桐裕二「[ナポレオン](https://gamefarm.jp/rule/napoleon.html)」：『トランプゲーム大全』
  系の公開ルール。4～7人の人数別配札、soft pass、副官、捨札、通常play、Joker、セイム2、
  よろめき、勝敗と多数のvariantを区別して記述する主資料。
- **G** — ゴクラクテン「[[トランプ] ナポレオン](https://gokurakism.com/napoleon/)」：同書の
  基本ルールで実際に5人playした記録。soft pass、秘密副官、5勝先取、Joker lead、
  セイム2、よろめきを相互確認する。候補台帳の元資料でもある。
- **T** — トランプスタジアム「[ナポレオンのゲームルール](https://playingcards.jp/game_rules/napoleon_rules.html)」：
  5人53枚を実際に提供する実装資料。最低12/13枚、複数巡の競り、全員pass、秘密副官、
  3枚交換、絵札20枚、10 trickを確認する。
- **N** — Napoleon wiki「[ナポレオンのルール](https://w.atwiki.jp/shintaruo/pages/18.html)」：
  5人53枚系の詳細な別variant。Jokerの通常時順位、Club 3請求、捨札の帰属、固定点表がAと
  異なるため、採用系統へ混ぜず比較資料として使う。
- **M** — 名古屋大学サイト所収「[Napoleon のルール](https://www.math.nagoya-u.ac.jp/~m04026b/napo/napo12.pdf)」：
  4～5人・Jokerなし系。競り、秘密副官、初回制約、戦略記述を比較するために用いる。
- **P** — Pagat「[Napoleon](https://www.pagat.com/picture/napoleon.html)」：日本式Napoleonの
  英語資料。Joker leadと得点法に複数variantがあることを相互確認する。

全国統一の競技団体規則は確認できなかった。A自身もgroupごとの差が極めて多いと明記する。
したがって「一般的なナポレオン」として各資料の特殊規則を合成せず、A/G系を採用variantとし、
CLIで必要な最小限だけを明示的なoptionまたは正規化とする。

## 項目別照合 — 人数、deck、配札

| 項目 | 資料照合 | 採用仕様・判定 |
|---|---|---|
| 標準人数 | A/Gは4～7人で5人が最良。T/Nは5人専用、Mは4～5人 | Runtimeは4～7人、推奨5人 |
| deck | A/Gは通常52枚＋Joker 1枚。T/Nも5人53枚 | 53枚。通常札は各1枚で重複なし |
| Joker枚数 | A/G/T/Nは1枚。MはJokerなし別系統 | 1枚 |
| 4人配札 | Aは各12枚、widow 5枚 | 12×4＋5 |
| 5人配札 | A/G/T/Nは各10枚、widow 3枚 | 10×5＋3 |
| 6人配札 | Aは各8枚、widow 5枚 | 8×6＋5 |
| 7人配札 | Aは各7枚、widow 4枚 | 7×7＋4 |
| 初dealer | A/Gは任意に決定 | seedと無関係なCLI正規化としてP0 |
| dealer交代 | A/Gは時計回り | dealごと、全員passの流れでも左隣へ交代 |
| 最初の競り手 | A/Gはdealer左隣 | `(dealer + 1) % Players` |

## 項目別照合 — 競りと副官

| 項目 | 資料照合 | 採用仕様・判定 |
|---|---|---|
| 最低宣言 | Aは10～14などgroup差、Tは12/13、Gの例は12 | 既定12。`minimum_bid=10..20`で生成時固定 |
| 宣言内容 | A/G/T/Nは切り札suitと絵札獲得枚数 | `bid N:C/D/H/S`、最大20 |
| bid順位 | A/G/Tは枚数優先、同数はS＞H＞D＞C | C＜D＜H＜S、次の枚数へ進む全順序 |
| 同数でsuit変更 | A/G/Tは暫定宣言より強いsuitなら可 | 可。弱いsuitへは少なくとも1枚増やす |
| pass後の再入札 | A/Gはsoft pass。Aは一度pass後も参加可 | 可。passはその時点の発言だけ |
| 競り終了 | A/Gは最後のbid後に他全員が連続pass | `Players-1`連続passで最後のbidderをNapoleonに確定 |
| 全員pass | A/G/Tは流して配り直し。Aは次dealer | 次dealerへ移り、新deckをshuffle/deal |
| Napoleon確定 | A/Gは最後に最高bidをしたplayer | そのplayer、契約枚数、trumpを公開 |
| 副官指定 | A/G/NはNapoleonがcardを1枚指定 | 通常52枚またはJokerの任意の1枚。seat指定・副官なし宣言は不可 |
| 指定不能card | Aは「どのcardでもよい」 | なし。存在しないcard、複数Joker等はactionに出ない |
| Napoleon自身が保持 | A/Gは副官なしの1対残員 | `solo`。独立した副官席を作らない |
| 指定cardがwidow | A/Gは副官なし | widow取得後にNapoleon本人だけがsoloと分かる |
| 副官不在・兼任 | Aはself/widow callを副官なし、NはNapoleon自身を副官と表現 | IGameではどちらも1対残員の`solo`へ正規化 |
| Napoleonの認識 | G/Mは副官本人以外に秘密。Napoleonは指定cardのholderを知らない | Napoleonに真のholderを表示・CPU入力しない。self/widow callだけ自己情報からsoloと知る |
| 副官本人 | A/Gは指定cardを持つ本人だけが自分の役を知る | 本人の`View`だけ`your_role=adjutant` |
| 公開時期 | A/Gは指定cardをplayするまで名乗れない | そのcardを`Apply(play)`した直後に全viewerへ公開 |

## 項目別照合 — widow、play、特殊札

| 項目 | 資料照合 | 採用仕様・判定 |
|---|---|---|
| widow取得 | A/Gは副官指定後にNapoleonが全て取る | 指定action後に手札へ加える |
| 捨札 | Aはwidowと同数を任意に捨てる | 4/5/6/7人で5/3/5/4枚を1枚ずつ選ぶ |
| 得点札の捨札 | Aは可、表向きにし相手軍のもの。Gは好きなcardを裏向き | Aを優先。得点札だけ公開しNapoleon軍の獲得数へ含めない |
| 非得点捨札 | Aは裏向き | Napoleon本人だけ内容を保持し、他playerには枚数のみ |
| 捨てられないcard | Aに禁止なし | なし。Mighty、役J、Joker、指定cardもself/widow callなら捨てられる |
| 最初のlead | A/G/T/NはNapoleon | 交換終了後のNapoleon |
| 次trickのlead | A/G/T/Nは前trick勝者 | 勝者を`CurrentPlayer`にする |
| 通常follow | A/G/T/Nはlead suitを持てばmust-follow | printed suitで判定。Mighty・裏Jも元のsuitに属する |
| trumpを出せる条件 | 通常はvoidなら任意。Joker lead時はtrump保持者へ請求 | 通常leadではvoid時のみ任意trump。Joker leadでは保持trumpを強制 |
| following Joker | A/Gはlead suitに関係なくいつでも出せ、通常は最弱 | follow保有中でも合法。通常playでは勝敗比較の最下位 |
| Joker lead | A/Gは可能だが初trickは禁止 | 第2trick以降に`lead_joker` |
| Joker lead効果 | A/Gはtrump請求、JokerはMighty以外に勝つ | suit指定actionはなく、現在trumpを請求 |
| Joker hunter | AではSpade 3等はvariant、NはClub 3を採用 | 不採用。3のleadでJokerを強制しない |
| Mighty | A/G/T/NはSpade A | 常に`AS`で最上位（よろめき成立時を除く） |
| Spade trump時 | AはMightyの変更を記さない | Mightyは`AS`のまま。Heart A等への変更なし |
| 正J | A/G/T/Nはtrump suitのJ | Mighty/Joker leadに次ぐ固定役札 |
| 裏J | A/G/T/Nはtrumpと同色の別suitのJ | 正Jの次。printed suitのfollow義務は維持 |
| 通常比較 | AはMighty＞正J＞裏J＞trump A..2＞lead A..2 | Jokerをfollowした場合はこの末尾 |
| セイム2 | A/Gは全normal cardが同じsuit、Jokerなし。初trick無効 | 既定ON。Mighty・正J・裏Jより下、trump/leadより上 |
| よろめき | Aはoption、Gは採用。Spade AとHeart Qが同trick | `yoromeki=true`を既定とし、成立時`QH`が最上位 |
| 特殊札の総優先 | A/Gの個別記述を同一順へ展開 | よろめき成立QH ＞ Mighty ＞ lead Joker ＞ 正J ＞ 裏J ＞ Same 2 ＞ trump ＞ lead suit ＞その他／follow Joker |
| 初trick制限 | AはJoker lead禁止、Same 2無効。他の制限はvariant | この2点だけ。trump、Mighty、正裏J、following Jokerは有効 |
| trick獲得物 | A/Gは勝者が場の絵札を獲得 | 得点札をplayer別に公開集計。非得点札は勝敗数に影響しない |

## 項目別照合 — 勝敗、得点、session

| 項目 | 資料照合 | 採用仕様・判定 |
|---|---|---|
| 得点札 | A/G/T/Nは各suitのA/K/Q/J/10 | 合計20枚。Jokerと2～9は非得点札 |
| Napoleon軍 | Napoleon＋公開前は秘密の副官、soloならNapoleonだけ | 両者の獲得得点札を合算 |
| 契約ちょうど | A/G/Tは宣言枚数以上なら成功 | 成功、Napoleon軍各playerにdeal勝数1 |
| 契約超過 | A/G/Tは同じく成功 | 追加点なし |
| 契約未達 | A/G/Tは連合軍勝利 | 連合軍各playerにdeal勝数1 |
| 全20枚 | Aは通常成功。20枚で敗北する規則はvariant | 通常成功、特別bonusなし |
| solo倍率 | Aは基本勝敗に倍率を定めず、N等に別の点表 | 倍率なし。勝った側の各playerが1勝 |
| 宣言枚数比例点 | Aは別得点variantとして紹介、Nは固定点とpenalty | 不採用 |
| ゼロ和性 | A/Gは「勝ち数」を記録し、支払点ではない | 非ゼロ和。各dealの勝った側全員へ1。`Scores`は累積勝数 |
| 1 dealかsessionか | A/Gは複数dealの最多勝。Gの実戦は5勝先取 | 既定5勝先取。`target_score=1..100` |
| session終了 | Gの5勝先取をCLI向けに確定 | 誰かが目標勝数へ達したdeal終了時。最多勝数のplayerを`Winners` |
| 次deal | A/Gはdealerを時計回りに移す | score未達なら新しいdealer・shuffle・競りへ遷移 |

## 状態機械

```text
deal: 53枚をshuffleし、dealer左から1枚ずつ人数別枚数を配る
  ↓
bid: dealer左から bid(goal,trump) / pass をsoft-passで反復
  ├─ 全員pass → dealer交代 → deal
  └─ 最終bid以外が連続pass → Napoleon・goal・trump確定
  ↓
call_adjutant: Napoleonが通常札またはJokerを1枚指定
  ↓
discard_widow: widow全取得 → 同数をNapoleonが選んで捨てる
  ↓
play: Napoleon lead → clockwise response → trick winnerが次lead
  ├─ 通常lead: must-follow、Jokerは例外
  └─ Joker lead: trump請求
  ↓ handSize trick
score_deal: 得点札20枚中のNapoleon軍獲得数とgoalを比較し、勝った側へ各1勝
  ├─ target_score未達 → dealer交代 → deal
  └─ 到達 → finished / Result
```

constructorの初回deal以外では、選択可能な操作を`LegalActions()`だけが生成する。`Apply()`は
同じ集合で検査した後にだけbid、役職、widow、捨札、手札、trick、得点、dealを変更する。
`ChooseCpuAction()`と`View()`は状態を変更しない。

## CLI正規化と生成option

- `target_score=1..100`：複数dealの終了を有限かつ再現可能にする勝数。既定5はGの実戦記録に合わせる。
- `minimum_bid=10..20`：資料間で一定しない最低宣言。既定12はGの例とTの標準選択に合わせる。
- `yoromeki=true|false`：Aがoptionとする地域規則。候補元Gで採用されているため既定true。
- `same_two=true|false`：A/G基本規則を既定trueとし、導入用に無効化できる。
- 最初のdealerを決める物理的抽選はP0へ、shuffle/cutは注入`DeterministicRandom`へ正規化する。
- CLIの人間はindex選択の既存構文のまま、bid/pass、副官card、全捨札、全playを選べる。

optionはconstructorで読み、instance fieldだけへ保存する。global設定、UnityEngine、独自乱数は使わない。

## 公開情報・非公開情報とCPU

全playerへphase、deal、dealer、公開bid、Napoleon、goal、trump、副官指定card、公開済み副官、
各手札枚数、公開捨て得点札、trick、player別獲得得点札数、勝数を表示する。viewer本人だけへ
自手札、自分の役、Napoleonなら全捨札を表示する。

副官のseatは本人と、self/widow callを知ったNapoleon以外には指定cardがplayされるまで秘密である。
相手手札、widow内容、相手から見た非得点捨札、将来のshuffle順は表示しない。session終了時だけ
結果説明に必要な陣営を公開してよい。

CPUは次の観測可能情報だけを使う。

- bidは自手札の得点札、Mighty、Joker、正裏J候補、suit長から契約強度とtrumpを評価する。
- 副官指定は自手札にない公開上最強のcardを優先し、他playerやwidowを探索しない。
- Napoleon軍と自分だと分かる副官は、残りgoalと場の得点札を見て既知の味方のtrickを支援する。
- 連合軍はNapoleonまたは公開副官の勝利を妨げ、自分が取れる得点札を優先する。
- 未公開副官の真のseatは参照せず、自分が指定cardを持つという本人の私有情報か、公開済みcardだけを使う。
- 強さが同じ候補は決定的に選ぶ。現方策は乱数を使わないため、注入rng以外の乱数源もない。

別player間でprivateな`adjutant` fieldだけを変えてviewerの観測を同一にした状態で、CPU actionが
一致する観測同値テストを置く。

## 採用しないvariantと資料差

- Nの`Mighty > Joker > 正J > 裏J`という常時強いJoker、Club 3のJoker請求、捨札を初trick勝者へ
  渡す規則、8/4点等の点表はA/G系と異なるため採用しない。
- MのJokerなし、4人14枚・5人12枚開始、初trickのtrump無効、交換後のtrump変更は別系統として不採用。
- no-trump、pass後復帰禁止、順不同bid、強制Napoleon、widow公開後の再競りは採用しない。
- Spade/Club 3によるJoker hunter、Joker lead時の絵札請求、弱いJoker lead、suit指定Jokerは採用しない。
- MightyのHeart A等への変更、どよめき、よろめき返し、reverse、ご破算、裏Jのfree playは採用しない。
- 初trickのtrump禁止、Mighty/正裏J無効、following Joker禁止は採用しない。
- Siberian rule（全20枚でNapoleon軍敗北）、perfect宣言、契約枚数比例点、独り立ち倍率、
  多副官、裏切り、永久順位戦は採用しない。
- Tのbidと副官cardの同時宣言はUI固有正規化であり、本RuntimeではA/GどおりNapoleon確定後に指定する。

## 固定seed・規則境界テスト

`NapoleonContractTests`は次を独立に検証する。

- 53枚、全通常札・Joker各1、得点札20枚、4～7人の配札とwidow境界
- dealer左の競り、同数上位suit、soft pass後再入札、全員pass、最低値違反、20S上限
- Napoleon確定、任意card副官、本人だけの秘密役、play時公開、self/widow callのsolo
- widow取得、同数捨札、得点札だけの公開、非得点捨札の秘匿
- printed-suit followとJoker例外、初trickJoker lead禁止、後続Joker leadのtrump請求
- trump/lead、Mighty、正J、裏J、following/leading Joker、Same 2、よろめきの競合順位
- Same 2の初trick無効とoption無効、Spade trump時もMightyがASのままであること
- 契約ちょうど、超過、未達、20枚、soloの勝数（bonus・倍率なし）
- option既定、instance分離、最小最大値、人数3/8拒否、同一seed決定性
- 秘密副官だけが違う観測同値CPU、全phaseのCPU合法性、4～7人×複数seed完走

資料・本仕様・Runtime・固定seedテスト・CLI検証・複数seed検証の採用variant内に既知の不一致が
ないため、Catalogueで`Verified`とする。
