using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab
{
    public enum CandidateStatus
    {
        Prototype,
        RuleSpecific,
        Verified
    }

    public sealed class Candidate
    {
        public string Name { get; }
        public string Players { get; }
        public string Family { get; }
        public string Source { get; }
        public string? ImplementationId { get; }
        public CandidateStatus Status { get; }
        public Candidate(string name,string players,string family,string source,
            string? implementationId=null,CandidateStatus status=CandidateStatus.Prototype)
        {Name=name;Players=players;Family=family;Source=source;ImplementationId=implementationId;Status=status;}
    }
    public static class GameCatalogue
    {
        private static readonly Dictionary<string,string[]> Groups=new Dictionary<string,string[]>
        {
            ["1"]=new[]{"Card Capture","Scoundrel（悪党）","御三卿"},
            ["2"]=new[]{"ジャーマンホイスト","ジン・ラミー","ソノ","Crisp","クリベッジ","スーパートランプ","2人用大富豪","ブリスコラ","ボヘミアン・シュナイダー","ピケ","デュラック","将校スカート","クラバヤス","ノルウェージャンホイスト","シュナプセン","ゴールドマイン"},
            ["3"]=new[]{"ネイブ","ハムレット","WHO’S WHO","Farbwechsel","シェリフ","ミゼルカ","ナインティナイン","ファイブハンドレッド","スカート","グズベリー・フール","ウルティ","フォートリックス","イタリアン・ホイスト","ミニモ","替え玉トリック","Trick of the Dead","コルポ","たぬき"},
            ["4"]=new[]{"マルチスタック","ドゥビトー","スリートリックス","ミニミゼール","アゴニーアント","コルージョン","コンファメーション","大老二","トリプルクラウン","ドッペルコップ","ギロチン","44A（ササキ）","シャーフコップ","ザ・トリテ","トルフ"},
            ["4-team"]=new[]{"パスカットラン","フィネス"},
            ["5+"]=new[]{"ヤニブ","トランプクルー","保皇","五行相克","シュミア","ブリスコラ・キアマタ","ブリスコラ・ブジャルダ","ゴニンカン","ポートランド","ナポレオン","ツーペン","ブラックレディー"}
        };
        private static readonly Dictionary<string,string> Implementations=ImplementationIds();
        private static readonly Tuple<string,string,string,string?>[] Extras=
        {
            Tuple.Create<string,string,string,string?>("戦争","2-4","comparison","war"),
            Tuple.Create<string,string,string,string?>("ブラックジャック","1-5+dealer","banking","blackjack"),
            Tuple.Create<string,string,string,string?>("クレイジーエイト","2-5","shedding","crazy_eights"),
            Tuple.Create<string,string,string,string?>("ゴーフィッシュ","2-5","collection","go_fish"),
            Tuple.Create<string,string,string,string?>("ババ抜き","2-6","matching","old_maid"),
            Tuple.Create<string,string,string,string?>("スピード／スピット","2","real-time",null),
            Tuple.Create<string,string,string,string?>("GOPS","2","simultaneous",null),
            Tuple.Create<string,string,string,string?>("スパイト・アンド・マリス","2","patience-race",null),
            Tuple.Create<string,string,string,string?>("カシノ","2-4","capture",null),
            Tuple.Create<string,string,string,string?>("ゴルフ","2-4","layout",null),
            Tuple.Create<string,string,string,string?>("七並べ","3-8","shedding",null),
            Tuple.Create<string,string,string,string?>("神経衰弱","2-6","memory",null),
            Tuple.Create<string,string,string,string?>("ダウト","3-6","bluffing",null),
            Tuple.Create<string,string,string,string?>("ページワン","2-6","shedding",null),
            Tuple.Create<string,string,string,string?>("セブンブリッジ","2-6","rummy",null),
            Tuple.Create<string,string,string,string?>("ラミー500","2-8","rummy",null),
            Tuple.Create<string,string,string,string?>("カナスタ","4","rummy",null),
            Tuple.Create<string,string,string,string?>("ピノクル","4","trick-taking",null),
            Tuple.Create<string,string,string,string?>("ハーツ","3-6","trick-avoidance",null),
            Tuple.Create<string,string,string,string?>("スペード","4","trick-taking/team",null),
            Tuple.Create<string,string,string,string?>("ユーカー","4","trick-taking/team",null),
            Tuple.Create<string,string,string,string?>("オーヘル","3-7","exact-bid",null),
            Tuple.Create<string,string,string,string?>("テキサスホールデム","2-10","poker",null),
            Tuple.Create<string,string,string,string?>("ファイブカードドロー","2-6","poker",null),
            Tuple.Create<string,string,string,string?>("バカラ","1+banker","banking",null),
            Tuple.Create<string,string,string,string?>("24","2+","arithmetic",null)
        };
        public static IReadOnlyList<Candidate> Candidates()
        {
            var result=new List<Candidate>();
            foreach(KeyValuePair<string,string[]> group in Groups)foreach(string name in group.Value)
            {
                string? id=Implementations.TryGetValue(name,out string value)?value:null;
                string players=id=="napoleon"?"4-7":group.Key;
                result.Add(new Candidate(name,players,"unclassified","gokurakism",id,Status(id)));
            }
            var names=new HashSet<string>(result.Select(c=>c.Name));
            foreach(var item in Extras)if(!names.Contains(item.Item1))
            {
                string? id=Implementations.TryGetValue(item.Item1,out string value)?value:item.Item4;
                result.Add(new Candidate(item.Item1,item.Item2,item.Item3,"traditional index",id,Status(id)));
            }
            return result;
        }

        private static CandidateStatus Status(string? implementationId) =>
            implementationId!=null && VerifiedIds.Contains(implementationId)
                ? CandidateStatus.Verified
                : implementationId!=null && DedicatedIds.Contains(implementationId)
                    ? CandidateStatus.RuleSpecific : CandidateStatus.Prototype;

        private static readonly HashSet<string> DedicatedIds=new HashSet<string>
        {
            "blackjack","black_lady","briscola","crazy_eights","four_tricks",
            "concentration","gops","hearts","sevens","twenty_four",
            "baccarat","five_card_draw","texas_holdem",
            "oh_hell","spades",
            "euchre",
            "pinochle",
            "rummy_500","seven_bridge",
            "canasta",
            "cheat","golf","page_one","speed",
            "casino","spite_and_malice",
            "card_capture","gosankyo","scoundrel",
            "crisp","cribbage","durak","schnapsen",
            "sono","super_trump","daifugo_two","officer_skat",
            "bohemian_schneider","piquet","klaberjass","norwegian_whist","goldmine",
            "whos_who","gooseberry_fool","tanuki",
            "hamlet","farbwechsel","sheriff","mizerka",
            "ninety_nine","five_hundred",
            "skat","ulti","italian_whist","kaedama_trick","trick_of_the_dead","corpo",
            "multi_stack","dubito","mini_misere","agony_aunt","collusion","confirmation","the_trick","truf",
            "big_two","sasaki_44a",
            "triple_crown","guillotine","pass_cut_run",
            "finesse","schafkopf","doppelkopf",
            "yaniv","portland","toepen",
            "trump_crew","wuxing_xiangke","schmear",
            "briscola_chiamata","briscola_bugiarda",
            "goninkan","napoleon",
            "baohuang",
            "german_whist","gin_rummy","go_fish","knave","minimo","old_maid",
            "three_tricks","war"
        };

        // This set is deliberately separate from registration. An ID is added only after its
        // own source-variant audit is recorded in docs/rules/<game-id>.md, its implementation
        // and fixed-seed tests have been checked, and the audit has no unresolved differences.
        // A dedicated state machine alone is RuleSpecific, not Verified.
        private static readonly HashSet<string> VerifiedIds=new HashSet<string>
        {
            "trump_crew", "baohuang", "napoleon",
            "card_capture", "scoundrel", "gosankyo", "german_whist", "gin_rummy",
            "sono", "crisp", "cribbage", "super_trump", "daifugo_two",
            "briscola", "bohemian_schneider", "durak", "officer_skat",
            "klaberjass", "norwegian_whist", "schnapsen", "goldmine", "knave", "hamlet", "whos_who", "mizerka", "sheriff", "farbwechsel", "kaedama_trick", "ninety_nine",
            "minimo", "trick_of_the_dead", "corpo", "tanuki", "multi_stack", "dubito",
            "three_tricks", "mini_misere", "agony_aunt", "collusion", "confirmation", "big_two",
            "triple_crown", "guillotine", "the_trick", "truf", "pass_cut_run", "finesse", "yaniv", "wuxing_xiangke",
            "schmear", "briscola_chiamata", "portland", "go_fish", "old_maid", "gops", "spite_and_malice",
            "golf", "sevens", "concentration", "page_one", "rummy_500", "euchre", "oh_hell",
            "baccarat", "black_lady", "four_tricks"
        };

        private static Dictionary<string,string> ImplementationIds()
        {
            var result=new Dictionary<string,string>
            {
                ["ジャーマンホイスト"]="german_whist",["ジン・ラミー"]="gin_rummy",["ブリスコラ"]="briscola",
                ["ネイブ"]="knave",["フォートリックス"]="four_tricks",["ミニモ"]="minimo",
                ["ブラックレディー"]="black_lady",["スリートリックス"]="three_tricks",
                ["戦争"]="war",["ブラックジャック"]="blackjack",["クレイジーエイト"]="crazy_eights",
                ["ゴーフィッシュ"]="go_fish",["ババ抜き"]="old_maid"
            };
            foreach(Games.RuleProfile profile in Games.CandidateRuleGames.Profiles)
                result[profile.Name]=profile.Id;
            return result;
        }
    }
}
