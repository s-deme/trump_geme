using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    public sealed class BriscolaGame : GameBase
    {
        private static readonly Dictionary<int, int> Strength = new Dictionary<int, int>
            { [2]=0,[4]=1,[5]=2,[6]=3,[7]=4,[11]=5,[12]=6,[13]=7,[3]=8,[1]=9 };
        private static readonly Dictionary<int, int> Points = new Dictionary<int, int>
            { [1]=11,[3]=10,[13]=4,[12]=3,[11]=2 };
        private readonly List<List<Card>> hands = new List<List<Card>> { new List<Card>(), new List<Card>() };
        private readonly List<Card> stock;
        private readonly Suit trump;
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly List<List<Card>> captured = new List<List<Card>> { new List<Card>(), new List<Card>() };
        private bool finished;

        public override string GameId => "briscola";
        public override string Name => "ブリスコラ";

        public BriscolaGame(int players, DeterministicRandom rng)
        {
            Players = 2;
            stock = Cards.Shuffled(Cards.StandardDeck(new[] { 1,2,3,4,5,6,7,11,12,13 }), rng);
            for (int round = 0; round < 3; round++)
                foreach (List<Card> hand in hands) hand.Add(Pop(stock));
            trump = stock[0].Suit;
        }

        private bool Beats(Card challenger, Card leader) =>
            challenger.Suit == leader.Suit ? Strength[challenger.Rank] > Strength[leader.Rank] :
            challenger.Suit == trump && leader.Suit != trump;

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            return hands[actual].Select(card => new Action("play", card)).ToArray();
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player));
            Card card = action.Card!.Value; hands[player].Remove(card);
            trick.Add(Tuple.Create(player, card)); TurnCount++;
            if (trick.Count == 1) { CurrentPlayer = 1 - player; return; }
            int winner = Beats(trick[1].Item2, trick[0].Item2) ? trick[1].Item1 : trick[0].Item1;
            int loser = 1 - winner;
            captured[winner].AddRange(trick.Select(item => item.Item2)); trick.Clear();
            if (stock.Count > 0) hands[winner].Add(Pop(stock));
            if (stock.Count > 0) hands[loser].Add(Pop(stock));
            CurrentPlayer = winner; finished = hands[0].Count == 0;
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom rng, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (trick.Count == 0) return actions.OrderBy(action => action.Card!.Value.Suit == trump)
                .ThenBy(action => Point(action.Card!.Value)).ThenBy(action => Strength[action.Card!.Value.Rank]).First();
            Card lead = trick[0].Item2;
            Action[] winning = actions.Where(action => Beats(action.Card!.Value, lead)).ToArray();
            if (winning.Length > 0 && Point(lead) > 0)
                return winning.OrderBy(action => Strength[action.Card!.Value.Rank]).First();
            Action[] losing = actions.Where(action => !Beats(action.Card!.Value, lead)).ToArray();
            if (losing.Length > 0) return losing.OrderBy(action => Point(action.Card!.Value))
                .ThenBy(action => action.Card!.Value.Suit == trump).First();
            return winning.OrderBy(action => Strength[action.Card!.Value.Rank]).First();
        }

        private static int Point(Card card) => Points.TryGetValue(card.Rank, out int value) ? value : 0;
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            double[] scores = captured.Select(pile => (double)pile.Sum(Point)).ToArray();
            double high = scores.Max();
            return new GameResult(Enumerable.Range(0, 2).Where(i => scores[i] == high),
                scores, "card points (61 wins)", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            return $"trump={Card.SuitCode(trump)} stock={stock.Count} captured points=[{string.Join(",", captured.Select(p => p.Sum(Point)))}] " +
                $"lead={(trick.Count > 0 ? trick[0].Item2.ToString() : "-")}\nyour hand: {string.Join(" ", hands[viewer])}";
        }
        private static Card Pop(List<Card> cards) { Card card=cards[cards.Count-1]; cards.RemoveAt(cards.Count-1); return card; }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("briscola","ブリスコラ",2,2,"trick-taking",
                "40枚・メイフォローでカード点120点を奪い合う。","traditional / gokurakism"),
            (players,rng,options)=>new BriscolaGame(players,rng));
    }
}
