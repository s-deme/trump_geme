using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TrumpLab.Games
{
    internal static class RummyClassicGames
    {
        public static void RegisterGames(GameRegistry registry)
        {
            SevenBridgeGame.Register(registry);
            Rummy500Game.Register(registry);
            CanastaGame.Register(registry);
        }
    }

    internal readonly struct CanastaCard
    {
        public int Id { get; }public Suit? Suit { get; }public int Rank { get; }
        public CanastaCard(int id,Suit? suit,int rank){Id=id;Suit=suit;Rank=rank;}
        public bool IsJoker=>!Suit.HasValue;public bool IsWild=>IsJoker||Rank==2;
        public bool IsRedThree=>Rank==3&&(Suit==TrumpLab.Suit.Hearts||Suit==TrumpLab.Suit.Diamonds);
        public bool IsBlackThree=>Rank==3&&(Suit==TrumpLab.Suit.Clubs||Suit==TrumpLab.Suit.Spades);
        public override string ToString()=>IsJoker?$"JK{Id}":new Card(Suit!.Value,Rank).ToString()+"#"+Id;
    }

    public sealed class CanastaGame : GameBase
    {
        private readonly DeterministicRandom rng;private readonly int targetScore;private readonly int[] teamScores=new int[2];private readonly int[] redThrees=new int[2];
        private List<List<CanastaCard>> hands=new List<List<CanastaCard>>();private List<CanastaCard> stock=new List<CanastaCard>();private readonly List<CanastaCard> discard=new List<CanastaCard>();
        private readonly Dictionary<int,List<CanastaCard>>[] melds={new Dictionary<int,List<CanastaCard>>(),new Dictionary<int,List<CanastaCard>>()};
        private readonly bool[] opened=new bool[2];private int dealer=-1;private string phase="draw";private int outTeam=-1;private bool finished;
        public override string GameId=>"canasta";public override string Name=>"カナスタ";
        public CanastaGame(int players,DeterministicRandom rng,IReadOnlyDictionary<string,string> options)
        {Players=4;this.rng=rng;targetScore=options.Integer("target_score",5000);StartHand();}
        private void StartHand()
        {
            dealer=(dealer+1)%4;for(int team=0;team<2;team++){melds[team].Clear();opened[team]=false;redThrees[team]=0;}stock=BuildDeck();rng.Shuffle(stock);hands=Enumerable.Range(0,4).Select(_=>new List<CanastaCard>()).ToList();
            for(int round=0;round<11;round++)for(int player=0;player<4;player++){hands[player].Add(Pop(stock));ExposeRedThrees(player,true);}
            discard.Clear();discard.Add(Pop(stock));outTeam=-1;phase="draw";CurrentPlayer=(dealer+1)%4;
            for(int player=0;player<4;player++)ExposeRedThrees(player,true);
        }
        private static List<CanastaCard> BuildDeck()
        {var deck=new List<CanastaCard>();int id=0;for(int copy=0;copy<2;copy++)foreach(Suit suit in Enum.GetValues(typeof(Suit)))for(int rank=1;rank<=13;rank++)deck.Add(new CanastaCard(id++,suit,rank));for(int joker=0;joker<4;joker++)deck.Add(new CanastaCard(id++,null,0));return deck;}
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player),team=actual%2;
            if(phase=="draw")
            {
                var result=new List<Action>();if(stock.Count>0)result.Add(new Action("draw_stock"));if(CanTakePile(actual))result.Add(new Action("take_pile"));if(stock.Count==0)result.Add(new Action("end_hand"));return result;
            }
            var actions=new List<Action>();
            if(!opened[team]){List<List<CanastaCard>>? bundle=InitialBundle(hands[actual],team,null);if(bundle!=null&&CanRemove(actual,bundle.SelectMany(group=>group)))actions.Add(new Action("initial_meld"));}
            else
            {
                foreach(List<CanastaCard> group in AvailableGroups(hands[actual]))if(CanRemove(actual,group))actions.Add(new Action("meld_group",value:string.Join(",",group.Select(card=>card.Id))));
                for(int index=0;index<hands[actual].Count;index++)
                {CanastaCard card=hands[actual][index];if(CanAdd(team,card)&&CanRemove(actual,new[]{card}))actions.Add(new Action("add_meld",value:card.Id.ToString(CultureInfo.InvariantCulture)));}
            }
            bool canGoOut=HasCanasta(team);for(int index=0;index<hands[actual].Count;index++)if(hands[actual].Count>1||canGoOut)actions.Add(new Action("discard",value:hands[actual][index].Id.ToString(CultureInfo.InvariantCulture)));return actions;
        }
        private bool CanRemove(int player,IEnumerable<CanastaCard> cards)
        {CanastaCard[] removed=cards.ToArray();int remaining=hands[player].Count-removed.Count(card=>hands[player].Any(value=>value.Id==card.Id));int team=player%2;return remaining>=2||HasCanastaAfter(team,removed);}
        private bool HasCanastaAfter(int team,IEnumerable<CanastaCard> additions)
        {
            if(HasCanasta(team))return true;CanastaCard[] cards=additions.ToArray();
            CanastaCard[] naturals=cards.Where(card=>!card.IsWild&&!card.IsRedThree&&!card.IsBlackThree).ToArray();int wilds=cards.Count(card=>card.IsWild);
            if(naturals.Length==0)return wilds==1&&melds[team].Any(pair=>pair.Value.Count>=6&&pair.Value.Count(value=>value.IsWild)<3);
            return naturals.GroupBy(card=>card.Rank).Any(group=>
            {
                int existing=melds[team].TryGetValue(group.Key,out List<CanastaCard>? meld)?meld.Count:0;
                int usableWilds=naturals.Select(card=>card.Rank).Distinct().Count()==1?wilds:0;
                return existing+group.Count()+usableWilds>=7;
            });
        }
        private bool CanTakePile(int player)
        {
            if(discard.Count==0)return false;CanastaCard top=discard[discard.Count-1];if(top.IsWild||top.IsBlackThree||top.IsRedThree)return false;
            if(hands[player].Count(card=>!card.IsWild&&card.Rank==top.Rank)<2)return false;List<List<CanastaCard>>? bundle=opened[player%2]?new List<List<CanastaCard>>{hands[player].Where(card=>!card.IsWild&&card.Rank==top.Rank).Take(2).Concat(new[]{top}).ToList()}:InitialBundle(hands[player],player%2,top);
            if(bundle==null)return false;int handUsed=bundle.SelectMany(group=>group).Count(card=>hands[player].Any(value=>value.Id==card.Id));int pileAdded=discard.Count(card=>card.Id!=top.Id&&!card.IsRedThree);int remaining=hands[player].Count-handUsed+pileAdded;return remaining>=2||HasCanastaAfter(player%2,bundle.SelectMany(group=>group));
        }
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));TurnCount++;int team=player%2;
            if(phase=="draw")
            {
                if(action.Kind=="end_hand"){ScoreHand();return;}
                if(action.Kind=="draw_stock"){hands[player].Add(Pop(stock));if(ExposeRedThrees(player,true)){ScoreHand();return;}phase="meld";return;}
                CanastaCard top=discard[discard.Count-1];List<List<CanastaCard>> bundle;
                if(opened[team])
                {List<CanastaCard> pair=hands[player].Where(card=>!card.IsWild&&card.Rank==top.Rank).Take(2).ToList();pair.Add(top);bundle=new List<List<CanastaCard>>{pair};}
                else bundle=InitialBundle(hands[player],team,top)!;
                List<CanastaCard> pile=discard.ToList();discard.Clear();ApplyGroups(player,bundle);foreach(CanastaCard card in pile.Where(card=>bundle.SelectMany(group=>group).All(used=>used.Id!=card.Id)))
                {if(card.IsRedThree)redThrees[team]++;else hands[player].Add(card);}opened[team]=true;phase="meld";return;
            }
            if(action.Kind=="initial_meld")
            {List<List<CanastaCard>> bundle=InitialBundle(hands[player],team,null)!;ApplyGroups(player,bundle);opened[team]=true;if(hands[player].Count==0&&HasCanasta(team)){outTeam=team;ScoreHand();}return;}
            if(action.Kind=="meld_group")
            {int[] ids=ParseIds(action.Value!);List<CanastaCard> group=hands[player].Where(card=>ids.Contains(card.Id)).ToList();ApplyGroups(player,new[]{group});if(hands[player].Count==0&&HasCanasta(team)){outTeam=team;ScoreHand();}return;}
            if(action.Kind=="add_meld")
            {int id=int.Parse(action.Value!,CultureInfo.InvariantCulture);CanastaCard card=hands[player].First(value=>value.Id==id);hands[player].Remove(card);int rank=card.IsWild?BestWildTarget(team):card.Rank;melds[team][rank].Add(card);if(hands[player].Count==0&&HasCanasta(team)){outTeam=team;ScoreHand();}return;}
            int discardId=int.Parse(action.Value!,CultureInfo.InvariantCulture);CanastaCard thrown=hands[player].First(card=>card.Id==discardId);hands[player].Remove(thrown);discard.Add(thrown);if(hands[player].Count==0){outTeam=team;ScoreHand();return;}phase="draw";CurrentPlayer=(player+1)%4;
        }
        private void ApplyGroups(int player,IEnumerable<List<CanastaCard>> groups)
        {
            int team=player%2;foreach(List<CanastaCard> group in groups)
            {int rank=group.First(card=>!card.IsWild).Rank;foreach(CanastaCard card in group.Where(card=>hands[player].Any(value=>value.Id==card.Id)).ToArray())hands[player].RemoveAll(value=>value.Id==card.Id);if(!melds[team].ContainsKey(rank))melds[team][rank]=new List<CanastaCard>();foreach(CanastaCard card in group)if(melds[team][rank].All(value=>value.Id!=card.Id))melds[team][rank].Add(card);}
        }
        private List<List<CanastaCard>>? InitialBundle(List<CanastaCard> hand,int team,CanastaCard? forcedTop)
        {
            var groups=new List<List<CanastaCard>>();var used=new HashSet<int>();
            if(forcedTop.HasValue)
            {CanastaCard[] pair=hand.Where(card=>!card.IsWild&&card.Rank==forcedTop.Value.Rank).Take(2).ToArray();if(pair.Length<2)return null;var forced=new List<CanastaCard>{pair[0],pair[1],forcedTop.Value};groups.Add(forced);foreach(CanastaCard card in pair)used.Add(card.Id);}
            var wilds=new Queue<CanastaCard>(hand.Where(card=>card.IsWild&&!used.Contains(card.Id)));
            foreach(var rankGroup in hand.Where(card=>!card.IsWild&&!card.IsRedThree&&!card.IsBlackThree&&!used.Contains(card.Id)).GroupBy(card=>card.Rank).OrderByDescending(group=>group.Sum(CardValue)))
            {var group=rankGroup.ToList();if(group.Count<3&&group.Count==2&&wilds.Count>0)group.Add(wilds.Dequeue());if(group.Count>=3){groups.Add(group);foreach(CanastaCard card in group)used.Add(card.Id);}}
            int required=InitialRequirement(team);var selected=new List<List<CanastaCard>>();int total=0;foreach(List<CanastaCard> group in groups.OrderByDescending(group=>group.Sum(CardValue))){selected.Add(group);total+=group.Sum(CardValue);if(total>=required)return selected;}return null;
        }
        private static List<List<CanastaCard>> AvailableGroups(List<CanastaCard> hand)
        {
            var result=new List<List<CanastaCard>>();var wilds=new Queue<CanastaCard>(hand.Where(card=>card.IsWild));
            foreach(var rankGroup in hand.Where(card=>!card.IsWild&&!card.IsRedThree&&!card.IsBlackThree).GroupBy(card=>card.Rank))
            {var group=rankGroup.ToList();if(group.Count<3&&group.Count==2&&wilds.Count>0)group.Add(wilds.Dequeue());if(group.Count>=3)result.Add(group);}return result;
        }
        private bool CanAdd(int team,CanastaCard card)
        {
            if(card.IsRedThree||card.IsBlackThree)return false;if(!card.IsWild)return melds[team].ContainsKey(card.Rank);
            return melds[team].Any(pair=>pair.Value.Count(value=>value.IsWild)<3&&pair.Value.Count(value=>!value.IsWild)>pair.Value.Count(value=>value.IsWild));
        }
        private int BestWildTarget(int team)=>melds[team].Where(pair=>pair.Value.Count(card=>card.IsWild)<3&&pair.Value.Count(card=>!card.IsWild)>pair.Value.Count(card=>card.IsWild)).OrderByDescending(pair=>pair.Value.Count).First().Key;
        private int InitialRequirement(int team)=>teamScores[team]<0?15:teamScores[team]<1500?50:teamScores[team]<3000?90:120;
        private bool HasCanasta(int team)=>melds[team].Values.Any(cards=>cards.Count>=7);
        private bool ExposeRedThrees(int player,bool replace)
        {
            while(true){int index=hands[player].FindIndex(card=>card.IsRedThree);if(index<0)return false;hands[player].RemoveAt(index);redThrees[player%2]++;if(!replace)return false;if(stock.Count==0)return true;hands[player].Add(Pop(stock));}
        }
        private static int CardValue(CanastaCard card)=>card.IsJoker?50:card.Rank==1||card.Rank==2?20:card.Rank>=8?10:5;
        private void ScoreHand()
        {
            for(int team=0;team<2;team++)
            {int value=melds[team].Values.SelectMany(cards=>cards).Sum(CardValue)-Enumerable.Range(0,4).Where(player=>player%2==team).Sum(player=>hands[player].Sum(CardValue));foreach(List<CanastaCard> canasta in melds[team].Values.Where(cards=>cards.Count>=7))value+=canasta.Any(card=>card.IsWild)?300:500;int threes=redThrees[team]==4?800:redThrees[team]*100;value+=opened[team]?threes:-threes;if(outTeam==team)value+=100;teamScores[team]+=value;}
            if(teamScores.Max()>=targetScore)finished=true;else StartHand();
        }
        public override Action ChooseCpuAction(int player,DeterministicRandom random,int difficulty=1)
        {IReadOnlyList<Action> actions=LegalActions(player);if(phase=="draw"){if(actions.Any(action=>action.Kind=="take_pile"))return actions.First(action=>action.Kind=="take_pile");if(actions.Any(action=>action.Kind=="draw_stock"))return actions.First(action=>action.Kind=="draw_stock");return actions.First();}Action[] meldActions=actions.Where(action=>action.Kind=="initial_meld"||action.Kind=="meld_group"||action.Kind=="add_meld").ToArray();if(meldActions.Length>0)return meldActions[0];return actions.Where(action=>action.Kind=="discard").OrderByDescending(action=>CardValue(hands[player].First(card=>card.Id==int.Parse(action.Value!,CultureInfo.InvariantCulture)))).First();}
        public override bool IsTerminal=>finished;
        public override GameResult Result(){if(!finished)throw new InvalidOperationException("Game is not over.");int high=teamScores.Max();return new GameResult(Enumerable.Range(0,4).Where(player=>teamScores[player%2]==high),Enumerable.Range(0,4).Select(player=>(double)teamScores[player%2]),"classic canasta partnership score",TurnCount,new Dictionary<string,object>{{"team_scores",teamScores.ToArray()}});}
        public override string View(int? player=null){int viewer=player??CurrentPlayer;return $"phase={phase} scores=[{string.Join(",",teamScores)}] opened=[{string.Join(",",opened)}] red3=[{string.Join(",",redThrees)}] stock={stock.Count} discard={(discard.Count==0?"-":discard.Last().ToString())} canastas=[{string.Join(",",melds.Select(team=>team.Values.Count(cards=>cards.Count>=7)))}] hand_counts=[{string.Join(",",hands.Select(hand=>hand.Count))}]\nyour hand: {string.Join(" ",hands[viewer])}";}
        private static int[] ParseIds(string value)=>value.Split(',').Select(text=>int.Parse(text,CultureInfo.InvariantCulture)).ToArray();private static CanastaCard Pop(List<CanastaCard> cards){CanastaCard card=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return card;}
        public static void Register(GameRegistry registry)=>registry.Register(new GameInfo("canasta","カナスタ",4,4,"partnership-rummy","4人固定ペア、108枚、捨て札山常時凍結のクラシック採用仕様。赤3、初回メルド下限、7枚カナスタを得点化する。","Pagat Classic Canasta",new Dictionary<string,string>{{"target_score","勝利点（既定5000）"}}),(p,r,o)=>new CanastaGame(p,r,o));
    }

    internal sealed class TableMeld
    {
        public List<Card> Cards { get; }=new List<Card>();
        public List<int> CardOwners { get; }=new List<int>();
        public string Kind { get; set; }
        public int Owner { get; }
        public TableMeld(IEnumerable<Card> cards,string kind,int owner){Cards.AddRange(cards);CardOwners.AddRange(Cards.Select(_=>owner));Kind=kind;Owner=owner;}
        public void Add(Card card,int owner){Cards.Add(card);CardOwners.Add(owner);}
        public override string ToString()=>$"{Kind}[{string.Join(" ",Cards.Select((card,index)=>card+"@P"+CardOwners[index]))}]";
    }

    internal static class RummyRules
    {
        public static bool IsSet(IReadOnlyList<Card> cards)=>cards.Count>=3&&cards.Select(card=>card.Rank).Distinct().Count()==1&&cards.Select(card=>card.Suit).Distinct().Count()==cards.Count;
        public static bool IsRun(IReadOnlyList<Card> cards,bool aceHigh=false)
        {
            if(cards.Count<3||cards.Select(card=>card.Suit).Distinct().Count()!=1||cards.Select(card=>card.Rank).Distinct().Count()!=cards.Count)return false;
            int[] low=cards.Select(card=>card.Rank).OrderBy(value=>value).ToArray();if(Consecutive(low))return true;
            if(!aceHigh||!low.Contains(1))return false;int[] high=low.Select(value=>value==1?14:value).OrderBy(value=>value).ToArray();return Consecutive(high);
        }
        private static bool Consecutive(int[] values)=>values.Zip(values.Skip(1),(left,right)=>right-left).All(value=>value==1);
        public static IReadOnlyList<int[]> NewMelds(IReadOnlyList<Card> hand,bool singleSeven=false,bool aceHigh=false)
        {
            var result=new List<int[]>();
            foreach(var group in hand.Select((card,index)=>Tuple.Create(card,index)).GroupBy(item=>item.Item1.Rank))
            {
                int[] indexes=group.GroupBy(item=>item.Item1.Suit).Select(suit=>suit.First().Item2).ToArray();
                for(int size=3;size<=Math.Min(4,indexes.Length);size++)AddCombinations(indexes,size,0,new List<int>(),result);
            }
            foreach(Suit suit in Enum.GetValues(typeof(Suit)).Cast<Suit>())
            {
                var byRank=hand.Select((card,index)=>Tuple.Create(card,index)).Where(item=>item.Item1.Suit==suit).GroupBy(item=>item.Item1.Rank).ToDictionary(group=>group.Key,group=>group.First().Item2);
                int[] ranks=byRank.Keys.OrderBy(value=>value).ToArray();for(int start=0;start<ranks.Length;start++)for(int end=start+2;end<ranks.Length;end++)
                {int[] selected=ranks.Skip(start).Take(end-start+1).ToArray();if(Consecutive(selected))result.Add(selected.Select(rank=>byRank[rank]).ToArray());}
                if(aceHigh&&byRank.ContainsKey(1))
                {int[] high=byRank.Keys.Where(rank=>rank!=1).Concat(new[]{14}).OrderBy(value=>value).ToArray();for(int start=0;start<high.Length;start++)for(int end=start+2;end<high.Length;end++){int[] selected=high.Skip(start).Take(end-start+1).ToArray();if(Consecutive(selected))result.Add(selected.Select(rank=>byRank[rank==14?1:rank]).ToArray());}}
            }
            if(singleSeven)for(int index=0;index<hand.Count;index++)if(hand[index].Rank==7)result.Add(new[]{index});
            return result.GroupBy(indexes=>string.Join(",",indexes.OrderBy(i=>i))).Select(group=>group.First()).ToArray();
        }
        private static void AddCombinations(int[] values,int size,int offset,List<int> chosen,List<int[]> result)
        {if(chosen.Count==size){result.Add(chosen.ToArray());return;}for(int index=offset;index<=values.Length-(size-chosen.Count);index++){chosen.Add(values[index]);AddCombinations(values,size,index+1,chosen,result);chosen.RemoveAt(chosen.Count-1);}}
        public static string Kind(IReadOnlyList<Card> cards)=>cards.Count==1&&cards[0].Rank==7?"seven":IsSet(cards)?"set":"run";
        public static bool CanLayOff(TableMeld meld,Card card,bool aceHigh=false)
        {
            var combined=meld.Cards.Concat(new[]{card}).ToArray();if(meld.Kind=="set")return IsSet(combined);if(meld.Kind=="run")return IsRun(combined,aceHigh);
            return card.Rank==7?IsSet(combined):IsRun(combined,aceHigh);
        }
        public static int[] ParseIndexes(string value)=>string.IsNullOrEmpty(value)?Array.Empty<int>():value.Split(',').Select(text=>int.Parse(text,CultureInfo.InvariantCulture)).ToArray();
        public static List<Card> RemoveIndexes(List<Card> hand,IEnumerable<int> indexes)
        {int[] values=indexes.Distinct().OrderBy(index=>index).ToArray();var cards=values.Select(index=>hand[index]).ToList();foreach(int index in values.OrderByDescending(index=>index))hand.RemoveAt(index);return cards;}
    }

    public sealed class SevenBridgeGame : GameBase
    {
        private readonly DeterministicRandom rng;private readonly List<List<Card>> hands;private List<Card> stock;private readonly List<Card> discard=new List<Card>();private readonly List<TableMeld> melds=new List<TableMeld>();
        private readonly bool[] hasPlayed;private string phase="draw";private List<int> claimers=new List<int>();private int claimIndex;private int normalNext;private int winner=-1;private int recycles;private bool stalemate;private bool hadMeldAtTurnStart;private bool winnerHadMeld;private bool finished;
        public override string GameId=>"seven_bridge";public override string Name=>"セブンブリッジ";
        public SevenBridgeGame(int players,DeterministicRandom rng)
        {
            Players=players;this.rng=rng;stock=Cards.Shuffled(Cards.StandardDeck(),rng);hands=Enumerable.Range(0,players).Select(_=>new List<Card>()).ToList();
            for(int round=0;round<7;round++)for(int player=0;player<players;player++)hands[player].Add(Pop(stock));discard.Add(Pop(stock));hasPlayed=new bool[players];
        }
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player);
            if(phase=="draw")return new[]{new Action("draw_stock")};
            if(phase=="claim_pon")
            {var actions=new List<Action>{new Action("pass")};if(hasPlayed[actual]&&hands[actual].Count>2)foreach(int[] pair in PairClaims(hands[actual],discard[discard.Count-1]))actions.Add(new Action("pon",value:string.Join(",",pair)));return actions;}
            if(phase=="claim_chi")
            {var actions=new List<Action>{new Action("pass")};if(hasPlayed[actual]&&hands[actual].Count>2)foreach(int[] pair in RunClaims(hands[actual],discard[discard.Count-1]))actions.Add(new Action("chi",value:string.Join(",",pair)));return actions;}
            var result=new List<Action>();
            foreach(int[] indexes in RummyRules.NewMelds(hands[actual],singleSeven:true))if(indexes.Length<hands[actual].Count)result.Add(new Action("meld",value:string.Join(",",indexes)));
            if(melds.Any(meld=>meld.Owner==actual)&&hands[actual].Count>1)for(int index=0;index<hands[actual].Count;index++)for(int target=0;target<melds.Count;target++)if(RummyRules.CanLayOff(melds[target],hands[actual][index]))result.Add(new Action("layoff",target:target,value:index.ToString(CultureInfo.InvariantCulture)));
            for(int index=0;index<hands[actual].Count;index++)result.Add(new Action("discard",value:index.ToString(CultureInfo.InvariantCulture)));return result;
        }
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));TurnCount++;
            if(phase=="draw"){hadMeldAtTurnStart=melds.Any(meld=>meld.Owner==player);if(!Recycle()){stalemate=true;finished=true;return;}hands[player].Add(Pop(stock));phase="meld";return;}
            if(phase=="claim_pon"||phase=="claim_chi")
            {
                if(action.Kind=="pass"){AdvanceClaim();return;}hadMeldAtTurnStart=melds.Any(meld=>meld.Owner==player);List<Card> cards=RummyRules.RemoveIndexes(hands[player],RummyRules.ParseIndexes(action.Value!));cards.Add(Pop(discard));melds.Add(new TableMeld(cards,action.Kind=="pon"?"set":"run",player));phase="meld";return;
            }
            if(action.Kind=="meld")
            {List<Card> cards=RummyRules.RemoveIndexes(hands[player],RummyRules.ParseIndexes(action.Value!));melds.Add(new TableMeld(cards,RummyRules.Kind(cards),player));return;}
            if(action.Kind=="layoff")
            {int index=int.Parse(action.Value!,CultureInfo.InvariantCulture);Card card=hands[player][index];hands[player].RemoveAt(index);TableMeld meld=melds[action.Target!.Value];meld.Add(card,player);if(meld.Kind=="seven")meld.Kind=card.Rank==7?"set":"run";return;}
            int discardIndex=int.Parse(action.Value!,CultureInfo.InvariantCulture);Card thrown=hands[player][discardIndex];hands[player].RemoveAt(discardIndex);discard.Add(thrown);hasPlayed[player]=true;
            if(hands[player].Count==0){winner=player;winnerHadMeld=hadMeldAtTurnStart;finished=true;return;}BeginClaims(player);
        }
        private void BeginClaims(int discarder)
        {normalNext=(discarder+1)%Players;claimers=Enumerable.Range(1,Players-1).Select(offset=>(discarder+offset)%Players).ToList();claimIndex=0;phase="claim_pon";CurrentPlayer=claimers[0];}
        private void AdvanceClaim()
        {
            claimIndex++;if(phase=="claim_pon"&&claimIndex<claimers.Count){CurrentPlayer=claimers[claimIndex];return;}
            if(phase=="claim_pon"){phase="claim_chi";CurrentPlayer=normalNext;return;}phase="draw";CurrentPlayer=normalNext;
        }
        private static IEnumerable<int[]> PairClaims(IReadOnlyList<Card> hand,Card card)
        {int[] matches=Enumerable.Range(0,hand.Count).Where(index=>hand[index].Rank==card.Rank&&hand[index].Suit!=card.Suit).ToArray();var result=new List<int[]>();for(int a=0;a<matches.Length-1;a++)for(int b=a+1;b<matches.Length;b++)if(hand[matches[a]].Suit!=hand[matches[b]].Suit)result.Add(new[]{matches[a],matches[b]});return result;}
        private static IEnumerable<int[]> RunClaims(IReadOnlyList<Card> hand,Card card)
        {var result=new List<int[]>();for(int a=0;a<hand.Count-1;a++)for(int b=a+1;b<hand.Count;b++)if(RummyRules.IsRun(new[]{hand[a],hand[b],card}))result.Add(new[]{a,b});return result;}
        private bool Recycle(){if(stock.Count>0)return true;if(recycles>=1||discard.Count<=1)return false;recycles++;Card top=Pop(discard);stock=discard.ToList();discard.Clear();discard.Add(top);rng.Shuffle(stock);return stock.Count>0;}
        private static int Penalty(Card card)=>card.Rank==7?20:card.Rank==1?1:Math.Min(card.Rank,10);
        public override Action ChooseCpuAction(int player,DeterministicRandom random,int difficulty=1)
        {IReadOnlyList<Action> actions=LegalActions(player);if(phase=="draw")return actions[0];if(phase.StartsWith("claim",StringComparison.Ordinal))return actions.FirstOrDefault(action=>action.Kind!="pass")==default?actions[0]:actions.First(action=>action.Kind!="pass");Action[] plays=actions.Where(action=>action.Kind=="meld"||action.Kind=="layoff").ToArray();if(plays.Length>0)return plays[0];return actions.Where(action=>action.Kind=="discard").OrderByDescending(action=>Penalty(hands[player][int.Parse(action.Value!,CultureInfo.InvariantCulture)])).First();}
        public override bool IsTerminal=>finished;
        public override GameResult Result(){if(!finished)throw new InvalidOperationException("Game is not over.");if(stalemate){double[] drawScores=hands.Select(hand=>(double)-hand.Sum(Penalty)).ToArray();return new GameResult(Enumerable.Range(0,Players),drawScores,"second stock exhaustion draw",TurnCount);}int points=Enumerable.Range(0,Players).Where(i=>i!=winner).Sum(i=>hands[i].Sum(Penalty));if(!winnerHadMeld)points*=2;double[] scores=Enumerable.Range(0,Players).Select(i=>i==winner?(double)points:-hands[i].Sum(Penalty)).ToArray();return new GameResult(new[]{winner},scores,"seven bridge going out",TurnCount,new Dictionary<string,object>{{"winner_points",points}});}
        public override string View(int? player=null){int viewer=player??CurrentPlayer;return $"phase={phase} stock={stock.Count} discard={discard.Last()} melds={string.Join(" ",melds)} hand_counts=[{string.Join(",",hands.Select(hand=>hand.Count))}]\nyour hand: {string.Join(" ",hands[viewer])}";}
        private static Card Pop(List<Card> cards){Card card=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return card;}
        public static void Register(GameRegistry registry)=>registry.Register(new GameInfo("seven_bridge","セブンブリッジ",2,6,"rummy","7枚を配り、通常メルド・付け札に加えて捨て札へのポン優先／チーを順次応答で処理し、最後の1枚を捨てて上がる。","Pagat Seven Bridge"),(p,r,o)=>new SevenBridgeGame(p,r));
    }

    public sealed class Rummy500Game : GameBase
    {
        private readonly DeterministicRandom rng;private readonly int targetScore;private readonly int[] scores;private readonly int[] meldedValues;private List<List<Card>> hands=new List<List<Card>>();private List<Card> stock=new List<Card>();private readonly List<Card> discard=new List<Card>();private readonly List<TableMeld> melds=new List<TableMeld>();
        private int dealer=-1;private string phase="draw";private int protectedIndex=-1;private bool finished;
        public override string GameId=>"rummy_500";public override string Name=>"ラミー500";
        public Rummy500Game(int players,DeterministicRandom rng,IReadOnlyDictionary<string,string> options)
        {Players=players;this.rng=rng;targetScore=options.Integer("target_score",500);scores=new int[players];meldedValues=new int[players];StartHand();}
        private void StartHand()
        {
            dealer=(dealer+1)%Players;stock=Cards.Shuffled(Cards.StandardDeck(copies:Players>=5?2:1),rng);hands=Enumerable.Range(0,Players).Select(_=>new List<Card>()).ToList();int size=Players==2?13:7;
            for(int round=0;round<size;round++)for(int player=0;player<Players;player++)hands[player].Add(Pop(stock));discard.Clear();discard.Add(Pop(stock));melds.Clear();Array.Clear(meldedValues,0,meldedValues.Length);phase="draw";protectedIndex=-1;CurrentPlayer=(dealer+1)%Players;
        }
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player);
            if(phase=="draw")
            {
                var result=new List<Action>();if(stock.Count>0)result.Add(new Action("draw_stock"));result.Add(new Action("draw_discard"));
                for(int pileIndex=0;pileIndex<discard.Count-1;pileIndex++)foreach(string claim in DiscardClaims(hands[actual],discard[pileIndex]))result.Add(new Action("take_discard_meld",value:$"{pileIndex}|{claim}"));if(stock.Count==0)result.Add(new Action("end_hand"));return result;
            }
            var actions=new List<Action>();foreach(int[] indexes in RummyRules.NewMelds(hands[actual],aceHigh:true))actions.Add(new Action("meld",value:string.Join(",",indexes)));
            for(int index=0;index<hands[actual].Count;index++)for(int target=0;target<melds.Count;target++)if(RummyRules.CanLayOff(melds[target],hands[actual][index],aceHigh:true))actions.Add(new Action("layoff",target:target,value:index.ToString(CultureInfo.InvariantCulture)));
            for(int index=0;index<hands[actual].Count;index++)if(index!=protectedIndex)actions.Add(new Action("discard",value:index.ToString(CultureInfo.InvariantCulture)));return actions;
        }
        private static IEnumerable<string> DiscardClaims(IReadOnlyList<Card> hand,Card selected)
        {var combined=hand.Concat(new[]{selected}).ToArray();int selectedIndex=combined.Length-1;return RummyRules.NewMelds(combined,aceHigh:true).Where(indexes=>indexes.Contains(selectedIndex)).Select(indexes=>string.Join(",",indexes.Where(index=>index!=selectedIndex)));}
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));TurnCount++;
            if(phase=="draw")
            {
                if(action.Kind=="end_hand"){ScoreHand();return;}if(action.Kind=="draw_stock"){hands[player].Add(Pop(stock));protectedIndex=-1;phase="meld";return;}
                if(action.Kind=="draw_discard"){hands[player].Add(Pop(discard));protectedIndex=hands[player].Count-1;phase="meld";return;}
                string[] parts=action.Value!.Split('|');int pileIndex=int.Parse(parts[0],CultureInfo.InvariantCulture);Card selected=discard[pileIndex];List<Card> claimed=discard.Skip(pileIndex).ToList();discard.RemoveRange(pileIndex,discard.Count-pileIndex);
                List<Card> cards=RummyRules.RemoveIndexes(hands[player],RummyRules.ParseIndexes(parts[1]));cards.Add(selected);claimed.RemoveAt(0);hands[player].AddRange(claimed);melds.Add(new TableMeld(cards,RummyRules.Kind(cards),player));meldedValues[player]+=MeldValue(cards);protectedIndex=-1;phase="meld";return;
            }
            if(action.Kind=="meld")
            {int[] indexes=RummyRules.ParseIndexes(action.Value!);AdjustProtected(indexes);List<Card> cards=RummyRules.RemoveIndexes(hands[player],indexes);melds.Add(new TableMeld(cards,RummyRules.Kind(cards),player));meldedValues[player]+=MeldValue(cards);if(hands[player].Count==0)ScoreHand();return;}
            if(action.Kind=="layoff")
            {int index=int.Parse(action.Value!,CultureInfo.InvariantCulture);AdjustProtected(new[]{index});Card card=hands[player][index];hands[player].RemoveAt(index);TableMeld meld=melds[action.Target!.Value];meld.Add(card,player);meldedValues[player]+=card.Rank==1&&meld.Kind=="run"&&meld.Cards.Any(value=>value.Rank==2)?1:Value(card);if(hands[player].Count==0)ScoreHand();return;}
            int discardIndex=int.Parse(action.Value!,CultureInfo.InvariantCulture);discard.Add(hands[player][discardIndex]);hands[player].RemoveAt(discardIndex);if(hands[player].Count==0){ScoreHand();return;}phase="draw";protectedIndex=-1;CurrentPlayer=(player+1)%Players;
        }
        private static int Value(Card card)=>card.Rank==1?15:Math.Min(card.Rank,10);
        private static int MeldValue(IEnumerable<Card> cards)
        {Card[] values=cards.ToArray();bool lowAce=values.Any(card=>card.Rank==1)&&values.Any(card=>card.Rank==2)&&RummyRules.IsRun(values,true);return values.Sum(card=>card.Rank==1&&lowAce?1:Value(card));}
        private void AdjustProtected(IEnumerable<int> removed)
        {if(protectedIndex<0)return;int[] indexes=removed.ToArray();if(indexes.Contains(protectedIndex)){protectedIndex=-1;return;}protectedIndex-=indexes.Count(index=>index<protectedIndex);}
        private void ScoreHand(){for(int player=0;player<Players;player++)scores[player]+=meldedValues[player]-hands[player].Sum(Value);int high=scores.Max();if(high>=targetScore&&scores.Count(score=>score==high)==1)finished=true;else StartHand();}
        public override Action ChooseCpuAction(int player,DeterministicRandom random,int difficulty=1)
        {IReadOnlyList<Action> actions=LegalActions(player);if(phase=="draw"){Action[] claims=actions.Where(action=>action.Kind=="take_discard_meld").ToArray();if(claims.Length>0)return claims[0];if(actions.Any(action=>action.Kind=="end_hand"))return actions.First(action=>action.Kind=="end_hand");return actions.First(action=>action.Kind=="draw_stock"||action.Kind=="draw_discard");}Action[] plays=actions.Where(action=>action.Kind=="meld"||action.Kind=="layoff").ToArray();if(plays.Length>0)return plays[0];return actions.Where(action=>action.Kind=="discard").OrderByDescending(action=>Value(hands[player][int.Parse(action.Value!,CultureInfo.InvariantCulture)])).First();}
        public override bool IsTerminal=>finished;
        public override GameResult Result(){if(!finished)throw new InvalidOperationException("Game is not over.");int high=scores.Max();return new GameResult(Enumerable.Range(0,Players).Where(i=>scores[i]==high),scores.Select(v=>(double)v),"first to 500",TurnCount);}
        public override string View(int? player=null){int viewer=player??CurrentPlayer;return $"phase={phase} scores=[{string.Join(",",scores)}] stock={stock.Count} discard=[{string.Join(" ",discard)}] melds={string.Join(" ",melds)} hand_counts=[{string.Join(",",hands.Select(hand=>hand.Count))}]\nyour hand: {string.Join(" ",hands[viewer])}";}
        private static Card Pop(List<Card> cards){Card card=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return card;}
        public static void Register(GameRegistry registry)=>registry.Register(new GameInfo("rummy_500","ラミー500",2,8,"rummy","ジョーカーなし採用仕様。捨て札を遡って取り、その最下札を即時メルドし、公開札点－手札点を500点まで累積する。","Pagat 500 Rummy",new Dictionary<string,string>{{"target_score","勝利点（既定500）"}}),(p,r,o)=>new Rummy500Game(p,r,o));
    }
}
