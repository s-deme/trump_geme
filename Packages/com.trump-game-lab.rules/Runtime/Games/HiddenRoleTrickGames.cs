using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TrumpLab.Games
{
    internal static class HiddenRoleTrickGames
    {
        public static void RegisterGames(GameRegistry registry)
        {
            WhosWhoGame.Register(registry);
            GooseberryFoolGame.Register(registry);
            TanukiGame.Register(registry);
        }
    }

    public sealed class WhosWhoGame : GameBase
    {
        private sealed class WhoCard
        {
            public string Id { get; }
            public Card? Card { get; }
            public bool Joker => !Card.HasValue;
            public WhoCard(Card card) { Card = card; Id = card.ToString(); }
            public WhoCard(string id) { Id = id; }
            public override string ToString() => Id;
        }

        private readonly DeterministicRandom rng;
        private readonly int targetScore;
        private readonly List<List<WhoCard>> hands = new List<List<WhoCard>>
        {
            new List<WhoCard>(), new List<WhoCard>(), new List<WhoCard>()
        };
        private readonly List<Tuple<int, WhoCard>> trick = new List<Tuple<int, WhoCard>>();
        private readonly int[] tricks = new int[3];
        private readonly int[] scores = new int[3];
        private readonly int[] initialJokers = new int[3];
        private int dealer = 2;
        private int soloist;
        private int? chooser;
        private string phase = "play";
        private bool finished;

        public override string GameId => "whos_who";
        public override string Name => "WHO'S WHO";

        public WhosWhoGame(int players, DeterministicRandom rng,
            IReadOnlyDictionary<string, string> options)
        {
            Players = 3;
            this.rng = rng;
            targetScore = Math.Max(1, options.Integer("target_score", 100));
            StartDeal();
        }

        private void StartDeal()
        {
            foreach (List<WhoCard> hand in hands) hand.Clear();
            trick.Clear(); Array.Clear(tricks, 0, 3); Array.Clear(initialJokers, 0, 3);
            var deck = Cards.StandardDeck(new[] { 1, 5, 6, 7, 8, 9, 10, 11, 12, 13 })
                .Select(card => new WhoCard(card)).ToList();
            deck.Add(new WhoCard("X0")); deck.Add(new WhoCard("X1")); rng.Shuffle(deck);
            dealer = (dealer + 1) % 3;
            for (int round = 0; round < 14; round++)
                for (int offset = 1; offset <= 3; offset++) hands[(dealer + offset) % 3].Add(Pop(deck));
            for (int player = 0; player < 3; player++) initialJokers[player] = hands[player].Count(card => card.Joker);
            int doubleHolder = Array.FindIndex(initialJokers, count => count == 2);
            soloist = doubleHolder >= 0 ? doubleHolder : Array.FindIndex(initialJokers, count => count == 0);
            CurrentPlayer = (dealer + 1) % 3;
            chooser = null; phase = "play";
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "choose_winner")
                return Enumerable.Range(0, 3).Select(target => new Action("assign_trick", target: target)).ToArray();
            IEnumerable<WhoCard> cards = hands[actual];
            if (trick.Count == 0)
            {
                WhoCard[] natural = cards.Where(card => !card.Joker).ToArray();
                if (natural.Length > 0) cards = natural;
            }
            else
            {
                Card? leadCard = trick[0].Item2.Card;
                if (leadCard.HasValue)
                {
                    WhoCard[] follow = cards.Where(card => !card.Joker && card.Card!.Value.Suit == leadCard.Value.Suit).ToArray();
                    if (follow.Length > 0) cards = follow;
                }
            }
            return cards.Select(card => new Action("play", card.Card, value: card.Id)).ToArray();
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null);
            Guard.Legal(action, LegalActions(player));
            TurnCount++;
            if (phase == "choose_winner")
            {
                ResolveTrick(action.Target!.Value);
                return;
            }
            WhoCard card = hands[player].Single(item => item.Id == action.Value);
            hands[player].Remove(card);
            trick.Add(Tuple.Create(player, card));
            if (trick.Count < 3) { CurrentPlayer = (player + 1) % 3; return; }
            WhoCard[] jokers = trick.Where(item => item.Item2.Joker).Select(item => item.Item2).ToArray();
            if (jokers.Length > 0)
            {
                chooser = jokers.Length == 1 ? trick.Single(item => item.Item2.Joker).Item1 : soloist;
                phase = "choose_winner";
                CurrentPlayer = chooser.Value;
                return;
            }
            ResolveTrick(SecondHighWinner());
        }

        private int SecondHighWinner()
        {
            Suit led = trick[0].Item2.Card!.Value.Suit;
            Tuple<int, WhoCard>[] eligible = trick.Where(item => item.Item2.Card!.Value.Suit == led)
                .OrderByDescending(item => Strength(item.Item2.Card!.Value)).ToArray();
            return eligible.Length == 1 ? eligible[0].Item1 : eligible[1].Item1;
        }

        private void ResolveTrick(int winner)
        {
            tricks[winner]++;
            trick.Clear(); chooser = null; phase = "play";
            if (hands.All(hand => hand.Count == 0)) FinishDeal();
            else CurrentPlayer = winner;
        }

        private void FinishDeal()
        {
            int[] partners = Enumerable.Range(0, 3).Where(player => player != soloist).ToArray();
            int[] ordered = tricks.OrderBy(value => value).ToArray();
            bool soloMiddle = tricks[soloist] > ordered[0] && tricks[soloist] < ordered[2];
            bool partnersEqual = tricks[partners[0]] == tricks[partners[1]];
            int points = 10 + tricks[soloist];
            if (soloMiddle || partnersEqual) scores[soloist] += points;
            else { scores[partners[0]] += points; scores[partners[1]] += points; }
            if (scores.Max() >= targetScore) finished = true;
            else StartDeal();
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "choose_winner") return actions.OrderBy(action => tricks[action.Target!.Value]).First();
            return actions.OrderBy(action => action.Card.HasValue ? Strength(action.Card.Value) : 99).First();
        }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 3).Where(player => scores[player] == high),
                scores.Select(value => (double)value), "first to " + targetScore + " hidden-role points", TurnCount,
                new Dictionary<string, object> { { "last_soloist", soloist } });
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            string ownRole = initialJokers[viewer] == 2 ? "soloist" : initialJokers[viewer] == 1 ? "unknown-partner" : "unknown";
            return $"phase={phase} trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] " +
                $"tricks=[{string.Join(",", tricks)}] scores=[{string.Join(",", scores)}] hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}] " +
                $"your_role={ownRole}\nyour hand: {string.Join(" ", hands[viewer])}";
        }

        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static WhoCard Pop(List<WhoCard> cards) { WhoCard card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("whos_who", "WHO'S WHO", 3, 3, "hidden-role trick-taking",
                "2～4を除いた40枚とJoker2枚を配り、Joker所持で1対2を決める。通常はlead suitの2番目、Joker時は指定相手がtrickを取る100点戦。",
                "David Parlett Who's Who", new Dictionary<string, string> { { "target_score", "100" } }),
            (players, random, options) => new WhosWhoGame(players, random, options));
    }

    public sealed class GooseberryFoolGame : GameBase
    {
        private sealed class GooseCard
        {
            public Card? Card { get; }
            public bool Joker => !Card.HasValue;
            public string Id => Joker ? "X" : Card!.Value.ToString();
            public GooseCard(Card? card) { Card = card; }
            public override string ToString() => Id;
        }

        private readonly DeterministicRandom rng;
        private readonly int targetScore;
        private readonly List<List<GooseCard>> hands = new List<List<GooseCard>>
        {
            new List<GooseCard>(), new List<GooseCard>(), new List<GooseCard>()
        };
        private readonly List<Tuple<int, GooseCard>> trick = new List<Tuple<int, GooseCard>>();
        private readonly int[] tricks = new int[3];
        private readonly int[] scores = new int[3];
        private int dealer = 2;
        private string phase = "play";
        private bool finished;

        public override string GameId => "gooseberry_fool";
        public override string Name => "グズベリー・フール";

        public GooseberryFoolGame(int players, DeterministicRandom rng,
            IReadOnlyDictionary<string, string> options)
        {
            Players = 3; this.rng = rng;
            targetScore = Math.Max(1, options.Integer("target_score", 100));
            StartDeal();
        }

        private void StartDeal()
        {
            foreach (List<GooseCard> hand in hands) hand.Clear();
            trick.Clear(); Array.Clear(tricks, 0, 3);
            var deck = Cards.StandardDeck(new[] { 1, 7, 8, 9, 10, 11, 12, 13 })
                .Select(card => new GooseCard(card)).ToList();
            deck.Add(new GooseCard(null)); rng.Shuffle(deck);
            dealer = (dealer + 1) % 3;
            for (int round = 0; round < 11; round++)
                for (int offset = 1; offset <= 3; offset++) hands[(dealer + offset) % 3].Add(Pop(deck));
            CurrentPlayer = (dealer + 1) % 3; phase = "play";
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "choose_winner")
                return Enumerable.Range(0, 3).Select(target => new Action("goose", target: target)).ToArray();
            IEnumerable<GooseCard> cards = hands[actual];
            if (trick.Count == 0)
            {
                GooseCard[] natural = cards.Where(card => !card.Joker).ToArray();
                if (natural.Length > 0) cards = natural;
            }
            else
            {
                Card? leadCard = trick[0].Item2.Card;
                if (leadCard.HasValue)
                {
                    GooseCard[] follow = cards.Where(card => !card.Joker && card.Card!.Value.Suit == leadCard.Value.Suit).ToArray();
                    if (follow.Length > 0) cards = follow;
                }
                else
                {
                    GooseCard? joker = cards.FirstOrDefault(card => card.Joker);
                    if (joker != null) cards = new[] { joker };
                }
            }
            return cards.Select(card => new Action("play", card.Card, value: card.Id)).ToArray();
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "choose_winner") { ResolveTrick(action.Target!.Value); return; }
            GooseCard card = hands[player].Single(item => item.Id == action.Value);
            hands[player].Remove(card); trick.Add(Tuple.Create(player, card));
            if (trick.Count < 3) { CurrentPlayer = (player + 1) % 3; return; }
            Tuple<int, GooseCard>? goose = trick.FirstOrDefault(item => item.Item2.Joker);
            if (goose != null) { phase = "choose_winner"; CurrentPlayer = goose.Item1; return; }
            ResolveTrick(OddWinner());
        }

        private int OddWinner()
        {
            Suit[] suits = trick.Select(item => item.Item2.Card!.Value.Suit).ToArray();
            if (suits.Distinct().Count() == 1)
                return trick.OrderByDescending(item => Strength(item.Item2.Card!.Value)).Skip(1).First().Item1;
            IGrouping<Suit, Tuple<int, GooseCard>>[] groups = trick.GroupBy(item => item.Item2.Card!.Value.Suit).ToArray();
            if (groups.Length == 2) return groups.Single(group => group.Count() == 1).Single().Item1;
            bool[] red = trick.Select(item => item.Item2.Card!.Value.Suit == Suit.Hearts ||
                item.Item2.Card.Value.Suit == Suit.Diamonds).ToArray();
            bool oddColor = red.Count(value => value) == 1;
            return trick[Array.FindIndex(red, value => value == oddColor)].Item1;
        }

        private void ResolveTrick(int winner)
        {
            tricks[winner]++; trick.Clear(); phase = "play";
            if (hands.All(hand => hand.Count == 0)) FinishDeal();
            else CurrentPlayer = winner;
        }

        private void FinishDeal()
        {
            int[] dealScores = Enumerable.Range(0, 3).Select(player => tricks[player] + 2 * tricks[(player + 2) % 3]).ToArray();
            int median = Enumerable.Range(0, 3).OrderBy(player => dealScores[player]).Skip(1).First();
            dealScores[median] += 10;
            for (int player = 0; player < 3; player++) scores[player] += dealScores[player];
            if (scores.Count(score => score >= targetScore) >= 1) finished = true;
            else StartDeal();
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "choose_winner") return actions.OrderBy(action => tricks[action.Target!.Value]).First();
            return actions.OrderBy(action => action.Card.HasValue ? Strength(action.Card.Value) : 99).First();
        }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int[] distinct = scores.Distinct().ToArray();
            int[] winners;
            if (distinct.Length < 3)
            {
                int repeated = scores.GroupBy(value => value).First(group => group.Count() > 1).Key;
                winners = Enumerable.Range(0, 3).Where(player => scores[player] != repeated).ToArray();
            }
            else
            {
                int median = scores.OrderBy(value => value).Skip(1).First();
                winners = Enumerable.Range(0, 3).Where(player => scores[player] == median).ToArray();
            }
            return new GameResult(winners, scores.Select(value => (double)value), "middle cumulative score", TurnCount);
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            return $"phase={phase} trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] " +
                $"tricks=[{string.Join(",", tricks)}] scores=[{string.Join(",", scores)}] hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\n" +
                $"your hand: {string.Join(" ", hands[viewer])}";
        }

        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static GooseCard Pop(List<GooseCard> cards) { GooseCard card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("gooseberry_fool", "グズベリー・フール", 3, 3, "odd-card trick-taking",
                "7～AとJokerで、3枚のsuit分布からodd cardを勝者にする。Jokerは最初のvoid時に強制され、所持者がtrickを譲渡できる100点戦。",
                "David Parlett Gooseberry Fool", new Dictionary<string, string> { { "target_score", "100" } }),
            (players, random, options) => new GooseberryFoolGame(players, random, options));
    }

    public sealed class TanukiGame : GameBase
    {
        private readonly DeterministicRandom rng;
        private readonly List<List<Card>> hands = new List<List<Card>>
        {
            new List<Card>(), new List<Card>(), new List<Card>()
        };
        private readonly List<List<Card>> captured = new List<List<Card>>
        {
            new List<Card>(), new List<Card>(), new List<Card>()
        };
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly Suit?[] choices = new Suit?[3];
        private readonly int[] scores = new int[3];
        private int dealer = 2;
        private int dealsPlayed;
        private bool trumpRevealed;
        private string revealedRoles = "none";
        private string phase = "choose_roles";
        private bool finished;

        public override string GameId => "tanuki";
        public override string Name => "たぬき";

        public TanukiGame(int players, DeterministicRandom rng)
        {
            Players = 3; this.rng = rng; StartDeal();
        }

        private int TrumpPlayer => (dealer + 1) % 3;
        private int MinusPlayer => dealer;
        private int PlusPlayer => (dealer + 2) % 3;
        private Suit TrumpSuit => choices[TrumpPlayer]!.Value;
        private Suit MinusSuit => choices[MinusPlayer]!.Value;
        private Suit PlusSuit => choices[PlusPlayer]!.Value;

        private void StartDeal()
        {
            foreach (List<Card> hand in hands) hand.Clear();
            foreach (List<Card> pile in captured) pile.Clear();
            trick.Clear(); for (int player = 0; player < 3; player++) choices[player] = null;
            List<Card> deck = Cards.Shuffled(Cards.StandardDeck(new[] { 1, 6, 7, 8, 9, 10, 11, 12, 13 }), rng);
            dealer = (dealer + 1) % 3;
            for (int round = 0; round < 12; round++)
                for (int offset = 1; offset <= 3; offset++) hands[(dealer + offset) % 3].Add(Pop(deck));
            trumpRevealed = false; phase = "choose_roles"; CurrentPlayer = TrumpPlayer;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "choose_roles") return Enum.GetValues(typeof(Suit)).Cast<Suit>()
                .Select(suit => new Action("choose_suit", value: Card.SuitCode(suit))).ToArray();
            IEnumerable<Card> cards = hands[actual];
            bool mustFollow = dealsPlayed >= 3 && dealsPlayed < 6;
            if (mustFollow && trick.Count > 0)
            {
                Suit led = trick[0].Item2.Suit;
                Card[] follow = cards.Where(card => card.Suit == led).ToArray();
                if (follow.Length > 0) cards = follow;
            }
            return cards.Select(card => new Action("play", card)).ToArray();
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "choose_roles")
            {
                choices[player] = Card.ParseSuit(action.Value!);
                if (player == TrumpPlayer) CurrentPlayer = MinusPlayer;
                else if (player == MinusPlayer) CurrentPlayer = PlusPlayer;
                else { phase = "play"; CurrentPlayer = TrumpPlayer; }
                return;
            }
            Card card = action.Card!.Value; hands[player].Remove(card); trick.Add(Tuple.Create(player, card));
            if (trick.Count < 3) { CurrentPlayer = (player + 1) % 3; return; }
            Suit ledSuit = trick[0].Item2.Suit;
            if (!trumpRevealed && ledSuit != TrumpSuit && trick.Any(item => item.Item2.Suit == TrumpSuit)) trumpRevealed = true;
            Tuple<int, Card> winner = trick.Any(item => item.Item2.Suit == TrumpSuit)
                ? trick.Where(item => item.Item2.Suit == TrumpSuit).OrderByDescending(item => Strength(item.Item2)).First()
                : trick.Where(item => item.Item2.Suit == ledSuit).OrderByDescending(item => Strength(item.Item2)).First();
            captured[winner.Item1].AddRange(trick.Select(item => item.Item2));
            trick.Clear();
            if (hands.All(hand => hand.Count == 0)) FinishDeal();
            else CurrentPlayer = winner.Item1;
        }

        private void FinishDeal()
        {
            revealedRoles = $"deal={dealsPlayed + 1},trump=P{TrumpPlayer}:{Card.SuitCode(TrumpSuit)}," +
                $"minus=P{MinusPlayer}:{Card.SuitCode(MinusSuit)},plus=P{PlusPlayer}:{Card.SuitCode(PlusSuit)}";
            for (int player = 0; player < 3; player++)
            {
                if (PlusSuit == MinusSuit)
                {
                    bool redRole = PlusSuit == Suit.Hearts || PlusSuit == Suit.Diamonds;
                    scores[player] += captured[player].Sum(card =>
                        (card.Suit == Suit.Hearts || card.Suit == Suit.Diamonds) == redRole ? 1 : -1);
                }
                else scores[player] += captured[player].Sum(card => card.Suit == PlusSuit ? 1 : card.Suit == MinusSuit ? -1 : 0);
            }
            dealsPlayed++;
            if (dealsPlayed >= 9) finished = true;
            else StartDeal();
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "choose_roles")
            {
                string role = Role(player);
                return role == "minus"
                    ? actions.OrderBy(action => hands[player].Count(card => card.Suit == Card.ParseSuit(action.Value!))).First()
                    : actions.OrderByDescending(action => hands[player].Count(card => card.Suit == Card.ParseSuit(action.Value!))).First();
            }
            return actions.OrderBy(action => Strength(action.Card!.Value)).First();
        }

        private string Role(int player) => player == TrumpPlayer ? "trump" : player == MinusPlayer ? "minus" : "plus";

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 3).Where(player => scores[player] == high),
                scores.Select(value => (double)value), "nine hidden-suit-role deals", TurnCount);
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            return $"phase={phase} deal={dealsPlayed + 1}/9 follow={(dealsPlayed >= 3 && dealsPlayed < 6 ? "must" : "may")} " +
                $"trump={(trumpRevealed ? Card.SuitCode(TrumpSuit) : "?")} trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] " +
                $"scores=[{string.Join(",", scores)}] hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}] " +
                $"revealed_roles=[{revealedRoles}] " +
                $"your_role={Role(viewer)} your_suit={(choices[viewer].HasValue ? Card.SuitCode(choices[viewer]!.Value) : "-")}\n" +
                $"your hand: {string.Join(" ", hands[viewer])}";
        }

        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("tanuki", "たぬき", 3, 3, "hidden-role trick-taking",
                "6～Aの36枚で各自が秘密にtrump・plus・minus suitを1つ選ぶ。may/must/may followを各3ディール、計9ディール行う。",
                "Gokurakism Tanuki"),
            (players, random, options) => new TanukiGame(players, random));
    }
}
