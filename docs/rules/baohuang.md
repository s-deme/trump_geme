# Baohuang（保皇）検証仕様

## 検証状態と資料方針

ゲームID `baohuang` は、山東・日照系168枚ルールを採用し、資料・Runtime・固定seedテスト・
CLI契約を項目別に照合した。参照日は2026年8月14日である。

- [Pagat: Bao Huang](https://www.pagat.com/climbing/baohuang.html)：山東省日照を含む広く行われる系統として、168枚、秘密陣営、皇帝の譲渡、宣言、組、soft pass、早期終了、順位点、独保、次dealの上納までを一貫して記述する主資料。現地player 3名からの情報を基礎とする詳細資料である。
- 竞技世界（北京）ネットワーク技術有限公司（JJ）の[保皇ゲーム規則](https://www.jj.cn/help/baohuang.html)：5人、168枚、印付き大小Joker、皇帝の一巡譲渡、皇帝と侍衛の情報非対称、組比較、皇帝lead、接風、明暗独保と早期終了を確認する公式ゲーム運営資料。
- 同社の[JJ標準保皇得点表](https://www.jj.cn/news/13/20090519134900000267.html)：通常2対3、暗独保、明独保の全順位得点と宣言時2倍を照合する主得点表。
- [遠航ゲームセンターの保皇規則](https://www.yhgame.cn/rules/game-baohuang.html)：168枚、印付きJokerの通常Joker同等性、6最後、hard-pass、造反という別系統を比較するための相互確認資料。

統一競技規則は確認できず、資料にも地域差が明記されている。このため「一般保皇」へ複数系統を
勝手に統合せず、Pagatの山東・日照系を正本とする。JJ資料は一致項目と得点の一次性が高い
相互確認先として用いる。遠航のhard-pass・6最後必須などは別variantとして採用しない。

## 項目別照合

表中のPはPagat、JはJJ規則、JSはJJ得点表、Yは遠航を示す。

| 項目 | 資料照合 | 採用仕様・判定 |
|---|---|---|
| 人数 | P/J/Yはいずれも5人 | 5人専用。Registryも5..5 |
| deck | P/J/Yは4組から3・4・5を除く160枚＋大小Joker各4＝168枚 | 各通常rankは4 suit×4 copy＝16枚。大小Joker各4 |
| 3・4・5 | P/J/Yはいずれも除外 | 不使用。216枚variantは不採用 |
| Jokerの強さ | P/J/Yは大＞小＞2＞A…＞6 | 印付き大・小も同色Jokerと同じ強さ。印付きだけを最強にはしない |
| 皇帝 | P/Jは印付き大Joker保持者。辞退時は無交換で隣席へ渡り、一巡辞退なら元保持者が強制受諾 | `accept_emperor` / `pass_emperor`。5回のpass後はacceptだけ |
| 警護官・侍衛 | P/Jは印付き小Joker保持者 | その保持者をguardとする。3枚提示によるpartner指名variantは不採用 |
| 相互認識 | Jは皇帝が侍衛を知らず、他playerも秘密の協力者を知らないと明記。Pもmarked cardが出るまで不明 | 皇帝は公開。guardは本人だけが知り、宣言または印付き小Jokerのplayで公開 |
| guard不在・兼任 | P/Jは印付き小Jokerが必ずあり、皇帝と同一保持なら独保 | guard不在はない。同一playerなら1対4 |
| 陣営宣言・造反 | Pは皇帝以外が賛否を宣言でき、誰かの宣言で得点2倍。Jは明保・明独保、威海差分に造反 | 全非皇帝へ同じ`remain_hidden` / `declare_allegiance`を提示。guard宣言は保皇、一般人宣言は造反。複数宣言でも倍率は累積せず×2 |
| 独保宣言 | P/J/JSは暗独保と明独保を区別し、明独保は2倍 | 皇帝だけが`remain_hidden` / `declare_solo`を選ぶ |
| 配札 | Jは3人34枚、2人33枚。Pは1枚ずつ取り切り、開始席をdealごとに交代 | 注入rngでshuffleし、dealごとに開始席を1つ進めて全168枚を巡回配札 |
| 最初のlead | P/J/Yは皇帝 | 皇帝が任意の合法組をlead |
| 組 | P/Jは同rank任意枚数＋任意枚数・色のJoker。PはJokerだけの組も明記 | 単枚、同rank複数、同rank＋大小Joker、Jokerだけを許可。straight等はない |
| Jokerの意味 | P/Jは「挂」として組へ加える。比較例は通常札と各Jokerを一対一比較 | 通常rankの枚数へ代入するwildcardではなく、独立した強さを持つ組構成札 |
| 応手枚数 | P/Jは直前組と同枚数 | 枚数違いは`LegalActions()`へ出さない |
| 組比較 | Pは各札が対応する前組札をすべてstrictに上回る場合だけ。Jの小Joker付き比較例も一致 | sorted全要素strict比較。同rank相等、小Joker対小Joker、大Jokerを含む前組は上回れない |
| pass | Pはpass後も別playerが上げれば次巡に再参加できるsoft pass。Yの「過牌不准上」は別系統 | soft passを採用 |
| 場のreset | Pは最後のplay以外が連続passすると、そのplayerが自由lead。上がっていれば次のactive playerが接風 | active player数に応じた連続passでreset |
| 上がりplayer | P/Jは手札をなくすとplayから外れる | 次手番探索から除外する |
| deal終了 | Pはどちらか一方の全員が上がるまで。Jは2対3、暗独保、明独保の早期終了条件を明記 | 2対3は皇帝側2人または一般側3人完了。暗独保は皇帝完了または一般人2人完了。明独保は皇帝完了または一般人1人完了 |
| 未完順位 | 早期終了後はteam得点がすでに一意。個々の残順位は資料上不要 | `IGame`正規化として残playerを席順で補完。team順位和と得点は補完順に依存しない |
| 通常得点 | P/JSは順位2/1/0/-1/-2、皇帝側の和をguardへ、皇帝へその2倍、一般側へ反対符号 | そのまま採用。全player合計は0 |
| 独保得点 | P/JSは1位+12対各-3、2位0、3位以下-12対各+3 | そのまま採用 |
| 倍率順 | P/JSは陣営宣言時にdeal得点を2倍 | 基礎team点→皇帝の役割2倍配分→宣言倍率×2。明保＋造反などを×4にはしない |
| session deal数 | 各資料は1 dealの規則と次dealを記すが、固定5dealは確認できない | 既定1。CLI練習用`deals=1..100`でsession化。旧候補の固定5deal主張を撤回 |
| 上納 | Pは2deal目以降、前deal敗者が最高の非Jokerを伏せて勝者へ渡す。皇帝は2枚、guardは1枚、独保は4枚。引分はなし | 前dealの陣営とplayerを保持し、同じplayer間で自動移送。通常は2+1、独保は4 |
| 返礼・拒否 | Pの採用系統には返礼も抗贡条件もない | actionを設けない。受領先が複数なら席順で一意化 |
| 上納の公開性 | Pは伏せて出す | 他player向け公開履歴にcardを出さず、当事者の新しい自手札だけへ反映 |
| 6最後 | Pは頻出する追加rule、Yは必須、JJ標準は不採用と明記 | 既定OFF。`sixes_last=true`時だけ、手札が6だけになった最後の1組として全6を出す。Joker併用不可 |
| 情報境界 | P/Jは相手手札非公開、皇帝公開、guard/一般人は宣言・marked card playまで非公開 | 下記のView/CPU境界を契約化 |

## 状態機械

```text
deal（168枚を配る）
  ├─ 2deal目以降かつ前deal非引分 → tribute / resolve_tribute
  └─ 初dealまたは前deal引分 ───────┘
                         ↓
emperor_choice / accept_emperor | pass_emperor
  ├─ pass: 印付き大Jokerを次席へ（最大1周）
  └─ accept
       ├─ 独保 → solo_declaration
       └─ 2対3
                         ↓
allegiance（皇帝以外4人が同一action集合から秘密維持または陣営宣言）
                         ↓
play（皇帝lead → play_combo/pass → 連続pass reset → 上がりplayer除外）
                         ↓
一方が到達不能順位になった時点でfinish order補完・deal得点確定
  ├─ deals未達 → 次deal
  └─ deals到達 → finished / Result
```

`LegalActions()`だけが現在playerの選択肢を生成する。`Apply()`は同じ合法手集合で検査した後にだけ、
役職譲渡、宣言公開、上納、手札除去、pass、finish order、得点、deal遷移を変更する。
constructorの初期deal以外に、`ChooseCpuAction()`や`View()`から状態を変更しない。

## 公開情報・非公開情報とCPU

公開するのはphase、deal番号、開始席、確定皇帝、公開済み陣営宣言、現在の組の強さ、最後に
playしたplayer、連続pass数、確定した上がり順、累計得点、各手札枚数である。viewer本人には
自分の全手札と、自分が皇帝・guard・一般人のどれかも示す。

guardの席、未宣言独保、未宣言の各陣営、相手の手札、上納cardの第三者向け内容は非公開である。
guardの席は本人、陣営宣言、印付き小Jokerのplay、session終了のいずれかで必要な範囲へ公開する。
皇帝は自分が印付き小Jokerも持つかは自手札から知るが、通常時にguardの席を特別には知らない。

CPUの陣営判定に秘密の`guard`を使うのは「自分がそのcardを持つか」という自己役職の確認だけである。
既知の味方が直前の組を保持していればpassし、公開上の相手には最小費用で上回り、leadではJokerを
温存して最大の通常同rank組を優先する。宣言は自手札の評価だけで決める。相手手札、秘密guard、
将来のshuffle順は読まない。別player間で秘密guardだけを変え、viewerの`View`を同一に保った状態で
CPU actionが一致するテストを置く。

## CLI正規化・採用外variant

- `deals`は資料上の固定session数ではなく、上納と累積点をCLIで検証するための1～100のローカルoption。既定1である。
- `sixes_last`は地域optionで既定false。instanceごとに保持しglobal状態へ残さない。
- 配札時の実物のshuffle/cut動作は、注入された`DeterministicRandom`によるshuffleとdealごとの開始席交代へ正規化する。
- 伏せ上納は選択肢がなく最高非Jokerが一意なので、`resolve_tribute` 1 actionで全移送を`Apply()`する。返礼はない。
- 216枚、威海165枚、3枚提示partner、印付き大Joker最強、3枚の2で大Jokerを倒す、皇帝cardの2回目以降への添え札、抢独、踢、hard pass、返礼・抗贡は採用しない。

## 固定seed検証

`BaohuangContractTests`は次を独立に検証する。

- 168枚、各通常rank 16枚、大小Joker各4、印付き各1、33/34枚配札、3・4・5不在
- 皇帝cardの譲渡と一巡後強制受諾、guard・独保の秘密表示と公開契機
- 全非皇帝の同一宣言action集合、明保、造反、明暗独保、単一×2倍率
- 単枚、同rank複数枚、通常札＋Joker、同枚数・全札strict比較、不正action拒否
- soft pass後の再参加、全員連続passによるreset
- 通常2対3、造反倍率、独保の1位・2位・3位以下得点表
- 2deal目の2+1または4枚上納、返礼action不在、deal遷移
- `deals` / `sixes_last`のinstance分離、option最小最大、5人境界
- 秘密guardだけが異なる観測同値状態のCPU一致、全phaseでCPUが合法手だけを返すこと
- 1deal/2dealの複数seed完走と同一seed決定性

資料・本仕様・Runtime・上記テストの採用variant内に既知の不一致はないため、Catalogueでは
`Verified`とする。
