using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    internal static class FourPlayerClimbingGames
    {
        public static void RegisterGames(GameRegistry registry)
        {
            BigTwoGame.Register(registry);
            Sasaki44AGame.Register(registry);
        }
    }

    public sealed class BigTwoGame : GameBase
    {
        private sealed class Combo
        {
            public Card[] Cards { get; }
            public int Count => Cards.Length;
            public int Category { get; }
            public int[] Key { get; }
            public string Id => string.Join("+", Cards.Select(card => card.ToString()));
            public Combo(IEnumerable<Card> cards, int category, params int[] key)
            { Cards = cards.OrderBy(BigRank).ThenBy(SuitRank).ToArray(); Category = category; Key = key; }
        }

        private readonly List<List<Card>> hands = Enumerable.Range(0, 4).Select(_ => new List<Card>()).ToList();
        private readonly int[] scores = new int[4];
        private Combo? table;
        private int lastPlayer = -1;
        private int passes;
        private bool opening = true;
        private bool finished;

        public override string GameId => "big_two";
        public override string Name => "大老二";

        public BigTwoGame(int players, DeterministicRandom rng)
        {
            Players = 4; List<Card> deck = Cards.Shuffled(Cards.StandardDeck(), rng);
            for (int round = 0; round < 13; round++) for (int player = 0; player < 4; player++) hands[player].Add(Pop(deck));
            CurrentPlayer = Enumerable.Range(0, 4).Single(player => hands[player].Contains(new Card(Suit.Clubs, 3)));
            int dragon = Enumerable.Range(0, 4).Where(player => hands[player].Select(card => card.Rank).Distinct().Count() == 13)
                .DefaultIfEmpty(-1).First();
            if (dragon >= 0) Settle(dragon, hands[dragon].Select(card => card.Suit).Distinct().Count() == 1 ? 4 : 3);
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player); var actions = ValidCombos(hands[actual])
                .Where(combo => (!opening || combo.Cards.Contains(new Card(Suit.Clubs, 3))) && (table == null || Beats(combo, table)))
                .Select(combo => new Action("play_combination", value: combo.Id)).ToList();
            if (table != null) actions.Add(new Action("pass"));
            return actions;
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (action.Kind == "pass")
            {
                passes++;
                if (passes >= 3) { CurrentPlayer = lastPlayer; table = null; passes = 0; }
                else CurrentPlayer = Previous(player);
                return;
            }
            Combo combo = ValidCombos(hands[player]).Single(item => item.Id == action.Value);
            foreach (Card card in combo.Cards) hands[player].Remove(card);
            table = combo; lastPlayer = player; passes = 0; opening = false;
            if (hands[player].Count == 0) { Settle(player, 1); return; }
            CurrentPlayer = Previous(player);
        }

        private void Settle(int winner, int specialMultiplier)
        {
            int total = 0;
            for (int player = 0; player < 4; player++) if (player != winner)
            {
                int count = hands[player].Count;
                int multiplier = specialMultiplier > 1 ? specialMultiplier : count == 13 ? 4 : count >= 8 ? 3 : 1;
                multiplier *= 1 << hands[player].Count(card => card.Rank == 2);
                int penalty = count * multiplier; scores[player] = -penalty; total += penalty;
            }
            scores[winner] = total; finished = true;
        }

        private static IEnumerable<Combo> ValidCombos(IReadOnlyList<Card> hand)
        {
            foreach (Card card in hand) yield return new Combo(new[] { card }, -1, BigRank(card), SuitRank(card));
            foreach (Card[] cards in Choose(hand, 2).Where(cards => cards[0].Rank == cards[1].Rank))
                yield return new Combo(cards, -1, BigRank(cards[0]), cards.Max(SuitRank));
            foreach (Card[] cards in Choose(hand, 3).Where(cards => cards.Select(card => card.Rank).Distinct().Count() == 1))
                yield return new Combo(cards, -1, BigRank(cards[0]), cards.Max(SuitRank));
            foreach (Card[] cards in Choose(hand, 5))
            {
                Combo? combo = FiveCardCombo(cards); if (combo != null) yield return combo;
            }
        }

        private static Combo? FiveCardCombo(Card[] cards)
        {
            int[] ranks = cards.Select(BigRank).OrderBy(value => value).ToArray();
            bool straight = ranks.Distinct().Count() == 5 && ranks[4] <= 14 && ranks.Zip(ranks.Skip(1), (a, b) => b - a).All(delta => delta == 1);
            bool flush = cards.Select(card => card.Suit).Distinct().Count() == 1;
            int[] counts = cards.GroupBy(card => card.Rank).Select(group => group.Count()).OrderByDescending(value => value).ToArray();
            if (straight && flush) return new Combo(cards, 4, ranks[4], SuitRank(cards.Single(card => BigRank(card) == ranks[4])));
            if (counts[0] == 4)
            {
                int quad = BigRank(cards.GroupBy(card => card.Rank).Single(group => group.Count() == 4).First());
                int kicker = BigRank(cards.GroupBy(card => card.Rank).Single(group => group.Count() == 1).First());
                return new Combo(cards, 3, quad, kicker);
            }
            if (counts.SequenceEqual(new[] { 3, 2 }))
            {
                int triple = BigRank(cards.GroupBy(card => card.Rank).Single(group => group.Count() == 3).First());
                int pair = BigRank(cards.GroupBy(card => card.Rank).Single(group => group.Count() == 2).First());
                return new Combo(cards, 2, triple, pair);
            }
            if (flush)
            {
                int[] descending = ranks.OrderByDescending(value => value).ToArray();
                return new Combo(cards, 1, new[] { SuitRank(cards[0]) }.Concat(descending).ToArray());
            }
            if (straight) return new Combo(cards, 0, ranks[4], SuitRank(cards.Single(card => BigRank(card) == ranks[4])));
            return null;
        }

        private static bool Beats(Combo candidate, Combo previous)
        {
            if (candidate.Count != previous.Count) return false;
            if (candidate.Count == 5 && candidate.Category != previous.Category) return candidate.Category > previous.Category;
            if (candidate.Count == 5 && candidate.Category == 2)
                return candidate.Key[0] > previous.Key[0] && candidate.Key[1] > previous.Key[1];
            return LexCompare(candidate.Key, previous.Key) > 0;
        }

        private static int LexCompare(IReadOnlyList<int> left, IReadOnlyList<int> right)
        {
            for (int i = 0; i < Math.Min(left.Count, right.Count); i++) if (left[i] != right[i]) return left[i].CompareTo(right[i]);
            return left.Count.CompareTo(right.Count);
        }
        private static IEnumerable<Card[]> Choose(IReadOnlyList<Card> cards, int count)
        {
            var selected = new Card[count];
            IEnumerable<Card[]> Walk(int start, int depth)
            {
                if (depth == count) { yield return selected.ToArray(); yield break; }
                for (int index = start; index <= cards.Count - (count - depth); index++)
                { selected[depth] = cards[index]; foreach (Card[] result in Walk(index + 1, depth + 1)) yield return result; }
            }
            return Walk(0, 0);
        }
        private int Previous(int player) => (player + 3) % 4;
        private static int BigRank(Card card) => card.Rank == 2 ? 15 : card.Rank == 1 ? 14 : card.Rank;
        private static int SuitRank(Card card) => (int)card.Suit;
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            return actions.FirstOrDefault(action => action.Kind == "play_combination").Kind == "play_combination"
                ? actions.First(action => action.Kind == "play_combination") : actions[0];
        }
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over."); int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 4).Where(player => scores[player] == high), scores.Select(value => (double)value), "first player out with Big Two penalties", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            return $"opening={opening} table={(table == null ? "-" : table.Id)} last={(lastPlayer < 0 ? "-" : "P" + lastPlayer)} passes={passes}/3 " +
                $"hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("big_two", "大老二", 4, 4, "climbing poker",
                "3Cを含む組から反時計回りに開始し、2>A>…>3・S>H>D>Cで単枚、pair、triple、5枚のstraight/flush/full house/four+1/straight flushを競る。上がり時の残札・2・8枚以上罰則を精算する。straightは2を含めない採用仕様。",
                "gokurakism/Big Two"),
            (players, random, options) => new BigTwoGame(players, random));
    }

    public sealed class Sasaki44AGame : GameBase
    {
        private sealed class Combo
        {
            public Card[] Cards { get; }
            public string Shape { get; }
            public int Special { get; }
            public int Rank { get; }
            public string Id => Special + ":" + Shape + ":" + string.Join("+", Cards.Select(card => card.ToString()));
            public Combo(IEnumerable<Card> cards, string shape, int special, int rank)
            { Cards = cards.OrderBy(Strength).ThenBy(card => card.Suit).ToArray(); Shape = shape; Special = special; Rank = rank; }
        }

        private readonly bool night;
        private readonly List<List<Card>> hands = Enumerable.Range(0, 4).Select(_ => new List<Card>()).ToList();
        private readonly HashSet<int> redTeam = new HashSet<int>();
        private readonly List<int> finishOrder = new List<int>();
        private readonly bool[] outPlayers = new bool[4];
        private readonly int[] scores = new int[4];
        private Combo? table;
        private int lastPlayer = -1;
        private int passes;
        private int offers;
        private int kickPlayer = -1;
        private int kickRank;
        private int stabPasses;
        private bool opening = true;
        private bool run;
        private bool stopped;
        private bool teamsPublic;
        private string phase = "run_offer";
        private bool finished;

        public override string GameId => "sasaki_44a";
        public override string Name => "44A（ササキ）";

        public Sasaki44AGame(int players, DeterministicRandom rng, IReadOnlyDictionary<string, string> options)
        {
            Players = 4; night = options.Boolean("night", false);
            List<Card> deck = Cards.Shuffled(Cards.StandardDeck().Where(card => card.Rank != 2), rng);
            for (int round = 0; round < 12; round++) for (int player = 0; player < 4; player++) hands[player].Add(Pop(deck));
            Card redDiamond = new Card(Suit.Diamonds, 10), redHeart = new Card(Suit.Hearts, 10);
            for (int player = 0; player < 4; player++) if (hands[player].Contains(redDiamond) || hands[player].Contains(redHeart)) redTeam.Add(player);
            CurrentPlayer = 0;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "run_offer")
            {
                bool canRun = hands[actual].Contains(new Card(Suit.Diamonds, 10)) || hands[actual].Contains(new Card(Suit.Hearts, 10));
                return canRun ? new[] { new Action("keep_hidden"), new Action("run") } : new[] { new Action("keep_hidden") };
            }
            if (phase == "stop_offer") return redTeam.Contains(actual)
                ? new[] { new Action("decline_stop") }
                : new[] { new Action("decline_stop"), new Action("stop") };
            if (phase == "stab")
            {
                var actions = new List<Action> { new Action("pass_stab") };
                actions.AddRange(hands[actual].Where(card => card.Rank == kickRank).Select(card => new Action("stab", card)));
                return actions;
            }
            var plays = ValidCombos(hands[actual]).Where(combo =>
                (!opening || combo.Cards.Contains(new Card(night ? Suit.Spades : Suit.Hearts, 3))) &&
                (table == null || Beats(combo, table))).Select(combo => new Action("play_combination", value: combo.Id)).ToList();
            if (table != null)
            {
                Card single = table.Cards.Length == 1 ? table.Cards[0] : default;
                if (table.Cards.Length == 1)
                    plays.AddRange(Choose(hands[actual], 2).Where(cards => cards.All(card => card.Rank == single.Rank))
                        .Select(cards => new Action("kick", value: string.Join("+", cards.Select(card => card.ToString())))));
                plays.Add(new Action("pass"));
            }
            return plays;
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "run_offer")
            {
                offers++; if (action.Kind == "run") { run = true; teamsPublic = true; phase = "stop_offer"; offers = 0; CurrentPlayer = Next(player); return; }
                if (offers >= 4) BeginPlay(); else CurrentPlayer = Next(player); return;
            }
            if (phase == "stop_offer")
            {
                offers++; if (action.Kind == "stop") { stopped = true; BeginPlay(); return; }
                if (offers >= 4) BeginPlay(); else CurrentPlayer = Next(player); return;
            }
            if (phase == "stab")
            {
                if (action.Kind == "stab")
                {
                    Card card = action.Card!.Value; hands[player].Remove(card); RecordOut(player);
                    table = null; passes = 0; phase = "play"; CurrentPlayer = outPlayers[player] ? NextActive(player) : player;
                    if (CheckFinished()) return; return;
                }
                stabPasses++;
                if (stabPasses >= RequiredResponses(kickPlayer))
                { table = null; passes = 0; phase = "play"; CurrentPlayer = outPlayers[kickPlayer] ? NextActive(kickPlayer) : kickPlayer; }
                else CurrentPlayer = NextActive(player);
                return;
            }
            if (action.Kind == "pass")
            {
                passes++;
                if (passes >= RequiredResponses(lastPlayer)) { table = null; passes = 0; CurrentPlayer = outPlayers[lastPlayer] ? NextActive(lastPlayer) : lastPlayer; }
                else CurrentPlayer = NextActive(player);
                return;
            }
            if (action.Kind == "kick")
            {
                Card[] cards = action.Value!.Split('+').Select(Card.Parse).ToArray(); foreach (Card card in cards) hands[player].Remove(card);
                RecordOut(player); kickPlayer = player; kickRank = cards[0].Rank; stabPasses = 0; phase = "stab"; teamsPublic |= kickRank == 10;
                if (CheckFinished()) return; CurrentPlayer = NextActive(player); return;
            }
            Combo combo = ValidCombos(hands[player]).Single(item => item.Id == action.Value);
            foreach (Card card in combo.Cards) { hands[player].Remove(card); if (card.Rank == 10 && (card.Suit == Suit.Diamonds || card.Suit == Suit.Hearts)) teamsPublic = true; }
            table = combo; lastPlayer = player; passes = 0; opening = false; RecordOut(player);
            if (CheckFinished()) return; CurrentPlayer = NextActive(player);
        }

        private void BeginPlay()
        {
            phase = "play"; opening = true; table = null; passes = 0;
            Card starter = new Card(night ? Suit.Spades : Suit.Hearts, 3);
            CurrentPlayer = Enumerable.Range(0, 4).Single(player => hands[player].Contains(starter));
        }

        private void RecordOut(int player)
        {
            if (hands[player].Count == 0 && !outPlayers[player]) { outPlayers[player] = true; finishOrder.Add(player); }
        }
        private bool CheckFinished()
        {
            if (ActiveCount() > 1) return false;
            int last = Enumerable.Range(0, 4).Single(player => !outPlayers[player]); finishOrder.Add(last); Settle(); return true;
        }
        private void Settle()
        {
            int multiplier = stopped ? 4 : run ? 2 : 1;
            if (redTeam.Count == 1)
            {
                int solo = redTeam.Single(), position = finishOrder.IndexOf(solo) + 1;
                if (position == 1) { scores[solo] = 6 * multiplier; for (int p = 0; p < 4; p++) if (p != solo) scores[p] = -2 * multiplier; }
                else if (position == 4) { scores[solo] = -3 * multiplier; for (int p = 0; p < 4; p++) if (p != solo) scores[p] = multiplier; }
            }
            else
            {
                int[] positions = redTeam.Select(player => finishOrder.IndexOf(player) + 1).OrderBy(value => value).ToArray();
                int amount = positions.SequenceEqual(new[] { 1, 2 }) ? 2 : positions.SequenceEqual(new[] { 1, 3 }) ? 1 :
                    positions.SequenceEqual(new[] { 2, 4 }) ? -1 : positions.SequenceEqual(new[] { 3, 4 }) ? -2 : 0;
                for (int player = 0; player < 4; player++) scores[player] = (redTeam.Contains(player) ? amount : -amount) * multiplier;
            }
            finished = true;
        }

        private static IEnumerable<Combo> ValidCombos(IReadOnlyList<Card> hand)
        {
            foreach (Card card in hand) yield return new Combo(new[] { card }, "single", 0, Strength(card));
            foreach (Card[] cards in Choose(hand, 2).Where(cards => cards[0].Rank == cards[1].Rank))
                yield return new Combo(cards, "pair", 0, Strength(cards[0]));
            foreach (int length in Enumerable.Range(3, Math.Max(0, hand.Count - 2)))
                foreach (Card[] cards in Choose(hand, length))
                {
                    int[] ranks = cards.Select(Strength).Distinct().OrderBy(value => value).ToArray();
                    if (ranks.Length == length && ranks.Zip(ranks.Skip(1), (a, b) => b - a).All(delta => delta == 1))
                        yield return new Combo(cards, "straight" + length, 0, ranks.Last());
                }
            foreach (Card[] cards in Choose(hand, 3).Where(cards => cards.Select(card => card.Rank).Distinct().Count() == 1))
                yield return new Combo(cards, "special", 1, Strength(cards[0]));
            foreach (Card[] cards in Choose(hand, 3).Where(cards => cards.Count(card => card.Rank == 4) == 2 && cards.Count(card => card.Rank == 1) == 1))
                yield return new Combo(cards, "special", 2, 0);
            foreach (Card[] cards in Choose(hand, 4).Where(cards => cards.Select(card => card.Rank).Distinct().Count() == 1))
                yield return new Combo(cards, "special", 3, Strength(cards[0]));
            Card[] redPig = { new Card(Suit.Diamonds, 10), new Card(Suit.Hearts, 10) };
            if (redPig.All(hand.Contains)) yield return new Combo(redPig, "special", 4, 0);
            Card[] blackPig = { new Card(Suit.Spades, 10), new Card(Suit.Clubs, 10) };
            if (blackPig.All(hand.Contains)) yield return new Combo(blackPig, "special", 5, 0);
        }
        private static bool Beats(Combo candidate, Combo previous)
        {
            if (candidate.Special > 0 || previous.Special > 0)
                return candidate.Special > previous.Special || candidate.Special == previous.Special && candidate.Rank > previous.Rank;
            return candidate.Shape == previous.Shape && candidate.Rank > previous.Rank;
        }
        private static IEnumerable<Card[]> Choose(IReadOnlyList<Card> cards, int count)
        {
            var selected = new Card[count];
            IEnumerable<Card[]> Walk(int start, int depth)
            {
                if (depth == count) { yield return selected.ToArray(); yield break; }
                for (int index = start; index <= cards.Count - (count - depth); index++)
                { selected[depth] = cards[index]; foreach (Card[] result in Walk(index + 1, depth + 1)) yield return result; }
            }
            return Walk(0, 0);
        }
        private int ActiveCount() => outPlayers.Count(value => !value);
        private int RequiredResponses(int leader) => ActiveCount() - (outPlayers[leader] ? 0 : 1);
        private int Next(int player) => (player + 3) % 4;
        private int NextActive(int player) { int next = Next(player); while (outPlayers[next]) next = Next(next); return next; }
        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            string[] preferences = { "play_combination", "stab", "kick", "keep_hidden", "decline_stop", "pass_stab", "pass" };
            foreach (string kind in preferences) if (actions.Any(action => action.Kind == kind)) return actions.First(action => action.Kind == kind);
            return actions[0];
        }
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over."); int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 4).Where(player => scores[player] == high), scores.Select(value => (double)value), "hidden red-ten team finishing order", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            string teams = teamsPublic || finished ? string.Join(",", Enumerable.Range(0, 4).Select(player => "P" + player + ":" + (redTeam.Contains(player) ? "red-ten" : "other"))) :
                (redTeam.Contains(viewer) ? "you:red-ten; others hidden" : "you:other; others hidden");
            return $"phase={phase} mode={(night ? "night" : "morning")} run={run} stopped={stopped} teams={teams} table={(table == null ? "-" : table.Id)} " +
                $"passes={passes} finish_order=[{string.Join(",", finishOrder.Select(player => "P" + player))}] hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("sasaki_44a", "44A（ササキ）", 4, 4, "hidden-team climbing",
                "2を除く48枚で、赤10保持側対他方を隠して単枚・pair・3枚以上straightを競る。triple、4+4+A、four、赤豚、黒豚の特殊役、単枚への『ける』と応答『さす』、走る／止まれ倍率、上がり順の2対2・1対3精算を扱う。",
                "gokurakism/44A", new Dictionary<string, string> { { "night", "夜ならtrue（既定false）" } }),
            (players, random, options) => new Sasaki44AGame(players, random, options));
    }
}
