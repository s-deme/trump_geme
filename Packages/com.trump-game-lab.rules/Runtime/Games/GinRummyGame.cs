using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    public sealed class GinRummyGame:GameBase
    {
        private readonly DeterministicRandom rng;private readonly int targetScore,knockLimit;private int dealer=1,roundNo;
        private int[] scores=new int[2],handWins=new int[2];private string phase="draw",reason="";private List<List<Card>> hands=new List<List<Card>>();
        private List<Card> stock=new List<Card>(),discard=new List<Card>();private Card? lastDrawn;private bool finished;
        public override string GameId=>"gin_rummy";public override string Name=>"ジン・ラミー";
        public GinRummyGame(int players,DeterministicRandom rng,IReadOnlyDictionary<string,string> options)
        {Players=2;this.rng=rng;targetScore=options.Integer("target_score",100);knockLimit=options.Integer("knock_limit",10);StartRound();}
        public static int CardPoints(Card c)=>Math.Min(c.Rank,10);
        public static Tuple<int,List<List<Card>>,List<Card>> BestMelds(IEnumerable<Card> source)
        {
            Card[] cards=source.OrderBy(c=>c).ToArray();var masks=new HashSet<int>();
            foreach(IGrouping<int,Card> group in cards.GroupBy(c=>c.Rank))
            {
                int[] ids=Enumerable.Range(0,cards.Length).Where(i=>cards[i].Rank==group.Key).ToArray();
                if(ids.Length>=3){masks.Add(ids.Aggregate(0,(m,i)=>m|(1<<i)));if(ids.Length==4)
                    foreach(int omit in ids)masks.Add(ids.Where(i=>i!=omit).Aggregate(0,(m,i)=>m|(1<<i)));}
            }
            foreach(Suit suit in Enum.GetValues(typeof(Suit)))
            {
                var suited=Enumerable.Range(0,cards.Length).Where(i=>cards[i].Suit==suit).OrderBy(i=>cards[i].Rank).ToArray();
                for(int start=0;start<suited.Length;start++)for(int end=start+3;end<=suited.Length;end++)
                {int[] segment=suited.Skip(start).Take(end-start).ToArray();if(segment.Zip(segment.Skip(1),(a,b)=>cards[b].Rank==cards[a].Rank+1).All(v=>v))
                    masks.Add(segment.Aggregate(0,(m,i)=>m|(1<<i)));}
            }
            var memo=new Dictionary<int,Tuple<int,List<int>>>();
            Func<int,Tuple<int,List<int>>> solve=null!;
            solve=available=>{
                if(memo.TryGetValue(available,out Tuple<int,List<int>> found))return found;
                var best=Tuple.Create(Enumerable.Range(0,cards.Length).Where(i=>(available&(1<<i))!=0).Sum(i=>CardPoints(cards[i])),new List<int>());
                foreach(int mask in masks)if((mask&available)==mask){Tuple<int,List<int>> next=solve(available^mask);if(next.Item1<best.Item1)
                    best=Tuple.Create(next.Item1,new[]{mask}.Concat(next.Item2).ToList());}
                memo[available]=best;return best;};
            Tuple<int,List<int>> result=solve((1<<cards.Length)-1);int used=result.Item2.Aggregate(0,(m,v)=>m|v);
            List<List<Card>> melds=result.Item2.Select(mask=>Enumerable.Range(0,cards.Length).Where(i=>(mask&(1<<i))!=0).Select(i=>cards[i]).ToList()).ToList();
            return Tuple.Create(result.Item1,melds,Enumerable.Range(0,cards.Length).Where(i=>(used&(1<<i))==0).Select(i=>cards[i]).ToList());
        }
        private void StartRound()
        {
            roundNo++;dealer=1-dealer;List<Card> deck=Cards.Shuffled(Cards.StandardDeck(),rng);
            hands=new List<List<Card>>{new List<Card>(),new List<Card>()};for(int r=0;r<10;r++){hands[1-dealer].Add(Pop(deck));hands[dealer].Add(Pop(deck));}
            discard=new List<Card>{Pop(deck)};stock=deck;CurrentPlayer=1-dealer;phase="initial_offer";lastDrawn=null;
        }
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player);
            if(phase=="initial_offer"||phase=="dealer_offer")
                return new[]{new Action("take_upcard",discard.Last()),new Action("pass_upcard")};
            if(phase=="initial_stock")return new[]{new Action("draw_stock")};
            if(phase=="draw"){var a=new List<Action>{new Action("draw_stock")};if(discard.Count>0)a.Add(new Action("draw_discard",discard.Last()));return a;}
            var actions=new List<Action>();foreach(Card card in hands[actual])
            {
                if(lastDrawn.HasValue&&card==lastDrawn.Value)continue;var remaining=new List<Card>(hands[actual]);remaining.Remove(card);
                int dead=BestMelds(remaining).Item1;actions.Add(new Action("discard",card));if(dead<=knockLimit)actions.Add(new Action("knock",card));
            }return actions;
        }
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));
            if(phase=="initial_offer"||phase=="dealer_offer")
            {
                if(action.Kind=="take_upcard")
                {Card c=Pop(discard);hands[player].Add(c);lastDrawn=c;phase="discard";TurnCount++;return;}
                if(phase=="initial_offer"){phase="dealer_offer";CurrentPlayer=dealer;}
                else{phase="initial_stock";CurrentPlayer=1-dealer;}
                TurnCount++;return;
            }
            if(phase=="draw"||phase=="initial_stock"){if(action.Kind=="draw_stock"){hands[player].Add(Pop(stock));lastDrawn=null;}else{Card c=Pop(discard);hands[player].Add(c);lastDrawn=c;}
                phase="discard";TurnCount++;return;}
            Card card=action.Card!.Value;hands[player].Remove(card);discard.Add(card);TurnCount++;if(action.Kind=="knock"){ScoreKnock(player);return;}
            if(stock.Count<=2){StartRound();return;}CurrentPlayer=1-player;phase="draw";lastDrawn=null;
        }
        private static bool CanLayoff(Card card,List<Card> meld)
        {
            int[] ranks=meld.Select(c=>c.Rank).Distinct().OrderBy(v=>v).ToArray();Suit[] suits=meld.Select(c=>c.Suit).Distinct().ToArray();
            return ranks.Length==1?(card.Rank==ranks[0]&&meld.Count<4):(suits.Length==1&&card.Suit==suits[0]&&(card.Rank==ranks[0]-1||card.Rank==ranks[ranks.Length-1]+1));
        }
        private static int MinimumAfterLayoff(List<Card> unmatched,List<List<Card>> melds)
        {
            int best=unmatched.Sum(CardPoints);Action<List<Card>,List<List<Card>>> search=null!;
            search=(remaining,groups)=>{best=Math.Min(best,remaining.Sum(CardPoints));for(int i=0;i<remaining.Count;i++)for(int j=0;j<groups.Count;j++)
                if(CanLayoff(remaining[i],groups[j])){var rest=new List<Card>(remaining);Card c=rest[i];rest.RemoveAt(i);var copy=groups.Select(g=>new List<Card>(g)).ToList();copy[j].Add(c);search(rest,copy);}};
            search(unmatched,melds.Select(g=>new List<Card>(g)).ToList());return best;
        }
        private void ScoreKnock(int knocker)
        {
            int opponent=1-knocker;var own=BestMelds(hands[knocker]);var opp=BestMelds(hands[opponent]);
            int scorer;
            if(own.Item1==0){int points=20+opp.Item1;scores[knocker]+=points;reason="gin +"+points;scorer=knocker;}
            else{int after=MinimumAfterLayoff(opp.Item3,own.Item2);if(own.Item1<after){int points=after-own.Item1;scores[knocker]+=points;reason="knock +"+points;scorer=knocker;}
                else{int points=10+own.Item1-after;scores[opponent]+=points;reason=$"undercut P{opponent} +{points}";scorer=opponent;}}
            handWins[scorer]++;
            if(scores.Max()>=targetScore)FinishMatch();else StartRound();
        }
        private void FinishMatch()
        {
            int winner=scores[0]>scores[1]?0:1,loser=1-winner;
            int gameBonus=scores[loser]==0?200:100;
            scores[winner]+=gameBonus;
            for(int player=0;player<scores.Length;player++)scores[player]+=20*handWins[player];
            reason+=$"; game bonus +{gameBonus}, line bonuses applied";
            finished=true;
        }
        public override Action ChooseCpuAction(int player,DeterministicRandom random,int difficulty=1)
        {
            IReadOnlyList<Action> actions=LegalActions(player);
            if(phase=="initial_offer"||phase=="dealer_offer")
            {Card top=discard.Last();int current=BestMelds(hands[player]).Item1;var with=new List<Card>(hands[player]){top};
                int after=Enumerable.Range(0,with.Count).Where(i=>with[i]!=top).Select(i=>BestMelds(with.Where((c,j)=>j!=i)).Item1).Min();
                return actions[after<current?0:1];}
            if(phase=="initial_stock")return actions[0];
            if(phase=="draw"){Card top=discard.Last();int current=BestMelds(hands[player]).Item1;var with=new List<Card>(hands[player]){top};
                int after=Enumerable.Range(0,with.Count).Where(i=>with[i]!=top).Select(i=>BestMelds(with.Where((c,j)=>j!=i)).Item1).Min();return after<current?actions[1]:actions[0];}
            Action[] knocks=actions.Where(a=>a.Kind=="knock").ToArray();IEnumerable<Action> pool=knocks.Length>0?knocks:actions;
            return pool.OrderBy(a=>BestMelds(hands[player].Where(c=>c!=a.Card!.Value)).Item1).First();
        }
        public override bool IsTerminal=>finished;
        public override GameResult Result(){if(!finished)throw new InvalidOperationException("Game is not over.");int high=scores.Max();
            return new GameResult(Enumerable.Range(0,2).Where(i=>scores[i]==high),scores.Select(v=>(double)v),reason,TurnCount);}
        public override string View(int? player=null){int viewer=player??CurrentPlayer;return
            $"round={roundNo} phase={phase} scores=[{string.Join(",",scores)}] stock={stock.Count} discard={discard.Last()} opponent cards={hands[1-viewer].Count}\n"+
            $"your hand (deadwood={BestMelds(hands[viewer]).Item1}): {string.Join(" ",hands[viewer])}";}
        private static Card Pop(List<Card> cards){Card c=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return c;}
        public static void Register(GameRegistry registry)=>registry.Register(new GameInfo("gin_rummy","ジン・ラミー",2,2,"rummy",
            "セットとランを作り、デッドウッド10点以下でノックする。","traditional / gokurakism",
            new Dictionary<string,string>{{"target_score","勝利点（既定100）"},{"knock_limit","ノック可能なデッドウッド上限（既定10）"}}),(p,r,o)=>new GinRummyGame(p,r,o));
    }
}
