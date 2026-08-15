using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    /// <summary>
    /// Five-player Bao Huang using the 168-card Shandong/Rizhao rules documented in
    /// docs/rules/baohuang.md.  Seat numbers advance anticlockwise.
    /// </summary>
    public sealed class BaohuangGame : GameBase
    {
        private sealed class BCard
        {
            public Card? Card { get; }
            public int Copy { get; }
            public int JokerKind { get; }
            public bool Marked { get; }
            public bool Joker => JokerKind > 0;
            public string Id => Joker
                ? (JokerKind == 2 ? "BIG" : "SMALL") + (Marked ? "*" : "") + "#" + Copy
                : Card!.Value + "#" + Copy;

            public BCard(Card? card, int copy, int jokerKind = 0, bool marked = false)
            {
                Card = card;
                Copy = copy;
                JokerKind = jokerKind;
                Marked = marked;
            }

            public override string ToString() => Id;
        }

        private readonly DeterministicRandom rng;
        private readonly List<List<BCard>> hands = Enumerable.Range(0, 5)
            .Select(_ => new List<BCard>()).ToList();
        private readonly int[] scores = new int[5];
        private readonly List<int> finishOrder = new List<int>();
        private readonly List<int> previousCombo = new List<int>();
        private readonly int[] publicAllegiance = new int[5];
        private readonly int sessionDeals;
        private readonly bool sixesLast;

        private int dealsPlayed;
        private int firstTaker;
        private int emperor = -1;
        private int guard = -1;
        private int originalEmperor = -1;
        private int emperorPasses;
        private int declarationsRemaining;
        private int lastPlayer = -1;
        private int passes;

        private int previousEmperor = -1;
        private int previousGuard = -1;
        private int previousOutcome;
        private bool previousSolo;

        private bool solo;
        private bool revealedSolo;
        private bool rebellion;
        private bool scoreDoubled;
        private bool guardRevealed;
        private string phase = "emperor_choice";
        private bool finished;

        public override string GameId => "baohuang";
        public override string Name => "保皇";
        public int SessionDeals => sessionDeals;
        public bool SixesLast => sixesLast;
        public static int DeckSize => 168;

        public BaohuangGame(int players, DeterministicRandom rng,
            IReadOnlyDictionary<string, string> options)
        {
            Players = 5;
            this.rng = rng;
            sessionDeals = options.Integer("deals", 1);
            if (sessionDeals < 1 || sessionDeals > 100)
                throw new ArgumentOutOfRangeException("deals", "deals must be in 1..100.");
            sixesLast = options.Boolean("sixes_last", false);
            StartDeal();
        }

        public static IReadOnlyDictionary<string, int> DeckComposition()
        {
            var result = new Dictionary<string, int>();
            foreach (int rank in new[] { 1, 2, 6, 7, 8, 9, 10, 11, 12, 13 })
                result[RankLabel(rank)] = 16;
            result["small_joker"] = 4;
            result["big_joker"] = 4;
            result["marked_small_joker"] = 1;
            result["marked_big_joker"] = 1;
            return result;
        }

        private void StartDeal()
        {
            foreach (List<BCard> hand in hands) hand.Clear();
            finishOrder.Clear();
            previousCombo.Clear();
            Array.Clear(publicAllegiance, 0, publicAllegiance.Length);

            var deck = new List<BCard>();
            int[] ranks = { 1, 2, 6, 7, 8, 9, 10, 11, 12, 13 };
            for (int copy = 0; copy < 4; copy++)
                deck.AddRange(Cards.StandardDeck(ranks).Select(card => new BCard(card, copy)));
            for (int copy = 0; copy < 4; copy++)
            {
                deck.Add(new BCard(null, copy, 1, copy == 0));
                deck.Add(new BCard(null, copy, 2, copy == 0));
            }

            rng.Shuffle(deck);
            firstTaker = (dealsPlayed + 1) % 5;
            int seat = firstTaker;
            while (deck.Count > 0)
            {
                hands[seat].Add(Pop(deck));
                seat = NextSeat(seat);
            }

            emperor = guard = originalEmperor = -1;
            emperorPasses = declarationsRemaining = 0;
            solo = revealedSolo = rebellion = scoreDoubled = guardRevealed = false;
            lastPlayer = -1;
            passes = 0;

            if (dealsPlayed > 0 && previousOutcome != 0)
            {
                phase = "tribute";
                CurrentPlayer = previousEmperor;
            }
            else PrepareEmperorSelection();
        }

        private void PrepareEmperorSelection()
        {
            emperor = originalEmperor = FindHolder(2, true);
            guard = FindHolder(1, true);
            emperorPasses = 0;
            publicAllegiance[emperor] = 1;
            phase = "emperor_choice";
            CurrentPlayer = emperor;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            switch (phase)
            {
                case "tribute":
                    return new[] { new Action("resolve_tribute") };
                case "emperor_choice":
                    return emperorPasses >= 5
                        ? new[] { new Action("accept_emperor") }
                        : new[] { new Action("accept_emperor"), new Action("pass_emperor") };
                case "solo_declaration":
                    return new[] { new Action("remain_hidden"), new Action("declare_solo") };
                case "allegiance":
                    return new[] { new Action("remain_hidden"), new Action("declare_allegiance") };
            }

            List<Action> plays = ComboActions(hands[actual]);
            if (sixesLast) plays = plays.Where(action => SixesLastLegal(
                hands[actual], ParseCards(hands[actual], action.Value!))).ToList();
            if (previousCombo.Count > 0)
            {
                plays = plays.Where(action => StrengthsBeat(
                    ParseCards(hands[actual], action.Value!).Select(Strength), previousCombo)).ToList();
                plays.Add(new Action("pass"));
            }
            return plays;
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null);
            Guard.Legal(action, LegalActions(player));
            TurnCount++;

            if (phase == "tribute")
            {
                ResolveTribute();
                PrepareEmperorSelection();
                return;
            }
            if (phase == "emperor_choice")
            {
                ApplyEmperorChoice(player, action);
                return;
            }
            if (phase == "solo_declaration")
            {
                if (action.Kind == "declare_solo")
                {
                    revealedSolo = true;
                    scoreDoubled = true;
                    publicAllegiance[emperor] = 1;
                }
                BeginAllegianceDeclarations();
                return;
            }
            if (phase == "allegiance")
            {
                ApplyAllegiance(player, action);
                return;
            }
            if (action.Kind == "pass")
            {
                ApplyPass(player);
                return;
            }

            List<BCard> cards = ParseCards(hands[player], action.Value!);
            foreach (BCard card in cards) hands[player].Remove(card);
            if (cards.Any(card => card.JokerKind == 1 && card.Marked))
            {
                guardRevealed = true;
                publicAllegiance[guard] = 1;
            }

            previousCombo.Clear();
            previousCombo.AddRange(cards.Select(Strength).OrderBy(value => value));
            lastPlayer = player;
            passes = 0;
            if (hands[player].Count == 0) finishOrder.Add(player);

            if (ShouldFinishDeal())
            {
                FinishDeal();
                return;
            }
            CurrentPlayer = NextActive(player);
        }

        private void ApplyEmperorChoice(int player, Action action)
        {
            if (action.Kind == "pass_emperor")
            {
                int next = NextSeat(player);
                MoveMarkedBig(player, next);
                publicAllegiance[player] = 0;
                emperor = next;
                publicAllegiance[emperor] = 1;
                emperorPasses++;
                CurrentPlayer = emperor;
                return;
            }

            solo = emperor == guard;
            if (solo)
            {
                phase = "solo_declaration";
                CurrentPlayer = emperor;
            }
            else BeginAllegianceDeclarations();
        }

        private void BeginAllegianceDeclarations()
        {
            declarationsRemaining = 4;
            phase = "allegiance";
            CurrentPlayer = NextSeat(emperor);
        }

        private void ApplyAllegiance(int player, Action action)
        {
            if (action.Kind == "declare_allegiance")
            {
                scoreDoubled = true;
                if (player == guard && !solo)
                {
                    guardRevealed = true;
                    publicAllegiance[player] = 1;
                }
                else
                {
                    rebellion = true;
                    publicAllegiance[player] = -1;
                }
            }

            declarationsRemaining--;
            if (declarationsRemaining == 0) BeginPlay();
            else CurrentPlayer = NextSeat(player);
        }

        private void BeginPlay()
        {
            phase = "play";
            CurrentPlayer = emperor;
            previousCombo.Clear();
            lastPlayer = -1;
            passes = 0;
        }

        private void ApplyPass(int player)
        {
            passes++;
            int required = ActivePlayers().Count() - (hands[lastPlayer].Count > 0 ? 1 : 0);
            if (passes >= required)
            {
                previousCombo.Clear();
                passes = 0;
                CurrentPlayer = hands[lastPlayer].Count > 0 ? lastPlayer : NextActive(lastPlayer);
            }
            else CurrentPlayer = NextActive(player);
        }

        private void ResolveTribute()
        {
            if (previousSolo)
            {
                if (previousOutcome > 0)
                {
                    foreach (int player in Enumerable.Range(0, 5).Where(p => p != previousEmperor))
                        MoveTribute(player, previousEmperor, 1);
                }
                else
                {
                    foreach (int recipient in Enumerable.Range(0, 5).Where(p => p != previousEmperor))
                        MoveTribute(previousEmperor, recipient, 1);
                }
                return;
            }

            int[] people = Enumerable.Range(0, 5)
                .Where(player => player != previousEmperor && player != previousGuard).ToArray();
            if (previousOutcome > 0)
            {
                MoveTribute(people[0], previousEmperor, 1);
                MoveTribute(people[1], previousEmperor, 1);
                MoveTribute(people[2], previousGuard, 1);
            }
            else
            {
                MoveTribute(previousEmperor, people[0], 1);
                MoveTribute(previousEmperor, people[1], 1);
                MoveTribute(previousGuard, people[2], 1);
            }
        }

        private void MoveTribute(int from, int to, int count)
        {
            for (int index = 0; index < count; index++)
            {
                BCard card = hands[from].Where(item => !item.Joker)
                    .OrderByDescending(Strength).ThenBy(item => item.Id, StringComparer.Ordinal).First();
                hands[from].Remove(card);
                hands[to].Add(card);
            }
        }

        private static List<Action> ComboActions(List<BCard> hand)
        {
            var result = new List<Action>();
            List<BCard[]> jokerSelections = JokerSelections(hand.Where(card => card.Joker).ToArray());

            foreach (IGrouping<int, BCard> group in hand.Where(card => !card.Joker)
                .GroupBy(card => card.Card!.Value.Rank))
            {
                BCard[] baseCards = group.OrderBy(card => card.Id, StringComparer.Ordinal).ToArray();
                for (int count = 1; count <= baseCards.Length; count++)
                {
                    BCard[] ordinary = baseCards.Take(count).ToArray();
                    result.Add(ComboAction(ordinary));
                    foreach (BCard[] jokers in jokerSelections)
                        result.Add(ComboAction(ordinary.Concat(jokers)));
                }
            }
            foreach (BCard[] jokers in jokerSelections) result.Add(ComboAction(jokers));
            return result.GroupBy(action => action.Value).Select(group => group.First()).ToList();
        }

        private static List<BCard[]> JokerSelections(BCard[] jokers)
        {
            var selections = new Dictionary<string, BCard[]>();
            for (int mask = 1; mask < (1 << jokers.Length); mask++)
            {
                BCard[] chosen = Enumerable.Range(0, jokers.Length)
                    .Where(index => (mask & (1 << index)) != 0).Select(index => jokers[index])
                    .OrderBy(card => card.JokerKind).ThenBy(card => card.Marked ? 0 : 1)
                    .ThenBy(card => card.Copy).ToArray();
                string key = string.Join(":", chosen.Count(card => card.JokerKind == 1),
                    chosen.Count(card => card.JokerKind == 2),
                    chosen.Any(card => card.JokerKind == 1 && card.Marked),
                    chosen.Any(card => card.JokerKind == 2 && card.Marked));
                if (!selections.ContainsKey(key)) selections.Add(key, chosen);
            }
            return selections.Values.ToList();
        }

        private static bool SixesLastLegal(List<BCard> hand, List<BCard> cards)
        {
            if (!cards.Any(card => !card.Joker && card.Card!.Value.Rank == 6)) return true;
            return hand.All(card => !card.Joker && card.Card!.Value.Rank == 6)
                && cards.Count == hand.Count;
        }

        public static bool StrengthsBeat(IEnumerable<int> candidate, IEnumerable<int> previous)
        {
            int[] left = candidate.OrderBy(value => value).ToArray();
            int[] right = previous.OrderBy(value => value).ToArray();
            if (left.Length != right.Length) return false;
            for (int index = 0; index < left.Length; index++)
                if (left[index] <= right[index]) return false;
            return true;
        }

        public static IReadOnlyList<int> ScoreFinishOrder(IReadOnlyList<int> order,
            int emperor, int guard, bool doubled)
        {
            if (order.Count != 5 || order.Distinct().Count() != 5)
                throw new ArgumentException("A complete five-player finish order is required.", nameof(order));
            if (emperor < 0 || emperor >= 5 || guard < 0 || guard >= 5)
                throw new ArgumentOutOfRangeException(nameof(emperor));

            int multiplier = doubled ? 2 : 1;
            var delta = new int[5];
            if (emperor == guard)
            {
                int place = order.ToList().IndexOf(emperor);
                if (place == 0)
                {
                    delta[emperor] = 12 * multiplier;
                    foreach (int player in Enumerable.Range(0, 5).Where(p => p != emperor))
                        delta[player] = -3 * multiplier;
                }
                else if (place >= 2)
                {
                    delta[emperor] = -12 * multiplier;
                    foreach (int player in Enumerable.Range(0, 5).Where(p => p != emperor))
                        delta[player] = 3 * multiplier;
                }
                return delta;
            }

            int[] rankScore = { 2, 1, 0, -1, -2 };
            int empireTotal = rankScore[order.ToList().IndexOf(emperor)]
                + rankScore[order.ToList().IndexOf(guard)];
            int peopleTotal = -empireTotal;
            foreach (int player in Enumerable.Range(0, 5))
                delta[player] = (player == emperor ? empireTotal * 2
                    : player == guard ? empireTotal : peopleTotal) * multiplier;
            return delta;
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "tribute") return actions[0];
            if (phase == "emperor_choice") return actions.First(action => action.Kind == "accept_emperor");
            if (phase == "solo_declaration")
                return HandPower(hands[player]) >= 42
                    ? actions.First(action => action.Kind == "declare_solo") : actions[0];
            if (phase == "allegiance")
            {
                bool ownEmpireSide = player == guard;
                int threshold = ownEmpireSide ? 39 : 43;
                return HandPower(hands[player]) >= threshold
                    ? actions.First(action => action.Kind == "declare_allegiance") : actions[0];
            }

            Action pass = actions.FirstOrDefault(action => action.Kind == "pass");
            bool canPass = pass.Kind == "pass";
            if (canPass && lastPlayer >= 0 && IsKnownAlly(player, lastPlayer)) return pass;

            Action[] plays = actions.Where(action => action.Kind == "play_combo").ToArray();
            if (plays.Length == 0) return pass;
            Action[] finishing = plays.Where(action =>
                ParseCards(hands[player], action.Value!).Count == hands[player].Count).ToArray();
            if (finishing.Length > 0) return finishing.OrderBy(PlayCost).First();

            if (previousCombo.Count > 0)
                return plays.OrderBy(PlayCost).ThenBy(action => action.Value, StringComparer.Ordinal).First();

            Action[] ordinary = plays.Where(action =>
                ParseCards(hands[player], action.Value!).All(card => !card.Joker)).ToArray();
            Action[] candidates = ordinary.Length > 0 ? ordinary : plays;
            return candidates.OrderByDescending(action => ParseCards(hands[player], action.Value!).Count)
                .ThenBy(PlayCost).ThenBy(action => action.Value, StringComparer.Ordinal).First();

            int PlayCost(Action action)
            {
                List<BCard> cards = ParseCards(hands[player], action.Value!);
                return cards.Count(card => card.Joker) * 1000 + cards.Sum(Strength);
            }
        }

        private bool IsKnownAlly(int viewer, int target)
        {
            bool viewerEmpireSide = viewer == emperor || viewer == guard;
            if (viewerEmpireSide)
                return target == emperor || publicAllegiance[target] == 1;
            return publicAllegiance[target] == -1;
        }

        private static int HandPower(IEnumerable<BCard> hand) => hand.Sum(card =>
            card.Joker ? (card.JokerKind == 2 ? 8 : 6) : Math.Max(0, Strength(card) - 9));

        private bool ShouldFinishDeal()
        {
            if (solo)
            {
                if (finishOrder.Contains(emperor)) return true;
                int soloOpponentsFinished = finishOrder.Count(player => player != emperor);
                return revealedSolo ? soloOpponentsFinished >= 1 : soloOpponentsFinished >= 2;
            }

            int empireFinished = finishOrder.Count(player => player == emperor || player == guard);
            int peopleFinished = finishOrder.Count - empireFinished;
            return empireFinished == 2 || peopleFinished == 3;
        }

        private void FinishDeal()
        {
            CompleteFinishOrder();
            int[] delta = ScoreFinishOrder(finishOrder, emperor, guard, scoreDoubled).ToArray();
            for (int player = 0; player < 5; player++) scores[player] += delta[player];

            previousEmperor = emperor;
            previousGuard = guard;
            previousSolo = solo;
            previousOutcome = delta[emperor] == 0 ? 0 : delta[emperor] > 0 ? 1 : -1;
            dealsPlayed++;
            if (dealsPlayed >= sessionDeals)
            {
                finished = true;
                phase = "finished";
            }
            else StartDeal();
        }

        private void CompleteFinishOrder()
        {
            int cursor = finishOrder.Count == 0 ? emperor : finishOrder[finishOrder.Count - 1];
            while (finishOrder.Count < 5)
            {
                cursor = NextSeat(cursor);
                if (!finishOrder.Contains(cursor)) finishOrder.Add(cursor);
            }
        }

        private IEnumerable<int> ActivePlayers() => Enumerable.Range(0, 5)
            .Where(player => hands[player].Count > 0);

        private int NextActive(int player)
        {
            int next = NextSeat(player);
            while (hands[next].Count == 0) next = NextSeat(next);
            return next;
        }

        private int FindHolder(int jokerKind, bool marked) => Enumerable.Range(0, 5).Single(player =>
            hands[player].Any(card => card.JokerKind == jokerKind && card.Marked == marked));

        private void MoveMarkedBig(int from, int to)
        {
            BCard card = hands[from].Single(item => item.JokerKind == 2 && item.Marked);
            hands[from].Remove(card);
            hands[to].Add(card);
        }

        private static Action ComboAction(IEnumerable<BCard> source)
        {
            BCard[] cards = source.OrderBy(Strength).ThenBy(card => card.Id, StringComparer.Ordinal).ToArray();
            return new Action("play_combo", cards[0].Card,
                value: string.Join(",", cards.Select(card => card.Id)));
        }

        private static List<BCard> ParseCards(List<BCard> hand, string value) => value.Split(',')
            .Select(id => hand.Single(card => card.Id == id)).ToList();

        private static int Strength(BCard card)
        {
            if (card.JokerKind == 2) return 15;
            if (card.JokerKind == 1) return 14;
            int rank = card.Card!.Value.Rank;
            return rank == 2 ? 13 : rank == 1 ? 12 : rank == 13 ? 11
                : rank == 12 ? 10 : rank == 11 ? 9 : rank - 2;
        }

        private static string RankLabel(int rank) => rank == 1 ? "A" : rank == 11 ? "J"
            : rank == 12 ? "Q" : rank == 13 ? "K" : rank.ToString();

        private static int NextSeat(int player) => (player + 1) % 5;

        private static BCard Pop(List<BCard> cards)
        {
            BCard card = cards[cards.Count - 1];
            cards.RemoveAt(cards.Count - 1);
            return card;
        }

        public override bool IsTerminal => finished;

        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 5).Where(player => scores[player] == high),
                scores.Select(value => (double)value), "Baohuang session score", TurnCount,
                new Dictionary<string, object>
                {
                    ["deals"] = dealsPlayed,
                    ["variant"] = "Shandong/Rizhao 168-card",
                    ["sixes_last"] = sixesLast
                });
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            bool revealAll = finished;
            string yourRole = viewer == emperor ? "emperor" : viewer == guard ? "guard" : "civilian";
            string guardView = revealAll || guardRevealed || viewer == guard ? "P" + guard : "hidden";
            bool roleSolo = phase == "emperor_choice" ? emperor == guard : solo;
            string soloView = revealAll || revealedSolo || viewer == emperor || viewer == guard
                ? (roleSolo ? "yes" : "no") : "hidden";
            string[] allegiance = Enumerable.Range(0, 5).Select(p =>
                "P" + p + ":" + (p == emperor ? "emperor" : publicAllegiance[p] > 0 ? "royalist"
                    : publicAllegiance[p] < 0 ? "revolutionary" : "hidden")).ToArray();
            int shownDeal = finished ? dealsPlayed : dealsPlayed + 1;
            string emperorView = emperor < 0 ? "unselected" : "P" + emperor;

            return $"phase={phase} deal={shownDeal}/{sessionDeals} first_taker=P{firstTaker} " +
                $"emperor={emperorView} guard={guardView} your_role={yourRole} solo={soloView} " +
                $"rebellion={rebellion} multiplier={(scoreDoubled ? 2 : 1)} " +
                $"allegiances=[{string.Join(",", allegiance)}] lead=[{string.Join(",", previousCombo)}] " +
                $"last={(lastPlayer < 0 ? "-" : "P" + lastPlayer)} passes={passes} " +
                $"out=[{string.Join(",", finishOrder.Select(p => "P" + p))}] " +
                $"scores=[{string.Join(",", scores)}] hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\n" +
                $"your hand: {string.Join(" ", hands[viewer].OrderBy(Strength).ThenBy(card => card.Id, StringComparer.Ordinal))}";
        }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("baohuang", "保皇", 5, 5, "hidden-team multi-deck climbing",
                "山東・日照系の4組168枚。印付き大Jokerの皇帝と印付き小Jokerの警護官が秘密teamとなり、" +
                "同rank組＋Jokerを同枚数・全札上位で重ねるsoft-pass戦。順位2/1/0/-1/-2をteam合算し、" +
                "皇帝は2倍、陣営宣言時はdeal得点を2倍にする。次dealの上納を含む。",
                "Pagat/Bao Huang; JJ standard Bao Huang",
                new Dictionary<string, string> { { "deals", "1" }, { "sixes_last", "false" } }),
            (players, random, options) => new BaohuangGame(players, random, options));
    }
}
