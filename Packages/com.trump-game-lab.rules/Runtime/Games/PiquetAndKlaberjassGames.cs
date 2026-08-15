using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TrumpLab.Games
{
    internal static class PiquetAndKlaberjassGames
    {
        public static void RegisterGames(GameRegistry registry)
        {
            PiquetGame.Register(registry);
            KlaberjassGame.Register(registry);
        }
    }

    public sealed class PiquetGame : GameBase
    {
        private readonly DeterministicRandom rng;
        private readonly List<List<Card>> hands = new List<List<Card>>
        {
            new List<Card>(), new List<Card>()
        };
        private readonly List<Card> talon = new List<Card>();
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly int[] dealPoints = new int[2];
        private readonly int[] totalPoints = new int[2];
        private readonly int[] tricks = new int[2];
        private readonly double[] finalScores = new double[2];
        private int dealer = 1;
        private int dealsPlayed;
        private int dealLimit = 6;
        private int elder;
        private int currentLeader;
        private string phase = "elder_exchange";
        private bool repique;
        private bool finished;

        public override string GameId => "piquet";
        public override string Name => "ピケ";

        public PiquetGame(int players, DeterministicRandom rng)
        {
            Players = 2;
            this.rng = rng;
            StartDeal();
        }

        private void StartDeal()
        {
            hands[0].Clear(); hands[1].Clear(); talon.Clear(); trick.Clear();
            dealPoints[0] = 0; dealPoints[1] = 0; tricks[0] = 0; tricks[1] = 0;
            talon.AddRange(Cards.Shuffled(Cards.StandardDeck(new[] { 1, 7, 8, 9, 10, 11, 12, 13 }), rng));
            dealer = 1 - dealer;
            elder = 1 - dealer;
            for (int packet = 0; packet < 6; packet++)
                for (int offset = 1; offset <= 2; offset++)
                    for (int card = 0; card < 2; card++) hands[(dealer + offset) % 2].Add(Pop(talon));
            for (int player = 0; player < 2; player++)
                if (!hands[player].Any(card => card.Rank == 11 || card.Rank == 12 || card.Rank == 13))
                    dealPoints[player] += 10;
            phase = "elder_exchange";
            CurrentPlayer = elder;
            repique = false;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "elder_exchange") return ExchangeActions(hands[actual], 1, Math.Min(5, talon.Count));
            if (phase == "younger_exchange") return ExchangeActions(hands[actual], 1, talon.Count);
            IEnumerable<Card> cards = hands[actual];
            if (trick.Count > 0)
            {
                Suit led = trick[0].Item2.Suit;
                Card[] follow = cards.Where(card => card.Suit == led).ToArray();
                if (follow.Length > 0) cards = follow;
            }
            return cards.Select(card => new Action("play", card)).ToArray();
        }

        private static IReadOnlyList<Action> ExchangeActions(IReadOnlyList<Card> hand, int minimum, int maximum)
        {
            var actions = new List<Action>();
            int possibilities = 1 << hand.Count;
            for (int mask = 0; mask < possibilities; mask++)
            {
                int count = CountBits(mask);
                if (count < minimum || count > maximum) continue;
                Card[] selected = Enumerable.Range(0, hand.Count).Where(index => (mask & (1 << index)) != 0)
                    .Select(index => hand[index]).ToArray();
                actions.Add(new Action("exchange", value: Encode(selected)));
            }
            return actions;
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null);
            Guard.Legal(action, LegalActions(player));
            TurnCount++;
            if (phase == "elder_exchange" || phase == "younger_exchange")
            {
                Card[] discards = Decode(action.Value!);
                foreach (Card card in discards) hands[player].Remove(card);
                for (int index = 0; index < discards.Length; index++) hands[player].Add(Pop(talon));
                if (phase == "elder_exchange") { phase = "younger_exchange"; CurrentPlayer = dealer; }
                else BeginDeclarations();
                return;
            }
            Card played = action.Card!.Value;
            hands[player].Remove(played);
            trick.Add(Tuple.Create(player, played));
            if (trick.Count == 1) { CurrentPlayer = 1 - player; return; }
            int winner = trick[1].Item2.Suit == trick[0].Item2.Suit &&
                Strength(trick[1].Item2) > Strength(trick[0].Item2) ? trick[1].Item1 : trick[0].Item1;
            dealPoints[winner] += winner == currentLeader ? 1 : 2;
            tricks[winner]++;
            trick.Clear();
            currentLeader = winner;
            if (hands[0].Count == 0) FinishDeal();
            else CurrentPlayer = winner;
        }

        private void BeginDeclarations()
        {
            ResolvePoint();
            ResolveSequences();
            ResolveSets();
            int declarer = dealPoints[0] >= 30 && dealPoints[1] == 0 ? 0 :
                dealPoints[1] >= 30 && dealPoints[0] == 0 ? 1 : -1;
            if (declarer >= 0) { dealPoints[declarer] += 60; repique = true; }
            phase = "play";
            currentLeader = elder;
            CurrentPlayer = elder;
            dealPoints[elder]++;
        }

        private void ResolvePoint()
        {
            Tuple<int, int>[] values = hands.Select(hand => hand.GroupBy(card => card.Suit)
                .Select(group => Tuple.Create(group.Count(), group.Sum(card => PipValue(card))))
                .OrderByDescending(value => value.Item1).ThenByDescending(value => value.Item2).First()).ToArray();
            int winner = Compare(values[0], values[1]);
            if (winner >= 0) dealPoints[winner] += values[winner].Item1;
        }

        private void ResolveSequences()
        {
            List<Tuple<int, int>>[] runs = hands.Select(AllRuns).ToArray();
            Tuple<int, int>? left = runs[0].OrderByDescending(run => run.Item1).ThenByDescending(run => run.Item2).FirstOrDefault();
            Tuple<int, int>? right = runs[1].OrderByDescending(run => run.Item1).ThenByDescending(run => run.Item2).FirstOrDefault();
            int winner = CompareNullable(left, right);
            if (winner >= 0) dealPoints[winner] += runs[winner].Sum(run => SequenceScore(run.Item1));
        }

        private void ResolveSets()
        {
            List<Tuple<int, int>>[] sets = hands.Select(hand => hand.Where(card => card.Rank == 1 || card.Rank >= 10)
                .GroupBy(card => Strength(card)).Where(group => group.Count() >= 3)
                .Select(group => Tuple.Create(group.Count(), group.Key)).ToList()).ToArray();
            Tuple<int, int>? left = sets[0].OrderByDescending(set => set.Item1).ThenByDescending(set => set.Item2).FirstOrDefault();
            Tuple<int, int>? right = sets[1].OrderByDescending(set => set.Item1).ThenByDescending(set => set.Item2).FirstOrDefault();
            int winner = CompareNullable(left, right);
            if (winner >= 0) dealPoints[winner] += sets[winner].Sum(set => set.Item1 == 4 ? 14 : 3);
        }

        private static List<Tuple<int, int>> AllRuns(List<Card> hand)
        {
            var result = new List<Tuple<int, int>>();
            foreach (IGrouping<Suit, Card> suit in hand.GroupBy(card => card.Suit))
            {
                int[] ranks = suit.Select(card => Strength(card)).OrderBy(value => value).ToArray();
                int start = 0;
                while (start < ranks.Length)
                {
                    int end = start;
                    while (end + 1 < ranks.Length && ranks[end + 1] == ranks[end] + 1) end++;
                    int length = end - start + 1;
                    if (length >= 3) result.Add(Tuple.Create(length, ranks[end]));
                    start = end + 1;
                }
            }
            return result;
        }

        private void FinishDeal()
        {
            int trickWinner = tricks[0] > tricks[1] ? 0 : 1;
            dealPoints[trickWinner] += tricks[trickWinner] == 12 ? 40 : tricks[trickWinner] >= 7 ? 10 : 0;
            if (!repique)
            {
                int pique = dealPoints[0] >= 30 && dealPoints[1] == 0 ? 0 :
                    dealPoints[1] >= 30 && dealPoints[0] == 0 ? 1 : -1;
                if (pique >= 0) dealPoints[pique] += 30;
            }
            totalPoints[0] += dealPoints[0]; totalPoints[1] += dealPoints[1];
            dealsPlayed++;
            if (dealsPlayed >= dealLimit)
            {
                if (totalPoints[0] == totalPoints[1])
                {
                    if (dealLimit == 6) { dealLimit = 8; StartDeal(); return; }
                    finalScores[0] = totalPoints[0];
                    finalScores[1] = totalPoints[1];
                    finished = true;
                    return;
                }
                int winner = totalPoints[0] > totalPoints[1] ? 0 : 1, loser = 1 - winner;
                finalScores[winner] = totalPoints[loser] >= 100
                    ? totalPoints[winner] - totalPoints[loser] + 100
                    : totalPoints[winner] + totalPoints[loser] + 100;
                finished = true;
            }
            else StartDeal();
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "elder_exchange" || phase == "younger_exchange")
                return actions.OrderBy(action => Decode(action.Value!).Length)
                    .ThenBy(action => Decode(action.Value!).Sum(PipValue)).First();
            return actions.OrderByDescending(action => Strength(action.Card!.Value)).First();
        }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int[] winners = finalScores[0] == finalScores[1] ? new[] { 0, 1 } :
                new[] { finalScores[0] > finalScores[1] ? 0 : 1 };
            return new GameResult(winners, finalScores,
                winners.Length == 2 ? "eight-deal Piquet draw" : "six-deal Piquet settlement", TurnCount,
                new Dictionary<string, object> { { "raw_points", totalPoints.ToArray() } });
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            return $"phase={phase} deal={dealsPlayed + 1}/{dealLimit} elder=P{elder} talon={talon.Count} " +
                $"trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] " +
                $"deal_points=[{string.Join(",", dealPoints)}] totals=[{string.Join(",", totalPoints)}] tricks=[{string.Join(",", tricks)}] " +
                $"hand_counts=[{hands[0].Count},{hands[1].Count}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }

        private static int Compare(Tuple<int, int> left, Tuple<int, int> right) =>
            left.Item1 != right.Item1 ? (left.Item1 > right.Item1 ? 0 : 1) :
            left.Item2 != right.Item2 ? (left.Item2 > right.Item2 ? 0 : 1) : -1;
        private static int CompareNullable(Tuple<int, int>? left, Tuple<int, int>? right)
        {
            if (left == null && right == null) return -1;
            if (left == null) return 1;
            if (right == null) return 0;
            return Compare(left, right);
        }
        private static int SequenceScore(int length) => length >= 5 ? length + 10 : length;
        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static int PipValue(Card card) => card.Rank == 1 ? 11 : Math.Min(card.Rank, 10);
        private static int CountBits(int value) { int count = 0; while (value != 0) { count += value & 1; value >>= 1; } return count; }
        private static string Encode(IEnumerable<Card> cards) => string.Join(",", cards.OrderBy(card => card));
        private static Card[] Decode(string value) => string.IsNullOrEmpty(value) ? Array.Empty<Card>() : value.Split(',').Select(Card.Parse).ToArray();
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("piquet", "ピケ", 2, 2, "declaration trick-taking",
                "32枚で各12枚。elderが1～5枚、youngerが残りtalonまで交換し、point・sequence・setを宣言後、切り札なし12トリックを6ディール行う。",
                "Gokurakism Piquet"),
            (players, random, options) => new PiquetGame(players, random));
    }

    public sealed class KlaberjassGame : GameBase
    {
        private readonly DeterministicRandom rng;
        private readonly int targetScore;
        private readonly List<List<Card>> hands = new List<List<Card>>
        {
            new List<Card>(), new List<Card>()
        };
        private readonly List<Card> stock = new List<Card>();
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly int[] dealPoints = new int[2];
        private readonly int[] scores = new int[2];
        private readonly int[] tricks = new int[2];
        private readonly MeldRun?[] meldBest = new MeldRun?[2];
        private readonly List<MeldRun>[] revealedMelds = new List<MeldRun>[]
        {
            new List<MeldRun>(), new List<MeldRun>()
        };
        private readonly bool[] bellaEligible = new bool[2];
        private Card upCard;
        private Card? exposedBottom;
        private Suit? trump;
        private int dealer = 1;
        private int elder;
        private int maker;
        private int bidStep;
        private int meldStep;
        private int meldWinner;
        private string? elderMeldHigh;
        private string? elderMeldTrump;
        private string? dealerMeldReply;
        private bool meldResolved;
        private string phase = "bid";
        private bool finished;

        private readonly struct MeldRun
        {
            public int Length { get; }
            public int HighStrength { get; }
            public Card HighCard { get; }
            public bool IsTrump { get; }
            public int Points => Length == 3 ? 20 : 50;

            public MeldRun(int length, int highStrength, Card highCard, bool isTrump)
            {
                Length = length;
                HighStrength = highStrength;
                HighCard = highCard;
                IsTrump = isTrump;
            }
        }

        public override string GameId => "klaberjass";
        public override string Name => "クラバヤス";

        public KlaberjassGame(int players, DeterministicRandom rng,
            IReadOnlyDictionary<string, string> options)
        {
            Players = 2;
            this.rng = rng;
            targetScore = Math.Max(1, options.Integer("target_score", 500));
            StartDeal();
        }

        private void StartDeal()
        {
            hands[0].Clear(); hands[1].Clear(); stock.Clear(); trick.Clear();
            Array.Clear(dealPoints, 0, 2); Array.Clear(tricks, 0, 2);
            meldBest[0] = null; meldBest[1] = null;
            revealedMelds[0].Clear(); revealedMelds[1].Clear();
            bellaEligible[0] = false; bellaEligible[1] = false;
            stock.AddRange(Cards.Shuffled(Cards.StandardDeck(new[] { 1, 7, 8, 9, 10, 11, 12, 13 }), rng));
            dealer = 1 - dealer; elder = 1 - dealer;
            for (int packet = 0; packet < 2; packet++)
                for (int offset = 1; offset <= 2; offset++)
                    for (int card = 0; card < 3; card++) hands[(dealer + offset) % 2].Add(Pop(stock));
            upCard = Pop(stock);
            exposedBottom = null;
            trump = null; maker = -1; bidStep = 0; meldStep = 0; meldWinner = -1;
            elderMeldHigh = null; elderMeldTrump = null; dealerMeldReply = null; meldResolved = false;
            phase = "bid"; CurrentPlayer = elder;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "bid")
            {
                if (bidStep < 2) return new[] { new Action("take"), new Action("pass") };
                var actions = Enum.GetValues(typeof(Suit)).Cast<Suit>()
                    .Select(suit => new Action("choose_trump", value: Card.SuitCode(suit))).ToList();
                actions.Add(new Action("pass"));
                return actions;
            }
            if (phase == "meld")
            {
                return MeldActions(actual);
            }
            IEnumerable<Card> cards = hands[actual];
            if (trick.Count > 0) cards = FollowCards(actual, trick[0].Item2);
            var plays = cards.Select(card => new Action("play", card)).ToList();
            foreach (Card card in cards.Where(card => CanDeclareBella(actual, card)))
                plays.Add(new Action("play_bella", card));
            return plays;
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null);
            Guard.Legal(action, LegalActions(player));
            TurnCount++;
            if (phase == "bid") { ApplyBid(action, player); return; }
            if (phase == "meld")
            {
                ApplyMeld(action, player);
                return;
            }
            Card card = action.Card!.Value;
            hands[player].Remove(card);
            if (action.Kind == "play_bella") dealPoints[player] += 20;
            trick.Add(Tuple.Create(player, card));
            if (trick.Count == 1) { CurrentPlayer = 1 - player; return; }
            int winner = TrickWinner();
            dealPoints[winner] += trick.Sum(item => CardPoint(item.Item2));
            tricks[winner]++;
            trick.Clear();
            if (hands[0].Count == 0)
            {
                dealPoints[winner] += 10;
                FinishDeal();
            }
            else CurrentPlayer = winner;
        }

        private void ApplyBid(Action action, int player)
        {
            if (action.Kind == "take") { trump = upCard.Suit; maker = player; CompleteDeal(); return; }
            if (action.Kind == "choose_trump") { trump = Card.ParseSuit(action.Value!); maker = player; CompleteDeal(); return; }
            bidStep++;
            if (bidStep == 1 || bidStep == 3) CurrentPlayer = dealer;
            else if (bidStep == 2) CurrentPlayer = elder;
            else StartDeal();
        }

        private void CompleteDeal()
        {
            for (int round = 0; round < 3; round++)
                for (int offset = 1; offset <= 2; offset++) hands[(dealer + offset) % 2].Add(Pop(stock));
            exposedBottom = stock[0];
            for (int player = 0; player < 2; player++) UpdateBella(player);
            phase = "meld";
            meldStep = 0;
            CurrentPlayer = elder;
        }

        private IReadOnlyList<Action> MeldActions(int player)
        {
            if (meldStep == 0 || (meldStep == 1 && !meldBest[elder].HasValue))
            {
                var actions = new List<Action>();
                if (CanExchangeSeven(player)) actions.Add(new Action("exchange_seven"));
                MeldRun? best = Best(AllRuns(hands[player]));
                if (best.HasValue)
                    actions.Add(new Action("declare_meld", value: best.Value.Points.ToString(CultureInfo.InvariantCulture)));
                actions.Add(new Action("skip_meld"));
                return actions;
            }
            if (meldStep == 1) return MeldReplyActions(player, 0);
            if (meldStep == 2)
                return new[] { new Action("declare_meld_high", value: RankLabel(meldBest[elder]!.Value.HighCard.Rank)) };
            if (meldStep == 3) return MeldReplyActions(player, 1);
            if (meldStep == 4)
                return new[] { new Action("declare_meld_trump", value: meldBest[elder]!.Value.IsTrump ? "trump" : "plain") };
            if (meldStep == 5) return MeldReplyActions(player, 2);
            throw new InvalidOperationException("Invalid Klaberjass meld step.");
        }

        private IReadOnlyList<Action> MeldReplyActions(int player, int component)
        {
            var actions = new List<Action> { new Action("meld_reply", value: "lose") };
            MeldRun? best = Best(AllRuns(hands[player]));
            if (best.HasValue)
            {
                string reply = CompareMeldComponent(best.Value, meldBest[elder]!.Value, component);
                if (reply != "lose") actions.Add(new Action("meld_reply", value: reply));
            }
            return actions;
        }

        private void ApplyMeld(Action action, int player)
        {
            if (action.Kind == "exchange_seven")
            {
                Card seven = new Card(trump!.Value, 7);
                hands[player].Remove(seven); hands[player].Add(upCard); upCard = seven;
                UpdateBella(player);
                return;
            }
            if (meldStep == 0 || (meldStep == 1 && !meldBest[elder].HasValue))
            {
                ApplyMeldDeclaration(action, player);
                return;
            }
            if (meldStep == 1) { ApplyMeldReply(action, player, 0); return; }
            if (meldStep == 2)
            {
                elderMeldHigh = action.Value;
                meldStep = 3;
                CurrentPlayer = dealer;
                return;
            }
            if (meldStep == 3) { ApplyMeldReply(action, player, 1); return; }
            if (meldStep == 4)
            {
                elderMeldTrump = action.Value;
                meldStep = 5;
                CurrentPlayer = dealer;
                return;
            }
            if (meldStep == 5) { ApplyMeldReply(action, player, 2); return; }
            throw new InvalidOperationException("Invalid Klaberjass meld step.");
        }

        private void ApplyMeldDeclaration(Action action, int player)
        {
            if (action.Kind == "declare_meld") meldBest[player] = Best(AllRuns(hands[player]));
            if (player == elder)
            {
                meldStep = 1;
                CurrentPlayer = dealer;
                return;
            }
            FinishMeld(meldBest[player].HasValue ? player : -1);
        }

        private void ApplyMeldReply(Action action, int player, int component)
        {
            dealerMeldReply = action.Value;
            if (action.Value == "lose")
            {
                FinishMeld(elder);
                return;
            }
            meldBest[player] = Best(AllRuns(hands[player]));
            if (action.Value == "win")
            {
                FinishMeld(player);
                return;
            }
            if (component == 0)
            {
                meldStep = 2;
                CurrentPlayer = elder;
                return;
            }
            if (component == 1)
            {
                meldStep = 4;
                CurrentPlayer = elder;
                return;
            }
            FinishMeld(-1);
        }

        private void FinishMeld(int winner)
        {
            meldWinner = winner;
            meldResolved = true;
            if (winner >= 0)
            {
                List<MeldRun> runs = AllRuns(hands[winner]);
                revealedMelds[winner].AddRange(runs);
                dealPoints[winner] += runs.Sum(run => run.Points);
            }
            phase = "play";
            CurrentPlayer = elder;
        }

        private List<MeldRun> AllRuns(List<Card> hand)
        {
            var result = new List<MeldRun>();
            foreach (IGrouping<Suit, Card> group in hand.GroupBy(card => card.Suit))
            {
                int[] strengths = group.Select(SequenceStrength).OrderBy(value => value).ToArray();
                int start = 0;
                while (start < strengths.Length)
                {
                    int end = start;
                    while (end + 1 < strengths.Length && strengths[end + 1] == strengths[end] + 1) end++;
                    int length = end - start + 1;
                    if (length >= 3)
                    {
                        Card highCard = group.Single(card => SequenceStrength(card) == strengths[end]);
                        result.Add(new MeldRun(length, strengths[end], highCard, group.Key == trump));
                    }
                    start = end + 1;
                }
            }
            return result;
        }

        private static MeldRun? Best(IEnumerable<MeldRun> runs) => runs
            .OrderByDescending(run => run.Points).ThenByDescending(run => run.HighStrength)
            .ThenByDescending(run => run.IsTrump).FirstOrDefault();

        private static string CompareMeldComponent(MeldRun challenger, MeldRun declaration, int component)
        {
            int compared = component == 0 ? challenger.Points.CompareTo(declaration.Points) :
                component == 1 ? challenger.HighStrength.CompareTo(declaration.HighStrength) :
                challenger.IsTrump.CompareTo(declaration.IsTrump);
            return compared > 0 ? "win" : compared < 0 ? "lose" : "tie";
        }

        private IEnumerable<Card> FollowCards(int player, Card led)
        {
            List<Card> hand = hands[player];
            if (led.Suit == trump)
            {
                Card[] trumps = hand.Where(card => card.Suit == trump).ToArray();
                Card[] higher = trumps.Where(card => TrumpStrength(card) > TrumpStrength(led)).ToArray();
                return higher.Length > 0 ? higher : trumps.Length > 0 ? trumps : hand;
            }
            Card[] follow = hand.Where(card => card.Suit == led.Suit).ToArray();
            if (follow.Length > 0) return follow;
            Card[] trumpCards = hand.Where(card => card.Suit == trump).ToArray();
            return trumpCards.Length > 0 ? trumpCards : hand;
        }

        private int TrickWinner()
        {
            Card first = trick[0].Item2, second = trick[1].Item2;
            if (first.Suit == second.Suit)
            {
                int left = first.Suit == trump ? TrumpStrength(first) : PlainStrength(first);
                int right = second.Suit == trump ? TrumpStrength(second) : PlainStrength(second);
                return right > left ? trick[1].Item1 : trick[0].Item1;
            }
            return second.Suit == trump ? trick[1].Item1 : trick[0].Item1;
        }

        private bool CanExchangeSeven(int player) => upCard.Suit == trump && upCard.Rank != 7 &&
            hands[player].Contains(new Card(trump!.Value, 7));
        private void UpdateBella(int player) => bellaEligible[player] =
            hands[player].Contains(new Card(trump!.Value, 12)) &&
            hands[player].Contains(new Card(trump.Value, 13));
        private bool CanDeclareBella(int player, Card card) => bellaEligible[player] && card.Suit == trump &&
            (card.Rank == 12 || card.Rank == 13) && !hands[player].Contains(new Card(trump!.Value, card.Rank == 12 ? 13 : 12));

        private void FinishDeal()
        {
            int defender = 1 - maker;
            if (dealPoints[maker] > dealPoints[defender])
            { scores[maker] += dealPoints[maker]; scores[defender] += dealPoints[defender]; }
            else if (dealPoints[maker] == dealPoints[defender]) scores[defender] += dealPoints[defender];
            else scores[defender] += dealPoints[maker] + dealPoints[defender];
            if (scores.Max() >= targetScore) finished = true;
            else StartDeal();
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "bid")
            {
                if (bidStep < 2)
                {
                    int count = hands[player].Count(card => card.Suit == upCard.Suit);
                    return actions.First(action => action.Kind == (count >= 2 ? "take" : "pass"));
                }
                Suit best = hands[player].GroupBy(card => card.Suit).OrderByDescending(group => group.Count()).First().Key;
                return actions.First(action => action.Kind == "choose_trump" && action.Value == Card.SuitCode(best));
            }
            if (phase == "meld")
            {
                if (actions.Any(action => action.Kind == "exchange_seven")) return actions.First(action => action.Kind == "exchange_seven");
                foreach (Action action in actions)
                    if (action.Kind == "declare_meld" || action.Kind == "declare_meld_high" ||
                        action.Kind == "declare_meld_trump" ||
                        (action.Kind == "meld_reply" && action.Value != "lose")) return action;
                return actions[0];
            }
            if (actions.Any(action => action.Kind == "play_bella")) return actions.First(action => action.Kind == "play_bella");
            return actions.Where(action => action.Kind == "play")
                .OrderByDescending(action => action.Card!.Value.Suit == trump ? TrumpStrength(action.Card.Value) : PlainStrength(action.Card.Value)).First();
        }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 2).Where(player => scores[player] == high),
                scores.Select(value => (double)value), "first to " + targetScore + " Klaberjass points", TurnCount);
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            return $"phase={phase} bid_step={bidStep} up={upCard} trump={(trump.HasValue ? Card.SuitCode(trump.Value) : "-")} " +
                $"bottom={(exposedBottom.HasValue ? exposedBottom.Value.ToString() : "-")} maker={(maker >= 0 ? "P" + maker : "-")} " +
                $"meld=[P0:{MeldClaimText(0)},P1:{MeldClaimText(1)}] meld_detail=P{elder}:{elderMeldHigh ?? "-"}/{elderMeldTrump ?? "-"} " +
                $"meld_reply={dealerMeldReply ?? "-"} meld_winner={(meldWinner >= 0 ? "P" + meldWinner : "-")} " +
                $"meld_resolved={meldResolved} meld_reveals=[{MeldRevealText()}] " +
                $"trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] " +
                $"deal_points=[{string.Join(",", dealPoints)}] scores=[{string.Join(",", scores)}] " +
                $"hand_counts=[{hands[0].Count},{hands[1].Count}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }

        private string MeldClaimText(int player)
        {
            if (player == elder)
            {
                if (meldStep == 0 && !meldResolved) return "-";
                return meldBest[player].HasValue ? meldBest[player]!.Value.Points.ToString(CultureInfo.InvariantCulture) : "none";
            }
            if (!meldBest[elder].HasValue)
            {
                if (!meldResolved) return "-";
                return meldBest[player].HasValue ? meldBest[player]!.Value.Points.ToString(CultureInfo.InvariantCulture) : "none";
            }
            return dealerMeldReply ?? "-";
        }

        private string MeldRevealText() => string.Join(",", Enumerable.Range(0, 2)
            .Where(player => revealedMelds[player].Count > 0)
            .Select(player => "P" + player + ":" + string.Join(" ", revealedMelds[player].Select(DescribeMeld))));

        private static string DescribeMeld(MeldRun run) => string.Join(" ", Enumerable
            .Range(run.HighStrength - run.Length + 1, run.Length)
            .Select(strength => new Card(run.HighCard.Suit, RankFromSequenceStrength(strength)).ToString()));

        private static int RankFromSequenceStrength(int strength)
        {
            switch (strength)
            {
                case 1: return 7;
                case 2: return 8;
                case 3: return 9;
                case 4: return 10;
                case 5: return 11;
                case 6: return 12;
                case 7: return 13;
                case 8: return 1;
                default: throw new ArgumentOutOfRangeException(nameof(strength));
            }
        }

        private static string RankLabel(int rank) => rank == 1 ? "A" : rank == 11 ? "J" :
            rank == 12 ? "Q" : rank == 13 ? "K" : rank.ToString(CultureInfo.InvariantCulture);

        private int CardPoint(Card card) => card.Suit == trump ? card.Rank == 11 ? 20 : card.Rank == 9 ? 14 :
            card.Rank == 1 ? 11 : card.Rank == 10 ? 10 : card.Rank == 13 ? 4 : card.Rank == 12 ? 3 : 0 :
            card.Rank == 1 ? 11 : card.Rank == 10 ? 10 : card.Rank == 13 ? 4 : card.Rank == 12 ? 3 : card.Rank == 11 ? 2 : 0;
        private static int SequenceStrength(Card card) => card.Rank == 1 ? 8 : card.Rank == 13 ? 7 :
            card.Rank == 12 ? 6 : card.Rank == 11 ? 5 : card.Rank == 10 ? 4 : card.Rank - 6;
        private static int PlainStrength(Card card) => card.Rank == 1 ? 8 : card.Rank == 10 ? 7 :
            card.Rank == 13 ? 6 : card.Rank == 12 ? 5 : card.Rank == 11 ? 4 : card.Rank - 6;
        private static int TrumpStrength(Card card) => card.Rank == 11 ? 8 : card.Rank == 9 ? 7 :
            card.Rank == 1 ? 6 : card.Rank == 10 ? 5 : card.Rank == 13 ? 4 : card.Rank == 12 ? 3 : card.Rank - 6;
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("klaberjass", "クラバヤス", 2, 2, "bid-meld trick-taking",
                "32枚で候補切り札のtakeまたは第2巡のスート指定を行い、9枚手札のsequence、bella、特殊切り札点を500点まで争う。",
                "Gokurakism Klaberjass", new Dictionary<string, string> { { "target_score", "500" } }),
            (players, random, options) => new KlaberjassGame(players, random, options));
    }
}
