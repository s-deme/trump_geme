using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    internal static class GoninkanAndNapoleonGames
    {
        public static void RegisterGames(GameRegistry registry)
        {
            GoninkanGame.Register(registry);
            NapoleonGame.Register(registry);
        }
    }

    public sealed class GoninkanGame : GameBase
    {
        private sealed class GCard
        {
            public Card? Card { get; }
            public bool Joker => !Card.HasValue;
            public string Id => Joker ? "JOKER" : Card!.Value.ToString();
            public GCard(Card? card) { Card = card; }
            public override string ToString() => Id;
        }
        private readonly DeterministicRandom rng;
        private readonly List<List<GCard>> hands = Enumerable.Range(0, 5).Select(_ => new List<GCard>()).ToList();
        private readonly List<List<GCard>> captured = Enumerable.Range(0, 5).Select(_ => new List<GCard>()).ToList();
        private readonly List<Tuple<int, GCard>> trick = new List<Tuple<int, GCard>>();
        private readonly int[] scores = new int[5];
        private readonly HashSet<int> relationship = new HashSet<int>();
        private int round;
        private int match = 1;
        private int trickNumber;
        private Suit trump;
        private Suit? jokerLeadSuit;
        private string phase = "play";
        private bool finished;
        public override string GameId => "goninkan";
        public override string Name => "ゴニンカン";
        public GoninkanGame(int players, DeterministicRandom rng) { Players = 5; this.rng = rng; StartRound(); }
        private void StartRound()
        {
            match = 1; trump = round == 9 ? Suit.Spades : (Suit)(round % 3); DealCards(true);
        }
        private void DealCards(bool determineTeam)
        {
            foreach (List<GCard> pile in hands) pile.Clear(); foreach (List<GCard> pile in captured) pile.Clear(); trick.Clear();
            var deck = Cards.StandardDeck().Where(card => card.Rank != 2 || card.Suit == Suit.Spades).Select(card => new GCard(card)).ToList(); deck.Add(new GCard(null)); rng.Shuffle(deck);
            for (int card = 0; card < 10; card++) for (int player = 0; player < 5; player++) hands[player].Add(Pop(deck));
            if (determineTeam)
            {
                relationship.Clear(); int jokerHolder = Enumerable.Range(0, 5).Single(player => hands[player].Any(card => card.Joker));
                int aceHolder = Enumerable.Range(0, 5).Single(player => hands[player].Any(card => card.Card == new Card(trump, 1)));
                relationship.Add(jokerHolder); relationship.Add(aceHolder == jokerHolder ? (jokerHolder + 2) % 5 : aceHolder);
            }
            trickNumber = 0; jokerLeadSuit = null; phase = "play"; CurrentPlayer = relationship.Min();
        }
        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "choose_trump") return Enum.GetValues(typeof(Suit)).Cast<Suit>().Select(suit => new Action("choose_trump", value: Card.SuitCode(suit))).ToArray();
            IEnumerable<GCard> cards = hands[actual];
            if (trick.Count > 0)
            {
                Suit? led = jokerLeadSuit ?? trick.First(item => !item.Item2.Joker).Item2.Card!.Value.Suit;
                GCard[] follow = cards.Where(card => !card.Joker && card.Card!.Value.Suit == led.Value).ToArray(); if (follow.Length > 0) cards = follow.Concat(cards.Where(card => card.Joker));
            }
            if ((trickNumber == 0 || trickNumber == 9) && cards.Count() > 1) cards = cards.Where(card => !card.Joker);
            var actions = new List<Action>();
            foreach (GCard card in cards)
            {
                if (trick.Count == 0 && card.Joker && trickNumber > 0 && trickNumber < 9)
                    foreach (Suit suit in Enum.GetValues(typeof(Suit))) actions.Add(new Action("lead_joker", value: Card.SuitCode(suit)));
                else actions.Add(new Action("play", card.Card, value: card.Id));
            }
            return actions;
        }
        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "choose_trump") { trump = Card.ParseSuit(action.Value!); DealCards(false); return; }
            GCard card = action.Kind == "lead_joker" ? hands[player].Single(item => item.Joker) : hands[player].Single(item => item.Id == action.Value);
            hands[player].Remove(card);
            if (action.Kind == "lead_joker") jokerLeadSuit = Card.ParseSuit(action.Value!);
            else if (trick.Count == 0 && card.Joker) jokerLeadSuit = trump;
            trick.Add(Tuple.Create(player, card));
            if (trick.Count < 5) { CurrentPlayer = (player + 1) % 5; return; }
            int winner = TrickWinner(); captured[winner].AddRange(trick.Select(item => item.Item2)); trick.Clear(); jokerLeadSuit = null; trickNumber++;
            if (trickNumber >= 10) FinishMatch(); else CurrentPlayer = winner;
        }
        private int TrickWinner()
        {
            Tuple<int, GCard>? joker = trick.FirstOrDefault(item => item.Item2.Joker); if (joker != null) return joker.Item1;
            Suit led = jokerLeadSuit ?? trick[0].Item2.Card!.Value.Suit; IEnumerable<Tuple<int, GCard>> eligible = trick.Any(item => item.Item2.Card!.Value.Suit == trump)
                ? trick.Where(item => item.Item2.Card!.Value.Suit == trump) : trick.Where(item => item.Item2.Card!.Value.Suit == led);
            return eligible.OrderByDescending(item => Strength(item.Item2.Card!.Value)).First().Item1;
        }
        private void FinishMatch()
        {
            int honors = relationship.Sum(player => captured[player].Count(card => card.Card.HasValue && (card.Card.Value.Rank == 1 || card.Card.Value.Rank >= 11)));
            int target = match == 2 ? 8 : 9; bool success = honors >= target;
            foreach (int player in Enumerable.Range(0, 5)) scores[player] += relationship.Contains(player) == success ? 1 : -1;
            if (success && match < 3) { match++; phase = "choose_trump"; CurrentPlayer = relationship.Min(); return; }
            if (success && match == 3) foreach (int player in relationship) scores[player]++;
            round++; if (round >= 10) finished = true; else StartRound();
        }
        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player); if (phase == "choose_trump") return actions[round % 4];
            return actions.OrderBy(action => action.Card.HasValue ? Strength(action.Card.Value) : 15).First();
        }
        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static GCard Pop(List<GCard> cards) { GCard card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over."); int high = scores.Max(); return new GameResult(Enumerable.Range(0, 5).Where(player => scores[player] == high), scores.Select(value => (double)value), "ten Goninkan rounds", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer; return $"phase={phase} round={round + 1}/10 match={match}/3 trump={Card.SuitCode(trump)} kankei=[{string.Join(",", relationship.Select(p => "P" + p))}] " +
                $"honors=[{string.Join(",", captured.Select(pile => pile.Count(card => card.Card.HasValue && (card.Card.Value.Rank == 1 || card.Card.Value.Rank >= 11))))}] scores=[{string.Join(",", scores)}] table=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("goninkan", "ゴニンカン", 5, 5, "two-versus-three honor-trick", "spade以外の2を除く49枚＋Jokerを各10枚。初戦はC/D/Hを3巡し最終roundはS、Joker所持者とtrump A所持者（同一なら2席先）がカンケイ。絵札9枚、勝てばtrump選択の第2戦8枚、第3戦9枚へ進み、各勝敗±1・三タテ+1で10roundを競う。", "gokurakism/Goninkan"),
            (players, random, options) => new GoninkanGame(players, random));
    }
}
