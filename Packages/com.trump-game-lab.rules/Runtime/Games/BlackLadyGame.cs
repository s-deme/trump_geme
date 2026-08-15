using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    public sealed class BlackLadyGame:GameBase
    {
        private readonly DeterministicRandom rng;private readonly int roundsToPlay;private int dealer,roundNo,carry;
        private int[] totalScores;private string phase="pass_two";private List<List<Card>> hands=new List<List<Card>>();
        private List<List<Card>?> pending=new List<List<Card>?>();private List<Card> table=new List<Card>();
        private readonly List<Tuple<int,Card>> trick=new List<Tuple<int,Card>>();private List<List<Card>> captured=new List<List<Card>>();private bool finished;
        public override string GameId=>"black_lady";public override string Name=>"ブラックレディー";
        public BlackLadyGame(int players,DeterministicRandom rng,IReadOnlyDictionary<string,string> options)
        {Players=players;this.rng=rng;roundsToPlay=options.Integer("rounds",players);dealer=players-1;totalScores=new int[players];StartRound();}
        private static int Strength(Card c)=>c.Rank==1?14:c.Rank;
        private static int Penalty(Card c)=>c.Suit==Suit.Hearts?1:c.Suit==Suit.Spades&&c.Rank==12?13:0;
        private void StartRound()
        {
            roundNo++;dealer=(dealer+1)%Players;List<Card> deck=Cards.Shuffled(Cards.StandardDeck(),rng);int size=deck.Count/Players;
            hands=Enumerable.Range(0,Players).Select(_=>new List<Card>()).ToList();
            for(int r=0;r<size;r++)for(int offset=1;offset<=Players;offset++)hands[(dealer+offset)%Players].Add(Pop(deck));
            table=deck;captured=Enumerable.Range(0,Players).Select(_=>new List<Card>()).ToList();trick.Clear();
            phase="pass_two";pending=Enumerable.Range(0,Players).Select(_=>(List<Card>?)null).ToList();CurrentPlayer=(dealer+1)%Players;
        }
        private int Right(int player)=>(player+Players-1)%Players;
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player);var actions=new List<Action>();
            if(phase=="pass_two"){for(int a=0;a<hands[actual].Count;a++)for(int b=a+1;b<hands[actual].Count;b++)
                actions.Add(new Action("pass_two",value:a+","+b));return actions;}
            if(phase=="pass_one")return hands[actual].Select(c=>new Action("pass_one",c)).ToArray();
            IEnumerable<Card> cards=hands[actual];if(trick.Count>0){Suit led=trick[0].Item2.Suit;Card[] follow=cards.Where(c=>c.Suit==led).ToArray();if(follow.Length>0)cards=follow;}
            return cards.Select(c=>new Action("play",c)).ToArray();
        }
        private void NextPassOrDeliver()
        {
            int start=(dealer+1)%Players;
            for(int offset=0;offset<Players;offset++){int candidate=(start+offset)%Players;if(pending[candidate]==null){CurrentPlayer=candidate;return;}}
            for(int sender=0;sender<Players;sender++)hands[Right(sender)].AddRange(pending[sender]!);
            if(phase=="pass_two"){phase="pass_one";pending=Enumerable.Range(0,Players).Select(_=>(List<Card>?)null).ToList();CurrentPlayer=start;}
            else{phase="play";CurrentPlayer=start;}
        }
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));
            if(phase=="pass_two")
            {
                int[] indices=action.Value!.Split(',').Select(int.Parse).OrderByDescending(i=>i).ToArray();var cards=new List<Card>();
                foreach(int index in indices){cards.Add(hands[player][index]);hands[player].RemoveAt(index);}pending[player]=cards;TurnCount++;NextPassOrDeliver();return;
            }
            if(phase=="pass_one"){Card c=action.Card!.Value;hands[player].Remove(c);pending[player]=new List<Card>{c};TurnCount++;NextPassOrDeliver();return;}
            Card card=action.Card!.Value;hands[player].Remove(card);trick.Add(Tuple.Create(player,card));TurnCount++;
            if(trick.Count<Players){CurrentPlayer=(player+1)%Players;return;}int winner=trick[0].Item1;Card winning=trick[0].Item2;Suit led=winning.Suit;
            foreach(Tuple<int,Card> item in trick.Skip(1))if(item.Item2.Suit==led&&Strength(item.Item2)>Strength(winning)){winner=item.Item1;winning=item.Item2;}
            captured[winner].AddRange(trick.Select(x=>x.Item2));trick.Clear();CurrentPlayer=winner;
            if(hands[0].Count==0){captured[winner].AddRange(table);ScoreRound();}
        }
        private void ScoreRound()
        {
            int[] penalties=captured.Select(p=>p.Sum(Penalty)).ToArray();for(int i=0;i<Players;i++)totalScores[i]-=penalties[i];
            int[] clear=Enumerable.Range(0,Players).Where(i=>penalties[i]==0).ToArray();int pool=carry+26;
            if(clear.Length>0){int share=pool/clear.Length;carry=pool%clear.Length;foreach(int p in clear)totalScores[p]+=share;}else carry=pool;
            if(roundNo>=roundsToPlay)finished=true;else StartRound();
        }
        public override Action ChooseCpuAction(int player,DeterministicRandom random,int difficulty=1)
        {
            IReadOnlyList<Action> actions=LegalActions(player);
            if(phase=="pass_two")return actions.OrderByDescending(a=>a.Value!.Split(',').Select(int.Parse)
                .Sum(i=>Penalty(hands[player][i])+Strength(hands[player][i])/20.0)).First();
            if(phase=="pass_one")return actions.OrderByDescending(a=>Penalty(a.Card!.Value)+Strength(a.Card.Value)/20.0).First();
            if(trick.Count==0)return actions.OrderBy(a=>Strength(a.Card!.Value)).First();Suit led=trick[0].Item2.Suit;
            Card high=trick.Where(x=>x.Item2.Suit==led).OrderByDescending(x=>Strength(x.Item2)).First().Item2;
            Action[] losing=actions.Where(a=>a.Card!.Value.Suit!=led||Strength(a.Card.Value)<Strength(high)).ToArray();
            if(losing.Length>0)return losing.OrderByDescending(a=>Penalty(a.Card!.Value)).ThenByDescending(a=>Strength(a.Card!.Value)).First();
            return actions.OrderBy(a=>Strength(a.Card!.Value)).First();
        }
        public override bool IsTerminal=>finished;
        public override GameResult Result(){if(!finished)throw new InvalidOperationException("Game is not over.");int high=totalScores.Max();
            return new GameResult(Enumerable.Range(0,Players).Where(i=>totalScores[i]==high),totalScores.Select(v=>(double)v),
                "highest score after penalties and clear bonuses",TurnCount,new Dictionary<string,object>{{"carry",carry}});}
        public override string View(int? player=null){int viewer=player??CurrentPlayer;return
            $"round={roundNo}/{roundsToPlay} phase={phase} dealer=P{dealer} scores=[{string.Join(",",totalScores)}] carry={carry}\n"+
            $"trick: {(trick.Count>0?string.Join(" ",trick.Select(x=>x.Item2)):"-")} hands=[{string.Join(",",hands.Select(h=>h.Count))}]\n"+
            $"your hand: {string.Join(" ",hands[viewer])}";}
        private static Card Pop(List<Card> cards){Card c=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return c;}
        public static void Register(GameRegistry registry)=>registry.Register(new GameInfo("black_lady","ブラックレディー",3,7,"trick-avoidance",
            "ハートとスペードQを避け、無失点者は繰越ボーナスを得る。","traditional / gokurakism",
            new Dictionary<string,string>{{"rounds","プレイするラウンド数（既定は人数と同じ）"}}),(p,r,o)=>new BlackLadyGame(p,r,o));
    }
}
