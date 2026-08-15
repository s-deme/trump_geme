using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    internal static class VariablePlayerGames
    {
        public static void RegisterGames(GameRegistry registry)
        { YanivGame.Register(registry); PortlandGame.Register(registry); ToepenGame.Register(registry); }
    }

    public sealed class YanivGame : GameBase
    {
        private sealed class YCard
        {
            public Card? Card { get; }
            public int Copy { get; }
            public int Joker { get; }
            public string Id => Card.HasValue ? Card.Value + "#" + Copy : "JK" + Joker + "#" + Copy;
            public int Points => !Card.HasValue ? 0 : Math.Min(10, Card.Value.Rank);
            public YCard(Card? card, int copy, int joker = 0) { Card = card; Copy = copy; Joker = joker; }
            public override string ToString() => Id;
        }

        private readonly DeterministicRandom rng;
        private readonly List<List<YCard>> hands;
        private readonly List<YCard> stock = new List<YCard>();
        private readonly List<YCard> lastDiscard = new List<YCard>();
        private readonly List<YCard> drawOptions = new List<YCard>();
        private readonly List<YCard> waste = new List<YCard>();
        private readonly List<string> revealedHands = new List<string>();
        private readonly int[] scores;
        private int dealer;
        private int rounds;
        private int nextStarter = -1;
        private string phase = "discard";
        private bool finished;
        public override string GameId => "yaniv";
        public override string Name => "ヤニブ";
        public YanivGame(int players, DeterministicRandom rng)
        { Players = players; this.rng = rng; hands = Enumerable.Range(0, players).Select(_ => new List<YCard>()).ToList(); scores = new int[players]; dealer = players - 1; StartRound(); }

        private void StartRound()
        {
            foreach (List<YCard> hand in hands) hand.Clear(); stock.Clear(); lastDiscard.Clear(); drawOptions.Clear(); waste.Clear();
            int copies = Players >= 4 ? 2 : 1;
            for (int copy = 0; copy < copies; copy++)
            {
                stock.AddRange(Cards.StandardDeck().Select(card => new YCard(card, copy)));
                stock.Add(new YCard(null, copy, 1)); stock.Add(new YCard(null, copy, 2));
            }
            rng.Shuffle(stock); dealer = (dealer + 1) % Players;
            for (int round = 0; round < 5; round++) for (int offset = 1; offset <= Players; offset++) hands[(dealer + offset) % Players].Add(Pop(stock));
            lastDiscard.Add(Pop(stock)); phase = "discard";
            CurrentPlayer = nextStarter >= 0 ? nextStarter : (dealer + 1) % Players; nextStarter = -1;
        }
        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "draw")
            {
                var actions = new List<Action>();
                if (stock.Count > 0) actions.Add(new Action("draw_stock"));
                else if (waste.Count > 0) actions.Add(new Action("draw_recycled"));
                if (drawOptions.Count > 0)
                {
                    actions.Add(ActionFor("draw_discard", drawOptions[0]));
                    if (drawOptions.Count > 1) actions.Add(ActionFor("draw_discard", drawOptions[drawOptions.Count - 1]));
                }
                return actions.Distinct().ToArray();
            }
            var result = new List<Action>();
            if (HandPoints(hands[actual]) <= 5) result.Add(new Action("declare_yaniv"));
            foreach (YCard card in hands[actual]) result.Add(ComboAction("discard", new[] { card }));
            foreach (IGrouping<int, YCard> group in hands[actual].Where(card => card.Card.HasValue).GroupBy(card => card.Card!.Value.Rank))
            {
                YCard[] values = group.ToArray();
                for (int size = 2; size <= values.Length; size++) AddCombinations(result, values, size, 0, new List<YCard>());
            }
            AddRuns(result, hands[actual]);
            return result.GroupBy(action => action.Value).Select(group => group.First()).ToArray();
        }
        private static void AddCombinations(List<Action> result, YCard[] values, int size, int index, List<YCard> selected)
        {
            if (selected.Count == size) { AddSetOrders(result, selected); return; }
            for (int i = index; i <= values.Length - (size - selected.Count); i++)
            { selected.Add(values[i]); AddCombinations(result, values, size, i + 1, selected); selected.RemoveAt(selected.Count - 1); }
        }
        private static void AddSetOrders(List<Action> result, IReadOnlyList<YCard> cards)
        {
            if (cards.Count <= 2) { result.Add(ComboAction("discard", cards)); return; }
            for (int first = 0; first < cards.Count; first++)
                for (int last = 0; last < cards.Count; last++)
                {
                    if (first == last) continue;
                    var ordered = new List<YCard> { cards[first] };
                    ordered.AddRange(cards.Where((card, index) => index != first && index != last)); ordered.Add(cards[last]);
                    result.Add(ComboAction("discard", ordered));
                }
        }
        private static void AddRuns(List<Action> result, List<YCard> hand)
        {
            YCard[] jokers = hand.Where(card => !card.Card.HasValue).ToArray();
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                for (int start = 1; start <= 11; start++) for (int length = 3; start + length - 1 <= 13; length++)
                {
                    var run = new List<YCard>(); int jokerIndex = 0; bool valid = true;
                    for (int rank = start; rank < start + length; rank++)
                    {
                        YCard? natural = hand.FirstOrDefault(card => card.Card.HasValue && card.Card.Value.Suit == suit && card.Card.Value.Rank == rank);
                        if (natural != null) run.Add(natural); else if (jokerIndex < jokers.Length) run.Add(jokers[jokerIndex++]); else { valid = false; break; }
                    }
                    if (valid) result.Add(ComboAction("discard_run", run));
                }
            }
        }
        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (action.Kind == "declare_yaniv") { ScoreRound(player); return; }
            if (phase == "draw")
            {
                if (action.Kind == "draw_recycled") { stock.AddRange(waste); waste.Clear(); rng.Shuffle(stock); }
                YCard card = action.Kind == "draw_stock" || action.Kind == "draw_recycled" ? Pop(stock) : drawOptions.Single(item => item.Id == action.Value);
                if (action.Kind == "draw_discard") drawOptions.Remove(card); hands[player].Add(card);
                waste.AddRange(drawOptions); drawOptions.Clear();
                phase = "discard"; CurrentPlayer = (player + 1) % Players; return;
            }
            YCard[] discarded = action.Value!.Split(',').Select(id => hands[player].Single(card => card.Id == id)).ToArray();
            foreach (YCard card in discarded) hands[player].Remove(card);
            drawOptions.Clear(); drawOptions.AddRange(lastDiscard); lastDiscard.Clear(); lastDiscard.AddRange(discarded); phase = "draw";
        }
        private void ScoreRound(int caller)
        {
            int[] handPoints = hands.Select(HandPoints).ToArray(); int minimum = handPoints.Min(); bool success = handPoints[caller] == minimum && handPoints.Count(value => value == minimum) == 1;
            revealedHands.Clear(); revealedHands.AddRange(hands.Select((hand, player) => "P" + player + ":" + string.Join(" ", hand)));
            for (int player = 0; player < Players; player++)
            {
                int added = player == caller && success ? 0 : handPoints[player] + (player == caller ? 30 : 0); scores[player] += added;
                if (scores[player] == 50) scores[player] = 25; else if (scores[player] == 100) scores[player] = 50;
            }
            nextStarter = Enumerable.Range(0, Players).OrderBy(player => handPoints[player])
                .ThenBy(player => (player - caller - 1 + Players) % Players).First();
            rounds++; if (scores.Any(score => score >= 101)) finished = true; else StartRound();
        }
        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player); Action yaniv = actions.FirstOrDefault(action => action.Kind == "declare_yaniv"); if (yaniv.Kind != null) return yaniv;
            if (phase == "draw")
            {
                return actions.First();
            }
            return actions.OrderByDescending(action => action.Value!.Split(',').Sum(id => hands[player].Single(card => card.Id == id).Points)).First();
        }
        private static int HandPoints(IEnumerable<YCard> cards) => cards.Sum(card => card.Points);
        private static Action ActionFor(string kind, YCard card) => new Action(kind, card.Card, value: card.Id);
        private static Action ComboAction(string kind, IEnumerable<YCard> cards)
        { YCard[] values = cards.ToArray(); return new Action(kind, values[0].Card, value: string.Join(",", values.Select(card => card.Id))); }
        private static YCard Pop(List<YCard> cards) { YCard card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over."); int low = scores.Min();
            return new GameResult(Enumerable.Range(0, Players).Where(player => scores[player] == low), scores.Select(value => -(double)value), "lowest Yaniv penalty after 101", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer; return $"phase={phase} round={rounds + 1} discard=[{string.Join(" ", lastDiscard)}] draw_options=[{string.Join(" ", drawOptions)}] stock={stock.Count} scores=[{string.Join(",", scores)}] revealed_hands=[{string.Join(" | ", revealedHands)}] " +
                $"hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}] your_points={HandPoints(hands[viewer])}\nyour hand: {string.Join(" ", hands[viewer])}";
        }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("yaniv", "ヤニブ", 2, 8, "draw-discard rummy", "各自5枚（4人以上は54枚組を2組）。単札、同rank組、同suit3枚以上の連番（Joker代用可）を捨て、山札または直前組の端から1枚引く。手札5点以下でYanivを宣言して全手札を公開し、失敗時+30、50/100ちょうどの減点を適用して101点到達時の最少失点を競う。山札枯渇時は直前組以外を再利用する。", "ゴクラキズム/ヤニブ・Pagat/Yaniv"),
            (players, random, options) => new YanivGame(players, random));
    }

    public sealed class PortlandGame : GameBase
    {
        private readonly DeterministicRandom rng;
        private readonly List<List<Card>> decks;
        private readonly List<List<Card>> hands;
        private readonly int[] scores;
        private readonly bool[] passed;
        private readonly int[] drawsThisRound;
        private Card? drawn;
        private int round;
        private int roundStarter;
        private string phase = "draw";
        private bool finished;
        public override string GameId => "portland";
        public override string Name => "ポートランド";
        public PortlandGame(int players, DeterministicRandom rng)
        {
            Players = players; this.rng = rng; decks = Enumerable.Range(0, players).Select(_ => Cards.Shuffled(Cards.StandardDeck(), rng)).ToList();
            hands = Enumerable.Range(0, players).Select(_ => new List<Card>()).ToList(); scores = new int[players]; passed = new bool[players]; drawsThisRound = new int[players]; StartRound();
        }
        private void StartRound()
        {
            round++; Array.Clear(passed, 0, Players); Array.Clear(drawsThisRound, 0, Players); drawn = null;
            for (int player = 0; player < Players; player++)
            {
                hands[player].Clear(); for (int i = 0; i < 5 && decks[player].Count > 0; i++) hands[player].Add(Pop(decks[player]));
                if (hands[player].Count < 5 || decks[player].Count == 0) passed[player] = true;
            }
            if (passed.All(value => value)) { ScoreRound(); return; }
            CurrentPlayer = Enumerable.Range(0, Players).Select(offset => (roundStarter + offset) % Players).First(player => !passed[player]); phase = "draw";
        }
        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "draw") return new[] { new Action("pass_round"), new Action("reveal_next") };
            var actions = new List<Action>();
            for (int index = 0; index < hands[actual].Count; index++) actions.Add(new Action("overwrite", drawn, target: index));
            return actions;
        }
        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "draw")
            {
                if (action.Kind == "pass_round") { passed[player] = true; AdvanceAfterTurn(player); return; }
                drawn = Pop(decks[player]); drawsThisRound[player]++; phase = "decide"; return;
            }
            if (action.Kind == "overwrite")
            {
                hands[player][action.Target!.Value] = drawn!.Value;
                if (decks[player].Count == 0) passed[player] = true;
            }
            drawn = null;
            if (decks[player].Count == 0) passed[player] = true;
            AdvanceAfterTurn(player);
        }
        private void AdvanceAfterTurn(int player)
        {
            if (passed.All(value => value)) { ScoreRound(); return; }
            int next = (player + 1) % Players; while (passed[next]) next = (next + 1) % Players;
            CurrentPlayer = next; phase = "draw";
        }
        private void ScoreRound()
        {
            PokerRank?[] ranks = hands.Select(hand => hand.Count == 5 ? PokerHandEvaluator.EvaluateFive(hand) : (PokerRank?)null).ToArray();
            int[] order = Enumerable.Range(0, Players).OrderByDescending(player => ranks[player], NullablePokerComparer.Instance)
                .ThenBy(player => (player - roundStarter + Players) % Players).ToArray();
            for (int place = 0; place < Players; place++) scores[order[place]] += (Players - place - 1) * round;
            roundStarter = order[0]; if (round >= 6) finished = true; else StartRound();
        }
        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "draw")
            {
                int reserve = (6 - round) * 5;
                return decks[player].Count > reserve && drawsThisRound[player] < 4 ? actions[1] : actions[0];
            }
            PokerRank current = PokerHandEvaluator.EvaluateFive(hands[player]);
            Action? best = null; PokerRank bestRank = current;
            foreach (Action action in actions.Where(action => action.Kind == "overwrite"))
            {
                Card[] candidate = hands[player].ToArray(); candidate[action.Target!.Value] = drawn!.Value; PokerRank rank = PokerHandEvaluator.EvaluateFive(candidate);
                if (rank.CompareTo(bestRank) > 0) { bestRank = rank; best = action; }
            }
            return best ?? actions[0];
        }
        private sealed class NullablePokerComparer : IComparer<PokerRank?>
        {
            public static readonly NullablePokerComparer Instance = new NullablePokerComparer();
            public int Compare(PokerRank? x, PokerRank? y) => x.HasValue ? y.HasValue ? x.Value.CompareTo(y.Value) : 1 : y.HasValue ? -1 : 0;
        }
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over."); int high = scores.Max();
            return new GameResult(Enumerable.Range(0, Players).Where(player => scores[player] == high), scores.Select(value => (double)value), "six Portland rounds", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer; string pending = drawn.HasValue ? drawn.Value.ToString() : "-";
            return $"phase={phase} round={round}/6 starter=P{roundStarter} drawn={pending} scores=[{string.Join(",", scores)}] passed=[{string.Join(",", passed.Select(value => value ? 1 : 0))}] " +
                $"tables=[{string.Join(" | ", hands.Select((hand, owner) => "P" + owner + ":" + string.Join(" ", hand)))}] deck_counts=[{string.Join(",", decks.Select(deck => deck.Count))}]\nyour five: {string.Join(" ", hands[viewer])}";
        }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("portland", "ポートランド", 2, 5, "poker push-your-luck", "各自が独立した52枚deckを全6roundで使う。各roundは5枚を公開し、1枚ずつめくって5枠の上書きを続けるかpassする。全員pass後にPoker役で順位を決め、(人数－順位)×round数を得る。5枚を用意できなければ最弱。", "gokurakism/Portland"),
            (players, random, options) => new PortlandGame(players, random));
    }

    public sealed class ToepenGame : GameBase
    {
        private readonly DeterministicRandom rng;
        private readonly List<List<Card>> hands;
        private readonly List<Card> stock = new List<Card>();
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly int[] losses;
        private readonly bool[] folded;
        private int dealer;
        private int exchangeCount;
        private int trickNumber;
        private int stake;
        private int lastKnocker = -1;
        private int pendingExchanger = -1;
        private bool pendingExchangeHonest;
        private int knockResponses;
        private int resumePlayer;
        private string phase = "exchange";
        private bool finished;
        public override string GameId => "toepen";
        public override string Name => "ツーペン";
        public ToepenGame(int players, DeterministicRandom rng)
        { Players = players; this.rng = rng; hands = Enumerable.Range(0, players).Select(_ => new List<Card>()).ToList(); losses = new int[players]; folded = new bool[players]; dealer = players - 1; StartDeal(); }
        private void StartDeal()
        {
            foreach (List<Card> hand in hands) hand.Clear(); trick.Clear(); stock.Clear(); Array.Clear(folded, 0, Players);
            stock.AddRange(Cards.Shuffled(Cards.StandardDeck(new[] { 1, 7, 8, 9, 10, 11, 12, 13 }), rng));
            for (int round = 0; round < 4; round++) for (int offset = 1; offset <= Players; offset++) hands[(dealer + offset) % Players].Add(Pop(stock));
            exchangeCount = 0; trickNumber = 0; stake = 1; lastKnocker = pendingExchanger = -1; phase = "exchange"; CurrentPlayer = (dealer + 1) % Players;
        }
        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "exchange") return stock.Count >= 4 ? new[] { new Action("keep_hand"), new Action("exchange_hand") } : new[] { new Action("keep_hand") };
            if (phase == "challenge") return new[] { new Action("accept_exchange"), new Action("challenge_exchange") };
            if (phase == "knock_response") return new[] { new Action("stay"), new Action("fold") };
            IEnumerable<Card> cards = hands[actual];
            if (trick.Count > 0) { Suit led = trick[0].Item2.Suit; Card[] follow = cards.Where(card => card.Suit == led).ToArray(); if (follow.Length > 0) cards = follow; }
            var actions = cards.Select(card => new Action("play", card)).ToList();
            if (actual != lastKnocker && stake < Math.Max(2, 10 - losses[actual])) actions.Add(new Action("knock"));
            return actions;
        }
        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "exchange")
            {
                exchangeCount++;
                if (action.Kind == "exchange_hand")
                {
                    pendingExchanger = player; pendingExchangeHonest = hands[player].All(card => card.Rank == 1 || card.Rank >= 11);
                    hands[player].Clear(); for (int i = 0; i < 4; i++) hands[player].Add(Pop(stock));
                    phase = "challenge"; CurrentPlayer = (player + 1) % Players; return;
                }
                AdvanceExchange(player); return;
            }
            if (phase == "challenge")
            {
                if (action.Kind == "challenge_exchange")
                {
                    losses[pendingExchangeHonest ? player : pendingExchanger]++;
                }
                AdvanceExchange(pendingExchanger); return;
            }
            if (phase == "knock_response")
            {
                if (action.Kind == "fold") { folded[player] = true; losses[player] += stake; }
                knockResponses--;
                if (ActivePlayers().Count() <= 1) { FinishDeal(ActivePlayers().First()); return; }
                if (knockResponses <= 0) { stake++; phase = "play"; CurrentPlayer = folded[resumePlayer] ? NextActive(resumePlayer) : resumePlayer; }
                else CurrentPlayer = NextActive(player);
                return;
            }
            if (action.Kind == "knock")
            {
                lastKnocker = player; resumePlayer = player; knockResponses = ActivePlayers().Count() - 1; phase = "knock_response"; CurrentPlayer = NextActive(player); return;
            }
            Card card = action.Card!.Value; hands[player].Remove(card); trick.Add(Tuple.Create(player, card));
            if (trick.Count < ActivePlayers().Count()) { CurrentPlayer = NextActive(player); return; }
            Suit ledSuit = trick[0].Item2.Suit; int winner = trick.Where(item => item.Item2.Suit == ledSuit).OrderByDescending(item => Strength(item.Item2)).First().Item1;
            trick.Clear(); trickNumber++;
            if (trickNumber >= 4) FinishDeal(winner); else CurrentPlayer = winner;
        }
        private void AdvanceExchange(int previous)
        {
            if (exchangeCount >= Players) { phase = "play"; CurrentPlayer = (dealer + 1) % Players; }
            else { phase = "exchange"; CurrentPlayer = (previous + 1) % Players; }
        }
        private void FinishDeal(int winner)
        {
            foreach (int player in ActivePlayers().Where(player => player != winner)) losses[player] += stake;
            dealer = winner; if (losses.Any(loss => loss >= 10)) finished = true; else StartDeal();
        }
        private IEnumerable<int> ActivePlayers() => Enumerable.Range(0, Players).Where(player => !folded[player]);
        private int NextActive(int player) { int next = (player + 1) % Players; while (folded[next]) next = (next + 1) % Players; return next; }
        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "exchange") return hands[player].All(card => card.Rank == 1 || card.Rank >= 11) && actions.Count > 1 ? actions[1] : actions[0];
            if (phase == "challenge") return actions[0];
            if (phase == "knock_response") return losses[player] + stake >= 10 ? actions[1] : actions[0];
            return actions.Where(action => action.Kind == "play").OrderBy(action => Strength(action.Card!.Value)).First();
        }
        private static int Strength(Card card) => card.Rank == 10 ? 8 : card.Rank == 9 ? 7 : card.Rank == 8 ? 6 : card.Rank == 7 ? 5 : card.Rank == 1 ? 4 : card.Rank == 13 ? 3 : card.Rank == 12 ? 2 : 1;
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over."); int low = losses.Min();
            return new GameResult(Enumerable.Range(0, Players).Where(player => losses[player] == low), losses.Select(value => -(double)value), "Toepen: first player reaches ten losses", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer; return $"phase={phase} dealer=P{dealer} trick={trickNumber + 1}/4 stake={stake} losses=[{string.Join(",", losses)}] " +
                $"folded=[{string.Join(",", folded.Select(value => value ? 1 : 0))}] table=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("toepen", "ツーペン", 2, 8, "short trick and knock", "7～Aの32枚から4枚。希望者は4枚交換（challenge可）後、10>9>8>7>A>K>Q>Jのno-trump・must-followを4trick行う。knockごとに継続stakeを上げ、foldは現在stake、最終trick敗者は確定stakeを失い、10失点者が出るまで続ける（口笛・起立は表示演出外）。", "gokurakism/Toepen"),
            (players, random, options) => new ToepenGame(players, random));
    }
}
