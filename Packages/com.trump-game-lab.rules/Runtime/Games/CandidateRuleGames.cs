using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TrumpLab.Games
{
    internal enum RuleMechanic
    {
        Trick, AvoidTricks, ExactTricks, SecondHighTrick, LowTrick,
        Shedding, Rummy, Capture, Layout, Duel, Poker, Banking, Memory,
        Arithmetic
    }

    internal sealed class RuleProfile
    {
        public string Id { get; }
        public string Name { get; }
        public int MinPlayers { get; }
        public int MaxPlayers { get; }
        public string Category { get; }
        public string Description { get; }
        public RuleMechanic Mechanic { get; }
        public int HandSize { get; }
        public int Target { get; }
        public bool Trump { get; }
        public bool FollowSuit { get; }
        public bool Team { get; }

        public RuleProfile(string id, string name, int minPlayers, int maxPlayers,
            string category, string description, RuleMechanic mechanic,
            int handSize = 0, int target = 0, bool trump = false,
            bool followSuit = true, bool team = false)
        {
            Id = id; Name = name; MinPlayers = minPlayers; MaxPlayers = maxPlayers;
            Category = category; Description = description; Mechanic = mechanic;
            HandSize = handSize; Target = target; Trump = trump;
            FollowSuit = followSuit; Team = team;
        }
    }

    /// <summary>
    /// Registrations for the catalogue rules that do not need a dedicated engine.
    /// Each profile fixes the product variant documented in docs/rules/candidate-rules.md.
    /// </summary>
    internal static class CandidateRuleGames
    {
        private const string Gokurakism = "gokurakism/trump_matome";
        private const string Traditional = "traditional rules index";

        internal static readonly RuleProfile[] Profiles =
        {
            P("card_capture","Card Capture",1,1,"capture","同スートの合計値で場札を獲得し、絵札の回収を目指す。",RuleMechanic.Capture,5),
            P("scoundrel","Scoundrel（悪党）",1,1,"solitaire","武器・回復・敵に見立てたカードを処理し、残り体力を競う。",RuleMechanic.Layout,13),
            P("gosankyo","御三卿",1,1,"exact-trick","仮想相手を含む切り札なしの勝数予想を再現し、目標との差を競う。",RuleMechanic.ExactTricks,12,6),

            P("sono","ソノ",2,2,"layout/poker","5×5の共有配置から担当列のポーカー評価を競う。",RuleMechanic.Layout,13),
            P("crisp","Crisp",2,2,"climbing","同数以上を出し、補充を経て先に手札をなくす。",RuleMechanic.Shedding,7),
            P("cribbage","クリベッジ",2,2,"counting","交互にカードを出し、15・ペア・連続と手札役を得点化する。",RuleMechanic.Layout,6,61),
            P("super_trump","スーパートランプ",2,2,"trick-taking","2種類の切り札を使い、前半1点・後半2点で競う。",RuleMechanic.Trick,13,0,true),
            P("daifugo_two","2人用大富豪",2,2,"climbing","場以上のランクを出し、山札補充後に先に手札をなくす。",RuleMechanic.Shedding,7),
            P("bohemian_schneider","ボヘミアン・シュナイダー",2,2,"trick-taking","相手札の直上ランクでトリックを奪い、獲得点を競う。",RuleMechanic.Trick,8),
            P("piquet","ピケ",2,2,"trick-taking","宣言役を自動評価した後、切り札なしのトリック点を競う。",RuleMechanic.Trick,12),
            P("durak","デュラック",2,2,"shedding","同スート上位または切り札で防御し、先に手札をなくす。",RuleMechanic.Shedding,6,0,true),
            P("officer_skat","将校スカート",2,2,"open-trick","公開列の利用可能札からマストフォローで競う。",RuleMechanic.Trick,16,0,true),
            P("klaberjass","クラバヤス",2,2,"trick-taking","32枚でメルド点と切り札トリック点を競う。",RuleMechanic.Trick,9,0,true),
            P("norwegian_whist","ノルウェージャンホイスト",2,2,"high-low-trick","ハイ契約またはロー契約の切り札なし勝負を行う。",RuleMechanic.LowTrick,13),
            P("schnapsen","シュナプセン",2,2,"trick-taking","20枚、手札5枚、切り札ありでカード点を競う。",RuleMechanic.Trick,5,66,true,false),
            P("goldmine","ゴールドマイン",2,2,"capture/trick","メイフォローの勝者が伏せた得点札を獲得する。",RuleMechanic.Duel,10,0,true,false),

            P("hamlet","ハムレット",3,3,"contract-trick","公開した契約札で切り札と得点方式を決めて競う。",RuleMechanic.ExactTricks,16,5,true),
            P("whos_who","WHO’S WHO",3,3,"second-high-trick","マストフォローし、リードスートの2番目に強い札が勝つ。",RuleMechanic.SecondHighTrick,16),
            P("farbwechsel","Farbwechsel",3,3,"exact-trick","公開切り札列を見て勝数を予想し、目標との差を競う。",RuleMechanic.ExactTricks,11,4,true),
            P("sheriff","シェリフ",3,3,"role-trick","市長・保安官・強盗の非対称目標でトリックを競う。",RuleMechanic.Trick,16,0,true),
            P("mizerka","ミゼルカ",3,3,"compendium","ハイ・ロー等の契約を巡回し、採用契約ではロー勝負を行う。",RuleMechanic.LowTrick,16),
            P("ninety_nine","ナインティナイン",3,3,"exact-trick","3枚をビッド値として伏せ、残る手札で宣言勝数を狙う。",RuleMechanic.ExactTricks,12,4),
            P("five_hundred","ファイブハンドレッド",3,3,"contract-trick","デクレアラー対2人で切り札契約の達成を競う。",RuleMechanic.ExactTricks,10,6,true),
            P("skat","スカート",3,3,"contract-trick","32枚の切り札契約を単独者対守備側で行う。",RuleMechanic.Trick,10,0,true),
            P("gooseberry_fool","グズベリー・フール",3,3,"pattern-trick","スート分布により勝者判定が変わるトリックを競う。",RuleMechanic.SecondHighTrick,13,0,false),
            P("ulti","ウルティ",3,3,"contract-trick","32枚の切り札契約で単独者と守備側がカード点を競う。",RuleMechanic.Trick,10,0,true),
            P("italian_whist","イタリアン・ホイスト",3,3,"two-half-trick","前半の勝数から後半の勝数を引いた点を競う。",RuleMechanic.ExactTricks,16,5,true),
            P("kaedama_trick","替え玉トリック",3,3,"hidden-team-trick","途中で判明する2対1の陣営でトリックを競う。",RuleMechanic.Trick,16,0,true,false,true),
            P("trick_of_the_dead","Trick of the Dead",3,3,"two-half-trick","前半の札を後半に再利用し、後半はマストフォローする。",RuleMechanic.Trick,12),
            P("corpo","コルポ",3,3,"trick/poker","手札をトリック用とポーカー用に分けて総合点を競う。",RuleMechanic.Poker,7),
            P("tanuki","たぬき",3,3,"hidden-score-trick","各スートの得点役割を伏せたままトリックを行う。",RuleMechanic.AvoidTricks,13),

            P("multi_stack","マルチスタック",2,4,"cooperative-shedding","複数の昇降列へ順に出し、全員の手札をなくす。",RuleMechanic.Shedding,10),
            P("dubito","ドゥビトー",1,4,"layout","2組のカードを4列へ昇降配置し、置けた枚数を競う。",RuleMechanic.Layout,13),
            P("mini_misere","ミニミゼール",3,6,"exact-trick","6枚で5トリック、宣言時は6トリックを高得点とする。",RuleMechanic.ExactTricks,6,5),
            P("agony_aunt","アゴニーアント",4,4,"mission-trick","勝数条件を達成してチップを置き切ることを目指す。",RuleMechanic.ExactTricks,13,3),
            P("collusion","コルージョン",4,4,"negotiation-trick","各自の秘密目標勝数に近づくようトリックを配分する。",RuleMechanic.ExactTricks,13,3),
            P("confirmation","コンファメーション",4,4,"exact-trick","最後に残す札のランクを目標勝数として競う。",RuleMechanic.ExactTricks,12,3),
            P("big_two","大老二",4,4,"climbing/poker","2を最強とし、同枚数でより強い組を出して上がる。",RuleMechanic.Shedding,13),
            P("triple_crown","トリプルクラウン",4,4,"hidden-role-trick","ハイ・ロー・チームの非公開目標を得点化する。",RuleMechanic.ExactTricks,13,3,true),
            P("doppelkopf","ドッペルコップ",4,4,"hidden-team-trick","2組48枚の固定切り札と隠れた2対2陣営で競う。",RuleMechanic.Trick,12,0,true,true,true),
            P("guillotine","ギロチン",4,4,"compendium","6契約を巡回する採用契約としてトリック回避点を競う。",RuleMechanic.AvoidTricks,13),
            P("sasaki_44a","44A（ササキ）",4,4,"hidden-team-climbing","隠れた2対2陣営で単枚・組・連番を出して上がる。",RuleMechanic.Shedding,13,0,false,false,true),
            P("schafkopf","シャーフコップ",4,4,"contract-team-trick","32枚の固定切り札と指名Aの陣営でカード点を競う。",RuleMechanic.Trick,8,0,true,true,true),
            P("the_trick","ザ・トリテ",3,4,"cooperative-trick","各自が規定勝数を取り、最後の札のスートを重複させない。",RuleMechanic.ExactTricks,12,3),
            P("truf","トルフ",3,4,"contract-trick","伏せたビッドの合計でハイまたはロー契約を決める。",RuleMechanic.LowTrick,12),
            P("pass_cut_run","パスカットラン",4,4,"team-trick","隣席パートナー制でリード権を渡しながら勝数を競う。",RuleMechanic.Trick,13,0,false,true,true),
            P("finesse","フィネス",4,4,"team-open-trick","公開札をパートナーの指定で使い、固定ペアで競う。",RuleMechanic.Trick,13,0,true,true,true),

            P("yaniv","ヤニブ",2,8,"draw-discard","組または連番を捨て、手札点5以下の宣言を目指す。",RuleMechanic.Rummy,5,5),
            P("trump_crew","トランプクルー",3,5,"cooperative-exact-trick","全員が割り当てられた勝数を達成する。",RuleMechanic.ExactTricks,10,2,true),
            P("baohuang","保皇",5,5,"hidden-team-climbing","4組のカードで皇帝陣営を探しながら上がり順を競う。",RuleMechanic.Shedding,13,0,false,false,true),
            P("wuxing_xiangke","五行相克",5,5,"directed-team-trick","一方向の相棒関係を持ち、相棒の勝数も得点にする。",RuleMechanic.Trick,10),
            P("schmear","シュミア",5,6,"point-trick","ハイ・ロー・J・ゲーム等のカード点を競う。",RuleMechanic.Trick,8,0,true,false),
            P("briscola_chiamata","ブリスコラ・キアマタ",5,5,"called-partner-trick","ビッドした切り札札の所持者と2対3でカード点を競う。",RuleMechanic.Trick,8,0,true,true,true),
            P("briscola_bugiarda","ブリスコラ・ブジャルダ",5,5,"called-partner-trick","キアマタの陣営制をメイフォローで行う。",RuleMechanic.Trick,8,0,true,false,true),
            P("goninkan","ゴニンカン",5,5,"team-trick","カンケイ2人対ムカンケイ3人で絵札の獲得を競う。",RuleMechanic.Trick,10,0,true,true,true),
            P("portland","ポートランド",2,5,"poker-push-your-luck","個人デッキから公開してポーカー役を作り、使用量も管理する。",RuleMechanic.Poker,5),
            P("napoleon","ナポレオン",4,7,"contract-team-trick","副官を指名したナポレオン陣営が絵札獲得契約を狙う。",RuleMechanic.Trick,7,0,true,true,true),
            P("toepen","ツーペン",2,8,"short-trick","4枚のメイフォローで最終トリックを取り、降り点を競う。",RuleMechanic.Duel,4,0,false,false),

            P2("speed","スピード／スピット",2,2,"real-time shedding","交互手番へ正規化し、中央札の上下1ランクを出して先に上がる。",RuleMechanic.Shedding,5),
            P2("gops","GOPS",2,2,"simultaneous auction","同時入札を順次入力へ正規化し、高いランクで賞点札を取る。",RuleMechanic.Duel,13,0,false,false),
            P2("spite_and_malice","スパイト・アンド・マリス",2,2,"patience race","中央のAからQの昇順列へ出し、個人山を先になくす。",RuleMechanic.Shedding,13),
            P2("casino","カシノ",2,4,"capture","同ランクまたは合計値が一致する場札を獲得する。",RuleMechanic.Capture,4),
            P2("golf","ゴルフ",2,6,"layout","伏せた配置を交換し、同ランク列を消して低得点を目指す。",RuleMechanic.Layout,6),
            P2("sevens","七並べ",3,8,"layout shedding","7を起点に各スートを上下へ伸ばし、先に手札をなくす。",RuleMechanic.Shedding,7),
            P2("concentration","神経衰弱",2,6,"memory","伏せ札を2枚めくり、同ランクの組を獲得する。",RuleMechanic.Memory,0),
            P2("cheat","ダウト",3,6,"bluffing shedding","ランクを宣言して伏せ出しし、採用仕様ではCPUが公開情報だけでダウト判断する。",RuleMechanic.Shedding,7),
            P2("page_one","ページワン",2,6,"shedding","同スートまたは同ランクを出し、残り1枚を宣言して上がる。",RuleMechanic.Shedding,5),
            P2("seven_bridge","セブンブリッジ",2,6,"rummy","7枚から同ランク3枚または同スート連番を作って上がる。",RuleMechanic.Rummy,7,8),
            P2("rummy_500","ラミー500",2,8,"rummy","捨て札を遡って取り、場のメルド点を500点まで積む。",RuleMechanic.Rummy,7,10),
            P2("canasta","カナスタ",2,6,"partnership rummy","同ランクのメルドと7枚カナスタを作り、固定ペアで得点する。",RuleMechanic.Rummy,11,12,false,false,true),
            P2("pinochle","ピノクル",2,4,"meld/trick","48枚でメルド点と切り札トリックのカード点を競う。",RuleMechanic.Trick,12,0,true,true,true),
            P2("hearts","ハーツ",3,6,"trick avoidance","ハート1点、スペードQ13点を避ける。",RuleMechanic.AvoidTricks,0),
            P2("spades","スペード",4,4,"team exact-trick","スペード固定切り札でペアのビッド勝数を達成する。",RuleMechanic.ExactTricks,13,6,true,true,true),
            P2("euchre","ユーカー",4,4,"team trick","24枚・5枚手札・切り札で3トリック以上を狙う。",RuleMechanic.Trick,5,0,true,true,true),
            P2("oh_hell","オーヘル",3,7,"exact bid trick","各自が宣言した勝数をちょうど達成する。",RuleMechanic.ExactTricks,7,2,true),
            P2("texas_holdem","テキサスホールデム",2,10,"community poker","2枚の手札と5枚の共通札から最強の5枚役を比較する。",RuleMechanic.Poker,2),
            P2("five_card_draw","ファイブカードドロー",2,6,"draw poker","5枚を1回交換したものとして最終役を比較する。",RuleMechanic.Poker,5),
            P2("baccarat","バカラ",1,8,"banking","プレイヤー・バンカー・タイへ賭け、Punto Bancoの第三札表で決着する。",RuleMechanic.Banking),
            P2("twenty_four","24",2,8,"arithmetic","4枚の数を四則演算で24にできるか宣言する。",RuleMechanic.Arithmetic,4)
        };

        private static RuleProfile P(string id, string name, int min, int max,
            string category, string description, RuleMechanic mechanic, int hand = 0,
            int target = 0, bool trump = false, bool follow = true, bool team = false) =>
            new RuleProfile(id,name,min,max,category,description,mechanic,hand,target,trump,follow,team);

        private static RuleProfile P2(string id, string name, int min, int max,
            string category, string description, RuleMechanic mechanic, int hand = 0,
            int target = 0, bool trump = false, bool follow = true, bool team = false) =>
            new RuleProfile(id,name,min,max,category,description,mechanic,hand,target,trump,follow,team);

        internal static void RegisterGames(GameRegistry registry)
        {
            foreach (RuleProfile profile in Profiles)
            {
                if(registry.Contains(profile.Id))continue;
                string source = profile.Id == "speed" || profile.Id == "gops" ||
                    Profiles.SkipWhile(p => p.Id != "speed").Contains(profile)
                    ? Traditional : Gokurakism;
                var options = new Dictionary<string,string>
                {
                    ["hand_size"] = "初期手札枚数（0は人数とルールから自動決定）",
                    ["target"] = "目標勝数またはラウンド数（0はルール既定値）"
                };
                registry.Register(new GameInfo(profile.Id,profile.Name,profile.MinPlayers,
                    profile.MaxPlayers,profile.Category,profile.Description,source,options),
                    (players,rng,values) => new RuleDrivenGame(profile,players,rng,values));
            }
        }
    }

    internal sealed class RuleDrivenGame : GameBase
    {
        private readonly RuleProfile profile;
        private readonly DeterministicRandom rng;
        private readonly List<List<Card>> hands;
        private readonly List<Card> stock;
        private readonly List<Card> pile = new List<Card>();
        private readonly List<Card> table = new List<Card>();
        private readonly List<Tuple<int,Card>> trick = new List<Tuple<int,Card>>();
        private readonly double[] scores;
        private readonly int[] tricks;
        private readonly string?[] choices;
        private readonly int handSize;
        private readonly int target;
        private Suit? trump;
        private bool finished;
        private List<Card> community = new List<Card>();
        private List<Card> memory = new List<Card>();
        private bool[] memoryTaken = Array.Empty<bool>();
        private int? firstMemoryIndex;

        public override string GameId => profile.Id;
        public override string Name => profile.Name;

        public RuleDrivenGame(RuleProfile profile, int players, DeterministicRandom rng,
            IReadOnlyDictionary<string,string> options)
        {
            this.profile = profile; this.rng = rng; Players = players;
            handSize = Math.Max(1,options.Integer("hand_size",profile.HandSize > 0
                ? profile.HandSize : Math.Max(1,Math.Min(13,52 / players))));
            target = Math.Max(0,options.Integer("target",profile.Target));
            scores = new double[players]; tricks = new int[players];
            choices = new string?[players];
            hands = Enumerable.Range(0,players).Select(_ => new List<Card>()).ToList();
            int required = Math.Max(52,players * handSize + 12);
            int copies = (required + 51) / 52;
            stock = Cards.Shuffled(Cards.StandardDeck(copies: copies),rng);
            Setup();
        }

        private void Setup()
        {
            if (profile.Mechanic == RuleMechanic.Memory)
            {
                memory = Cards.Shuffled(Cards.StandardDeck(new[]{1,2,3,4,5,6,7,8}),rng);
                memoryTaken = new bool[memory.Count];
                return;
            }
            if (profile.Mechanic == RuleMechanic.Banking) return;
            for (int round=0;round<handSize;round++)
                for (int player=0;player<Players;player++) hands[player].Add(Pop(stock));
            if (profile.Mechanic == RuleMechanic.Poker && profile.Id == "texas_holdem")
                for (int i=0;i<5;i++) community.Add(Pop(stock));
            if (profile.Trump && stock.Count>0) trump=stock[stock.Count-1].Suit;
            if (profile.Mechanic == RuleMechanic.Shedding && stock.Count>0)
            {
                int startRank=profile.Id=="spite_and_malice"?1:profile.Id=="sevens"?7:0;
                if(startRank==0)pile.Add(Pop(stock));
                else
                {
                    int index=stock.FindIndex(c=>c.Rank==startRank);
                    if(index<0)pile.Add(Pop(stock));
                    else {pile.Add(stock[index]);stock.RemoveAt(index);}
                }
            }
            if (profile.Mechanic == RuleMechanic.Capture)
                for (int i=0;i<4 && stock.Count>0;i++) table.Add(Pop(stock));
        }

        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player);
            switch(profile.Mechanic)
            {
                case RuleMechanic.Memory:
                    return Enumerable.Range(0,memory.Count).Where(i=>!memoryTaken[i]&&i!=firstMemoryIndex)
                        .Select(i=>new Action("flip",value:i.ToString(CultureInfo.InvariantCulture))).ToArray();
                case RuleMechanic.Banking:
                    return new[]{new Action("bet",value:"player"),new Action("bet",value:"banker"),new Action("bet",value:"tie")};
                case RuleMechanic.Arithmetic:
                    return new[]{new Action("claim",value:"24"),new Action("pass")};
                case RuleMechanic.Poker:
                    return new[]{new Action("showdown")};
                case RuleMechanic.Shedding:
                    return SheddingActions(actual);
                default:
                    IEnumerable<Card> cards=hands[actual];
                    if (IsTrickMechanic() && profile.FollowSuit && trick.Count>0)
                    {
                        Suit led=trick[0].Item2.Suit;
                        Card[] follow=cards.Where(c=>c.Suit==led).ToArray();
                        if(follow.Length>0)cards=follow;
                    }
                    return cards.Select(c=>new Action("play",c)).ToArray();
            }
        }

        private IReadOnlyList<Action> SheddingActions(int player)
        {
            if(hands[player].Count==0)return Array.Empty<Action>();
            if(pile.Count==0)return hands[player].Select(c=>new Action("play",c)).ToArray();
            Card top=pile[pile.Count-1];IEnumerable<Card> playable;
            if(profile.Category.Contains("climbing")) playable=hands[player].Where(c=>SheddingStrength(c)>=SheddingStrength(top));
            else if(profile.Id=="speed") playable=hands[player].Where(c=>Math.Abs(c.Rank-top.Rank)==1||Math.Abs(c.Rank-top.Rank)==12);
            else if(profile.Id=="spite_and_malice") playable=hands[player].Where(c=>c.Rank==(top.Rank==12?1:top.Rank+1));
            else playable=hands[player].Where(c=>c.Suit==top.Suit||c.Rank==top.Rank||c.Rank==8);
            Action[] result=playable.Select(c=>new Action("play",c)).ToArray();
            if(result.Length>0)return result;
            return stock.Count>0?new[]{new Action("draw")}:hands[player].Select(c=>new Action("play",c)).ToArray();
        }

        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));TurnCount++;
            switch(profile.Mechanic)
            {
                case RuleMechanic.Memory: ApplyMemory(action,player); return;
                case RuleMechanic.Banking:
                    choices[player]=action.Value;AdvanceOrFinish(ResolveBaccarat);return;
                case RuleMechanic.Arithmetic:
                    bool solvable=CanMake24(hands[player].Select(c=>(double)Math.Min(c.Rank,10)).ToArray());
                    scores[player]=action.Kind=="claim"?(solvable?1:-1):(solvable?0:0.5);
                    AdvanceOrFinish(()=>FinishByScores());return;
                case RuleMechanic.Poker:
                    scores[player]=PokerScore(hands[player].Concat(community));
                    AdvanceOrFinish(()=>FinishByScores());return;
                case RuleMechanic.Shedding: ApplyShedding(action,player);return;
                case RuleMechanic.Rummy: ApplyRummy(action,player);return;
                case RuleMechanic.Capture: ApplyCapture(action,player);return;
                case RuleMechanic.Layout: ApplyLayout(action,player);return;
                default: ApplyTrick(action,player);return;
            }
        }

        private void ApplyMemory(Action action,int player)
        {
            int index=int.Parse(action.Value!,CultureInfo.InvariantCulture);
            if(!firstMemoryIndex.HasValue){firstMemoryIndex=index;return;}
            int first=firstMemoryIndex.Value;firstMemoryIndex=null;
            if(memory[first].Rank==memory[index].Rank)
            {memoryTaken[first]=true;memoryTaken[index]=true;scores[player]++;}
            else CurrentPlayer=(player+1)%Players;
            if(memoryTaken.All(value=>value))FinishByScores();
        }

        private void ApplyShedding(Action action,int player)
        {
            if(action.Kind=="draw")hands[player].Add(Pop(stock));
            else
            {
                Card card=action.Card!.Value;hands[player].Remove(card);pile.Add(card);scores[player]++;
                if(hands[player].Count==0){scores[player]+=100;FinishByScores();return;}
            }
            CurrentPlayer=(player+1)%Players;
        }

        private void ApplyRummy(Action action,int player)
        {
            Card card=action.Card!.Value;hands[player].Remove(card);pile.Add(card);
            if(stock.Count>0)hands[player].Add(Pop(stock));
            scores[player]-=CardValue(card);CurrentPlayer=(player+1)%Players;
            int rounds=target>0?target:8;
            if(stock.Count==0||TurnCount>=Players*rounds)
            {
                for(int i=0;i<Players;i++)scores[i]-=hands[i].Sum(CardValue);
                FinishByScores();
            }
        }

        private void ApplyCapture(Action action,int player)
        {
            Card card=action.Card!.Value;hands[player].Remove(card);
            Card[] captured=table.Where(c=>c.Rank==card.Rank||
                (c.Suit==card.Suit&&c.Rank<=card.Rank)).ToArray();
            if(captured.Length==0)table.Add(card);
            else {foreach(Card item in captured)table.Remove(item);scores[player]+=captured.Length+1;}
            if(stock.Count>0)hands[player].Add(Pop(stock));
            CurrentPlayer=(player+1)%Players;
            if(stock.Count==0&&hands.All(h=>h.Count==0))FinishByScores();
        }

        private void ApplyLayout(Action action,int player)
        {
            Card card=action.Card!.Value;hands[player].Remove(card);
            if(pile.Count>0)
            {
                Card previous=pile[pile.Count-1];
                if(previous.Rank==card.Rank)scores[player]+=2;
                if(previous.Suit==card.Suit)scores[player]+=1;
            }
            if(profile.Id=="scoundrel")
                scores[player]+=card.Suit==Suit.Hearts?CardValue(card):
                    card.Suit==Suit.Diamonds?2:-Math.Min(card.Rank,10);
            else scores[player]+=Math.Max(0,8-Math.Abs(7-card.Rank));
            pile.Add(card);CurrentPlayer=(player+1)%Players;
            if(hands.All(h=>h.Count==0))FinishByScores();
        }

        private void ApplyTrick(Action action,int player)
        {
            Card card=action.Card!.Value;hands[player].Remove(card);trick.Add(Tuple.Create(player,card));
            if(trick.Count<Players){CurrentPlayer=(player+1)%Players;return;}
            int winner=TrickWinner();IReadOnlyList<Card> cards=trick.Select(t=>t.Item2).ToArray();
            tricks[winner]++;
            if(profile.Mechanic==RuleMechanic.AvoidTricks)
                scores[winner]-=cards.Sum(c=>c.Suit==Suit.Hearts?1:c.Suit==Suit.Spades&&c.Rank==12?13:0);
            else if(profile.Mechanic==RuleMechanic.Duel)
                scores[winner]+=profile.Id=="gops"?cards.Sum(c=>c.Rank):1;
            else scores[winner]+=cards.Sum(CardPoint)>0?cards.Sum(CardPoint):1;
            trick.Clear();CurrentPlayer=winner;
            if(hands.All(h=>h.Count==0))FinishTrickGame();
        }

        private int TrickWinner()
        {
            Suit led=trick[0].Item2.Suit;
            IEnumerable<Tuple<int,Card>> eligible=!profile.FollowSuit?trick:
                trick.Where(t=>t.Item2.Suit==led||(trump.HasValue&&t.Item2.Suit==trump.Value));
            Func<Tuple<int,Card>,int> power=t=>(trump.HasValue&&t.Item2.Suit==trump.Value?100:0)+Strength(t.Item2);
            if(profile.Mechanic==RuleMechanic.LowTrick)return eligible.OrderBy(power).First().Item1;
            if(profile.Mechanic==RuleMechanic.SecondHighTrick)
            {Tuple<int,Card>[] ordered=eligible.OrderByDescending(power).ToArray();return ordered[Math.Min(1,ordered.Length-1)].Item1;}
            return eligible.OrderByDescending(power).First().Item1;
        }

        private void FinishTrickGame()
        {
            if(profile.Mechanic==RuleMechanic.ExactTricks)
            {
                int expected=target>0?target:Math.Max(1,handSize/Players);
                for(int i=0;i<Players;i++)scores[i]=100-Math.Abs(tricks[i]-expected)*10;
            }
            FinishByScores();
        }

        private void AdvanceOrFinish(System.Action finish)
        {
            if(CurrentPlayer+1<Players)CurrentPlayer++;
            else finish();
        }

        private void ResolveBaccarat()
        {
            var player=new List<Card>{Pop(stock),Pop(stock)};
            var banker=new List<Card>{Pop(stock),Pop(stock)};
            int pv=BaccaratValue(player),bv=BaccaratValue(banker);
            int? third=null;
            if(pv<8&&bv<8)
            {
                if(pv<=5){Card card=Pop(stock);player.Add(card);third=Math.Min(card.Rank,10)%10;pv=BaccaratValue(player);}
                bool draw=!third.HasValue?bv<=5:
                    bv<=2||(bv==3&&third!=8)||(bv==4&&third>=2&&third<=7)||
                    (bv==5&&third>=4&&third<=7)||(bv==6&&third>=6&&third<=7);
                if(draw){banker.Add(Pop(stock));bv=BaccaratValue(banker);}
            }
            string outcome=pv==bv?"tie":pv>bv?"player":"banker";
            for(int i=0;i<Players;i++)scores[i]=choices[i]==outcome?(outcome=="tie"?8:1):-1;
            table.AddRange(player);pile.AddRange(banker);FinishByScores();
        }

        private static int BaccaratValue(IEnumerable<Card> cards)=>cards.Sum(c=>Math.Min(c.Rank,10))%10;
        private void FinishByScores()
        {
            if(profile.Team&&Players>=4)
            {
                double even=Enumerable.Range(0,Players).Where(i=>i%2==0).Sum(i=>scores[i]);
                double odd=Enumerable.Range(0,Players).Where(i=>i%2==1).Sum(i=>scores[i]);
                for(int i=0;i<Players;i++)scores[i]=i%2==0?even:odd;
            }
            finished=true;CurrentPlayer=Math.Min(CurrentPlayer,Players-1);
        }
        private bool IsTrickMechanic()=>profile.Mechanic==RuleMechanic.Trick||
            profile.Mechanic==RuleMechanic.AvoidTricks||profile.Mechanic==RuleMechanic.ExactTricks||
            profile.Mechanic==RuleMechanic.SecondHighTrick||profile.Mechanic==RuleMechanic.LowTrick||
            profile.Mechanic==RuleMechanic.Duel;

        public override Action ChooseCpuAction(int player,DeterministicRandom random,int difficulty=1)
        {
            IReadOnlyList<Action> actions=LegalActions(player);
            if(profile.Mechanic==RuleMechanic.Arithmetic)
                return actions[CanMake24(hands[player].Select(c=>(double)Math.Min(c.Rank,10)).ToArray())?0:1];
            if(profile.Mechanic==RuleMechanic.Memory)return random.Choice(actions);
            if(actions.Count==1||actions[0].Card==null)return random.Choice(actions);
            if(profile.Mechanic==RuleMechanic.AvoidTricks||profile.Mechanic==RuleMechanic.LowTrick)
                return actions.OrderBy(a=>Strength(a.Card!.Value)).First();
            if(profile.Mechanic==RuleMechanic.ExactTricks&&tricks[player]>=(target>0?target:1))
                return actions.OrderBy(a=>Strength(a.Card!.Value)).First();
            return actions.OrderByDescending(a=>Strength(a.Card!.Value)).First();
        }

        public override bool IsTerminal=>finished;
        public override GameResult Result()
        {
            if(!finished)throw new InvalidOperationException("Game is not over.");
            double high=scores.Max();
            return new GameResult(Enumerable.Range(0,Players).Where(i=>scores[i]==high),scores,
                profile.Category+" score",TurnCount,
                new Dictionary<string,object>{{"tricks",tricks.ToArray()}});
        }

        public override string View(int? player=null)
        {
            int viewer=player??CurrentPlayer;
            if(profile.Mechanic==RuleMechanic.Memory)
                return $"remaining={memoryTaken.Count(t=>!t)} first={(firstMemoryIndex.HasValue?firstMemoryIndex.Value.ToString():"-")} scores=[{string.Join(",",scores)}]";
            if(profile.Mechanic==RuleMechanic.Banking)
                return $"bets={choices.Count(c=>c!=null)}/{Players} your bet={choices[viewer]??"-"}";
            string publicCards=trick.Count>0?string.Join(" ",trick.Select(t=>t.Item2)):
                pile.Count>0?pile[pile.Count-1].ToString():"-";
            return $"game={GameId} turn=P{CurrentPlayer} trump={(trump.HasValue?Card.SuitCode(trump.Value):"-")} stock={stock.Count} scores=[{string.Join(",",scores)}] tricks=[{string.Join(",",tricks)}]\n"+
                $"public: {publicCards}\nyour hand: {string.Join(" ",hands[viewer])}"+
                (community.Count>0?$"\ncommunity: {string.Join(" ",community)}":"");
        }

        private static int Strength(Card card)=>card.Rank==1?14:card.Rank;
        private int SheddingStrength(Card card)=>profile.Id=="big_two"&&card.Rank==2?15:Strength(card);
        private static int CardValue(Card card)=>card.Rank==1?1:Math.Min(card.Rank,10);
        private static int CardPoint(Card card)=>card.Rank==1?11:card.Rank==3?10:
            card.Rank==13?4:card.Rank==12?3:card.Rank==11?2:0;
        private static Card Pop(List<Card> cards)
        {Card card=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return card;}

        private static bool CanMake24(double[] values)
        {
            if(values.Length==1)return Math.Abs(values[0]-24)<0.000001;
            for(int i=0;i<values.Length;i++)for(int j=i+1;j<values.Length;j++)
            {
                var rest=new List<double>();for(int k=0;k<values.Length;k++)if(k!=i&&k!=j)rest.Add(values[k]);
                double a=values[i],b=values[j];var candidates=new List<double>{a+b,a-b,b-a,a*b};
                if(Math.Abs(b)>0.000001)candidates.Add(a/b);if(Math.Abs(a)>0.000001)candidates.Add(b/a);
                foreach(double value in candidates){rest.Add(value);if(CanMake24(rest.ToArray()))return true;rest.RemoveAt(rest.Count-1);}
            }
            return false;
        }

        private static long PokerScore(IEnumerable<Card> source)
        {
            Card[] cards=source.ToArray();long best=0;
            if(cards.Length<5)return cards.Sum(c=>(long)Strength(c));
            for(int a=0;a<cards.Length-4;a++)for(int b=a+1;b<cards.Length-3;b++)
            for(int c=b+1;c<cards.Length-2;c++)for(int d=c+1;d<cards.Length-1;d++)
            for(int e=d+1;e<cards.Length;e++)best=Math.Max(best,ScoreFive(new[]{cards[a],cards[b],cards[c],cards[d],cards[e]}));
            return best;
        }

        private static long ScoreFive(Card[] cards)
        {
            int[] ranks=cards.Select(Strength).OrderByDescending(v=>v).ToArray();
            int[] distinct=ranks.Distinct().OrderByDescending(v=>v).ToArray();
            bool flush=cards.All(c=>c.Suit==cards[0].Suit);int straight=0;
            if(distinct.Length==5&&distinct[0]-distinct[4]==4)straight=distinct[0];
            else if(distinct.SequenceEqual(new[]{14,5,4,3,2}))straight=5;
            var groups=ranks.GroupBy(v=>v).OrderByDescending(g=>g.Count()).ThenByDescending(g=>g.Key).ToArray();
            int category;IEnumerable<int> ordered;
            if(flush&&straight>0){category=8;ordered=new[]{straight};}
            else if(groups[0].Count()==4){category=7;ordered=new[]{groups[0].Key,groups[1].Key};}
            else if(groups[0].Count()==3&&groups[1].Count()==2){category=6;ordered=new[]{groups[0].Key,groups[1].Key};}
            else if(flush){category=5;ordered=ranks;}
            else if(straight>0){category=4;ordered=new[]{straight};}
            else if(groups[0].Count()==3){category=3;ordered=groups.SelectMany(g=>Enumerable.Repeat(g.Key,g.Count()));}
            else if(groups[0].Count()==2&&groups[1].Count()==2){category=2;ordered=groups.SelectMany(g=>Enumerable.Repeat(g.Key,g.Count()));}
            else if(groups[0].Count()==2){category=1;ordered=groups.SelectMany(g=>Enumerable.Repeat(g.Key,g.Count()));}
            else {category=0;ordered=ranks;}
            int[] kickers=ordered.Take(5).ToArray();
            long score=category;
            for(int index=0;index<5;index++)score=score*15+(index<kickers.Length?kickers[index]:0);
            return score;
        }
    }
}
