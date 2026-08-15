using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    public sealed class GermanWhistGame : GameBase
    {
        private readonly List<List<Card>> hands = new List<List<Card>> { new List<Card>(), new List<Card>() };
        private readonly List<Card> stock;
        private Card? faceUp;
        private readonly Suit trump;
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly int[] secondPhaseTricks = new int[2];
        private int? winner;
        public override string GameId => "german_whist";
        public override string Name => "ジャーマンホイスト";

        public GermanWhistGame(int players, DeterministicRandom rng)
        {
            Players = 2; stock = Cards.Shuffled(Cards.StandardDeck(), rng);
            for (int round = 0; round < 13; round++)
                foreach (List<Card> hand in hands) hand.Add(Pop(stock));
            faceUp = Pop(stock); trump = faceUp.Value.Suit;
        }
        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private bool Beats(Card challenger, Card leader) =>
            challenger.Suit == leader.Suit ? Strength(challenger) > Strength(leader) :
            challenger.Suit == trump && leader.Suit != trump;
        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player); IEnumerable<Card> cards = hands[actual];
            if (trick.Count > 0)
            {
                Suit led = trick[0].Item2.Suit;
                Card[] following = cards.Where(card => card.Suit == led).ToArray();
                if (following.Length > 0) cards = following;
            }
            return cards.Select(card => new Action("play", card)).ToArray();
        }
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null); Guard.Legal(action,LegalActions(player));
            Card card=action.Card!.Value; hands[player].Remove(card); trick.Add(Tuple.Create(player,card)); TurnCount++;
            if(trick.Count==1){CurrentPlayer=1-player;return;}
            int win=Beats(trick[1].Item2,trick[0].Item2)?trick[1].Item1:trick[0].Item1;
            int lose=1-win;
            if(faceUp.HasValue)
            {
                hands[win].Add(faceUp.Value); hands[lose].Add(Pop(stock));
                faceUp=stock.Count>0?Pop(stock):(Card?)null;
            }
            else secondPhaseTricks[win]++;
            trick.Clear(); CurrentPlayer=win;
            if(hands[0].Count==0)
                winner=secondPhaseTricks[0]==secondPhaseTricks[1]?-1:
                    (secondPhaseTricks[0]>secondPhaseTricks[1]?0:1);
        }
        public override Action ChooseCpuAction(int player,DeterministicRandom rng,int difficulty=1)
        {
            IReadOnlyList<Action> actions=LegalActions(player);
            if(trick.Count==0)
                return faceUp.HasValue&&(faceUp.Value.Suit==trump||Strength(faceUp.Value)>=11)
                    ?actions.OrderByDescending(a=>Strength(a.Card!.Value)).First()
                    :actions.OrderBy(a=>Strength(a.Card!.Value)).First();
            Card lead=trick[0].Item2;
            Action[] wins=actions.Where(a=>Beats(a.Card!.Value,lead)).ToArray();
            return (wins.Length>0?wins:actions).OrderBy(a=>Strength(a.Card!.Value)).First();
        }
        public override bool IsTerminal=>winner.HasValue;
        public override GameResult Result()
        {
            if(!winner.HasValue)throw new InvalidOperationException("Game is not over.");
            return new GameResult(winner.Value<0?Array.Empty<int>():new[]{winner.Value},
                secondPhaseTricks.Select(v=>(double)v),"second-phase tricks",TurnCount);
        }
        public override string View(int? player=null)
        {
            int viewer=player??CurrentPlayer;
            return $"phase={(faceUp.HasValue?1:2)} trump={Card.SuitCode(trump)} face_up={(faceUp.HasValue?faceUp.Value.ToString():"-")} " +
                $"lead={(trick.Count>0?trick[0].Item2.ToString():"-")} tricks=[{string.Join(",",secondPhaseTricks)}]\n" +
                $"your hand: {string.Join(" ",hands[viewer])}";
        }
        private static Card Pop(List<Card> cards){Card c=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return c;}
        public static void Register(GameRegistry registry)=>registry.Register(
            new GameInfo("german_whist","ジャーマンホイスト",2,2,"trick-taking",
                "前半で後半用のカードを獲得し、後半13トリックを競う。","traditional / gokurakism"),
            (players,rng,options)=>new GermanWhistGame(players,rng));
    }
}
