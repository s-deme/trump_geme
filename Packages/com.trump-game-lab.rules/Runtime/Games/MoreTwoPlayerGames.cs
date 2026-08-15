using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TrumpLab.Games
{
    internal static class MoreTwoPlayerGames
    {
        public static void RegisterGames(GameRegistry registry)
        {
            SonoGame.Register(registry);
            SuperTrumpGame.Register(registry);
            DaifugoTwoGame.Register(registry);
            OfficerSkatGame.Register(registry);
        }
    }

    public sealed class SonoGame : GameBase
    {
        private sealed class SonoCard
        {
            public Card? Card { get; }
            public bool Joker => !Card.HasValue;
            public SonoCard(Card? card) { Card = card; }
            public override string ToString() => Joker ? "X" : Card!.Value.ToString();
        }

        private readonly List<List<SonoCard>> hands = new List<List<SonoCard>>
        {
            new List<SonoCard>(), new List<SonoCard>()
        };
        private readonly SonoCard?[] board = new SonoCard?[25];
        private readonly bool[] revealed = new bool[25];
        private readonly int[] scores = new int[2];
        private bool finished;

        public override string GameId => "sono";
        public override string Name => "ソノ";

        public SonoGame(int players, DeterministicRandom rng)
        {
            Players = 2;
            var deck = Cards.StandardDeck(new[] { 1, 9, 10, 11, 12, 13 })
                .Select(card => new SonoCard(card)).ToList();
            deck.Add(new SonoCard(null));
            rng.Shuffle(deck);
            for (int round = 0; round < 10; round++)
                for (int player = 0; player < 2; player++) hands[player].Add(Pop(deck));
            foreach (int position in new[] { 0, 6, 12, 18, 24 }) board[position] = Pop(deck);
            CurrentPlayer = 0;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            int[] positions = Enumerable.Range(0, 25).Where(index => board[index] == null && AdjacentToCard(index)).ToArray();
            return hands[actual].SelectMany(card => positions.Select(position =>
                new Action("place", card.Card, value: (card.Joker ? "X" : card.ToString()) + "@" +
                    position.ToString(CultureInfo.InvariantCulture)))).ToArray();
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null);
            Guard.Legal(action, LegalActions(player));
            TurnCount++;
            string[] parts = action.Value!.Split('@');
            int position = int.Parse(parts[1], CultureInfo.InvariantCulture);
            SonoCard card = parts[0] == "X" ? hands[player].Single(item => item.Joker) :
                hands[player].Single(item => !item.Joker && item.Card!.Value == Card.Parse(parts[0]));
            hands[player].Remove(card);
            board[position] = card;
            revealed[position] = true;
            RevealDiagonals();
            if (board.All(item => item != null))
            {
                for (int column = 0; column < 5; column++)
                    scores[0] += LineScore(Enumerable.Range(0, 5).Select(row => board[row * 5 + column]!));
                for (int row = 0; row < 5; row++)
                    scores[1] += LineScore(Enumerable.Range(0, 5).Select(column => board[row * 5 + column]!));
                finished = true;
                return;
            }
            CurrentPlayer = 1 - player;
        }

        private bool AdjacentToCard(int position)
        {
            int row = position / 5, column = position % 5;
            return row > 0 && board[position - 5] != null || row < 4 && board[position + 5] != null ||
                column > 0 && board[position - 1] != null || column < 4 && board[position + 1] != null;
        }

        private void RevealDiagonals()
        {
            foreach (int position in new[] { 0, 6, 12, 18, 24 })
            {
                int row = position / 5, column = position % 5, occupied = 0;
                if (row > 0 && board[position - 5] != null) occupied++;
                if (row < 4 && board[position + 5] != null) occupied++;
                if (column > 0 && board[position - 1] != null) occupied++;
                if (column < 4 && board[position + 1] != null) occupied++;
                if (occupied >= 2) revealed[position] = true;
            }
        }

        public static int LineScore(IEnumerable<Card?> source)
        {
            Card?[] values = source.ToArray();
            int jokerCount = values.Count(card => !card.HasValue);
            Card[] fixedCards = values.Where(card => card.HasValue).Select(card => card!.Value).ToArray();
            if (jokerCount == 0) return ScoreConcrete(fixedCards);
            int best = 0;
            foreach (Card replacement in Cards.StandardDeck(new[] { 1, 9, 10, 11, 12, 13 }))
                best = Math.Max(best, ScoreConcrete(fixedCards.Concat(new[] { replacement }).ToArray()));
            return best;
        }

        private static int LineScore(IEnumerable<SonoCard> source) =>
            LineScore(source.Select(card => card.Card));

        private static int ScoreConcrete(Card[] cards)
        {
            int[] counts = cards.GroupBy(card => card.Rank).Select(group => group.Count())
                .OrderByDescending(value => value).ToArray();
            int poker = counts[0] == 5 ? 10 : counts[0] == 4 ? 6 :
                counts[0] == 3 && counts.Length > 1 && counts[1] == 2 ? 5 :
                IsStraight(cards) ? 5 : counts[0] == 3 ? 3 :
                counts.Count(value => value == 2) == 2 ? 2 : counts[0] == 2 ? 1 : 0;
            int clans = 0;
            if (cards.All(card => card.Suit == Suit.Diamonds || card.Suit == Suit.Hearts)) clans += 3;
            if (cards.All(card => card.Suit == Suit.Clubs || card.Suit == Suit.Spades)) clans += 3;
            if (cards.All(card => card.Rank == 1 || card.Rank == 9 || card.Rank == 10)) clans += 3;
            if (cards.All(card => card.Rank >= 11)) clans += 3;
            return poker + clans;
        }

        private static bool IsStraight(IEnumerable<Card> cards)
        {
            int[] ranks = cards.Select(card => card.Rank == 1 ? 14 : card.Rank).Distinct().OrderBy(value => value).ToArray();
            return ranks.Length == 5 && ranks[4] - ranks[0] == 4;
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom rng, int difficulty = 1)
        {
            return LegalActions(player).OrderByDescending(action =>
            {
                int position = int.Parse(action.Value!.Split('@')[1], CultureInfo.InvariantCulture);
                int row = position / 5, column = position % 5;
                return player == 0 ? board.Count(item => item != null) + column :
                    board.Count(item => item != null) + row;
            }).First();
        }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 2).Where(player => scores[player] == high),
                scores.Select(value => (double)value), "column versus row poker and clan score", TurnCount);
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            string rows = string.Join("/", Enumerable.Range(0, 5).Select(row => string.Join(" ",
                Enumerable.Range(0, 5).Select(column =>
                {
                    int index = row * 5 + column;
                    return board[index] == null ? "." : revealed[index] ? board[index]!.ToString() : "?";
                }))));
            return $"board={rows} orientation=P0-columns/P1-rows hand_counts=[{hands[0].Count},{hands[1].Count}]\n" +
                $"your hand: {string.Join(" ", hands[viewer])}";
        }

        private static SonoCard Pop(List<SonoCard> cards)
        {
            SonoCard card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card;
        }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("sono", "ソノ", 2, 2, "layout/poker",
                "A・9・10・J・Q・Kとジョーカーの25枚を隣接配置し、P0は縦、P1は横のポーカー役とクランを得点化する。",
                "Gokurakism Sono"),
            (players, random, options) => new SonoGame(players, random));
    }

    public sealed class SuperTrumpGame : GameBase
    {
        private readonly List<List<Card>> hands;
        private readonly List<Card> stock;
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly int[] scores = new int[2];
        private Card? faceUp;
        private Suit? trump;
        private int? superRank;
        private int stage = 1;
        private string phase = "choose_trump";
        private bool finished;

        public override string GameId => "super_trump";
        public override string Name => "スーパートランプ";

        public SuperTrumpGame(int players, DeterministicRandom rng)
        {
            Players = 2;
            stock = Cards.Shuffled(Cards.StandardDeck(), rng);
            hands = new List<List<Card>> { new List<Card>(), new List<Card>() };
            for (int round = 0; round < 13; round++)
                for (int player = 0; player < 2; player++) hands[player].Add(Pop(stock));
            CurrentPlayer = 1;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "choose_trump")
                return Enum.GetValues(typeof(Suit)).Cast<Suit>()
                    .Select(suit => new Action("choose_trump", value: Card.SuitCode(suit))).ToArray();
            if (phase == "choose_super")
                return Enumerable.Range(1, 13).Select(rank => new Action("choose_super", value:
                    rank.ToString(CultureInfo.InvariantCulture))).ToArray();
            IEnumerable<Card> cards = hands[actual];
            if (trick.Count > 0)
            {
                Suit effectiveLed = EffectiveSuit(trick[0].Item2);
                Card[] follow = cards.Where(card => EffectiveSuit(card) == effectiveLed).ToArray();
                if (follow.Length > 0) cards = follow;
            }
            return cards.Select(card => new Action("play", card)).ToArray();
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null);
            Guard.Legal(action, LegalActions(player));
            TurnCount++;
            if (phase == "choose_trump")
            {
                trump = Card.ParseSuit(action.Value!);
                phase = "choose_super";
                CurrentPlayer = 0;
                return;
            }
            if (phase == "choose_super")
            {
                superRank = int.Parse(action.Value!, CultureInfo.InvariantCulture);
                faceUp = Pop(stock);
                phase = "play";
                CurrentPlayer = 1;
                return;
            }
            Card card = action.Card!.Value;
            hands[player].Remove(card);
            trick.Add(Tuple.Create(player, card));
            if (trick.Count == 1) { CurrentPlayer = 1 - player; return; }
            ResolveTrick();
        }

        private void ResolveTrick()
        {
            int winner = TrickWinner();
            scores[winner] += stage == 1 ? 1 : 2;
            trick.Clear();
            if (stage == 1)
            {
                int loser = 1 - winner;
                hands[winner].Add(faceUp!.Value);
                hands[loser].Add(Pop(stock));
                faceUp = stock.Count > 0 ? Pop(stock) : (Card?)null;
                if (!faceUp.HasValue) stage = 2;
            }
            if (stage == 2 && hands[0].Count == 0)
            {
                finished = true;
                return;
            }
            CurrentPlayer = winner;
        }

        private int TrickWinner()
        {
            Card first = trick[0].Item2, second = trick[1].Item2;
            if (first.Rank == superRank) return trick[0].Item1;
            if (second.Rank == superRank) return trick[1].Item1;
            bool firstTrump = first.Suit == trump, secondTrump = second.Suit == trump;
            if (firstTrump != secondTrump) return firstTrump ? trick[0].Item1 : trick[1].Item1;
            if (first.Suit == second.Suit) return Strength(second) > Strength(first) ? trick[1].Item1 : trick[0].Item1;
            return trick[0].Item1;
        }

        private Suit EffectiveSuit(Card card) => card.Rank == superRank ? trump!.Value : card.Suit;

        public override Action ChooseCpuAction(int player, DeterministicRandom rng, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "choose_trump")
                return actions.OrderByDescending(action => hands[player].Count(card =>
                    card.Suit == Card.ParseSuit(action.Value!))).First();
            if (phase == "choose_super")
                return actions.OrderByDescending(action => hands[player].Count(card =>
                    card.Rank == int.Parse(action.Value!, CultureInfo.InvariantCulture))).First();
            return actions.OrderBy(action => action.Card!.Value.Rank == superRank ? 2 :
                    action.Card.Value.Suit == trump ? 1 : 0)
                .ThenBy(action => Strength(action.Card!.Value)).First();
        }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 2).Where(player => scores[player] == high),
                scores.Select(value => (double)value), "13 tricks at one point and 13 at two points", TurnCount);
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            return $"phase={phase} stage={stage} trump={(trump.HasValue ? Card.SuitCode(trump.Value) : "-")} " +
                $"super={(superRank.HasValue ? superRank.Value.ToString(CultureInfo.InvariantCulture) : "-")} " +
                $"face_up={(faceUp.HasValue ? faceUp.Value.ToString() : "-")} stock={stock.Count} " +
                $"trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] " +
                $"scores=[{string.Join(",", scores)}] hand_counts=[{hands[0].Count},{hands[1].Count}]\n" +
                $"your hand: {string.Join(" ", hands[viewer])}";
        }

        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static Card Pop(List<Card> cards)
        {
            Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card;
        }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("super_trump", "スーパートランプ", 2, 2, "two-stage trick-taking",
                "non-dealerが切り札スート、dealerが全切り札より強いrankを選び、前半13トリック各1点、後半各2点で競う。",
                "Gokurakism Supertrump"),
            (players, random, options) => new SuperTrumpGame(players, random));
    }

    public sealed class OfficerSkatGame : GameBase
    {
        private readonly List<List<List<Card>>> layout;
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly int[] cardPoints = new int[2];
        private Suit? trump;
        private string phase = "choose_trump";
        private bool finished;

        public override string GameId => "officer_skat";
        public override string Name => "将校スカート";

        public OfficerSkatGame(int players, DeterministicRandom rng)
        {
            Players = 2;
            List<Card> deck = Cards.Shuffled(Cards.StandardDeck(
                new[] { 1, 7, 8, 9, 10, 11, 12, 13 }), rng);
            layout = Enumerable.Range(0, 2).Select(_ => Enumerable.Range(0, 8)
                .Select(__ => new List<Card>()).ToList()).ToList();
            for (int column = 0; column < 8; column++)
                for (int player = 0; player < 2; player++) layout[player][column].Add(Pop(deck));
            for (int column = 0; column < 8; column++)
                for (int player = 0; player < 2; player++) layout[player][column].Add(Pop(deck));
            CurrentPlayer = 0;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "choose_trump")
                return Enum.GetValues(typeof(Suit)).Cast<Suit>()
                    .Select(suit => new Action("choose_trump", value: Card.SuitCode(suit))).ToArray();
            IEnumerable<Card> cards = Available(actual);
            if (trick.Count > 0)
            {
                int led = EffectiveSuit(trick[0].Item2);
                Card[] follow = cards.Where(card => EffectiveSuit(card) == led).ToArray();
                if (follow.Length > 0) cards = follow;
            }
            return cards.Select(card => new Action("play", card)).ToArray();
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null);
            Guard.Legal(action, LegalActions(player));
            TurnCount++;
            if (phase == "choose_trump")
            {
                trump = Card.ParseSuit(action.Value!);
                phase = "play";
                return;
            }
            Card card = action.Card!.Value;
            List<Card> pile = layout[player].Single(column => column.Count > 0 && column[column.Count - 1] == card);
            pile.RemoveAt(pile.Count - 1);
            trick.Add(Tuple.Create(player, card));
            if (trick.Count == 1) { CurrentPlayer = 1 - player; return; }
            int winner = TrickWinner();
            cardPoints[winner] += trick.Sum(item => CardPoint(item.Item2));
            trick.Clear();
            if (layout.SelectMany(playerLayout => playerLayout).All(column => column.Count == 0))
            {
                finished = true;
                return;
            }
            CurrentPlayer = winner;
        }

        private IEnumerable<Card> Available(int player) => layout[player]
            .Where(column => column.Count > 0).Select(column => column[column.Count - 1]);

        private int EffectiveSuit(Card card)
        {
            if (card.Rank == 11 || card.Suit == trump) return 4;
            return (int)card.Suit;
        }

        private int TrickWinner()
        {
            Card first = trick[0].Item2, second = trick[1].Item2;
            int firstSuit = EffectiveSuit(first), secondSuit = EffectiveSuit(second);
            if (firstSuit != secondSuit) return secondSuit == 4 ? trick[1].Item1 : trick[0].Item1;
            return Power(second) > Power(first) ? trick[1].Item1 : trick[0].Item1;
        }

        private int Power(Card card)
        {
            if (card.Rank == 11)
            {
                int[] jackOrder = { 0, 3, 2, 1 };
                return 200 - Array.IndexOf(jackOrder, (int)card.Suit) * 10;
            }
            int strength = card.Rank == 1 ? 7 : card.Rank == 10 ? 6 : card.Rank == 13 ? 5 :
                card.Rank == 12 ? 4 : card.Rank - 6;
            return (card.Suit == trump ? 100 : 0) + strength;
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom rng, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "choose_trump")
                return actions.OrderByDescending(action => layout[player].Take(4)
                    .Select(column => column[column.Count - 1]).Count(card =>
                    card.Suit == Card.ParseSuit(action.Value!) || card.Rank == 11)).First();
            return actions.OrderBy(action => Power(action.Card!.Value)).First();
        }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int winner = cardPoints[0] > 60 ? 0 : 1;
            return new GameResult(new[] { winner }, cardPoints.Select(value => (double)value),
                cardPoints[0] == 60 ? "60-60 favors the defender" : "majority of 120 card points", TurnCount);
        }

        public override string View(int? player = null)
        {
            if (phase == "choose_trump")
                return "phase=choose_trump P0_first_row=[" + string.Join(" ", layout[0].Take(4)
                    .Select(column => column[column.Count - 1])) + "] P1_first_row=[pending]";
            string layouts = string.Join(" ", Enumerable.Range(0, 2).Select(player => "P" + player + "[" +
                string.Join(" ", layout[player].Select(column => column.Count == 0 ? "-" :
                    column[column.Count - 1] + (column.Count > 1 ? "/?" : ""))) + "]"));
            return $"phase={phase} trump={(trump.HasValue ? Card.SuitCode(trump.Value) : "-")} " +
                $"trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] " +
                $"points=[{string.Join(",", cardPoints)}]\npublic layouts: {layouts}";
        }

        private static int CardPoint(Card card) => card.Rank == 1 ? 11 : card.Rank == 10 ? 10 :
            card.Rank == 13 ? 4 : card.Rank == 12 ? 3 : card.Rank == 11 ? 2 : 0;
        private static Card Pop(List<Card> cards)
        {
            Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card;
        }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("officer_skat", "将校スカート", 2, 2, "open trick-taking",
                "32枚を各8列の伏札＋表札にし、J4枚と宣言スートを切り札として16トリックの120カード点を争う。",
                "Officers' Skat rules"),
            (players, random, options) => new OfficerSkatGame(players, random));
    }
}
