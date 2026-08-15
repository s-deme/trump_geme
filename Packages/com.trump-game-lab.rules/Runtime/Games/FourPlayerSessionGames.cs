using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    internal static class FourPlayerSessionGames
    {
        public static void RegisterGames(GameRegistry registry)
        {
            TripleCrownGame.Register(registry);
            GuillotineGame.Register(registry);
            PassCutRunGame.Register(registry);
        }
    }

    public sealed class TripleCrownGame : GameBase
    {
        private readonly DeterministicRandom rng;
        private readonly int targetScore;
        private readonly int? sessionDeals;
        private readonly List<List<Card>> hands = Enumerable.Range(0, 4).Select(_ => new List<Card>()).ToList();
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly int[] tricks = new int[4];
        private readonly int[] scores = new int[4];
        private int dealer = 3;
        private int dealsPlayed;
        private int highPlayer;
        private int lowPlayer;
        private int doublePlayer = -1;
        private bool declaredHigh;
        private Suit? trump;
        private string phase = "play";
        private bool finished;

        public override string GameId => "triple_crown";
        public override string Name => "トリプルクラウン";
        public TripleCrownGame(int players, DeterministicRandom rng, IReadOnlyDictionary<string, string> options)
        {
            Players = 4; this.rng = rng; targetScore = Math.Max(1, options.Integer("target_score", 15));
            sessionDeals = options.ContainsKey("deals") ? Math.Max(1, options.Integer("deals", 4)) : (int?)null;
            StartDeal();
        }

        private void StartDeal()
        {
            foreach (List<Card> hand in hands) hand.Clear(); trick.Clear(); Array.Clear(tricks, 0, 4);
            List<Card> deck = Cards.Shuffled(Cards.StandardDeck(), rng); dealer = (dealer + 1) % 4;
            for (int round = 0; round < 13; round++) for (int offset = 1; offset <= 4; offset++) hands[(dealer + offset) % 4].Add(Pop(deck));
            highPlayer = Enumerable.Range(0, 4).Single(player => hands[player].Contains(new Card(Suit.Spades, 1)));
            lowPlayer = Enumerable.Range(0, 4).Single(player => hands[player].Contains(new Card(Suit.Diamonds, 2)));
            doublePlayer = highPlayer == lowPlayer ? highPlayer : -1; trump = null;
            phase = doublePlayer >= 0 ? "choose_double" : "play";
            CurrentPlayer = doublePlayer >= 0 ? doublePlayer : (dealer + 1) % 4;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "choose_double") return Enum.GetValues(typeof(Suit)).Cast<Suit>().SelectMany(suit => new[]
            {
                new Action("choose_double", value: "high:" + Card.SuitCode(suit)),
                new Action("choose_double", value: "low:" + Card.SuitCode(suit))
            }).ToArray();
            IEnumerable<Card> cards = hands[actual];
            if (trick.Count > 0) { Card[] follow = cards.Where(card => card.Suit == trick[0].Item2.Suit).ToArray(); if (follow.Length > 0) cards = follow; }
            return cards.Select(card => new Action("play", card)).ToArray();
        }
        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "choose_double")
            {
                string[] parts = action.Value!.Split(':'); declaredHigh = parts[0] == "high"; trump = Card.ParseSuit(parts[1]);
                phase = "play"; CurrentPlayer = (dealer + 1) % 4; return;
            }
            Card card = action.Card!.Value; hands[player].Remove(card); trick.Add(Tuple.Create(player, card));
            if (trick.Count < 4) { CurrentPlayer = (player + 1) % 4; return; }
            Suit led = trick[0].Item2.Suit; IEnumerable<Tuple<int, Card>> eligible = trump.HasValue && trick.Any(item => item.Item2.Suit == trump.Value)
                ? trick.Where(item => item.Item2.Suit == trump.Value) : trick.Where(item => item.Item2.Suit == led);
            int winner = eligible.OrderByDescending(item => Strength(item.Item2)).First().Item1; tricks[winner]++; trick.Clear();
            if (tricks.Sum() >= 13) FinishDeal(); else CurrentPlayer = winner;
        }
        private void FinishDeal()
        {
            if (doublePlayer >= 0)
            {
                bool success = tricks[doublePlayer] >= 5 || tricks[doublePlayer] == 0;
                if (success) scores[doublePlayer] += 5;
                else
                {
                    int award = 2 * (declaredHigh ? 5 - tricks[doublePlayer] : tricks[doublePlayer]);
                    for (int player = 0; player < 4; player++) if (player != doublePlayer) scores[player] += award;
                }
            }
            else
            {
                if (tricks[highPlayer] >= 5) scores[highPlayer] += 2;
                if (tricks[lowPlayer] == 0) scores[lowPlayer] += 3;
                int teamAward = Math.Max(0, 5 - tricks[highPlayer]) + tricks[lowPlayer];
                for (int player = 0; player < 4; player++) if (player != highPlayer && player != lowPlayer) scores[player] += teamAward;
            }
            dealsPlayed++;
            if (sessionDeals.HasValue ? dealsPlayed >= sessionDeals.Value : scores.Max() >= targetScore) finished = true;
            else StartDeal();
        }
        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "choose_double")
            {
                bool high = hands[player].Count(card => Strength(card) >= 11) >= 5;
                Suit suit = Enum.GetValues(typeof(Suit)).Cast<Suit>().OrderByDescending(candidate => hands[player].Count(card => card.Suit == candidate)).First();
                return actions.First(action => action.Value == (high ? "high:" : "low:") + Card.SuitCode(suit));
            }
            string role = RoleOf(player);
            return role == "low" ? actions.OrderBy(action => Strength(action.Card!.Value)).First() : actions.OrderByDescending(action => Strength(action.Card!.Value)).First();
        }
        private string RoleOf(int player) => doublePlayer == player ? "double" : highPlayer == player ? "high" : lowPlayer == player ? "low" : "team";
        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over."); int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 4).Where(player => scores[player] == high), scores.Select(value => (double)value),
                sessionDeals.HasValue ? "configured hidden-crown deals" : "first to the Triple Crown target", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer; string role = RoleOf(viewer);
            string session = sessionDeals.HasValue ? $"deal={dealsPlayed + 1}/{sessionDeals.Value}" : $"deal={dealsPlayed + 1} target={targetScore}";
            return $"phase={phase} {session} dealer=P{dealer} your_role={role} trump={(trump.HasValue ? Card.SuitCode(trump.Value) : "none")} " +
                $"trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] tricks=[{string.Join(",", tricks)}] scores=[{string.Join(",", scores)}] " +
                $"hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("triple_crown", "トリプルクラウン", 4, 4, "hidden-objective trick-taking",
                "AS保持者は5勝以上のHigh、2D保持者は0勝のLow、他2人は両者の失敗量を得るTeam Crown。両札保持者はHigh/Lowを秘密選択して切り札を指定し、どちらか達成で5点、失敗時は他3人へ宣言不足量の2倍を与える。",
                "gokurakism/Triple Crown", new Dictionary<string, string> { { "target_score", "15" }, { "deals", "明示時のみ短縮戦" } }),
            (players, random, options) => new TripleCrownGame(players, random, options));
    }

    public sealed class GuillotineGame : GameBase
    {
        private readonly DeterministicRandom rng;
        private readonly List<List<Card>> hands = Enumerable.Range(0, 4).Select(_ => new List<Card>()).ToList();
        private readonly List<List<Card>> captured = Enumerable.Range(0, 4).Select(_ => new List<Card>()).ToList();
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly List<Card> layout = new List<Card>();
        private readonly bool[,] used = new bool[4, 6];
        private readonly int[] tricks = new int[4];
        private readonly int[] scores = new int[4];
        private readonly List<int> dominoOrder = new List<int>();
        private readonly bool[] dominoOut = new bool[4];
        private int dealer = 3;
        private int contract = -1;
        private int dealsPlayed;
        private int trickNumber;
        private int firstWinner = -1;
        private int lastWinner = -1;
        private int dominoPasses;
        private bool aceRun;
        private string phase = "choose_contract";
        private bool finished;

        public override string GameId => "guillotine";
        public override string Name => "ギロチン";
        public GuillotineGame(int players, DeterministicRandom rng) { Players = 4; this.rng = rng; StartDeal(); }
        private void StartDeal()
        {
            foreach (List<Card> hand in hands) hand.Clear(); foreach (List<Card> pile in captured) pile.Clear();
            trick.Clear(); layout.Clear(); dominoOrder.Clear(); Array.Clear(dominoOut, 0, 4); Array.Clear(tricks, 0, 4);
            List<Card> deck = Cards.Shuffled(Cards.StandardDeck(new[] { 1, 7, 8, 9, 10, 11, 12, 13 }), rng);
            dealer = (dealer + 1) % 4; for (int round = 0; round < 8; round++) for (int offset = 1; offset <= 4; offset++) hands[(dealer + offset) % 4].Add(Pop(deck));
            contract = -1; trickNumber = 0; firstWinner = -1; lastWinner = -1;
            dominoPasses = 0; aceRun = false; phase = "choose_contract"; CurrentPlayer = dealer;
        }
        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "choose_contract") return Enumerable.Range(0, 6).Where(index => !used[dealer, index])
                .Select(index => new Action("choose_contract", value: index.ToString())).ToArray();
            if (phase == "domino")
            {
                var actions = hands[actual].Where(card => layout.Count == 0 || layout.Any(placed => Adjacent(card, placed)))
                    .Select(card => new Action("place_domino", card)).ToList();
                if (aceRun && actions.Count == 0) actions.Add(new Action("finish_ace_run"));
                else if (actions.Count == 0) actions.Add(new Action("pass"));
                return actions;
            }
            IEnumerable<Card> cards = hands[actual];
            if (trick.Count > 0) { Card[] follow = cards.Where(card => card.Suit == trick[0].Item2.Suit).ToArray(); if (follow.Length > 0) cards = follow; }
            return cards.Select(card => new Action("play", card)).ToArray();
        }
        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "choose_contract")
            {
                contract = int.Parse(action.Value!); used[dealer, contract] = true; phase = contract == 5 ? "domino" : "play";
                CurrentPlayer = contract == 5 ? dealer : (dealer + 1) % 4; return;
            }
            if (phase == "domino") { ApplyDomino(player, action); return; }
            Card card = action.Card!.Value; hands[player].Remove(card); trick.Add(Tuple.Create(player, card));
            if (trick.Count < 4) { CurrentPlayer = (player + 1) % 4; return; }
            int winner = trick.Where(item => item.Item2.Suit == trick[0].Item2.Suit).OrderByDescending(item => Strength(item.Item2)).First().Item1;
            tricks[winner]++; captured[winner].AddRange(trick.Select(item => item.Item2)); trickNumber++;
            if (trickNumber == 1) firstWinner = winner; lastWinner = winner; trick.Clear();
            if (trickNumber >= 8) FinishDeal(); else CurrentPlayer = winner;
        }
        private void ApplyDomino(int player, Action action)
        {
            if (action.Kind == "finish_ace_run") { aceRun = false; CurrentPlayer = NextDomino(player); return; }
            if (action.Kind == "pass")
            {
                dominoPasses++; if (dominoPasses >= 4 - dominoOrder.Count) FinishDeal(); else CurrentPlayer = NextDomino(player); return;
            }
            Card card = action.Card!.Value; hands[player].Remove(card); layout.Add(card); dominoPasses = 0;
            if (hands[player].Count == 0 && !dominoOut[player]) { dominoOut[player] = true; dominoOrder.Add(player); if (dominoOrder.Count >= 2) { FinishDeal(); return; } }
            aceRun = card.Rank == 1 && layout.Count > 1 && !dominoOut[player];
            if (!aceRun) CurrentPlayer = NextDomino(player);
        }
        private int NextDomino(int player)
        {
            int next = (player + 1) % 4; while (dominoOut[next]) next = (next + 1) % 4; return next;
        }
        private void FinishDeal()
        {
            if (contract == 5)
            {
                if (dominoOrder.Count > 0) scores[dominoOrder[0]] -= 30;
                if (dominoOrder.Count > 1) scores[dominoOrder[1]] -= 10;
            }
            else for (int player = 0; player < 4; player++) scores[player] += ContractPenalty(player);
            dealsPlayed++; if (dealsPlayed >= 24) finished = true; else StartDeal();
        }
        private int ContractPenalty(int player)
        {
            List<Card> pile = captured[player]; Card heartKing = new Card(Suit.Hearts, 13); Card spadeQueen = new Card(Suit.Spades, 12);
            if (contract == 0) return (pile.Contains(heartKing) ? 20 : 0) + (pile.Contains(spadeQueen) ? 10 : 0);
            if (contract == 1) return pile.Count(card => card.Rank == 12) * 10 - (pile.Contains(heartKing) ? 10 : 0);
            if (contract == 2) return pile.Count(card => card.Suit == Suit.Spades) * 5 - (pile.Contains(heartKing) ? 10 : 0);
            if (contract == 3) return tricks[player] * -5 - (pile.Contains(heartKing) ? 10 : 0);
            int firstLast = (firstWinner == player ? 5 : 0) + (lastWinner == player ? 5 : 0);
            return (pile.Contains(heartKing) ? 10 : 0) + pile.Count(card => card.Suit == Suit.Spades) * 5 +
                pile.Count(card => card.Rank == 12) * 10 + firstLast;
        }
        private static bool Adjacent(Card left, Card right) => left.Rank == right.Rank && left.Suit != right.Suit ||
            left.Suit == right.Suit && Math.Abs(SequenceIndex(left) - SequenceIndex(right)) == 1;
        private static int SequenceIndex(Card card) => card.Rank == 1 ? 7 : card.Rank == 13 ? 6 : card.Rank == 12 ? 5 :
            card.Rank == 11 ? 4 : card.Rank - 7;
        private static int Strength(Card card) => card.Rank == 1 ? 8 : card.Rank == 10 ? 7 : card.Rank == 13 ? 6 :
            card.Rank == 12 ? 5 : card.Rank == 11 ? 4 : card.Rank - 7;
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }
        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "choose_contract") return actions[0];
            if (phase == "domino") return actions.FirstOrDefault(action => action.Kind == "place_domino").Kind == "place_domino"
                ? actions.First(action => action.Kind == "place_domino") : actions[0];
            return actions.OrderBy(action => Strength(action.Card!.Value)).First();
        }
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over."); int low = scores.Min();
            return new GameResult(Enumerable.Range(0, 4).Where(player => scores[player] == low), scores.Select(value => (double)value), "lowest score after twenty-four contracts", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            return $"phase={phase} deal={dealsPlayed + 1}/24 dealer=P{dealer} contract={contract} trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] " +
                $"layout=[{string.Join(" ", layout)}] tricks=[{string.Join(",", tricks)}] scores=[{string.Join(",", scores)}] " +
                $"hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("guillotine", "ギロチン", 4, 4, "twenty-four-deal compendium",
                "7～Aの32枚で各dealerがRoyalty、Queens、Spades、Parliament、Guillotine、Dominoを1回ずつ選ぶ24deal戦。前5契約はno-trump罰点、Dominoは同rank別suit／同suit隣接とA連続出しで先着-30/-10を得て、総点最少を競う。",
                "gokurakism/Guillotine"),
            (players, random, options) => new GuillotineGame(players, random));
    }

    public sealed class PassCutRunGame : GameBase
    {
        private readonly DeterministicRandom rng;
        private readonly int sessionDeals;
        private readonly List<List<Card>> hands = Enumerable.Range(0, 4).Select(_ => new List<Card>()).ToList();
        private readonly List<List<Card>> outgoing = Enumerable.Range(0, 4).Select(_ => new List<Card>()).ToList();
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly int[] selected = new int[4];
        private readonly int[] scores = new int[4];
        private readonly List<int> playOrder = new List<int>();
        private int dealer = 3;
        private int dealsPlayed;
        private int trickLeader;
        private int orderIndex;
        private Suit trump;
        private string phase = "pass_cards";
        private bool finished;

        public override string GameId => "pass_cut_run";
        public override string Name => "パスカットラン";
        public PassCutRunGame(int players, DeterministicRandom rng, IReadOnlyDictionary<string, string> options)
        { Players = 4; this.rng = rng; sessionDeals = Math.Max(1, options.Integer("deals", 4)); StartDeal(); }
        private static int Partner(int player) => player % 2 == 0 ? player + 1 : player - 1;
        private void StartDeal()
        {
            foreach (List<Card> hand in hands) hand.Clear(); foreach (List<Card> cards in outgoing) cards.Clear();
            trick.Clear(); Array.Clear(selected, 0, 4); List<Card> deck = Cards.Shuffled(Cards.StandardDeck(), rng); dealer = (dealer + 1) % 4;
            Card last = default;
            for (int round = 0; round < 13; round++) for (int offset = 1; offset <= 4; offset++)
            { int player = (dealer + offset) % 4; last = Pop(deck); hands[player].Add(last); }
            trump = last.Suit; phase = "pass_cards"; CurrentPlayer = (dealer + 1) % 4;
        }
        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "pass_cards") return hands[actual].Select(card => new Action("pass_to_partner", card, Partner(actual))).ToArray();
            IEnumerable<Card> cards = hands[actual];
            if (trick.Count > 0) { Card[] follow = cards.Where(card => card.Suit == trick[0].Item2.Suit).ToArray(); if (follow.Length > 0) cards = follow; }
            return cards.Select(card => new Action("play", card)).ToArray();
        }
        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "pass_cards")
            {
                Card card = action.Card!.Value; hands[player].Remove(card); outgoing[player].Add(card); selected[player]++;
                if (selected[player] < 2) return;
                if (selected.Sum() < 8) { CurrentPlayer = (player + 1) % 4; return; }
                for (int source = 0; source < 4; source++) hands[Partner(source)].AddRange(outgoing[source]);
                phase = "play"; BeginTrick((dealer + 2) % 4); return;
            }
            Card played = action.Card!.Value; hands[player].Remove(played); trick.Add(Tuple.Create(player, played)); orderIndex++;
            if (orderIndex < 4) { CurrentPlayer = playOrder[orderIndex]; return; }
            Suit led = trick[0].Item2.Suit; IEnumerable<Tuple<int, Card>> eligible = trick.Any(item => item.Item2.Suit == trump)
                ? trick.Where(item => item.Item2.Suit == trump) : trick.Where(item => item.Item2.Suit == led);
            int winner = eligible.OrderByDescending(item => Strength(item.Item2)).First().Item1;
            int points = winner == trickLeader ? 1 : winner == Partner(trickLeader) ? 4 : winner == (trickLeader + 2) % 4 ? 3 : 2;
            scores[winner] += points; trick.Clear();
            if (hands.All(hand => hand.Count == 0)) FinishDeal(); else BeginTrick(winner);
        }
        private void BeginTrick(int leader)
        {
            trickLeader = leader; playOrder.Clear(); int direction = (leader + 1) % 4 == Partner(leader) ? -1 : 1;
            for (int offset = 0; offset < 4; offset++) playOrder.Add((leader + direction * offset + 8) % 4);
            orderIndex = 0; CurrentPlayer = leader;
        }
        private void FinishDeal() { dealsPlayed++; if (dealsPlayed >= sessionDeals) finished = true; else StartDeal(); }
        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1) => LegalActions(player).OrderBy(action => Strength(action.Card!.Value)).First();
        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }
        private int TeamScore(int player) => scores[player] + scores[Partner(player)];
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over."); int team0 = TeamScore(0), team1 = TeamScore(2); int high = Math.Max(team0, team1);
            double[] result = { team0, team0, team1, team1 };
            return new GameResult(Enumerable.Range(0, 4).Where(player => result[player] == high), result, "adjacent-partner Pass/Cut/Run points", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            return $"phase={phase} deal={dealsPlayed + 1}/{sessionDeals} dealer=P{dealer} trump={Card.SuitCode(trump)} leader=P{trickLeader} order=[{string.Join(",", playOrder.Select(player => "P" + player))}] " +
                $"trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] scores=[{string.Join(",", scores)}] " +
                $"hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("pass_cut_run", "パスカットラン", 4, 4, "adjacent-partner trick-taking",
                "隣席固定partnerへ受取前に2枚ずつ渡し、dealer最終札suitを切り札とする。各leaderからpartnerが必ず4番手になる方向で出し、partner勝ちPass4、対面Cut3、他隣Cut2、自勝ちRun1を4deal集計する。",
                "gokurakism/Pass Cut Run", new Dictionary<string, string> { { "deals", "4" } }),
            (players, random, options) => new PassCutRunGame(players, random, options));
    }
}
