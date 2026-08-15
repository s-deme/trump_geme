using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TrumpLab.Games
{
    public sealed class DaifugoTwoGame : GameBase
    {
        private sealed class PlayingCard
        {
            public string Id { get; }
            public Card? Card { get; }
            public int JokerPower { get; }
            public bool IsJoker => JokerPower > 0;
            public PlayingCard(Card card) { Card = card; Id = card.ToString(); }
            public PlayingCard(string id, int jokerPower) { Id = id; JokerPower = jokerPower; }
            public override string ToString() => Id;
        }

        private sealed class Combo
        {
            public string Kind { get; }
            public int Top { get; }
            public PlayingCard[] Cards { get; }
            public Combo(string kind, int top, IEnumerable<PlayingCard> cards)
            {
                Kind = kind; Top = top; Cards = cards.OrderBy(card => card.Id).ToArray();
            }
            public string Encode() => Kind + ":" + Top.ToString(CultureInfo.InvariantCulture) + ":" +
                string.Join(",", Cards.Select(card => card.Id));
        }

        private readonly DeterministicRandom rng;
        private readonly int targetScore;
        private readonly List<List<PlayingCard>> hands = new List<List<PlayingCard>>
        {
            new List<PlayingCard>(), new List<PlayingCard>()
        };
        private readonly List<PlayingCard> stock = new List<PlayingCard>();
        private readonly int[] scores = new int[2];
        private Combo? currentCombo;
        private bool revolution;
        private int starter = 1;
        private bool finished;
        private string? cachedLegalKey;
        private IReadOnlyList<Action>? cachedLegalActions;

        public override string GameId => "daifugo_two";
        public override string Name => "2人用大富豪";

        public DaifugoTwoGame(int players, DeterministicRandom rng,
            IReadOnlyDictionary<string, string> options)
        {
            Players = 2;
            this.rng = rng;
            targetScore = Math.Max(1, options.Integer("target_score", 30));
            StartDeal();
        }

        private void StartDeal()
        {
            hands[0].Clear(); hands[1].Clear(); stock.Clear();
            foreach (Card card in Cards.StandardDeck(new[] { 1, 2, 7, 8, 9, 10, 11, 12, 13 }))
                stock.Add(new PlayingCard(card));
            stock.Add(new PlayingCard("J0", 1));
            stock.Add(new PlayingCard("J1", 2));
            rng.Shuffle(stock);
            for (int block = 0; block < 4; block++)
                for (int player = 0; player < 2; player++)
                    for (int card = 0; card < 4; card++) hands[player].Add(Pop(stock));
            starter = 1 - starter;
            CurrentPlayer = starter;
            currentCombo = null;
            revolution = false;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            string key = actual.ToString(CultureInfo.InvariantCulture) + "|" + revolution + "|" +
                (currentCombo == null ? "-" : currentCombo.Encode()) + "|" +
                string.Join(",", hands[actual].Select(card => card.Id).OrderBy(id => id));
            if (key == cachedLegalKey && cachedLegalActions != null) return cachedLegalActions;
            var result = Combinations(hands[actual]).Where(CanBeat)
                .Select(combo => new Action("play", value: combo.Encode())).ToList();
            if (currentCombo != null) result.Add(new Action("pass"));
            cachedLegalKey = key;
            cachedLegalActions = result.ToArray();
            return cachedLegalActions;
        }

        private bool CanBeat(Combo candidate)
        {
            if (currentCombo == null) return true;
            return candidate.Kind == currentCombo.Kind && candidate.Cards.Length == currentCombo.Cards.Length &&
                ComboStrength(candidate) > ComboStrength(currentCombo);
        }

        private int ComboStrength(Combo combo)
        {
            if (combo.Kind == "S" && combo.Cards[0].IsJoker) return 9 + combo.Cards[0].JokerPower;
            return revolution ? 8 - combo.Top : combo.Top;
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null);
            Guard.Legal(action, LegalActions(player));
            TurnCount++;
            if (action.Kind == "pass")
            {
                if (stock.Count > 0) hands[player].Add(Pop(stock));
                currentCombo = null;
                CurrentPlayer = 1 - player;
                return;
            }
            string[] parts = action.Value!.Split(':');
            string[] ids = parts[2].Split(',');
            PlayingCard[] selected = ids.Select(id => hands[player].Single(card => card.Id == id)).ToArray();
            foreach (PlayingCard card in selected) hands[player].Remove(card);
            currentCombo = new Combo(parts[0], int.Parse(parts[1], CultureInfo.InvariantCulture), selected);
            if (currentCombo.Kind == "G" && currentCombo.Cards.Length == 4) revolution = !revolution;
            if (hands[player].Count == 0)
            {
                scores[player] += hands[1 - player].Count;
                if (scores[player] >= targetScore) finished = true;
                else StartDeal();
                return;
            }
            CurrentPlayer = 1 - player;
        }

        private static IEnumerable<Combo> Combinations(IReadOnlyList<PlayingCard> hand)
        {
            var results = new Dictionary<string, Combo>();
            foreach (PlayingCard card in hand)
            {
                int top = card.IsJoker ? 8 + card.JokerPower : RankIndex(card.Card!.Value.Rank);
                Add(results, new Combo("S", top, new[] { card }));
            }

            PlayingCard[] jokers = hand.Where(card => card.IsJoker).ToArray();
            for (int rank = 0; rank < 9; rank++)
            {
                PlayingCard[] pool = hand.Where(card => !card.IsJoker &&
                    RankIndex(card.Card!.Value.Rank) == rank).Concat(jokers).ToArray();
                for (int size = 2; size <= Math.Min(4, pool.Length); size++)
                    foreach (PlayingCard[] subset in Subsets(pool, size))
                        Add(results, new Combo("G", rank, subset));
            }

            for (int length = 3; length <= 9; length++)
                for (int start = 0; start + length <= 9; start++)
                    BuildRuns(hand, start, length, 0, new List<PlayingCard>(), results);
            return results.Values;
        }

        private static void BuildRuns(IReadOnlyList<PlayingCard> hand, int start, int length, int offset,
            List<PlayingCard> selected, IDictionary<string, Combo> results)
        {
            if (offset == length)
            {
                Add(results, new Combo("R", start + length - 1, selected));
                return;
            }
            int rank = start + offset;
            PlayingCard[] naturals = hand.Where(card => !selected.Contains(card) && !card.IsJoker &&
                RankIndex(card.Card!.Value.Rank) == rank).ToArray();
            foreach (PlayingCard card in naturals)
            {
                selected.Add(card);
                BuildRuns(hand, start, length, offset + 1, selected, results);
                selected.RemoveAt(selected.Count - 1);
            }
            foreach (PlayingCard joker in hand.Where(card => card.IsJoker && !selected.Contains(card)))
            {
                selected.Add(joker);
                BuildRuns(hand, start, length, offset + 1, selected, results);
                selected.RemoveAt(selected.Count - 1);
            }
        }

        private static IEnumerable<PlayingCard[]> Subsets(PlayingCard[] cards, int size)
        {
            int possibilities = 1 << cards.Length;
            for (int mask = 0; mask < possibilities; mask++)
            {
                if (CountBits(mask) != size) continue;
                yield return Enumerable.Range(0, cards.Length).Where(index => (mask & (1 << index)) != 0)
                    .Select(index => cards[index]).ToArray();
            }
        }

        private static int CountBits(int value)
        {
            int count = 0;
            while (value != 0) { count += value & 1; value >>= 1; }
            return count;
        }

        private static void Add(IDictionary<string, Combo> values, Combo combo)
        {
            string key = combo.Encode();
            if (!values.ContainsKey(key)) values.Add(key, combo);
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            Action[] plays = actions.Where(action => action.Kind == "play").ToArray();
            if (plays.Length == 0) return actions.Single(action => action.Kind == "pass");
            return plays.OrderByDescending(action => action.Value!.Split(':')[2].Split(',').Length)
                .ThenBy(action => int.Parse(action.Value!.Split(':')[1], CultureInfo.InvariantCulture)).First();
        }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 2).Where(player => scores[player] == high),
                scores.Select(value => (double)value), "first to " + targetScore + " remaining-card points", TurnCount);
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            return $"revolution={revolution} pile={(currentCombo == null ? "-" : currentCombo.Encode())} " +
                $"stock={stock.Count} scores=[{string.Join(",", scores)}] hand_counts=[{hands[0].Count},{hands[1].Count}]\n" +
                $"your hand: {string.Join(" ", hands[viewer])}";
        }

        private static int RankIndex(int rank) => rank == 1 ? 7 : rank == 2 ? 8 : rank - 7;
        private static PlayingCard Pop(List<PlayingCard> cards)
        {
            PlayingCard card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card;
        }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("daifugo_two", "2人用大富豪", 2, 2, "climbing",
                "7～2と強さの異なるジョーカー2枚を16枚ずつ配り、単札・同数2～4枚・3枚以上の連番、革命、パス時補充で30点を競う。",
                "Gokurakism two-player Daifugo", new Dictionary<string, string> { { "target_score", "30" } }),
            (players, random, options) => new DaifugoTwoGame(players, random, options));
    }
}
