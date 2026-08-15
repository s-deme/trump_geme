using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    public sealed class MinimoGame:GameBase
    {
        private readonly DeterministicRandom rng;private int dealer=2,roundNo,pot;private int[] chips,roundTricks=new int[3];
        private string phase="double";private bool doubled,finished;private int doublePlayer;
        private List<List<Card>> hands=new List<List<Card>>();private readonly List<Tuple<int,Card>> trick=new List<Tuple<int,Card>>();
        public override string GameId=>"minimo";public override string Name=>"ミニモ";
        public MinimoGame(int players,DeterministicRandom rng,IReadOnlyDictionary<string,string> options)
        {Players=3;this.rng=rng;chips=Enumerable.Repeat(options.Integer("starting_chips",10),3).ToArray();StartRound();}
        private void Pay(int player,int amount){int paid=Math.Min(amount,chips[player]);chips[player]-=paid;pot+=paid;}
        private void StartRound()
        {
            dealer=(dealer+1)%3;roundNo++;for(int i=0;i<3;i++)Pay(i,1);
            var deck=new List<Card>();foreach(Suit suit in new[]{Suit.Spades,Suit.Hearts})for(int rank=2;rank<=6;rank++)deck.Add(new Card(suit,rank));
            rng.Shuffle(deck);hands=Enumerable.Range(0,3).Select(_=>new List<Card>()).ToList();
            for(int r=0;r<3;r++)for(int offset=1;offset<=3;offset++)hands[(dealer+offset)%3].Add(Pop(deck));
            roundTricks=new int[3];trick.Clear();phase="double";doubled=false;doublePlayer=(dealer+2)%3;CurrentPlayer=doublePlayer;
        }
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player);if(phase=="double")return new[]{new Action("no_double"),new Action("double")};
            IEnumerable<Card> cards=hands[actual];if(trick.Count>0){Suit led=trick[0].Item2.Suit;Card[] follow=cards.Where(c=>c.Suit==led).ToArray();if(follow.Length>0)cards=follow;}
            return cards.Select(c=>new Action("play",c)).ToArray();
        }
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));
            if(phase=="double"){doubled=action.Kind=="double";phase="play";CurrentPlayer=(dealer+1)%3;TurnCount++;return;}
            Card card=action.Card!.Value;hands[player].Remove(card);trick.Add(Tuple.Create(player,card));TurnCount++;
            if(trick.Count<3){CurrentPlayer=(player+1)%3;return;}int winner=trick[0].Item1;Card winning=trick[0].Item2;Suit led=winning.Suit;
            foreach(Tuple<int,Card> item in trick.Skip(1))if(item.Item2.Suit==led&&item.Item2.Rank>winning.Rank){winner=item.Item1;winning=item.Item2;}
            roundTricks[winner]++;trick.Clear();CurrentPlayer=winner;if(hands[0].Count==0)ScoreRound();
        }
        private void ScoreRound()
        {
            int[] one=Enumerable.Range(0,3).Where(i=>roundTricks[i]==1).ToArray();int sweep=Array.FindIndex(roundTricks,t=>t==3);
            if(one.Length==1){int winner=one[0];if(doubled)for(int i=0;i<3;i++)if(i!=winner)Pay(i,1);chips[winner]+=pot;pot=0;}
            else if(sweep>=0){Pay(sweep,1);if(doubled)Pay(doublePlayer,1);}else if(doubled)Pay(doublePlayer,1);
            if(chips.Contains(0))finished=true;else StartRound();
        }
        public override Action ChooseCpuAction(int player,DeterministicRandom random,int difficulty=1)
        {
            IReadOnlyList<Action> actions=LegalActions(player);if(phase=="double")return new Action(hands[player].Count(c=>c.Rank>=5)==1?"double":"no_double");
            if(trick.Count==0)return actions.OrderBy(a=>a.Card!.Value.Rank).First();Card lead=trick[0].Item2;
            Action[] wins=actions.Where(a=>a.Card!.Value.Suit==lead.Suit&&a.Card.Value.Rank>lead.Rank).ToArray();
            if(roundTricks[player]==0&&wins.Length>0)return wins.OrderBy(a=>a.Card!.Value.Rank).First();
            Action[] lose=actions.Except(wins).ToArray();return (lose.Length>0?lose:actions).OrderBy(a=>a.Card!.Value.Rank).First();
        }
        public override bool IsTerminal=>finished;
        public override GameResult Result(){if(!finished)throw new InvalidOperationException("Game is not over.");int high=chips.Max();
            return new GameResult(Enumerable.Range(0,3).Where(i=>chips[i]==high),chips.Select(v=>(double)v),"most chips",TurnCount);}
        public override string View(int? player=null){int viewer=player??CurrentPlayer;return
            $"round={roundNo} phase={phase} dealer=P{dealer} chips=[{string.Join(",",chips)}] pot={pot} tricks=[{string.Join(",",roundTricks)}]\n"+
            $"your hand: {string.Join(" ",hands[viewer])}";}
        private static Card Pop(List<Card> cards){Card c=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return c;}
        public static void Register(GameRegistry registry)=>registry.Register(new GameInfo("minimo","ミニモ",3,3,"exact-trick",
            "3枚の手札でちょうど1トリックを単独達成し、ポットを得る。","gokurakism",
            new Dictionary<string,string>{{"starting_chips","各プレイヤーの開始チップ（既定10）"}}),
            (p,r,o)=>new MinimoGame(p,r,o));
    }
}
