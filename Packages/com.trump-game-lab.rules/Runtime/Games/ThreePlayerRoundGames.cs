using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    internal static class ThreePlayerRoundGames
    {
        public static void RegisterGames(GameRegistry registry)
        {
            HamletGame.Register(registry);
            FarbwechselGame.Register(registry);
            SheriffGame.Register(registry);
            MizerkaGame.Register(registry);
        }
    }

    public sealed class FarbwechselGame : GameBase
    {
        private readonly DeterministicRandom rng;
        private readonly int targetScore;
        private readonly List<List<Card>> hands = new List<List<Card>>
        {
            new List<Card>(), new List<Card>(), new List<Card>()
        };
        private readonly List<List<Card>> captured = new List<List<Card>>
        {
            new List<Card>(), new List<Card>(), new List<Card>()
        };
        private readonly List<Card> trumpCards = new List<Card>();
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly int?[] bids = new int?[3];
        private readonly int[] tricks = new int[3];
        private readonly int[] scores = new int[3];
        private int dealer = 2;
        private int bidsMade;
        private int trickIndex;
        private string phase = "bid";
        private bool finished;

        public override string GameId => "farbwechsel";
        public override string Name => "Farbwechsel";

        public FarbwechselGame(int players, DeterministicRandom rng,
            IReadOnlyDictionary<string, string> options)
        {
            Players = 3; this.rng = rng;
            targetScore = Math.Max(1, options.Integer("target_score", 100)); StartDeal();
        }

        private Suit CurrentTrump => trumpCards[trickIndex].Suit;

        private void StartDeal()
        {
            foreach (List<Card> hand in hands) hand.Clear();
            foreach (List<Card> pile in captured) pile.Clear();
            trumpCards.Clear(); trick.Clear(); Array.Clear(tricks, 0, 3);
            for (int player = 0; player < 3; player++) bids[player] = null;
            List<Card> deck = Cards.Shuffled(Cards.StandardDeck(new[] { 1, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 }), rng);
            dealer = (dealer + 1) % 3;
            for (int round = 0; round < 11; round++)
                for (int offset = 1; offset <= 3; offset++) hands[(dealer + offset) % 3].Add(Pop(deck));
            while (deck.Count > 0) trumpCards.Add(Pop(deck));
            bidsMade = 0; trickIndex = 0; phase = "bid"; CurrentPlayer = (dealer + 1) % 3;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "bid") return Enumerable.Range(0, 12)
                .Select(value => new Action("bid", value: value.ToString())).ToArray();
            IEnumerable<Card> cards = hands[actual];
            Suit? required = trick.Count > 0 ? trick[0].Item2.Suit
                : trickIndex == 0 ? CurrentTrump : (Suit?)null;
            if (required.HasValue)
            {
                Card[] follow = cards.Where(card => card.Suit == required.Value).ToArray();
                if (follow.Length > 0) cards = follow;
            }
            return cards.Select(card => new Action("play", card)).ToArray();
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "bid")
            {
                bids[player] = int.Parse(action.Value!); bidsMade++;
                if (bidsMade < 3) CurrentPlayer = (player + 1) % 3;
                else { phase = "play"; CurrentPlayer = (dealer + 1) % 3; }
                return;
            }
            Card card = action.Card!.Value; hands[player].Remove(card); trick.Add(Tuple.Create(player, card));
            if (trick.Count < 3) { CurrentPlayer = (player + 1) % 3; return; }
            int winner = TrickWinner(); tricks[winner]++;
            captured[winner].AddRange(trick.Select(item => item.Item2)); captured[winner].Add(trumpCards[trickIndex]);
            trick.Clear(); trickIndex++;
            if (trickIndex >= 11) FinishDeal();
            else CurrentPlayer = winner;
        }

        private int TrickWinner()
        {
            Suit led = trick[0].Item2.Suit;
            IEnumerable<Tuple<int, Card>> eligible = trick.Any(item => item.Item2.Suit == CurrentTrump)
                ? trick.Where(item => item.Item2.Suit == CurrentTrump)
                : trick.Where(item => item.Item2.Suit == led);
            return eligible.OrderByDescending(item => Strength(item.Item2)).First().Item1;
        }

        private void FinishDeal()
        {
            for (int player = 0; player < 3; player++)
            {
                if (bids[player] == tricks[player]) scores[player] += 20;
                scores[player] += captured[player].Count(card => card.Rank == 10 || card.Rank == 11 || card.Rank == 12);
            }
            if (scores.Max() >= targetScore) finished = true;
            else StartDeal();
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "bid")
            {
                int estimate = Math.Min(11, hands[player].Count(card => Strength(card) >= 12));
                return actions.First(action => action.Value == estimate.ToString());
            }
            return actions.OrderBy(action => Strength(action.Card!.Value)).First();
        }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 3).Where(player => scores[player] == high),
                scores.Select(value => (double)value), "first to " + targetScore + " prediction points", TurnCount);
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            string trumps = string.Join(" ", trumpCards.Skip(trickIndex));
            return $"phase={phase} trick_no={trickIndex + 1}/11 trump={(trickIndex < 11 ? Card.SuitCode(CurrentTrump) : "-")} " +
                $"trump_cards=[{trumps}] bids_made={bidsMade}/3 your_bid={(bids[viewer].HasValue ? bids[viewer]!.Value.ToString() : "-")} " +
                $"trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] tricks=[{string.Join(",", tricks)}] " +
                $"scores=[{string.Join(",", scores)}] hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }

        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("farbwechsel", "Farbwechsel", 3, 3, "prediction trick-taking",
                "4～Aの44枚を使い、公開11枚がtrickごとの切り札になる。秘密予想の的中20点と獲得Q/J/10で100点を争う。",
                "gokurakism", new Dictionary<string, string> { { "target_score", "100" } }),
            (players, random, options) => new FarbwechselGame(players, random, options));
    }

    public sealed class HamletGame : GameBase
    {
        private sealed class HamletCard
        {
            public Card? Card { get; }
            public bool Joker => !Card.HasValue;
            public string Id => Joker ? "X" : Card!.Value.ToString();
            public HamletCard(Card? card) { Card = card; }
            public override string ToString() => Id;
        }

        private readonly DeterministicRandom rng;
        private readonly int targetScore;
        private readonly List<List<HamletCard>> hands = new List<List<HamletCard>>
        {
            new List<HamletCard>(), new List<HamletCard>(), new List<HamletCard>()
        };
        private readonly List<Tuple<int, HamletCard>> trick = new List<Tuple<int, HamletCard>>();
        private readonly HamletCard?[] modeCards = new HamletCard?[3];
        private readonly int[] tricks = new int[3];
        private readonly int[] scores = new int[3];
        private int dealer = 2;
        private int selections;
        private Suit trump;
        private bool toBe;
        private string phase = "choose_mode";
        private bool finished;

        public override string GameId => "hamlet";
        public override string Name => "ハムレット";

        public HamletGame(int players, DeterministicRandom rng,
            IReadOnlyDictionary<string, string> options)
        {
            Players = 3; this.rng = rng;
            targetScore = Math.Max(1, options.Integer("target_score", 250));
            StartDeal();
        }

        private void StartDeal()
        {
            foreach (List<HamletCard> hand in hands) hand.Clear();
            trick.Clear(); Array.Clear(tricks, 0, 3);
            for (int player = 0; player < 3; player++) modeCards[player] = null;
            var deck = Cards.StandardDeck(new[] { 1, 7, 8, 9, 10, 11, 12, 13 })
                .Select(card => new HamletCard(card)).ToList();
            deck.Add(new HamletCard(null)); rng.Shuffle(deck);
            dealer = (dealer + 1) % 3;
            for (int round = 0; round < 11; round++)
                for (int offset = 1; offset <= 3; offset++) hands[(dealer + offset) % 3].Add(Pop(deck));
            selections = 0; phase = "choose_mode"; CurrentPlayer = (dealer + 1) % 3;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "choose_mode") return hands[actual].Where(card => !card.Joker)
                .Select(card => new Action("choose_mode_card", card.Card, value: card.Id)).ToArray();
            IEnumerable<HamletCard> cards = hands[actual];
            Card? lead = trick.Select(item => item.Item2.Card).FirstOrDefault(card => card.HasValue);
            if (lead.HasValue)
            {
                HamletCard[] follow = cards.Where(card => !card.Joker && card.Card!.Value.Suit == lead.Value.Suit).ToArray();
                if (follow.Length > 0) cards = follow;
            }
            return cards.Select(card => new Action("play", card.Card, value: card.Id)).ToArray();
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "choose_mode")
            {
                modeCards[player] = hands[player].Single(card => card.Id == action.Value);
                selections++;
                if (selections < 3) CurrentPlayer = (player + 1) % 3;
                else ResolveMode();
                return;
            }
            HamletCard played = hands[player].Single(card => card.Id == action.Value);
            hands[player].Remove(played); trick.Add(Tuple.Create(player, played));
            if (trick.Count < 3) { CurrentPlayer = (player + 1) % 3; return; }
            int winner = TrickWinner(); tricks[winner]++; trick.Clear();
            if (hands.All(hand => hand.Count == 0)) FinishDeal();
            else CurrentPlayer = winner;
        }

        private void ResolveMode()
        {
            Card[] cards = modeCards.Select(card => card!.Card!.Value).ToArray();
            IGrouping<Suit, Card>? repeated = cards.GroupBy(card => card.Suit)
                .FirstOrDefault(group => group.Count() >= 2);
            trump = repeated != null
                ? repeated.Key
                : Enum.GetValues(typeof(Suit)).Cast<Suit>().Single(suit => cards.All(card => card.Suit != suit));
            toBe = cards.Any(card => card.Rank >= 11);
            phase = "play"; CurrentPlayer = (dealer + 1) % 3;
        }

        private int TrickWinner()
        {
            Tuple<int, HamletCard>? leadJoker = trick.FirstOrDefault(item => item.Item2.Joker);
            if (trick[0].Item2.Joker) return trick[0].Item1;
            Suit led = trick[0].Item2.Card!.Value.Suit;
            Tuple<int, HamletCard>[] naturals = trick.Where(item => !item.Item2.Joker).ToArray();
            IEnumerable<Tuple<int, HamletCard>> eligible = naturals.Any(item => item.Item2.Card!.Value.Suit == trump)
                ? naturals.Where(item => item.Item2.Card!.Value.Suit == trump)
                : naturals.Where(item => item.Item2.Card!.Value.Suit == led);
            return eligible.OrderByDescending(item => Strength(item.Item2.Card!.Value)).First().Item1;
        }

        private void FinishDeal()
        {
            int hamlet;
            IGrouping<int, int>[] groups = Enumerable.Range(0, 3).GroupBy(player => tricks[player]).ToArray();
            IGrouping<int, int>? tied = groups.FirstOrDefault(group => group.Count() == 2);
            hamlet = tied != null
                ? Enumerable.Range(0, 3).Single(player => !tied.Contains(player))
                : Enumerable.Range(0, 3).OrderBy(player => tricks[player]).Skip(1).First();
            for (int player = 0; player < 3; player++)
            {
                bool isHamlet = player == hamlet;
                if (toBe) scores[player] += isHamlet ? tricks[player] * 10 : tricks[player] == 0 ? 10 : tricks[player];
                else scores[player] += isHamlet ? tricks[player] : tricks[player] == 0 ? 100 : tricks[player] * 10;
            }
            if (scores.Max() >= targetScore) finished = true;
            else StartDeal();
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            return actions.OrderBy(action => action.Card.HasValue ? Strength(action.Card.Value) : 0).First();
        }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 3).Where(player => scores[player] == high),
                scores.Select(value => (double)value), "first to " + targetScore + " Hamlet points", TurnCount);
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            return $"phase={phase} chosen={selections}/3 mode={(phase == "play" ? (toBe ? "to_be" : "not_to_be") : "?")} " +
                $"trump={(phase == "play" ? Card.SuitCode(trump) : "?")} trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] " +
                $"tricks=[{string.Join(",", tricks)}] scores=[{string.Join(",", scores)}] hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\n" +
                $"your hand: {string.Join(" ", hands[viewer])}";
        }

        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static HamletCard Pop(List<HamletCard> cards) { HamletCard card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("hamlet", "ハムレット", 3, 3, "trick-taking",
                "33枚を配り、秘密に選んだ3枚で切り札とto-be/not-to-beを決める。Jokerのleadと中間trick数のHamlet役を含む250点戦。",
                "David Parlett / gokurakism", new Dictionary<string, string> { { "target_score", "250" } }),
            (players, random, options) => new HamletGame(players, random, options));
    }

    public sealed class SheriffGame : GameBase
    {
        private sealed class SheriffCard
        {
            public Card? Card { get; }
            public bool Joker => !Card.HasValue;
            public string Id => Joker ? "X" : Card!.Value.ToString();
            public SheriffCard(Card? card) { Card = card; }
            public override string ToString() => Id;
        }

        private readonly DeterministicRandom rng;
        private readonly int targetScore;
        private readonly List<List<SheriffCard>> hands = new List<List<SheriffCard>>
        {
            new List<SheriffCard>(), new List<SheriffCard>(), new List<SheriffCard>()
        };
        private readonly List<List<SheriffCard>> captured = new List<List<SheriffCard>>
        {
            new List<SheriffCard>(), new List<SheriffCard>(), new List<SheriffCard>()
        };
        private readonly List<Tuple<int, SheriffCard>> trick = new List<Tuple<int, SheriffCard>>();
        private readonly string?[] roles = new string?[3];
        private readonly int[] scores = new int[3];
        private int dealer = 2;
        private int rolesChosen;
        private Suit? trump;
        private string phase = "choose_roles";
        private bool finished;

        public override string GameId => "sheriff";
        public override string Name => "シェリフ";

        public SheriffGame(int players, DeterministicRandom rng,
            IReadOnlyDictionary<string, string> options)
        {
            Players = 3; this.rng = rng;
            targetScore = Math.Max(1, options.Integer("target_score", 8)); StartDeal();
        }

        private void StartDeal()
        {
            foreach (List<SheriffCard> hand in hands) hand.Clear();
            foreach (List<SheriffCard> pile in captured) pile.Clear();
            trick.Clear(); for (int player = 0; player < 3; player++) roles[player] = null;
            var deck = Cards.StandardDeck(new[] { 1, 10, 11, 12, 13 })
                .Select(card => new SheriffCard(card)).ToList();
            deck.Add(new SheriffCard(null)); rng.Shuffle(deck); dealer = (dealer + 1) % 3;
            for (int round = 0; round < 7; round++)
                for (int offset = 1; offset <= 3; offset++) hands[(dealer + offset) % 3].Add(Pop(deck));
            CurrentPlayer = hands.FindIndex(hand => hand.Any(card => card.Joker));
            rolesChosen = 0; trump = null; phase = "choose_roles";
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "choose_roles")
            {
                string[] used = roles.Where(role => role != null).Cast<string>().ToArray();
                return new[] { "mayor", "sheriff", "robber" }.Where(role => !used.Contains(role))
                    .Select(role => new Action("choose_role", value: role)).ToArray();
            }
            if (phase == "choose_trump")
                return new[] { "C", "D", "H", "S", "N" }.Select(value => new Action("choose_trump", value: value)).ToArray();
            IEnumerable<SheriffCard> cards = hands[actual];
            Card? lead = trick.Select(item => item.Item2.Card).FirstOrDefault(card => card.HasValue);
            if (lead.HasValue)
            {
                SheriffCard[] follow = cards.Where(card => !card.Joker && card.Card!.Value.Suit == lead.Value.Suit).ToArray();
                SheriffCard? joker = cards.FirstOrDefault(card => card.Joker);
                if (follow.Length > 0) cards = joker == null ? follow : follow.Concat(new[] { joker });
            }
            return cards.Select(card => new Action("play", card.Card, value: card.Id)).ToArray();
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "choose_roles")
            {
                roles[player] = action.Value; rolesChosen++;
                if (rolesChosen < 3) CurrentPlayer = (player + 1) % 3;
                else { phase = "choose_trump"; CurrentPlayer = Array.IndexOf(roles, "mayor"); }
                return;
            }
            if (phase == "choose_trump")
            {
                trump = action.Value == "N" ? (Suit?)null : Card.ParseSuit(action.Value!);
                phase = "play"; CurrentPlayer = Array.IndexOf(roles, "mayor"); return;
            }
            SheriffCard card = hands[player].Single(item => item.Id == action.Value);
            hands[player].Remove(card); trick.Add(Tuple.Create(player, card));
            if (trick.Count < 3) { CurrentPlayer = (player + 1) % 3; return; }
            int winner = TrickWinner(); captured[winner].AddRange(trick.Select(item => item.Item2)); trick.Clear();
            if (hands.All(hand => hand.Count == 0)) FinishDeal();
            else CurrentPlayer = winner;
        }

        private int TrickWinner()
        {
            Tuple<int, SheriffCard>[] naturals = trick.Where(item => !item.Item2.Joker).ToArray();
            Suit led = naturals[0].Item2.Card!.Value.Suit;
            IEnumerable<Tuple<int, SheriffCard>> eligible = trump.HasValue && naturals.Any(item => item.Item2.Card!.Value.Suit == trump.Value)
                ? naturals.Where(item => item.Item2.Card!.Value.Suit == trump.Value)
                : naturals.Where(item => item.Item2.Card!.Value.Suit == led);
            return eligible.OrderByDescending(item => Strength(item.Item2.Card!.Value)).First().Item1;
        }

        private void FinishDeal()
        {
            int sheriff = Array.IndexOf(roles, "sheriff");
            int robber = Array.IndexOf(roles, "robber");
            int mayor = Array.IndexOf(roles, "mayor");
            int sheriffKings = captured[sheriff].Count(card => card.Card.HasValue && card.Card.Value.Rank == 13);
            int robberTens = captured[robber].Count(card => card.Card.HasValue && card.Card.Value.Rank == 10);
            int mayorCitizens = captured[mayor].Count(card => card.Card.HasValue &&
                (card.Card.Value.Rank == 11 || card.Card.Value.Rank == 12));
            scores[sheriff] += sheriffKings;
            scores[robber] += robberTens;
            scores[mayor] += Math.Max(0, mayorCitizens - (4 - sheriffKings) - robberTens);
            if (scores.Max() >= targetScore) finished = true;
            else StartDeal();
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "choose_roles")
            {
                return actions.OrderByDescending(action => RoleValue(player, action.Value!)).First();
            }
            if (phase == "choose_trump")
            {
                return actions.OrderByDescending(action => action.Value == "N" ? 0 :
                    hands[player].Count(card => card.Card.HasValue && card.Card.Value.Suit == Card.ParseSuit(action.Value!))).First();
            }
            return actions.OrderBy(action => action.Card.HasValue ? Strength(action.Card.Value) : 0).First();
        }

        private int RoleValue(int player, string role)
        {
            if (role == "sheriff") return hands[player].Count(card => card.Card.HasValue && card.Card.Value.Rank == 13);
            if (role == "robber") return hands[player].Count(card => card.Card.HasValue && card.Card.Value.Rank == 10);
            return hands[player].Count(card => card.Card.HasValue && (card.Card.Value.Rank == 11 || card.Card.Value.Rank == 12));
        }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 3).Where(player => scores[player] == high),
                scores.Select(value => (double)value), "first to " + targetScore + " role points", TurnCount);
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            return $"phase={phase} roles=[{string.Join(",", roles.Select(role => role ?? "?"))}] " +
                $"trump={(phase == "choose_roles" || phase == "choose_trump" ? "?" : trump.HasValue ? Card.SuitCode(trump.Value) : "N")} " +
                $"trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] scores=[{string.Join(",", scores)}] " +
                $"hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }

        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static SheriffCard Pop(List<SheriffCard> cards) { SheriffCard card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("sheriff", "シェリフ", 3, 3, "role trick-taking",
                "21枚を7枚ずつ配り、Joker保持者から市長・保安官・強盗を選ぶ。役ごとにQ/J・K・10を得点する8点戦。",
                "The Game Gallery / gokurakism", new Dictionary<string, string> { { "target_score", "8" } }),
            (players, random, options) => new SheriffGame(players, random, options));
    }

    public sealed class MizerkaGame : GameBase
    {
        private static readonly string[] Contracts = { "C", "D", "H", "S", "NT", "M" };
        private readonly DeterministicRandom rng;
        private readonly List<List<Card>> hands = new List<List<Card>>
        {
            new List<Card>(), new List<Card>(), new List<Card>()
        };
        private readonly List<Card> talon = new List<Card>();
        private readonly List<Card> pendingDiscards = new List<Card>();
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly bool[,] usedContracts = new bool[3, 6];
        private readonly int[] tricks = new int[3];
        private readonly int[] scores = new int[3];
        private List<Card> remainingDeck = new List<Card>();
        private int dealer = 2;
        private int chooser;
        private int exchangePosition;
        private int dealsPlayed;
        private string contract = "";
        private string phase = "choose_contract";
        private bool finished;

        public override string GameId => "mizerka";
        public override string Name => "ミゼルカ";

        public MizerkaGame(int players, DeterministicRandom rng)
        {
            Players = 3; this.rng = rng; StartDeal();
        }

        private void StartDeal()
        {
            foreach (List<Card> hand in hands) hand.Clear();
            talon.Clear(); pendingDiscards.Clear(); trick.Clear(); Array.Clear(tricks, 0, 3);
            remainingDeck = Cards.Shuffled(Cards.StandardDeck(), rng); dealer = (dealer + 1) % 3;
            for (int round = 0; round < 6; round++)
            {
                for (int offset = 1; offset <= 3; offset++) hands[(dealer + offset) % 3].Add(Pop(remainingDeck));
                talon.Add(Pop(remainingDeck));
            }
            chooser = (dealer + 1) % 3; contract = ""; phase = "choose_contract"; CurrentPlayer = chooser;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "choose_contract") return Contracts.Select((value, index) => Tuple.Create(value, index))
                .Where(item => !usedContracts[actual, item.Item2])
                .Select(item => new Action("choose_contract", value: item.Item1)).ToArray();
            if (phase == "exchange")
            {
                var actions = new List<Action> { new Action("finish_exchange") };
                if (pendingDiscards.Count < talon.Count)
                    actions.AddRange(hands[actual].Select(card => new Action("discard_for_exchange", card)));
                return actions;
            }
            IEnumerable<Card> cards = hands[actual];
            if (trick.Count > 0)
            {
                Suit led = trick[0].Item2.Suit; Card[] follow = cards.Where(card => card.Suit == led).ToArray();
                if (follow.Length > 0) cards = follow;
            }
            return cards.Select(card => new Action("play", card)).ToArray();
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "choose_contract")
            {
                contract = action.Value!; usedContracts[player, Array.IndexOf(Contracts, contract)] = true;
                for (int round = 0; round < 7; round++)
                {
                    for (int offset = 1; offset <= 3; offset++) hands[(dealer + offset) % 3].Add(Pop(remainingDeck));
                    talon.Add(Pop(remainingDeck));
                }
                exchangePosition = 0; phase = "exchange"; CurrentPlayer = ExchangePlayer(exchangePosition); return;
            }
            if (phase == "exchange")
            {
                if (action.Kind == "discard_for_exchange")
                {
                    Card card = action.Card!.Value; hands[player].Remove(card); pendingDiscards.Add(card); return;
                }
                for (int count = 0; count < pendingDiscards.Count; count++) hands[player].Add(Pop(talon));
                pendingDiscards.Clear(); exchangePosition++;
                if (exchangePosition >= 3 || talon.Count == 0) BeginPlay();
                else CurrentPlayer = ExchangePlayer(exchangePosition);
                return;
            }
            Card played = action.Card!.Value; hands[player].Remove(played); trick.Add(Tuple.Create(player, played));
            if (trick.Count < 3) { CurrentPlayer = (player + 1) % 3; return; }
            int winner = TrickWinner(); tricks[winner]++; trick.Clear();
            if (hands.All(hand => hand.Count == 0)) FinishDeal();
            else CurrentPlayer = winner;
        }

        private int ExchangePlayer(int position) => position == 0 ? chooser : position == 1 ? (dealer + 2) % 3 : dealer;
        private void BeginPlay() { phase = "play"; CurrentPlayer = chooser; }

        private int TrickWinner()
        {
            Suit led = trick[0].Item2.Suit;
            Suit? trump = Array.IndexOf(Contracts, contract) < 4 ? Card.ParseSuit(contract) : (Suit?)null;
            IEnumerable<Tuple<int, Card>> eligible = trump.HasValue && trick.Any(item => item.Item2.Suit == trump.Value)
                ? trick.Where(item => item.Item2.Suit == trump.Value)
                : trick.Where(item => item.Item2.Suit == led);
            return eligible.OrderByDescending(item => Strength(item.Item2)).First().Item1;
        }

        private void FinishDeal()
        {
            int right = (dealer + 2) % 3;
            int[] baseline = contract == "M" ? new[] { 1, 5, 7 } : new[] { 7, 5, 1 };
            int[] players = { chooser, right, dealer };
            for (int index = 0; index < 3; index++)
                scores[players[index]] += contract == "M"
                    ? baseline[index] - tricks[players[index]]
                    : tricks[players[index]] - baseline[index];
            dealsPlayed++;
            if (dealsPlayed >= 18) finished = true;
            else StartDeal();
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "choose_contract")
            {
                return actions.OrderByDescending(action => Array.IndexOf(Contracts, action.Value!) < 4
                    ? hands[player].Count(card => card.Suit == Card.ParseSuit(action.Value!))
                    : action.Value == "NT" ? hands[player].Sum(card => Strength(card)) / 20 :
                    hands[player].Sum(card => 15 - Strength(card)) / 20).First();
            }
            if (phase == "exchange") return actions.First(action => action.Kind == "finish_exchange");
            return contract == "M"
                ? actions.OrderBy(action => Strength(action.Card!.Value)).First()
                : actions.OrderByDescending(action => Strength(action.Card!.Value)).First();
        }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 3).Where(player => scores[player] == high),
                scores.Select(value => (double)value), "eighteen-contract match", TurnCount);
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            string available = string.Join(",", Contracts.Select((value, index) => Tuple.Create(value, index))
                .Where(item => !usedContracts[viewer, item.Item2]).Select(item => item.Item1));
            return $"phase={phase} deal={dealsPlayed + 1}/18 dealer=P{dealer} chooser=P{chooser} contract={(contract == "" ? "?" : contract)} " +
                $"talon={talon.Count} pending={pendingDiscards.Count} trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] " +
                $"tricks=[{string.Join(",", tricks)}] scores=[{string.Join(",", scores)}] hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}] " +
                $"your_contracts=[{available}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }

        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("mizerka", "ミゼルカ", 3, 3, "contract trick-taking",
                "52枚を13枚ずつ配り、talon交換後に4切り札・no-trump・misereを各人1回ずつ選ぶ18ディール戦。",
                "traditional / gokurakism"),
            (players, random, options) => new MizerkaGame(players, random));
    }
}
