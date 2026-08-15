# 候補92件の採用ルール仕様・実装状況

## 1. 目的と正本

この文書は候補台帳92件について、UnityとCLIが再現する採用バリアントと完成状況を固定する。
ゲーム名だけではローカルルール、同時手番、交渉、身体動作、用具が一意に決まらないため、
本基盤では以下の共通契約と候補別表を正本とする。`ゲームID`は公開APIであり、変更しない。

ルールの由来は、候補1～66が
[ゴクラキズムのトランプゲーム一覧](https://gokurakism.com/trump_matome/)、候補67～92が
一般的な伝統ゲーム索引である。追加候補のうち、スピード、GOPS、スパイト・アンド・マリス、
ラミー500、ポーカー、バカラ等は
[Pagatのルール索引](https://www.pagat.com/)、戦争、ブラックジャック、クレイジーエイト、
ゴーフィッシュ、ババ抜き等は
[Bicycleのルール索引](https://bicyclecards.com/how-to-play)を照合先とする。

## 2. 基盤向け正規化

92件はすべてゲーム固有の状態機械を持つ。名称だけの共通トリック／配置処理には
委譲せず、候補別表に記した配札、宣言、交換、複数枚出し、継続戦、得点をそれぞれの
`LegalActions()`と`Apply()`で処理する。一方、物理ゲームをCLIとUnityの逐次入力で
再現するため、次の正規化は採用バリアントの一部とする。

- 同時手番とリアルタイム操作はプレイヤー番号順の手番へ変換する。
- 会話、口笛、立ち上がり、任意の自由文宣言は列挙済み`Action`または表示層の演出へ変換する。
- ビッド、契約、秘密役職はゲーム固有フェーズで選び、CPUは自分の観測可能情報だけで選択する。
- ジョーカー、専用ボード、チップ、裏面でスートが分かるカードは内部状態で表現する。
- 長時間または無期限の実ゲームは、候補別表に記した既定ディール数、目標点、再試行上限を使う。
- ローカルルールは生成時オプションで固定し、インスタンス外の状態へ残さない。
- `CandidateRuleGames`のプロファイルはメタデータ兼フォールバックとして残るが、現在の92件は
  いずれも先に登録した専用型を生成し、`RuleDrivenGame`を使用しない。

## 3. 共通の合法手・終了・得点

全ゲームで、現在手番が選べる操作は`LegalActions()`だけが返す。`Apply()`はその戻り値に
含まれない操作を拒否し、配札後の変更をすべて一箇所で行う。終了前の`Result()`は例外、
終了後は勝者、全員分の得点、理由、手番数を返す。乱数は注入された`DeterministicRandom`
だけを使い、同じseed・人数・オプション・方策なら同じ結果になる。

`View(viewer)`はviewer自身の手札、公開された場・役職・得点、他者の手札枚数だけを表示し、
相手の未公開手札と山札順を表示しない。秘密陣営を持つゲームは、viewer自身の役割または
ルール上公開済みの役割だけを表示する。

`CandidateStatus.RuleSpecific`は上記の専用状態機械を持つことを表す。外部ルールとの
項目別適合確認を終えたことは表さない。現在、`Verified`は個別監査を終えた66件だけで、
残る26件は`RuleSpecific`である。Verified個別監査書の例は[Trump Crew検証仕様](trump-crew.md)、
[Baohuang検証仕様](baohuang.md)、[Napoleon検証仕様](napoleon.md)、
[Card Capture検証仕様](card_capture.md)、[Scoundrel検証仕様](scoundrel.md)、
[御三卿検証仕様](gosankyo.md)、[German Whist検証仕様](german_whist.md)、
[Gin Rummy検証仕様](gin_rummy.md)、[ソノ検証仕様](sono.md)、[Crisp検証仕様](crisp.md)、
[クリベッジ検証仕様](cribbage.md)、[スーパートランプ検証仕様](super_trump.md)、
[2人用大富豪検証仕様](daifugo_two.md)、[ブリスコラ検証仕様](briscola.md)、
[ボヘミアン・シュナイダー検証仕様](bohemian_schneider.md)、[デュラック検証仕様](durak.md)、
[将校スカート検証仕様](officer_skat.md)、[クラバヤス検証仕様](klaberjass.md)、[ゴールドマイン検証仕様](goldmine.md)、[ネイブ検証仕様](knave.md)、[ノルウェージャンホイスト検証仕様](norwegian_whist.md)、
[シュナプセン検証仕様](schnapsen.md)、[ハムレット検証仕様](hamlet.md)、[WHO’S WHO検証仕様](whos_who.md)、
[ミゼルカ検証仕様](mizerka.md)、[シェリフ検証仕様](sheriff.md)、[Farbwechsel検証仕様](farbwechsel.md)、[替え玉トリック検証仕様](kaedama_trick.md)、[Ninety-Nine検証仕様](ninety_nine.md)、[Minimo検証仕様](minimo.md)、[Trick of the Dead検証仕様](trick_of_the_dead.md)、[Corpo検証仕様](corpo.md)、[Truf検証仕様](truf.md)、[Pass Cut Run検証仕様](pass_cut_run.md)、[Finesse検証仕様](finesse.md)、[Yaniv検証仕様](yaniv.md)、[五行相克検証仕様](wuxing_xiangke.md)、[Schmear検証仕様](schmear.md)、[Briscola Chiamata検証仕様](briscola_chiamata.md)、[Portland検証仕様](portland.md)、[Go Fish検証仕様](go_fish.md)、[Old Maid検証仕様](old_maid.md)、[GOPS検証仕様](gops.md)、[Spite and Malice検証仕様](spite_and_malice.md)、[Golf検証仕様](golf.md)、[七並べ検証仕様](sevens.md)、[神経衰弱検証仕様](concentration.md)、[Page One検証仕様](page_one.md)である。各`RuleSpecific`候補の以下の記述は、実装の暫定
プロダクト仕様であって、外部資料との正式照合完了記録ではない。
[Rummy 500検証仕様](rummy_500.md)、[Euchre検証仕様](euchre.md)、
[Oh Hell検証仕様](oh_hell.md)、[Baccarat検証仕様](baccarat.md)、
[Black Lady検証仕様](black_lady.md)、[Four Tricks検証仕様](four_tricks.md)である。全66件の正本は
第7節の正式照合記録からリンクする各個別監査書とする。

## 4. 候補1～66

`方式・初期`は採用実装の中心方式と配札枚数またはセッション長を示す。詳細は同じ行の
中心ルールおよび登録時オプションを優先する。

| No. | ゲームID | 名称 | 人数 | 方式・初期 | 採用する中心ルール |
|---:|---|---|---:|---|---|
| 1 | `card_capture` | Card Capture | 1 | 検証済・専用 | 2～4とJokerの個人deckを循環させ、同suit合計で場の敵札を捕獲する。捕獲札をdeckへ加えながら全A・絵札を含む敵deckの完走を目指す。詳細は[検証仕様](card_capture.md)。 |
| 2 | `scoundrel` | Scoundrel（悪党） | 1 | 検証済・4室 | 黒札をmonster、diamondを武器、heartを1室1回の回復として4枚の部屋から3枚ずつ処理する。逃げた部屋の再配置と武器の弱化を適用し、最後まで生存するか残体力を競う。詳細は[検証仕様](scoundrel.md)。 |
| 3 | `gosankyo` | 御三卿 | 1 | 検証済・12 | suitだけ見える仮想相手2人と36枚・12trickを行う。4、5、6、7勝の各bidを1回ずつ、失敗せず連続達成すれば完走。詳細は[検証仕様](gosankyo.md)。 |
| 4 | `german_whist` | ジャーマンホイスト | 2 | 検証済・13 | 前半は勝者が表札、敗者が伏札を補充。山切れ後の13トリックを多く取った側が勝つ。詳細は[検証仕様](german_whist.md)。 |
| 5 | `gin_rummy` | ジン・ラミー | 2 | 検証済・10 | 山か捨て札から引いて1枚捨て、セット／ラン以外10点以下でノック。100点先取。詳細は[検証仕様](gin_rummy.md)。 |
| 6 | `sono` | ソノ | 2 | 検証済・10 | A・9・10・J・Q・Kとジョーカーの25枚。10枚ずつを非公開手札、5枚を対角線の伏札とし、既存札に隣接して交互配置する。P0は5縦列、P1は5横列のポーカー役と赤・黒・数札・絵札クランを得点化する。詳細は[検証仕様](sono.md)。 |
| 7 | `crisp` | Crisp | 2 | 検証済・12 | 2～10・Qの40枚から12枚ずつ配る。単札、ペア、3枚以上のラン、2組以上のペアラン、特別役の3/4枚組で応酬し、パス時は直前のプレイヤーが表札か伏札を選んで補充する。1ディール1点、3点先取。詳細は[検証仕様](crisp.md)。 |
| 8 | `cribbage` | クリベッジ | 2 | 検証済・6 | 各6枚から2枚ずつdealerのcribへ捨て、31を超えないペギングで15、31、連続ペア、ラン、go/lastを得点化する。showは15、ペア、最長ランの重複、flush、nobをnon-dealer、dealer、cribの順に数え、121点先取。詳細は[検証仕様](cribbage.md)。 |
| 9 | `super_trump` | スーパートランプ | 2 | 検証済・13 | non-dealerが切り札スート、dealerが全切り札より強いsuper rankを選ぶ。マストフォローし、前半は勝者が表札・敗者が伏札を補充して各1点、後半は補充なしで各2点、全39点を争う。詳細は[検証仕様](super_trump.md)。 |
| 10 | `daifugo_two` | 2人用大富豪 | 2 | 検証済・16 | 3～6を除いて強さの異なるジョーカー2枚を加えた38枚。単札、同数2～4枚、3枚以上の連番を同型で上回り、4枚組で革命する。パス者は山から1枚補充し、上がり時の相手残数を得点として30点先取。詳細は[検証仕様](daifugo_two.md)。 |
| 11 | `briscola` | ブリスコラ | 2 | 検証済・3 | 40枚、メイフォロー、切り札あり。A=11、3=10、K=4、Q=3、J=2で61点以上を狙う。詳細は[検証仕様](briscola.md)。 |
| 12 | `bohemian_schneider` | ボヘミアン・シュナイダー | 2 | 検証済・6 | 7～Aの32枚を6枚ずつ配り、メイフォローする。応手がリードと同スートの直上rankのときだけリードを奪い、勝者から補充する。A・K・Q・J・10の20 honorで通常・Schneider・Schwarz点を7点まで争う。詳細は[検証仕様](bohemian_schneider.md)。 |
| 13 | `piquet` | ピケ | 2 | 監査差分あり・12 | 7～Aの32枚。elderが1～5枚、youngerが残talonまで交換し、point、sequence、setを比較宣言する。切り札なし12トリック、carte blanche、repique/pique、7勝・全勝bonusを含む6ディール戦。未解決差分は[監査記録](piquet.md)を参照。 |
| 14 | `durak` | デュラック | 2 | 検証済・6 | 6～Aの36枚。攻撃札と同ランクの札を最大6枚まで追加でき、防御側は同スート上位または切り札で各札を覆うか全札を拾う。攻撃側から6枚へ補充し、山切れ後に先に上がった側が勝つ。詳細は[検証仕様](durak.md)。 |
| 15 | `officer_skat` | 将校スカート | 2 | 検証済・16 | 7～Aの32枚を各8列の伏札＋表札へ配置する。non-dealerが切り札を選び、J4枚を最上位切り札、宣言スートを続く切り札として公開先端からマストフォローし、16トリックの全120カード点の過半数を争う。60対60は守備側勝利。詳細は[検証仕様](officer_skat.md)。 |
| 16 | `klaberjass` | クラバヤス | 2 | 検証済・9 | 7～Aの32枚。候補スートのtake/pass後、任意suit指定を1巡し、9枚手札でsequenceを点数・最高rank・切札の順に公開照合する。J・9が最強の切り札、切り札7交換、bella、最終10点、maker成否を含め500点先取。詳細は[検証仕様](klaberjass.md)。 |
| 17 | `norwegian_whist` | ノルウェージャンホイスト | 2 | 専用・26 | 各自10枚の非公開手札と8列の伏札＋表札を持つ。non-dealerからhigh/lowをビッドし、1人2枚ずつ交互に出す4枚トリックを13回行う。high契約成否またはlowの少数勝で得点し13点先取。 |
| 18 | `schnapsen` | シュナプセン | 2 | 専用・5 | A・10・K・Q・Jの20枚。山が開いている間はメイフォロー、閉鎖後は可能なら同スートで勝ち、次に同スート、切り札の順で強制する。K/Qマリッジ、切り札J交換、山札クローズ、66点宣言を実装し、7ゲーム点先取。 |
| 19 | `goldmine` | ゴールドマイン | 2 | 検証済・6 | H・C・Dの2～7を6枚ずつ配り、Sの2～7を伏せた金塊列にする。各trick前に一方が任意金塊を調査または手札交換し、他方が残る操作を行う。後手がleadし、follow義務なしで順番の金塊を獲得し30点先取。詳細は[検証仕様](goldmine.md)。 |
| 20 | `knave` | ネイブ | 3 | 検証済・17 | 52枚を各17枚と表向きtrump1枚に分け、dealer左からマストフォローする。各trick+1と獲得JのH=-4/D=-3/C=-2/S=-1を合算して20点先取。詳細は[検証仕様](knave.md)。 |
| 21 | `hamlet` | ハムレット | 3 | 専用・11 | 7～Aの32枚とJokerを11枚ずつ配り、各人が秘密に選んだ非Joker1枚で切り札とto-be/not-to-beを決める。マストフォローで、Jokerはleadなら最強、応手ではvoid時だけ出せて最弱。11トリック後の中間勝数（同数2人なら残る1人）をHamlet役とし、モード別倍率で得点して250点先取。 |
| 22 | `whos_who` | WHO’S WHO | 3 | 専用・14 | 5～Aの40枚とJoker2枚を14枚ずつ配る。Jokerを2枚持つ者、またはJokerを持たない者が単独側となる。マストフォローで通常はlead suitの2番目に強い札が勝ち、Jokerを含むtrickはJoker保持者（2枚なら単独側）が獲得者を指定する。単独側の勝数が一意な中間値、または相手2人が同数なら単独側成功として10点＋単独側勝数を加え、100点先取。 |
| 23 | `farbwechsel` | Farbwechsel | 3 | 検証済・11 | Joker・2・3を除く44枚。各人11枚と公開切り札列11枚に分け、0～11の獲得予想を秘密に記録する。第1trickのleadは切り札スートを強制し、以後は直前勝者が自由にleadするマストフォロー戦。各trickで公開列先頭のスートが切り札となり、その表示札も勝者が得る。予想的中20点と獲得Q/J/10各1点で100点先取。詳細は[検証仕様](farbwechsel.md)。 |
| 24 | `sheriff` | シェリフ | 3 | 検証済・7 | A・K・Q・J・10の20枚とJokerを7枚ずつ配り、Joker保持者から市長・保安官・強盗を重複なく選ぶ。市長が切り札またはno-trumpを決めてleadし、Jokerはいつでも出せるが必ず負ける。保安官は獲得K、強盗は獲得10、市長は獲得Q/Jから未逮捕Kと強盗の10を引いた非負点を得て8点先取。詳細は[検証仕様](sheriff.md)。 |
| 25 | `mizerka` | ミゼルカ | 3 | 専用・13 | 52枚。最初に各人6枚とtalon6枚を配り、dealer左が未使用のC/D/H/S/no-trump/misereから契約を選んだ後、各13枚とtalon13枚まで配る。chooser、dealer右、dealerの順にtalonが残る範囲で0～13枚を交換し、chooserからマストフォロー13trickを行う。通常契約は勝数－席別基準7/5/1、misereは基準1/5/7－勝数を得点とし、各人が6契約を選ぶ18ディール戦。 |
| 26 | `ninety_nine` | ナインティナイン | 3 | 検証済・9deal | 6～Aの36枚を12枚ずつ配り、C=3・H=2・S=1・D=0として手札3枚を秘密bidへ除外し、残る9枚でexact tricksを狙う。初回no-trump、以後は前ディール成功者3/2/1/0人に応じC/H/S/Dを切り札とする。declareはbid公開、revealは残手札も公開し、成功者だけが精算時にbidを公開する。成功人数別10/20/30点、trick各1点、premium30/60点を9ディール合計する。詳細は[検証仕様](ninety_nine.md)。 |
| 27 | `five_hundred` | ファイブハンドレッド | 3 | 専用・10 | 7～Aの32枚とJokerを各10枚＋kitty3枚に配る。6～10 tricksのS/C/D/H/no-trump、misere、open misereを得点順にauctionし、落札者はkittyを取って3枚戻す。Joker、right bower、同色Jのleft bowerを上位切り札とするマストフォロー10trick戦。落札成功は40～520点、失敗は同点減点、守備側はtrick×10点を得て、+500または-500到達で終了。 |
| 28 | `skat` | スカート | 3 | 専用・10 | 7～Aの32枚を各10枚＋Skat2枚に3-2-4-3で配る。昇順の数値auctionで単独者を決め、Skatを取って2枚戻すゲームまたはHandを選び、D/H/S/C/Grand/Null/Null OuvertとHand時のSchneider・Schwarz・Open宣言を行う。Suit/GrandではJ4枚を最上位切り札として120 card pointsの61点、Nullでは全trick回避を目指し、matador・Hand・Schneider・Schwarz倍率とoverbid、失敗時倍額を採点する。既定18ディール。 |
| 29 | `gooseberry_fool` | グズベリー・フール | 3 | 専用・11 | 7～Aの32枚とJoker1枚を11枚ずつ配る。マストフォローし、3枚が同スートなら中位、2枚だけ同スートなら異なるスート、全て異なるなら異なる色の札がtrickを取る。Jokerは最初にvoidになった時に強制され、その保持者が獲得者を指定する。自分の勝数＋右隣の勝数×2を得て、ディール中央値へ10点を加算し100点到達後の中央値を勝者とする。 |
| 30 | `ulti` | ウルティ | 3 | 専用・10 | 7～Aの32枚をdealer右12枚、他10枚に配り、最初の2枚捨てをtalonとして、以後はtalonを取って2枚戻すたび上位契約へ競り上げる。simple、40-100、20-100、Ulti、Betli、Durchmarsとheart/openの複合契約を収録。マストフォロー、void時マストトランプ、可能ならマストウィンし、A/10・最終trick・K/Q marriage、切り札7の最終trickを成分別に採点する。基礎stake（kontraなし）の既定12ディール。 |
| 31 | `four_tricks` | フォートリックス | 3 | 検証済・12×3 | 6～Aの36枚でno-trump/must-followを行い、最終trickを2勝扱いにする。4勝10点を頂点とする完全得点表で3 dealsを競う。詳細は[検証仕様](four_tricks.md)。 |
| 32 | `italian_whist` | イタリアン・ホイスト | 3 | 専用・18→9+9 | 52枚と色違いJoker2枚を18枚ずつ配り、各自が後半用9枚を第1/4dealは左、第2/5dealは右、第3/6dealは自分へ渡す。前半はdealer左lead、後半はdealer右leadのマストフォローで、前半勝数－後半勝数を得点化する。前3dealはno-trump、後3dealはspade切り札。Jokerは同色をfollowし、公開済みtrickで複数宣言が同値になる場合は最強になるスート・未出rankを決定論的に選ぶCLI正規化を採用する。 |
| 33 | `minimo` | ミニモ | 3 | 検証済・3 | spade/heartの2～6を各3枚＋伏せ札1枚に配る。各dealのante1、dealer右のdouble、no-trump・must-follow3trickを行う。2-1-0の1勝者はpot、1-1-1はcarry、3-0-0はsweep者が追加1chipを払い、double時の追加支払を適用する。deal後に0chip者が出た時の最多chipを勝者とする。詳細は[検証仕様](minimo.md)。 |
| 34 | `kaedama_trick` | 替え玉トリック | 3 | 検証済・10 | 8～Aの28枚とJoker2枚を10枚ずつ配り、spadeとJokerを切り札とするマストフォロー戦。最初にJokerを出した怪人二十面相と、残るJoker保持者の明智探偵・Jokerなしの小林少年を、76点、探偵間の大小・30点差で判定する。怪人がJoker2枚なら101点以上を敗北とする別条件を使い、勝利側へ規定カード点（最低10点）を与える9ディール戦。詳細は[検証仕様](kaedama_trick.md)。 |
| 35 | `trick_of_the_dead` | Trick of the Dead | 3 | 検証済・7→1+6 | 3スートの3～9・K、計24枚から各7枚を配る。前半6trickはメイフォロー・スート無関係の高rank勝ちで各1点とし、低rankを出した順に場の3枚から1枚をZombie札として伏せて回収する。残り1枚へZombie6枚を加えた後半7trickはKを固定切り札とするマストフォローで各2点。合計点の1の位が最大の者が勝つ。詳細は[検証仕様](trick_of_the_dead.md)。 |
| 36 | `corpo` | コルポ | 3 | 検証済・14→9+5 | 2・3を除く44枚から各14枚を配り、Poker用5枚を伏せる。残る9枚でspade切り札・マストフォローを行い、Colpo宣言者は7勝を狙う。無宣言で7勝者がなければ、straightとflushを除く5枚Pokerを全員公開して比較する。宣言成功は勝数、失敗は宣言者-7・他者各勝数、無宣言7勝は5点、Poker勝者は勝数（0勝なら3点）を得て15点先取。詳細は[検証仕様](corpo.md)。 |
| 37 | `tanuki` | たぬき | 3 | 専用・12 | 6～Aの36枚を12枚ずつ配る。dealer左は切り札、dealerはminus、dealer右はplusのスートを秘密選択する。1～3ディールはメイフォロー、4～6はマストフォロー、7～9は再びメイフォロー。off-suitの切り札が勝敗へ関与した時だけ切り札を公開する。plusカードは+1、minusは-1、同スート同色は+1、反対色は-1として9ディール合計を競う。 |
| 38 | `multi_stack` | マルチスタック | 2～4 | 専用・公開4 | 公開4枚手札から±1の共有stackへ人数別の役割制限で1枚以上出す。0～8枚まで補充し、1枚譲渡でき、Jで役が巡回する（3～4人はdummy込み4役、2人は同色/色交互の2役）。全員が出し切れば成功、全員連続停滞で失敗。 |
| 39 | `dubito` | ドゥビトー | 1～4 | 専用・8 | 2組104枚を8枚手札にし、昇順、同スート、同スート昇順、同rankの個人4列へ1枚置いて補充する。置けない者は終了し、列ごとの1～4倍点を競う。 |
| 40 | `three_tricks` | スリートリックス | 4 | 専用・13 | 切り札なし。0勝-5、1～3勝は勝数の二乗、4勝以上は勝数の負値。4ディール合計。 |
| 41 | `mini_misere` | ミニミゼール | 3～6 | 専用・5～7 | 人数別21/25/36枚で5～7trickを行う。2はlead時だけ最強、Jokerはlead勝ち・応手負け（3人の最後手は勝敗指定）。0勝と全勝直前を高得点とし、4～6人は全勝Lotも宣言できる。 |
| 42 | `agony_aunt` | アゴニーアント | 4 | 専用・13 | 公開dump札と同一札になるJokerを含む53枚戦。dump suit Q、Joker、4枚のQ、最終・最多・dump番trickの9罰点を3×3盤へ置き、3目列の追加損失と全勝・全敗・無罰回復を17chipで追跡する。 |
| 43 | `collusion` | コルージョン | 4 | 専用・13 | 52枚no-trumpの13trick。勝数各1点に、同数2人各10、全員別なら最少者20、同数3人なら残る1人30を加え100点を競う。自由会話は表示層で扱う。 |
| 44 | `confirmation` | コンファメーション | 4 | 専用・10 | A～10の40枚を10枚ずつ配り9trickを行う。唯一のfollow札を公開保護してoff-suitを出せ、最後の1枚（A=1、10=0）が目標勝数となる。勝数各1点と秘密的中10／公開的中5点を4deal集計する。 |
| 45 | `big_two` | 大老二 | 4 | 専用・13 | 3Cを含む組から開始し、2>A>…>3・S>H>D>Cで単枚、pair、triple、5枚のstraight/flush/full house/four+1/straight flushを競る。上がり時の残札・2・8枚以上罰則を精算する。straightは2を含めない。 |
| 46 | `triple_crown` | トリプルクラウン | 4 | 専用・13 | AS保持者は5勝以上のHigh、2D保持者は0勝のLow、他2人は両者の失敗量を得るTeam Crown。両札保持者はHigh/Lowを秘密選択して切り札を指定し、達成5点、失敗時は他3人へ不足量の2倍を与える。 |
| 47 | `doppelkopf` | ドッペルコップ | 4 | 専用・12 | 9～Aを2組使う48枚。heart10、全Q/J、diamondを通常切り札、club Q所持者をRe陣営とする。Marriage、Poverty交換、suit/queen/jack soloを宣言し、240点中121点とSchneider段階を8deal精算する。Re/Kontra宣言と特殊札bonusは採用外。 |
| 48 | `guillotine` | ギロチン | 4 | 専用・24deal | 7～Aの32枚で各dealerがRoyalty、Queens、Spades、Parliament、Guillotine、Dominoを1回ずつ選ぶ。前5契約はno-trump罰点、Dominoは同rank別suit／同suit隣接とA連続出しで先着-30/-10を得て、総点最少を競う。 |
| 49 | `sasaki_44a` | 44A（ササキ） | 4 | 専用・12 | 2を除く48枚で赤10保持側対他方を隠して単枚・pair・3枚以上straightを競る。triple、4+4+A、four、赤豚、黒豚、単枚への「ける／さす」、走る／止まれ倍率と1対3を含む順位精算を扱う。 |
| 50 | `schafkopf` | シャーフコップ | 4 | 専用・8 | 7～Aの32枚でPartner、Wenz、Suit Soloと各Toutをauctionする。PartnerはQ/J＋heart固定切り札と非切り札A指名、SoloはQ/J＋指定suit、WenzはJのみ。61点、Tout全勝、Schneider/Schwarzを8deal精算する（Stossなし）。 |
| 51 | `the_trick` | ザ・トリテ | 3～4 | 専用・12 | spade固定切り札。3人は勝数8/4/0と残札＋伏札4スート、4人は6/4/2/0と残札4スートを全員で達成する。相手のrankは隠しつつsuit背面情報を公開する協力戦。 |
| 52 | `truf` | トルフ | 3～4 | 検証済・13deal | 各自が手札1枚を秘密bidし、最高bid札のsuitを切り札、合計13超をatas・未満をbawahとする。13なら最高bidderが全bidを同量増減する。切り札はbreak前lead不可・trick終了まで伏せ、正差2倍／負差そのまま、調整後0bid成功+5を集計する。3人はclubを除く39枚。詳細は[検証仕様](truf.md)。 |
| 53 | `pass_cut_run` | パスカットラン | 4 | 検証済・13 | 隣席partnerへ受取前に2枚ずつ渡し、dealer最終札suitを切り札とする。各leaderからpartnerが必ず4番手になる方向で出し、partner勝ちPass4、対面Cut3、他隣Cut2、自勝ちRun1を4deal集計する。詳細は[検証仕様](pass_cut_run.md)。 |
| 54 | `finesse` | フィネス | 4 | 検証済・13+公開3 | 52枚＋J/Q/K複製12枚。lead時は自分の手札かpartnerの公開table札を指定し、初lead suitを切り札とする。table使用後は所有者が補充し、勝数曲線から残table切り札を減点後、最終4点を加えて42点を争う。詳細は[検証仕様](finesse.md)。 |
| 55 | `yaniv` | ヤニブ | 2～8 | 検証済・5 | 単札、同rank組、同suit3枚以上の連番（Joker代用可）を捨て、山札または直前組の端から1枚引く。5点以下で全手札を公開してYanivを宣言し、失敗+30、50/100ちょうどの減点を適用して101点到達時の最少失点を競う。詳細は[検証仕様](yaniv.md)。 |
| 56 | `trump_crew` | トランプクルー | 3～5 | 検証済・stage制 | 52枚＋Joker。stage1から手札を1枚ずつ増やし、余り1枚でtrumpを決める（Jokerならno-trump）。dealerの主観的な強・中・弱宣言後、左隣からbidしdealerがstage数までの残数を引き受ける。Jokerは常時出せて最強、lead時はsuit指定または無指定を選ぶ。通常札はmust-follow。全員exactなら次stage、失敗ならdealerを替えて同stageを無制限に再挑戦する。3～5人制限、`final_stage`短縮、任意の`max_attempts`は明記した基盤ローカル仕様。詳細は[検証仕様](trump-crew.md)。 |
| 57 | `baohuang` | 保皇 | 5 | 検証済・168 | 山東・日照系。4組から3/4/5を除く160枚＋大小Joker各4。印付き大Jokerの皇帝と印付き小Jokerのguardが秘密teamとなり、同rank組＋Jokerを同枚数・全札上位で重ねるsoft-pass戦。順位2/1/0/-1/-2をteam合算し皇帝へ2倍配分、陣営宣言時は全deal点を2倍にする。2deal目以降は前deal敗者から通常2+1枚、独保4枚を上納する。sessionは既定1dealで`deals` optionにより延長する。詳細は[検証仕様](baohuang.md)。 |
| 58 | `wuxing_xiangke` | 五行相克 | 5 | 検証済・10 | 52枚を各10枚＋公開2枚。最初の公開札suitを仮lead、spade固定trumpとする。A/K/Q/J/10各1点。公開得点札2枚なら非隣接2人、0～1枚なら右隣のさらに右隣1人を一方向partnerとし、12との差またはpartner点との関係で5deal採点する。詳細は[検証仕様](wuxing_xiangke.md)。 |
| 59 | `schmear` | シュミア | 5～6 | 専用・6 | Pagat St Paul版。6人は52枚＋Joker、5人は2・3を除く44枚＋Joker。3～6をbidし、非trumpを最大3枚交換する。5人はcard指名partner、6人は交互3対3。teamのHigh/Low/正J/裏J/Joker/Gameを21点まで争う。 |
| 60 | `briscola_chiamata` | ブリスコラ・キアマタ | 5 | 専用・8 | 8・9・10を除く40枚。A>3>K>Q>J>7>6>5>4>2を弱い方向へhard-pass auctionし、declarerがtrumpを決める。bid rankのtrump所持者を秘密partnerとしてmust-followで61点を狙い、単独±4／2対3は±2・±1、全trick倍で11点を争う。 |
| 61 | `briscola_bugiarda` | ブリスコラ・ブジャルダ | 5 | 専用・8・メイ | キアマタと同じ40枚、rank auction、秘密partnerを使うがfollow義務なし。declarer側のcard pointを61～70から120まで7段階（敗北側も対称）でchip精算し、5deal合計を競う。 |
| 62 | `goninkan` | ゴニンカン | 5 | 専用・10 | spade以外の2を除く49枚＋Joker。初戦はC/D/Hを3巡し最終roundはS、Joker所持者とtrump A所持者（同一なら2席先）がカンケイ。絵札9枚、勝てばtrump選択の第2戦8枚、第3戦9枚へ進み、各勝敗±1・三タテ+1で10roundを競う。 |
| 63 | `portland` | ポートランド | 2～5 | 専用・個人52 | 各自が独立した52枚deckを6round通して使う。各roundは5枚を公開し、1枚ずつめくって5枠を上書きするかpassする。全員pass後にPoker役で順位を決め、（人数－順位）×round数を得る。5枚を用意できなければ最弱。 |
| 64 | `napoleon` | ナポレオン | 4～7（5推奨） | 専用・12/10/8/7 | 52枚＋Joker 1枚。既定12～20枚とtrump suitをsoft-passで競り、任意cardの秘密副官を指名して5/3/5/4枚のwidowを交換する。printed-suit must-follow、Mighty、正J、裏J、Joker切札請求、Same 2、optionのよろめきで絵札20枚中の契約数を集め、既定5deal勝先取。 |
| 65 | `toepen` | ツーペン | 2～8 | 専用・4 | 7～Aの32枚。4枚交換（challenge可）後、10>9>8>7>A>K>Q>Jのno-trump・must-followを4trick行う。knockごとにstakeを上げ、foldは現在stake、最終trick敗者は確定stakeを失い、10失点者が出るまで続ける。口笛・起立は表示演出外。 |
| 66 | `black_lady` | ブラックレディー | 3～7 | 検証済・1巡 | 右へ2枚、その受領後に1枚をpassし、heart各-1、spade Q-13。減点札0枚のclear者がcarry込み26点を分ける。詳細は[検証仕様](black_lady.md)。 |

## 5. 追加候補67～92

| No. | ゲームID | 名称 | 人数 | 方式・初期 | 採用する中心ルール |
|---:|---|---|---:|---|---|
| 67 | `war` | 戦争 | 2～4 | 専用 | 全員が先頭札を公開しA高の単独最高者が場札を獲得。同値は既定1枚を伏せて再戦する。最後の所持者が勝者で、`war_down_cards`と安全用`max_turns`を変更できる。 |
| 68 | `blackjack` | ブラックジャック | 1～5＋dealer | 専用 | hit、stand、double、split、insuranceを選ぶ。Aは1/11、絵札10。natural、split A、dealer soft 17等を生成時オプションで固定し、バストせずdealerを上回る。 |
| 69 | `crazy_eights` | クレイジーエイト | 2～5 | 専用・5/7 | 同スートか同ランクを出し、8は次スートを指定。出せなければ引き、先に手札0で勝つ。 |
| 70 | `go_fish` | ゴーフィッシュ | 2～5 | 専用 | 所持ランクを相手へ要求し、不所持なら山から引く。同ランク4枚組を最も多く集めた者が勝つ。 |
| 71 | `old_maid` | ババ抜き | 2～6 | 専用 | Qを1枚除き、初期ペアを捨てる。次席の非公開手札から引いてペアを捨て、最後の1枚を持つ者が負ける。 |
| 72 | `speed` | スピード／スピット | 2 | 専用・4場札 | 各自deckと4枚の場札を使い、中央2山の上下1rankへ出す同時進行を決定論的な交互入力に正規化する。両者が出せないと中央を更新し、先に個人札をなくす。 |
| 73 | `gops` | GOPS | 2 | 専用・13 | Diamondを賞点札、SpadeとClubを同一構成の入札札とし、各roundの伏せ入札をP0、P1の順次入力後に同時公開する。高rank側が賞点を得て、同点賞点は次roundへ繰り越す。 |
| 74 | `spite_and_malice` | スパイト・アンド・マリス | 2 | 専用・2組 | 2組104枚から各20枚の支払い山と5枚手札を持ち、中央3列をAからQへ昇順配置する。手札補充と個人4脇山を使い、支払い山を先になくす。 |
| 75 | `casino` | カシノ | 2～4 | 専用・4 | 場札の同rank取り、合計取り、単一buildを行う。配り切り後に最多札3、最多spade1、A各1、10D=2、2S=1の全11点を採点し21点先取。 |
| 76 | `golf` | ゴルフ | 2～6 | 検証済・9hole | Pagat Six-card版。6枚を2×3に伏せ2枚公開し、山または捨札から1枠を交換する。同rank縦pairは0点、A=1、2=-2、K=0として9hole合計最少を競う。詳細は[検証仕様](golf.md)。 |
| 77 | `sevens` | 七並べ | 3～8 | 検証済 | ジョーカーなし・A/K非接続・3回pass版。4枚の7を起点に伸ばし、4回目passの失格札は孤立を保って公開する。詳細は[検証仕様](sevens.md)。 |
| 78 | `concentration` | 神経衰弱 | 2～6 | 検証済・52札 | 未獲得の位置を2つ選び、同rankなら組を獲得して続行、不一致なら公開後に伏せて次手番へ移る。全26組取得後の組数最大を勝者とする。詳細は[検証仕様](concentration.md)。 |
| 79 | `cheat` | ダウト | 3～6 | 専用・7 | AからKへ順に1～4枚を伏せてrankを宣言し、他者全員のchallenge機会を逐次処理する。虚偽なら出し手、真実ならchallenge者が山を取り、手札0を目指す。CPUは公開情報だけで判断する。 |
| 80 | `page_one` | ページワン | 2～6 | 検証済・4 | 古典trick-taking版。must-followし、出せなければ同suitが出るまで引く。Jokerと残り1枚時のPage One宣言／未宣言5枚罰を扱う。詳細は[検証仕様](page_one.md)。 |
| 81 | `seven_bridge` | セブンブリッジ | 2～6 | 専用・7 | 7枚からset／同suit runをmeldし、公開meldへの付け札を行う。捨札へのpon優先とchiを順次応答で解決し、最後の1枚を捨てて上がる。 |
| 82 | `rummy_500` | ラミー500 | 2～8 | 検証済・7/13 | Joker/Rummy callなし版。山または捨札を遡って取り、最下札を即時新規meldする。公開meld点から残手札点を引き、500点以上の単独首位まで続ける。詳細は[検証仕様](rummy_500.md)。 |
| 83 | `canasta` | カナスタ | 4（採用） | 専用・11・固定ペア | 108枚、捨札山常時凍結の4人Classic Canasta。赤3、累積点別の初回meld下限、自然／混成7枚Canasta、上がりを得点化し5000点先取。 |
| 84 | `pinochle` | ピノクル | 4（採用） | 専用・12・固定ペア | 9～A各2枚の48枚でauctionし、marriage、run、around、dix等をmeldする。宣言trumpでmust-follow・must-trump・可能なら勝ちを適用し、meld＋trick点で契約を判定する。 |
| 85 | `hearts` | ハーツ | 3～6 | 専用 | must-followし、heart各1点とspade Q13点を避ける。heart lead制限とshoot the moonを適用し、誰か100失点到達時の最少失点を競う。 |
| 86 | `spades` | スペード | 4 | 専用・13・固定ペア | 1～13を各自bidし、spade固定trumpのmust-followを行う。team契約成功10倍＋超過bag、失敗0点、10bag罰を累積して200点を争う。Nilは実装しない。 |
| 87 | `euchre` | ユーカー | 4 | 検証済・5・固定ペア | Bicycleの24枚・10点戦。3枚＋2枚を配り、2段階のtrump指定、right/left bower、alone、march、euchreを採点する。詳細は[検証仕様](euchre.md)。 |
| 88 | `oh_hell` | オーヘル | 3～7 | 検証済・可変手札 | Pagat標準の人数別最大手札から1枚へ降順、その後昇順に配る。dealer hookを適用し、実trick点＋bid的中10点を集計する。詳細は[検証仕様](oh_hell.md)。 |
| 89 | `texas_holdem` | テキサスホールデム | 2～10 | 専用・2＋共通5 | blind、preflop／flop／turn／riverの4 betting street、fold/call/raiseとsideを含まない単一potを扱い、7枚から最強5枚Poker役を比較する。 |
| 90 | `five_card_draw` | ファイブカードドロー | 2～6 | 専用・5 | ante後のbetting、0～3枚の1回交換、2回目bettingを行い、残存者の5枚Poker役でpotを分配する。 |
| 91 | `baccarat` | バカラ | 1～8＋banker | 検証済・単一coup | 8-deckオンラインPunto Banco。各自がplayer、banker、tieへ1 unit賭け、固定第三札表と1:1／19:20／8:1の純損益を精算する。詳細は[検証仕様](baccarat.md)。 |
| 92 | `twenty_four` | 24 | 2～8 | 専用・4 | 公開4数を各1回使い、加減乗除と括弧で24を作れるかclaim/passする。全結合順と演算を有理数で完全探索し、正誤を採点して目標点を争う。 |

## 6. Verifiedへの昇格条件

台帳の一意性、専用実装の登録、CPU完走、共通契約テストは必要条件ではあるが、単独では
`Verified`の根拠にしない。候補を昇格するには、当該ゲームについて次をすべて満たす。

1. `docs/rules/<game-id>.md`に、直接参照できる資料URL、参照日、採用variant、公式資料が
   ない場合のその事実と信頼できる代替資料を記録する。
2. 人数、使用カード、配札、開始状態、全フェーズ、合法手、特殊札・例外規則、勝敗、得点、
   終了条件、ローカルルールを項目別の照合表で資料と実装へ対応付ける。
3. CLI/Unityの逐次操作へ正規化した箇所ごとに、理由と、失われない選択肢を記録する。
4. 実装を資料と照合し、差分を修正する。固定seedテストには少なくとも主要フェーズ、得点又は
   終了、固有例外を追加する。秘密情報があるゲームにはViewとCPUの観測同値テストも追加する。
5. 文書・実装・テストがそろい、全テストを通して未解決差分がないことを確認した後にだけ
   `GameCatalogue`の`VerifiedIds`へ当該IDを追加する。

監査の順序と各単位の完了記録は[Verified監査計画](verification-audit-plan.md)に置く。残る31件の
個別監査が完了するまでは、台帳上の`Verified`数だけを完了宣言の根拠にしない。

## 7. 現在の正式照合記録

| Status | ゲームID | 個別照合書 | 状態 |
|---|---|---|---|
| `Verified` | `trump_crew` | [trump-crew.md](trump-crew.md) | 資料・実装・固定seed・観測同値を個別に照合済み |
| `Verified` | `baohuang` | [baohuang.md](baohuang.md) | 資料・実装・固定seed・観測同値を個別に照合済み |
| `Verified` | `napoleon` | [napoleon.md](napoleon.md) | 資料・実装・固定seed・観測同値を個別に照合済み |
| `Verified` | `card_capture` | [card_capture.md](card_capture.md) | 資料・実装・固定seedを個別に照合済み |
| `Verified` | `scoundrel` | [scoundrel.md](scoundrel.md) | 資料・実装・固定seedを個別に照合済み |
| `Verified` | `gosankyo` | [gosankyo.md](gosankyo.md) | 資料・実装・固定seed・観測同値を個別に照合済み |
| `Verified` | `german_whist` | [german_whist.md](german_whist.md) | 資料・実装・固定seed・観測同値を個別に照合済み |
| `Verified` | `gin_rummy` | [gin_rummy.md](gin_rummy.md) | 資料・実装・固定seed・観測同値を個別に照合済み |
| `Verified` | `sono` | [sono.md](sono.md) | 資料・実装・固定seed・観測同値を個別に照合済み |
| `Verified` | `crisp` | [crisp.md](crisp.md) | 資料・実装・固定seed・観測同値を個別に照合済み |
| `Verified` | `cribbage` | [cribbage.md](cribbage.md) | 資料・実装・固定seed・観測同値を個別に照合済み |
| `Verified` | `super_trump` | [super_trump.md](super_trump.md) | 資料・実装・固定seed・観測同値を個別に照合済み |
| `Verified` | `daifugo_two` | [daifugo_two.md](daifugo_two.md) | 資料・実装・固定seed・観測同値を個別に照合済み |
| `Verified` | `briscola` | [briscola.md](briscola.md) | 資料・実装・固定seed・観測同値を個別に照合済み |
| `Verified` | `bohemian_schneider` | [bohemian_schneider.md](bohemian_schneider.md) | 資料・実装・固定seed・観測同値を個別に照合済み |
| `Verified` | `durak` | [durak.md](durak.md) | 資料・実装・固定seed・観測同値を個別に照合済み |
| `Verified` | `officer_skat` | [officer_skat.md](officer_skat.md) | 資料・実装・固定seed・観測同値を個別に照合済み |
| `Verified` | `klaberjass` | [klaberjass.md](klaberjass.md) | 2人用資料・meld公開Action・固定seed・Viewを個別に照合済み |
| `Verified` | `goldmine` | [goldmine.md](goldmine.md) | 作者資料・action順・固定seed・秘密金塊Viewを個別に照合済み |
| `Verified` | `knave` | [knave.md](knave.md) | 3人用資料・J罰点・固定seed・観測同値を個別に照合済み |
| `Verified` | `norwegian_whist` | [norwegian_whist.md](norwegian_whist.md) | Pagat資料・実装・固定seed・Viewを個別に照合済み |
| `Verified` | `schnapsen` | [schnapsen.md](schnapsen.md) | DRS資料・実装・固定seed・Viewを個別に照合済み |
| `Verified` | `hamlet` | [hamlet.md](hamlet.md) | 作者資料・実装・固定seed・Viewを個別に照合済み |
| `Verified` | `whos_who` | [whos_who.md](whos_who.md) | 作者資料・実装・固定seed・秘密役職Viewを個別に照合済み |
| `Verified` | `mizerka` | [mizerka.md](mizerka.md) | Pagat資料・実装・固定seed・Viewを個別に照合済み |
| `Verified` | `sheriff` | [sheriff.md](sheriff.md) | Gokurakism完全規則・実装・固定seed・Joker境界・Viewを個別に照合済み |
| `Verified` | `farbwechsel` | [farbwechsel.md](farbwechsel.md) | Gokurakism完全規則・公開bid・固定seed・Viewを個別に照合済み |
| `Verified` | `kaedama_trick` | [kaedama_trick.md](kaedama_trick.md) | Gokurakism完全規則・Joker役職境界・固定seed・Viewを個別に照合済み |
| `Verified` | `ninety_nine` | [ninety_nine.md](ninety_nine.md) | 作者規則・9deal session・成功claim・固定seed・観測同値を個別に照合済み |
| `Verified` | `minimo` | [minimo.md](minimo.md) | 公開完全規則・double全精算・固定seed・観測同値を個別に照合済み |
| `Verified` | `trick_of_the_dead` | [trick_of_the_dead.md](trick_of_the_dead.md) | 公開完全規則・前後半/Zombie境界・固定seed・観測同値を個別に照合済み |
| `Verified` | `corpo` | [corpo.md](corpo.md) | 公開完全規則・Colpo/Poker公開・固定seed・観測同値を個別に照合済み |
| `RuleSpecific` | `piquet` | [piquet.md](piquet.md) | 宣言選択・Carte Blanche等の未解決差分あり |
| `RuleSpecific` | `five_hundred` | [five_hundred.md](five_hundred.md) | 地域contract/score差 |
| `RuleSpecific` | `skat` | [skat.md](skat.md) | 公式contract/得点表未実装 |
| `RuleSpecific` | `gooseberry_fool` | [gooseberry_fool.md](gooseberry_fool.md) | tie精算未照合 |
| `RuleSpecific` | `ulti` | [ulti.md](ulti.md) | 契約体系差 |
| `RuleSpecific` | `italian_whist` | [italian_whist.md](italian_whist.md) | Jokerのスート・rank指定Action未実装 |
| `Verified` | `tanuki` | [tanuki.md](tanuki.md) | 完全規則照合・局末全役公開・9局境界・観測同値 |
| `Verified` | `multi_stack` | [multi_stack.md](multi_stack.md) | 完全規則照合・人数別J役巡回・観測同値 |
| `Verified` | `dubito` | [dubito.md](dubito.md) | 標準Dubito完全規則・4列/得点/観測同値 |
| `Verified` | `three_tricks` | [three_tricks.md](three_tricks.md) | 完全規則・4ラウンドscore・観測同値 |
| `Verified` | `mini_misere` | [mini_misere.md](mini_misere.md) | 作者規則・人数別deck/score/Joker/Lot時機・観測同値 |
| `Verified` | `agony_aunt` | [agony_aunt.md](agony_aunt.md) | 作者規則・9罰点盤・回復・観測同値 |
| `Verified` | `collusion` | [collusion.md](collusion.md) | 作者規則・bonus/100点境界・観測同値 |
| `Verified` | `confirmation` | [confirmation.md](confirmation.md) | 完全規則・公開保護/残札bid・観測同値 |
| `Verified` | `big_two` | [big_two.md](big_two.md) | 採用variant完全規則・5枚役/罰点・観測同値 |
| `Verified` | `triple_crown` | [triple_crown.md](triple_crown.md) | 完全規則・15点session/役別精算・観測同値 |
| `RuleSpecific` | `doppelkopf` | [doppelkopf.md](doppelkopf.md) | Re/Kontra宣言、Fox/Charlie/Doppelkopf bonus・倍率未実装 |
| `RuleSpecific` | `sasaki_44a` | [sasaki_44a.md](sasaki_44a.md) | game終了、赤10交換、play方向差 |
| `RuleSpecific` | `schafkopf` | [schafkopf.md](schafkopf.md) | 勝ち抜きauction、Stoss系列宣言未実装 |
| `Verified` | `guillotine` | [guillotine.md](guillotine.md) | 完全規則・6契約/A連続・観測同値 |
| `Verified` | `the_trick` | [the_trick.md](the_trick.md) | 完全規則・人数別quota/背面情報・観測同値 |
| `Verified` | `truf` | [truf.md](truf.md) | Pagat完全規則・再配布/伏せtrump/得点・観測同値 |
| `Verified` | `pass_cut_run` | [pass_cut_run.md](pass_cut_run.md) | 完全規則・partner第4手/距離別得点・観測同値 |
| `Verified` | `finesse` | [finesse.md](finesse.md) | 完全規則・table lead/refill/得点順・観測同値 |
| `Verified` | `yaniv` | [yaniv.md](yaniv.md) | 完全規則・直前組draw/全手札公開/session・観測同値 |
| `Verified` | `wuxing_xiangke` | [wuxing_xiangke.md](wuxing_xiangke.md) | 完全規則・方向partner/5deal得点・観測同値 |
| `Verified` | `schmear` | [schmear.md](schmear.md) | Pagat St Paul版・人数別pack/交換/score・観測同値 |
| `Verified` | `briscola_chiamata` | [briscola_chiamata.md](briscola_chiamata.md) | 完全規則・hard pass/秘密partner/11点・観測同値 |
| `Verified` | `portland` | [portland.md](portland.md) | 完全規則・公開table/強制上書き/6round・deck観測同値 |
| `RuleSpecific` | `briscola_bugiarda` | [briscola_bugiarda.md](briscola_bugiarda.md) | 明示Solo bidとno-trump未実装 |
| `RuleSpecific` | `goninkan` | [goninkan.md](goninkan.md) | 二重関交換、じゅうろく系宣言、公式配点未実装 |
| `Verified` | `go_fish` | [go_fish.md](go_fish.md) | Bicycle完全規則・人数別配札/連続手番・観測同値 |
| `RuleSpecific` | `toepen` | [toepen.md](toepen.md) | challenge主体とfold済みtrick処理差 |
| `RuleSpecific` | `war` | [war.md](war.md) | 多人数warと既定turn打切り差 |
| `RuleSpecific` | `blackjack` | [blackjack.md](blackjack.md) | double/split house rule差 |
| `RuleSpecific` | `crazy_eights` | [crazy_eights.md](crazy_eights.md) | stock枯渇時のPagat再利用をBicycle版へ合成 |
| `Verified` | `old_maid` | [old_maid.md](old_maid.md) | Bicycle完全規則・pair/odd queen・観測同値 |
| `Verified` | `gops` | [gops.md](gops.md) | Pagat基本2人版・秘密bid/tie carry/91点 |
| `Verified` | `spite_and_malice` | [spite_and_malice.md](spite_and_malice.md) | Pagat最普及2人版・開始比較/center/観測同値 |
| `RuleSpecific` | `speed` | [speed.md](speed.md) | 同時競争の交互優先化と片側stock枯渇差 |
| `RuleSpecific` | `casino` | [casino.md](casino.md) | build所有・増築・multiple build未実装 |
| `Verified` | `golf` | [golf.md](golf.md) | Pagat Six-card基本版・強制交換/公開盤面/9hole・観測同値 |
| `Verified` | `sevens` | [sevens.md](sevens.md) | 日本式完全規則・3回pass/失格孤立札/順位・観測同値 |
| `Verified` | `concentration` | [concentration.md](concentration.md) | Bicycle完全規則・pair再手番/公開記憶/tie・観測同値 |
| `RuleSpecific` | `cheat` | [cheat.md](cheat.md) | 原典2～10人/任意枚数に対し3～6人/最大4枚 |
| `Verified` | `page_one` | [page_one.md](page_one.md) | Pagat/Bicycle古典版・Joker/宣言罰/無得点・観測同値 |
| `RuleSpecific` | `seven_bridge` | [seven_bridge.md](seven_bridge.md) | 7入り2枚meld、再利用回数、200点session、6人拡張差 |
| `Verified` | `rummy_500` | [rummy_500.md](rummy_500.md) | Pagat掲載variant・深い捨て札/Ace得点/同着延長・観測同値 |
| `RuleSpecific` | `canasta` | [canasta.md](canasta.md) | 初回表札、initial meld選択、partner上がり許可、bonus差 |
| `RuleSpecific` | `pinochle` | [pinochle.md](pinochle.md) | partner pass、Dix、overtrump義務、score換算差 |
| `RuleSpecific` | `hearts` | [hearts.md](hearts.md) | 6人1-deck拡張がPagat Cancellation Heartsと不一致 |
| `RuleSpecific` | `spades` | [spades.md](spades.md) | team合算とBicycle個人表記、Pagat Nil/失敗減点との差 |
| `Verified` | `euchre` | [euchre.md](euchre.md) | Bicycle北米版・packet配札/bower/alone/得点・観測同値 |
| `Verified` | `oh_hell` | [oh_hell.md](oh_hell.md) | Pagat標準列・dealer hook/普及配点・観測同値 |
| `RuleSpecific` | `texas_holdem` | [texas_holdem.md](texas_holdem.md) | side pot/all-in参加資格/複数hand session未対応 |
| `RuleSpecific` | `five_card_draw` | [five_card_draw.md](five_card_draw.md) | all-check redeal/side pot/複数hand session未対応 |
| `Verified` | `baccarat` | [baccarat.md](baccarat.md) | Pagat 8-deckオンラインPunto Banco・第三札表/純損益・観測同値 |
| `RuleSpecific` | `twenty_four` | [twenty_four.md](twenty_four.md) | 2人private stack戦、誤no-solution後2点、4人bluff差 |
| `Verified` | `black_lady` | [black_lady.md](black_lady.md) | 候補元/なかよし村版・公開kitty/pass/clear carry・観測同値 |
| `Verified` | `four_tricks` | [four_tricks.md](four_tricks.md) | 候補元完全規則・36枚/最終二重/得点表/3 deals・観測同値 |
