using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    public abstract class MultiRoundTrickGame:GameBase
    {
        protected readonly DeterministicRandom Rng;protected int RoundNo,Dealer;
        protected int[] TotalScores,RoundTricks;protected List<List<Card>> Hands=new List<List<Card>>();
        protected readonly List<Tuple<int,Card>> Trick=new List<Tuple<int,Card>>();
        protected Suit? Trump;private bool finished;
        protected MultiRoundTrickGame(int players,DeterministicRandom rng)
        {Players=players;Rng=rng;Dealer=players-1;TotalScores=new int[players];RoundTricks=new int[players];StartRound();}
        protected static int Strength(Card card)=>card.Rank==1?14:card.Rank;
        protected virtual List<Card> MakeDeck()=>Cards.StandardDeck();
        protected abstract int HandSize(List<Card> deck);
        protected virtual Suit? ChooseTrump(List<Card> deck)=>null;
        protected abstract int[] ScoreRound();
        protected abstract bool MatchOver();
        protected virtual int TrickValue()=>1;
        protected virtual void OnTrickWon(int winner,IReadOnlyList<Card> cards){}
        protected virtual int TargetTricks(int player)=>3;
        protected virtual void StartRound()
        {
            Dealer=(Dealer+1)%Players;RoundNo++;List<Card> deck=Cards.Shuffled(MakeDeck(),Rng);
            int size=HandSize(deck);Hands=Enumerable.Range(0,Players).Select(_=>new List<Card>()).ToList();
            for(int round=0;round<size;round++)for(int offset=1;offset<=Players;offset++)
                Hands[(Dealer+offset)%Players].Add(Pop(deck));
            Trump=ChooseTrump(deck);Trick.Clear();RoundTricks=new int[Players];CurrentPlayer=(Dealer+1)%Players;
        }
        private bool Beats(Card challenger,Card leader)=>challenger.Suit==leader.Suit
            ?Strength(challenger)>Strength(leader):Trump.HasValue&&challenger.Suit==Trump.Value&&leader.Suit!=Trump.Value;
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player);IEnumerable<Card> cards=Hands[actual];
            if(Trick.Count>0){Suit led=Trick[0].Item2.Suit;Card[] follow=cards.Where(c=>c.Suit==led).ToArray();if(follow.Length>0)cards=follow;}
            return cards.Select(c=>new Action("play",c)).ToArray();
        }
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));Card card=action.Card!.Value;
            Hands[player].Remove(card);Trick.Add(Tuple.Create(player,card));TurnCount++;
            if(Trick.Count<Players){CurrentPlayer=(player+1)%Players;return;}
            int winner=Trick[0].Item1;Card winning=Trick[0].Item2;
            foreach(Tuple<int,Card> item in Trick.Skip(1))if(Beats(item.Item2,winning)){winner=item.Item1;winning=item.Item2;}
            OnTrickWon(winner,Trick.Select(x=>x.Item2).ToArray());RoundTricks[winner]+=TrickValue();Trick.Clear();CurrentPlayer=winner;
            if(Hands[0].Count==0){int[] delta=ScoreRound();for(int i=0;i<Players;i++)TotalScores[i]+=delta[i];
                if(MatchOver())finished=true;else StartRound();}
        }
        public override Action ChooseCpuAction(int player,DeterministicRandom rng,int difficulty=1)
        {
            IReadOnlyList<Action> actions=LegalActions(player);int target=TargetTricks(player);
            if(Trick.Count==0)return (RoundTricks[player]>=target?actions.OrderBy(a=>Strength(a.Card!.Value)):
                actions.OrderByDescending(a=>Strength(a.Card!.Value))).First();
            Card lead=Trick[0].Item2;Action[] wins=actions.Where(a=>Beats(a.Card!.Value,lead)).ToArray();
            if(RoundTricks[player]<target&&wins.Length>0)return wins.OrderBy(a=>Strength(a.Card!.Value)).First();
            Action[] lose=actions.Except(wins).ToArray();return (lose.Length>0?lose:actions).OrderBy(a=>Strength(a.Card!.Value)).First();
        }
        public override bool IsTerminal=>finished;
        public override GameResult Result(){if(!finished)throw new InvalidOperationException("Game is not over.");int high=TotalScores.Max();
            return new GameResult(Enumerable.Range(0,Players).Where(i=>TotalScores[i]==high),TotalScores.Select(v=>(double)v),"match score",TurnCount);}
        public override string View(int? player=null){int viewer=player??CurrentPlayer;return
            $"round={RoundNo} dealer=P{Dealer} trump={(Trump.HasValue?Card.SuitCode(Trump.Value):"-")} round tricks=[{string.Join(",",RoundTricks)}] total=[{string.Join(",",TotalScores)}]\n"+
            $"trick: {(Trick.Count>0?string.Join(" ",Trick.Select(x=>x.Item2)):"-")}\nyour hand: {string.Join(" ",Hands[viewer])}";}
        protected static Card Pop(List<Card> cards){Card c=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return c;}

        public static void RegisterGames(GameRegistry registry)
        {
            registry.Register(new GameInfo("three_tricks","スリートリックス",4,4,"exact-trick",
                "切り札なしで、各ラウンド3トリック獲得を目指す。","gokurakism"),(p,r,o)=>new ThreeTricksGame(p,r));
            registry.Register(new GameInfo("knave","ネイブ",3,3,"trick-taking",
                "トリックは加点、獲得したJはスート別の減点になる。","traditional / gokurakism"),(p,r,o)=>new KnaveGame(p,r));
            registry.Register(new GameInfo("four_tricks","フォートリックス",3,3,"exact-trick",
                "全12トリックだが最終トリックを2扱いとし、4獲得を目指す。","gokurakism"),(p,r,o)=>new FourTricksGame(p,r));
        }
    }
    public sealed class ThreeTricksGame:MultiRoundTrickGame
    {
        public override string GameId=>"three_tricks";public override string Name=>"スリートリックス";
        public ThreeTricksGame(int p,DeterministicRandom r):base(p,r){}
        protected override int HandSize(List<Card> deck)=>13;
        protected override int[] ScoreRound()=>RoundTricks.Select(t=>t==0?-5:t<=3?t*t:-t).ToArray();
        protected override bool MatchOver()=>RoundNo>=4;
    }
    public sealed class KnaveGame:MultiRoundTrickGame
    {
        private int[] penalties=Array.Empty<int>();
        public override string GameId=>"knave";public override string Name=>"ネイブ";
        public KnaveGame(int p,DeterministicRandom r):base(p,r){}
        protected override int HandSize(List<Card> deck)=>17;
        protected override Suit? ChooseTrump(List<Card> deck)=>Pop(deck).Suit;
        protected override void StartRound(){base.StartRound();penalties=new int[Players];}
        protected override void OnTrickWon(int winner,IReadOnlyList<Card> cards)
        {foreach(Card c in cards.Where(c=>c.Rank==11))penalties[winner]+=c.Suit==Suit.Hearts?-4:c.Suit==Suit.Diamonds?-3:c.Suit==Suit.Clubs?-2:-1;}
        protected override int[] ScoreRound()=>RoundTricks.Select((t,i)=>t+penalties[i]).ToArray();
        protected override bool MatchOver()=>TotalScores.Max()>=20;
        protected override int TargetTricks(int player)=>17;
    }
    public sealed class FourTricksGame:MultiRoundTrickGame
    {
        public override string GameId=>"four_tricks";public override string Name=>"フォートリックス";
        public FourTricksGame(int p,DeterministicRandom r):base(p,r){}
        protected override List<Card> MakeDeck()=>Cards.StandardDeck(new[]{1,6,7,8,9,10,11,12,13});
        protected override int HandSize(List<Card> deck)=>12;
        protected override int TrickValue()=>Hands.All(h=>h.Count==0)?2:1;
        protected override int[] ScoreRound()=>RoundTricks.Select(t=>t==0?-5:t==1?1:t==2?3:t==3?6:t==4?10:-t).ToArray();
        protected override bool MatchOver()=>RoundNo>=3;
        protected override int TargetTricks(int player)=>4;
    }
}
