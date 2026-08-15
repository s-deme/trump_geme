using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    internal static class FourPlayerFoundationGames
    {
        public static void RegisterGames(GameRegistry registry)
        {
            MultiStackGame.Register(registry);
            DubitoGame.Register(registry);
            MiniMisereGame.Register(registry);
            AgonyAuntGame.Register(registry);
            CollusionGame.Register(registry);
            ConfirmationGame.Register(registry);
            TheTrickGame.Register(registry);
            TrufGame.Register(registry);
        }
    }

    public sealed class MultiStackGame : GameBase
    {
        private readonly List<List<Card>> hands;
        private readonly List<Card> stock;
        private readonly List<Card> stacks = new List<Card>();
        private readonly int[] roles = { 0, 1, 2, 3 };
        private string phase = "draw";
        private int playedThisTurn;
        private int stalledTurns;
        private bool finished;
        private bool won;

        public override string GameId => "multi_stack";
        public override string Name => "マルチスタック";

        public MultiStackGame(int players, DeterministicRandom rng)
        {
            Players = players; hands = Enumerable.Range(0, players).Select(_ => new List<Card>()).ToList();
            stock = Cards.Shuffled(Cards.StandardDeck(), rng);
            int stackCount = players == 4 ? 5 : players;
            for (int i = 0; i < stackCount; i++) stacks.Add(Pop(stock));
            for (int round = 0; round < 4; round++)
                for (int player = 0; player < players; player++) hands[player].Add(Pop(stock));
            CurrentPlayer = 0;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "draw")
            {
                int maximum = Math.Min(8 - hands[actual].Count, stock.Count);
                return Enumerable.Range(0, maximum + 1).Select(count => new Action("draw", value: count.ToString())).ToArray();
            }
            if (phase == "play")
            {
                var actions = new List<Action>();
                for (int cardIndex = 0; cardIndex < hands[actual].Count; cardIndex++)
                    for (int stack = 0; stack < stacks.Count; stack++)
                        if (CanPlace(hands[actual][cardIndex], stacks[stack], roles[actual]))
                            actions.Add(new Action("play_to_stack", hands[actual][cardIndex], stack));
                if (playedThisTurn > 0 || actions.Count == 0) actions.Add(new Action("finish_play"));
                return actions;
            }
            var give = new List<Action> { new Action("keep_all") };
            foreach (Card card in hands[actual])
                for (int target = 0; target < Players; target++) if (target != actual)
                    give.Add(new Action("give", card, target));
            return give;
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "draw")
            {
                int count = int.Parse(action.Value!);
                for (int i = 0; i < count; i++) hands[player].Add(Pop(stock));
                phase = "play"; playedThisTurn = 0; return;
            }
            if (phase == "play")
            {
                if (action.Kind == "finish_play") { phase = "give"; return; }
                Card card = action.Card!.Value; hands[player].Remove(card); stacks[action.Target!.Value] = card;
                playedThisTurn++;
                if (card.Rank == 11) RotateRoles();
                if (hands.All(hand => hand.Count == 0)) { finished = true; won = true; return; }
                return;
            }
            if (action.Kind == "give")
            {
                Card card = action.Card!.Value; hands[player].Remove(card); hands[action.Target!.Value].Add(card);
            }
            if (playedThisTurn == 0) stalledTurns++; else stalledTurns = 0;
            if (stalledTurns >= Players) { finished = true; won = false; return; }
            CurrentPlayer = (player + 1) % Players; phase = "draw"; playedThisTurn = 0;
        }

        private static bool CanPlace(Card card, Card top, int role)
        {
            int delta = (RankIndex(card) - RankIndex(top) + 13) % 13;
            if (delta != 1 && delta != 12) return false;
            bool sameRed = IsRed(card) == IsRed(top);
            if (role == 0) return sameRed;
            if (role == 1) return !sameRed;
            if (role == 2) return delta == 1;
            return delta == 12;
        }

        private void RotateRoles()
        {
            int last = roles[3];
            for (int index = 3; index > 0; index--) roles[index] = roles[index - 1];
            roles[0] = last;
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "draw")
            {
                bool canAlreadyPlay = CanPlayerPlay(player);
                return actions.First(action => action.Value == (canAlreadyPlay ? "0" : actions.Max(a => int.Parse(a.Value!)).ToString()));
            }
            if (phase == "play") return actions.Any(action => action.Kind == "play_to_stack")
                ? actions.First(action => action.Kind == "play_to_stack") : actions[0];
            return actions[0];
        }

        private bool CanPlayerPlay(int player) => hands[player].Any(card =>
            Enumerable.Range(0, stacks.Count).Any(stack => CanPlace(card, stacks[stack], roles[player])));
        private static int RankIndex(Card card) => card.Rank == 1 ? 0 : card.Rank - 1;
        private static bool IsRed(Card card) => card.Suit == Suit.Diamonds || card.Suit == Suit.Hearts;
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            return new GameResult(won ? Enumerable.Range(0, Players) : Array.Empty<int>(),
                Enumerable.Repeat(won ? 1.0 : 0.0, Players), won ? "all public hands emptied" : "all players stuck", TurnCount);
        }

        public override string View(int? player = null) =>
            $"phase={phase} role={RoleName(roles[CurrentPlayer])} stock={stock.Count} stacks=[{string.Join(" ", stacks)}] " +
            $"public_hands=[{string.Join(" | ", hands.Select((hand, p) => "P" + p + ":" + string.Join(" ", hand)))}]";

        private static string RoleName(int role) => role == 0 ? "same_color" : role == 1 ? "alternating" :
            role == 2 ? "upward" : "downward";

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("multi_stack", "マルチスタック", 2, 4, "cooperative shedding",
                "公開4枚手札から±1の共有stackへ、同色・色交互・上昇・下降の役割制限で1枚以上出す。0～8枚まで補充し、1枚譲渡でき、Jで4役が巡回する協力ゲーム。",
                "gokurakism/Multi Stacks"),
            (players, random, options) => new MultiStackGame(players, random));
    }

    public sealed class DubitoGame : GameBase
    {
        private readonly List<List<Card>> hands;
        private readonly List<List<List<Card>>> columns;
        private readonly List<Card> stock;
        private readonly bool[] stopped;
        private bool finished;

        public override string GameId => "dubito";
        public override string Name => "ドゥビトー";

        public DubitoGame(int players, DeterministicRandom rng)
        {
            Players = players; hands = Enumerable.Range(0, players).Select(_ => new List<Card>()).ToList();
            columns = Enumerable.Range(0, players).Select(_ => Enumerable.Range(0, 4)
                .Select(__ => new List<Card>()).ToList()).ToList(); stopped = new bool[players];
            stock = Cards.Shuffled(Cards.StandardDeck(copies: 2), rng);
            for (int round = 0; round < 8; round++)
                for (int player = 0; player < Players; player++) hands[player].Add(Pop(stock));
            CurrentPlayer = 0;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player); var actions = new List<Action>();
            foreach (Card card in hands[actual])
                for (int column = 0; column < 4; column++)
                    if (CanPlace(card, columns[actual][column], column)) actions.Add(new Action("place", card, column));
            if (actions.Count == 0) actions.Add(new Action("stop"));
            return actions;
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (action.Kind == "stop") stopped[player] = true;
            else
            {
                Card card = action.Card!.Value; hands[player].Remove(card); columns[player][action.Target!.Value].Add(card);
                if (stock.Count > 0) hands[player].Add(Pop(stock));
                if (stock.Count == 0) { finished = true; return; }
            }
            if (stopped.All(value => value)) { finished = true; return; }
            do CurrentPlayer = (CurrentPlayer + 1) % Players; while (stopped[CurrentPlayer]);
        }

        private static bool CanPlace(Card card, IReadOnlyList<Card> column, int index)
        {
            if (column.Count == 0) return true; Card last = column[column.Count - 1];
            if (index == 0) return Strength(card) > Strength(last);
            if (index == 1) return card.Suit == last.Suit;
            if (index == 2) return card.Suit == last.Suit && Strength(card) > Strength(last);
            return card.Rank == last.Rank;
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1) =>
            LegalActions(player).OrderByDescending(action => action.Target ?? -1).First();
        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }

        private int Score(int player) => columns[player].Select((column, index) => column.Count * (index + 1)).Sum();
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int[] scores = Enumerable.Range(0, Players).Select(Score).ToArray(); int high = scores.Max();
            return new GameResult(Enumerable.Range(0, Players).Where(player => scores[player] == high),
                scores.Select(value => (double)value), "weighted four-column layout", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            return $"stock={stock.Count} stopped=[{string.Join(",", stopped)}] scores=[{string.Join(",", Enumerable.Range(0, Players).Select(Score))}] " +
                $"columns=[{string.Join(" | ", columns[viewer].Select((column, index) => (index + 1) + ":" + string.Join(" ", column)))}] " +
                $"hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("dubito", "ドゥビトー", 1, 4, "weighted layout",
                "2組104枚・8枚手札で、昇順、同スート、同スート昇順、同rankの4列へ1枚置いて補充する。置けない者は終了し、列ごとの1～4倍点を競う。",
                "gokurakism/Dubito"),
            (players, random, options) => new DubitoGame(players, random));
    }

    public sealed class MiniMisereGame : GameBase
    {
        private sealed class MiniCard
        {
            public Card? Card { get; }
            public bool Joker => !Card.HasValue;
            public string Id => Joker ? "X" : Card!.Value.ToString();
            public MiniCard(Card? card) { Card = card; }
            public override string ToString() => Id;
        }

        private readonly DeterministicRandom rng;
        private readonly int targetScore;
        private readonly List<List<MiniCard>> hands;
        private readonly List<Tuple<int, MiniCard>> trick = new List<Tuple<int, MiniCard>>();
        private readonly int[] tricks;
        private readonly int[] scores;
        private readonly bool[] lot;
        private int dealer;
        private int declarations;
        private int handSize;
        private bool jokerWins;
        private string phase = "lot";
        private bool finished;

        public override string GameId => "mini_misere";
        public override string Name => "ミニミゼール";

        public MiniMisereGame(int players, DeterministicRandom rng,
            IReadOnlyDictionary<string, string> options)
        {
            Players = players; this.rng = rng; targetScore = Math.Max(1, options.Integer("target_score", players == 3 ? 31 : 25));
            hands = Enumerable.Range(0, players).Select(_ => new List<MiniCard>()).ToList();
            tricks = new int[players]; scores = new int[players]; lot = new bool[players]; dealer = players - 1; StartDeal();
        }

        private void StartDeal()
        {
            foreach (List<MiniCard> hand in hands) hand.Clear(); trick.Clear(); Array.Clear(tricks, 0, Players); Array.Clear(lot, 0, Players);
            List<MiniCard> deck;
            if (Players == 3) deck = Cards.StandardDeck(new[] { 1, 10, 11, 12, 13 }).Select(card => new MiniCard(card)).ToList();
            else if (Players == 6) deck = Cards.StandardDeck(new[] { 1, 2, 7, 8, 9, 10, 11, 12, 13 }).Select(card => new MiniCard(card)).ToList();
            else deck = Cards.StandardDeck(new[] { 1, 2, 10, 11, 12, 13 }).Select(card => new MiniCard(card)).ToList();
            if (Players != 6) deck.Add(new MiniCard(null)); rng.Shuffle(deck);
            handSize = Players == 3 ? 7 : Players == 5 ? 5 : 6; dealer = (dealer + 1) % Players;
            for (int round = 0; round < handSize; round++)
                for (int offset = 1; offset <= Players; offset++) hands[(dealer + offset) % Players].Add(Pop(deck));
            declarations = 0; jokerWins = false; phase = Players == 3 ? "play" : "lot"; CurrentPlayer = (dealer + 1) % Players;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "lot") return new[] { new Action("no_lot"), new Action("declare_lot") };
            IEnumerable<MiniCard> cards = hands[actual];
            if (trick.Count > 0 && !trick[0].Item2.Joker)
            {
                Suit led = trick[0].Item2.Card!.Value.Suit;
                MiniCard[] follow = cards.Where(card => !card.Joker && card.Card!.Value.Suit == led).ToArray();
                if (follow.Length > 0) cards = follow;
            }
            var actions = new List<Action>();
            foreach (MiniCard card in cards)
            {
                if (Players == 3 && card.Joker && trick.Count == 2)
                {
                    actions.Add(new Action("play_joker_lose", value: card.Id));
                    actions.Add(new Action("play_joker_win", value: card.Id));
                }
                else actions.Add(new Action("play", card.Card, value: card.Id));
            }
            return actions;
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "lot")
            {
                lot[player] = action.Kind == "declare_lot"; declarations++;
                if (declarations >= Players) { phase = "play"; CurrentPlayer = (dealer + 1) % Players; }
                else CurrentPlayer = (player + 1) % Players;
                return;
            }
            MiniCard card = hands[player].First(item => item.Id == action.Value); hands[player].Remove(card);
            if (action.Kind == "play_joker_win") jokerWins = true;
            trick.Add(Tuple.Create(player, card));
            if (trick.Count < Players) { CurrentPlayer = (player + 1) % Players; return; }
            int winner = TrickWinner(); tricks[winner]++; trick.Clear(); jokerWins = false;
            if (tricks.Sum() >= handSize) FinishDeal(); else CurrentPlayer = winner;
        }

        private int TrickWinner()
        {
            if (trick[0].Item2.Joker) return trick[0].Item1;
            if (jokerWins) return trick.Single(item => item.Item2.Joker).Item1;
            Suit led = trick[0].Item2.Card!.Value.Suit; Tuple<int, MiniCard> best = trick[0];
            foreach (Tuple<int, MiniCard> item in trick.Skip(1).Where(item => !item.Item2.Joker && item.Item2.Card!.Value.Suit == led))
                if (TrickStrength(item.Item2.Card!.Value, trick[0].Item2.Card!.Value.Rank == 2) >
                    TrickStrength(best.Item2.Card!.Value, trick[0].Item2.Card!.Value.Rank == 2)) best = item;
            return best.Item1;
        }

        private void FinishDeal()
        {
            int successfulLot = Enumerable.Range(0, Players).Where(player => lot[player] && tricks[player] == handSize)
                .DefaultIfEmpty(-1).First();
            if (lot.Any(value => value))
            {
                if (successfulLot >= 0) scores[successfulLot] += handSize * 2;
                else for (int player = 0; player < Players; player++) if (!lot[player]) scores[player] += handSize;
            }
            else for (int player = 0; player < Players; player++) scores[player] += ScoreForTricks(tricks[player]);
            int high = scores.Max();
            if (high >= targetScore && scores.Count(value => value == high) == 1) finished = true; else StartDeal();
        }

        private int ScoreForTricks(int count)
        {
            if (count == handSize) return 0;
            if (count == 0) return handSize;
            if (handSize == 5) return count <= 2 ? count : count == 3 ? 6 : 8;
            if (handSize == 6) return count <= 3 ? count : count == 4 ? 8 : 10;
            return count <= 3 ? count : 2 * count;
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "lot")
            {
                int high = hands[player].Count(card => card.Joker || (card.Card.HasValue && Strength(card.Card.Value) >= 12));
                return actions.First(action => action.Kind == (high >= handSize - 1 ? "declare_lot" : "no_lot"));
            }
            return actions.OrderBy(action => action.Card.HasValue ? Strength(action.Card.Value) : 15).First();
        }

        private static int TrickStrength(Card card, bool deuceLed) => card.Rank == 2 && deuceLed ? 20 : Strength(card);
        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank == 2 ? 2 : card.Rank;
        private static MiniCard Pop(List<MiniCard> cards) { MiniCard card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over."); int high = scores.Max();
            return new GameResult(Enumerable.Range(0, Players).Where(player => scores[player] == high),
                scores.Select(value => (double)value), "Minimisere target score", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            return $"phase={phase} dealer=P{dealer} hand_size={handSize} lot=[{string.Join(",", lot.Select((value, p) => value ? "P" + p : "-"))}] " +
                $"trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] tricks=[{string.Join(",", tricks)}] " +
                $"scores=[{string.Join(",", scores)}] hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("mini_misere", "ミニミゼール", 3, 6, "plain-trick scoring",
                "人数別21/25/36枚で5～7trickを行う。2はlead時だけ最強、Jokerはlead勝ち・応手負け（3人の最後手は勝敗指定）。0勝と全勝直前を高得点とし、4～6人は全勝Lotも宣言できる。",
                "David Parlett Minimisere", new Dictionary<string, string> { { "target_score", "25（3人既定31）" } }),
            (players, random, options) => new MiniMisereGame(players, random, options));
    }

    public sealed class AgonyAuntGame : GameBase
    {
        private sealed class AgonyCard
        {
            public Card? Card { get; }
            public bool Joker => !Card.HasValue;
            public string Id => Joker ? "X" : Card!.Value.ToString();
            public AgonyCard(Card? card) { Card = card; }
            public override string ToString() => Id;
        }

        private readonly DeterministicRandom rng;
        private readonly List<List<AgonyCard>> hands = Enumerable.Range(0, 4).Select(_ => new List<AgonyCard>()).ToList();
        private readonly List<List<AgonyCard>> captured = Enumerable.Range(0, 4).Select(_ => new List<AgonyCard>()).ToList();
        private readonly List<Tuple<int, AgonyCard>> trick = new List<Tuple<int, AgonyCard>>();
        private readonly bool[,] board = new bool[4, 9];
        private readonly int[] chips = { 17, 17, 17, 17 };
        private readonly int[] tricks = new int[4];
        private Card dump;
        private int dealer = 3;
        private int trickNumber;
        private bool finished;

        public override string GameId => "agony_aunt";
        public override string Name => "アゴニーアント";

        public AgonyAuntGame(int players, DeterministicRandom rng) { Players = 4; this.rng = rng; StartDeal(); }

        private void StartDeal()
        {
            foreach (List<AgonyCard> hand in hands) hand.Clear(); foreach (List<AgonyCard> pile in captured) pile.Clear();
            trick.Clear(); Array.Clear(tricks, 0, 4); Array.Clear(board, 0, board.Length);
            List<Card> normals = Cards.Shuffled(Cards.StandardDeck(), rng); dump = Pop(normals);
            var deck = normals.Select(card => new AgonyCard(card)).ToList(); deck.Add(new AgonyCard(null)); rng.Shuffle(deck);
            dealer = (dealer + 1) % 4;
            for (int round = 0; round < 13; round++)
                for (int offset = 1; offset <= 4; offset++) hands[(dealer + offset) % 4].Add(Pop(deck));
            trickNumber = 0; CurrentPlayer = (dealer + 1) % 4;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player); IEnumerable<AgonyCard> cards = hands[actual];
            if (trick.Count > 0)
            {
                Suit led = Effective(trick[0].Item2).Suit;
                AgonyCard[] follow = cards.Where(card => Effective(card).Suit == led).ToArray();
                if (follow.Length > 0) cards = follow;
            }
            return cards.Select(card => new Action("play", card.Card, value: card.Id)).ToArray();
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            AgonyCard card = hands[player].First(item => item.Id == action.Value); hands[player].Remove(card); trick.Add(Tuple.Create(player, card));
            if (trick.Count < 4) { CurrentPlayer = (player + 1) % 4; return; }
            int winner = TrickWinner(); tricks[winner]++; captured[winner].AddRange(trick.Select(item => item.Item2)); trickNumber++;
            ScoreImmediatePenalties(winner); trick.Clear();
            if (trickNumber >= 13) FinishDeal(); else CurrentPlayer = winner;
        }

        private void ScoreImmediatePenalties(int winner)
        {
            foreach (AgonyCard card in trick.Select(item => item.Item2))
            {
                if (card.Joker) Penalize(winner, 0);
                Card effective = Effective(card);
                if (effective.Rank == 12)
                {
                    Penalize(winner, QueenCell(effective.Suit));
                    if (effective.Suit == dump.Suit) Penalize(winner, 4);
                }
            }
            if (trickNumber == 13) Penalize(winner, 2);
            if (trickNumber == DumpNumber(dump)) Penalize(winner, 8);
        }

        private void FinishDeal()
        {
            int most = Enumerable.Range(0, 4).OrderByDescending(player => tricks[player])
                .ThenByDescending(player => captured[player].Count(card => Effective(card).Suit == dump.Suit))
                .ThenByDescending(player => captured[player].Where(card => Effective(card).Suit == dump.Suit)
                    .Select(card => Strength(Effective(card))).DefaultIfEmpty(0).Max()).First();
            Penalize(most, 6);
            int[][] lines =
            {
                new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 },
                new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 },
                new[] { 0, 4, 8 }, new[] { 2, 4, 6 }
            };
            for (int player = 0; player < 4; player++)
                foreach (int[] line in lines) if (line.All(cell => board[player, cell])) chips[player]--;
            int sweep = Enumerable.Range(0, 4).Where(player => tricks[player] == 13).DefaultIfEmpty(-1).First();
            for (int player = 0; player < 4; player++)
            {
                bool anyPenalty = Enumerable.Range(0, 9).Any(cell => board[player, cell]);
                if (player == sweep || (sweep < 0 && tricks[player] == 0)) chips[player] = 17;
                else if (tricks[player] > 0 && !anyPenalty) chips[player] += (17 - chips[player]) / 2;
                chips[player] = Math.Max(0, Math.Min(17, chips[player]));
            }
            if (chips.Any(value => value == 0)) finished = true; else StartDeal();
        }

        private void Penalize(int player, int cell) { chips[player]--; board[player, cell] = true; }
        private Card Effective(AgonyCard card) => card.Card ?? dump;
        private int TrickWinner()
        {
            Suit led = Effective(trick[0].Item2).Suit;
            return trick.Where(item => Effective(item.Item2).Suit == led)
                .OrderByDescending(item => Strength(Effective(item.Item2))).First().Item1;
        }
        private static int QueenCell(Suit suit) => suit == Suit.Clubs ? 1 : suit == Suit.Diamonds ? 3 : suit == Suit.Hearts ? 5 : 7;
        private static int DumpNumber(Card card) => card.Rank;
        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }
        private static AgonyCard Pop(List<AgonyCard> cards) { AgonyCard card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1) =>
            LegalActions(player).OrderBy(action => action.Card.HasValue ? Strength(action.Card.Value) : Strength(dump)).First();
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over."); int high = chips.Max();
            return new GameResult(Enumerable.Range(0, 4).Where(player => chips[player] == high),
                chips.Select(value => (double)value), "most of seventeen counters remaining", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            return $"dump={dump} trick_no={trickNumber + 1}/13 trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] " +
                $"tricks=[{string.Join(",", tricks)}] chips=[{string.Join(",", chips)}] board_cells=[{string.Join(",", Enumerable.Range(0, 9).Where(cell => board[viewer, cell]))}] " +
                $"hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("agony_aunt", "アゴニーアント", 4, 4, "trick avoidance board",
                "公開dump札と同一札になるJokerを含む53枚戦。dump suit Q、Joker、4枚のQ、最終・最多・dump番trickの9罰点を3×3盤へ置き、3目列の追加損失と全勝・全敗・無罰回復を17chipで追跡する。",
                "David Parlett Agony Aunt"),
            (players, random, options) => new AgonyAuntGame(players, random));
    }

    public sealed class CollusionGame : GameBase
    {
        private readonly DeterministicRandom rng;
        private readonly int targetScore;
        private readonly List<List<Card>> hands = Enumerable.Range(0, 4).Select(_ => new List<Card>()).ToList();
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly int[] tricks = new int[4];
        private readonly int[] scores = new int[4];
        private int dealer = 3;
        private bool finished;

        public override string GameId => "collusion";
        public override string Name => "コルージョン";
        public CollusionGame(int players, DeterministicRandom rng, IReadOnlyDictionary<string, string> options)
        { Players = 4; this.rng = rng; targetScore = Math.Max(1, options.Integer("target_score", 100)); StartDeal(); }

        private void StartDeal()
        {
            foreach (List<Card> hand in hands) hand.Clear(); trick.Clear(); Array.Clear(tricks, 0, 4);
            List<Card> deck = Cards.Shuffled(Cards.StandardDeck(), rng); dealer = (dealer + 1) % 4;
            for (int round = 0; round < 13; round++) for (int offset = 1; offset <= 4; offset++) hands[(dealer + offset) % 4].Add(Pop(deck));
            CurrentPlayer = (dealer + 1) % 4;
        }
        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player); IEnumerable<Card> cards = hands[actual];
            if (trick.Count > 0) { Card[] follow = cards.Where(card => card.Suit == trick[0].Item2.Suit).ToArray(); if (follow.Length > 0) cards = follow; }
            return cards.Select(card => new Action("play", card)).ToArray();
        }
        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++; Card card = action.Card!.Value;
            hands[player].Remove(card); trick.Add(Tuple.Create(player, card));
            if (trick.Count < 4) { CurrentPlayer = (player + 1) % 4; return; }
            int winner = trick.Where(item => item.Item2.Suit == trick[0].Item2.Suit).OrderByDescending(item => Strength(item.Item2)).First().Item1;
            tricks[winner]++; trick.Clear(); if (tricks.Sum() >= 13) FinishDeal(); else CurrentPlayer = winner;
        }
        private void FinishDeal()
        {
            int[] bonuses = new int[4]; var groups = Enumerable.Range(0, 4).GroupBy(player => tricks[player]).ToArray();
            if (groups.Any(group => group.Count() == 3))
            {
                int odd = groups.Single(group => group.Count() == 1).Single(); bonuses[odd] = 30;
            }
            else if (groups.Length == 4) bonuses[Enumerable.Range(0, 4).OrderBy(player => tricks[player]).First()] = 20;
            else foreach (IGrouping<int, int> group in groups.Where(group => group.Count() == 2)) foreach (int player in group) bonuses[player] += 10;
            for (int player = 0; player < 4; player++)
            {
                bool plainReach = bonuses[player] == 0 && scores[player] + tricks[player] >= targetScore;
                scores[player] += (plainReach ? -tricks[player] : tricks[player]) + bonuses[player];
            }
            if (scores.Max() >= targetScore) finished = true; else StartDeal();
        }
        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1) => LegalActions(player).OrderBy(action => Strength(action.Card!.Value)).First();
        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over."); int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 4).Where(player => scores[player] == high), scores.Select(value => (double)value), "first bonus-assisted score to 100", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            return $"dealer=P{dealer} trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] tricks=[{string.Join(",", tricks)}] " +
                $"scores=[{string.Join(",", scores)}] hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("collusion", "コルージョン", 4, 4, "negotiation trick-taking",
                "52枚no-trumpの13trick。勝数各1点に、同数2人各10、全員別なら最少者20、同数3人なら残る1人30を加え100点を競う。自由会話は表示層で扱う。",
                "David Parlett Collusion", new Dictionary<string, string> { { "target_score", "100" } }),
            (players, random, options) => new CollusionGame(players, random, options));
    }

    public sealed class ConfirmationGame : GameBase
    {
        private readonly DeterministicRandom rng;
        private readonly int sessionDeals;
        private readonly List<List<Card>> hands = Enumerable.Range(0, 4).Select(_ => new List<Card>()).ToList();
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly Card?[] protectedCards = new Card?[4];
        private readonly int[] tricks = new int[4];
        private readonly int[] scores = new int[4];
        private int dealer = 3;
        private int dealsPlayed;
        private int tricksPlayed;
        private bool finished;

        public override string GameId => "confirmation";
        public override string Name => "コンファメーション";
        public ConfirmationGame(int players, DeterministicRandom rng, IReadOnlyDictionary<string, string> options)
        { Players = 4; this.rng = rng; sessionDeals = Math.Max(1, options.Integer("deals", 4)); StartDeal(); }

        private void StartDeal()
        {
            foreach (List<Card> hand in hands) hand.Clear(); trick.Clear(); Array.Clear(tricks, 0, 4);
            for (int p = 0; p < 4; p++) protectedCards[p] = null;
            List<Card> deck = Cards.Shuffled(Cards.StandardDeck(Enumerable.Range(1, 10)), rng); dealer = (dealer + 1) % 4;
            for (int round = 0; round < 10; round++) for (int offset = 1; offset <= 4; offset++) hands[(dealer + offset) % 4].Add(Pop(deck));
            tricksPlayed = 0; CurrentPlayer = dealer;
        }
        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player); IEnumerable<Card> cards = hands[actual]; var actions = new List<Action>();
            if (trick.Count == 0) return cards.Select(card => new Action("play", card)).ToArray();
            Suit led = trick[0].Item2.Suit; Card[] follow = cards.Where(card => card.Suit == led).ToArray();
            if (follow.Length == 0) return cards.Select(card => new Action("play", card)).ToArray();
            actions.AddRange(follow.Select(card => new Action("play", card)));
            Card? protectedCard = protectedCards[actual];
            Card? candidate = follow.Length == 1 && (!protectedCard.HasValue || protectedCard.Value == follow[0]) ? follow[0] : (Card?)null;
            if (candidate.HasValue)
                actions.AddRange(cards.Where(card => card.Suit != led)
                    .Select(card => new Action("protect_and_play", card, value: candidate.Value.ToString())));
            return actions;
        }
        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (action.Kind == "protect_and_play") protectedCards[player] = Card.Parse(action.Value!);
            Card card = action.Card!.Value; hands[player].Remove(card); trick.Add(Tuple.Create(player, card));
            if (trick.Count < 4) { CurrentPlayer = (player + 1) % 4; return; }
            int winner = trick.Where(item => item.Item2.Suit == trick[0].Item2.Suit).OrderByDescending(item => Strength(item.Item2)).First().Item1;
            tricks[winner]++; tricksPlayed++; trick.Clear();
            if (tricksPlayed >= 9) FinishDeal(); else CurrentPlayer = winner;
        }
        private void FinishDeal()
        {
            for (int player = 0; player < 4; player++)
            {
                Card target = hands[player].Single(); int bid = target.Rank == 10 ? 0 : target.Rank;
                scores[player] += tricks[player];
                if (tricks[player] == bid) scores[player] += protectedCards[player].HasValue ? 5 : 10;
            }
            dealsPlayed++; if (dealsPlayed >= sessionDeals) finished = true; else StartDeal();
        }
        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            return actions.OrderBy(action => action.Kind == "protect_and_play" ? 0 : 1).ThenBy(action => Strength(action.Card!.Value)).First();
        }
        private static int Strength(Card card) => card.Rank == 1 ? 1 : card.Rank;
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over."); int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 4).Where(player => scores[player] == high), scores.Select(value => (double)value), "four Confirmation deals", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            return $"deal={dealsPlayed + 1}/{sessionDeals} trick_no={tricksPlayed + 1}/9 protected=[{string.Join(",", protectedCards.Select(card => card?.ToString() ?? "-"))}] " +
                $"trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] tricks=[{string.Join(",", tricks)}] scores=[{string.Join(",", scores)}] " +
                $"hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("confirmation", "コンファメーション", 4, 4, "dynamic exact-trick",
                "A～10の40枚を10枚ずつ配り9trickを行う。唯一のfollow札を公開保護してoff-suitを出せ、最後の1枚（A=1、10=0）が目標勝数となる。勝数各1点と秘密的中10／公開的中5点を4ディール集計する。",
                "gokurakism/Confirmation", new Dictionary<string, string> { { "deals", "4" } }),
            (players, random, options) => new ConfirmationGame(players, random, options));
    }

    public sealed class TheTrickGame : GameBase
    {
        private readonly List<List<Card>> hands;
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly int[] tricks;
        private readonly Card? undealt;
        private int tricksPlayed;
        private bool finished;

        public override string GameId => "the_trick";
        public override string Name => "ザ・トリテ";
        public TheTrickGame(int players, DeterministicRandom rng)
        {
            Players = players; hands = Enumerable.Range(0, players).Select(_ => new List<Card>()).ToList(); tricks = new int[players];
            IEnumerable<int> ranks = players == 3 ? new[] { 1, 5, 6, 7, 8, 9, 10, 11, 12, 13 } : Enumerable.Range(1, 13);
            List<Card> deck = Cards.Shuffled(Cards.StandardDeck(ranks), rng);
            for (int round = 0; round < 13; round++) for (int player = 0; player < Players; player++) hands[player].Add(Pop(deck));
            Card starter = new Card(Suit.Clubs, players == 3 ? 5 : 2);
            Card? leftover = deck.Count > 0 ? Pop(deck) : (Card?)null;
            if (!hands.Any(hand => hand.Contains(starter)))
            {
                Card replacement = hands[0][0]; hands[0][0] = starter; leftover = replacement;
            }
            undealt = leftover;
            CurrentPlayer = Enumerable.Range(0, Players).Single(player => hands[player].Contains(starter));
        }
        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player); IEnumerable<Card> cards = hands[actual];
            if (trick.Count > 0) { Card[] follow = cards.Where(card => card.Suit == trick[0].Item2.Suit).ToArray(); if (follow.Length > 0) cards = follow; }
            return cards.Select(card => new Action("play", card)).ToArray();
        }
        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++; Card card = action.Card!.Value;
            hands[player].Remove(card); trick.Add(Tuple.Create(player, card));
            if (trick.Count < Players) { CurrentPlayer = (player + 1) % Players; return; }
            Suit led = trick[0].Item2.Suit; IEnumerable<Tuple<int, Card>> eligible = trick.Any(item => item.Item2.Suit == Suit.Spades)
                ? trick.Where(item => item.Item2.Suit == Suit.Spades) : trick.Where(item => item.Item2.Suit == led);
            int winner = eligible.OrderByDescending(item => Strength(item.Item2)).First().Item1;
            tricks[winner]++; tricksPlayed++; trick.Clear(); if (tricksPlayed >= 12) finished = true; else CurrentPlayer = winner;
        }
        private bool Success()
        {
            int[] targets = Players == 3 ? new[] { 0, 4, 8 } : new[] { 0, 2, 4, 6 };
            if (!tricks.OrderBy(value => value).SequenceEqual(targets)) return false;
            var suits = new HashSet<Suit>(hands.Select(hand => hand.Single().Suit));
            if (undealt.HasValue) suits.Add(undealt.Value.Suit);
            return suits.Count == 4;
        }
        private int VictoryScore()
        {
            int highPlayer = Enumerable.Range(0, Players).OrderByDescending(player => tricks[player]).First();
            int lowPlayer = Enumerable.Range(0, Players).OrderBy(player => tricks[player]).First();
            return Strength(hands[highPlayer].Single()) - Strength(hands[lowPlayer].Single()) + 12;
        }
        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1) => LegalActions(player).OrderBy(action => Strength(action.Card!.Value)).First();
        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over."); bool success = Success(); double score = success ? VictoryScore() : 0;
            return new GameResult(success ? Enumerable.Range(0, Players) : Array.Empty<int>(), Enumerable.Repeat(score, Players), success ? "cooperative quotas and four suits achieved" : "cooperative mission failed", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            string suitBacks = string.Join(" | ", hands.Select((hand, playerIndex) => playerIndex == viewer ? "you" :
                "P" + playerIndex + ":" + string.Join("", hand.GroupBy(card => card.Suit).Select(group => Card.SuitCode(group.Key) + group.Count()))));
            return $"trick_no={tricksPlayed + 1}/12 trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] tricks=[{string.Join(",", tricks)}] suit_backs=[{suitBacks}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("the_trick", "ザ・トリテ", 3, 4, "cooperative trick-taking",
                "spade固定切り札で12trickを行い、3人は勝数8/4/0と残札＋伏札4スート、4人は6/4/2/0と残札4スートを全員で達成する。相手のrankは隠しつつsuit背面情報を公開する。",
                "gokurakism/The Torite"),
            (players, random, options) => new TheTrickGame(players, random));
    }

    public sealed class TrufGame : GameBase
    {
        private readonly DeterministicRandom rng;
        private readonly int sessionDeals;
        private readonly List<List<Card>> hands;
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly Card?[] bidCards;
        private readonly int[] bids;
        private readonly int[] originalBids;
        private readonly int[] tricks;
        private readonly int[] scores;
        private int dealer;
        private int bidsMade;
        private int highBidder;
        private int dealsPlayed;
        private Suit trump;
        private bool highMode;
        private bool trumpBroken;
        private bool clockwise;
        private string phase = "bid";
        private bool finished;

        public override string GameId => "truf";
        public override string Name => "トルフ";
        public TrufGame(int players, DeterministicRandom rng, IReadOnlyDictionary<string, string> options)
        {
            Players = players; this.rng = rng; sessionDeals = Math.Max(1, options.Integer("deals", 13));
            hands = Enumerable.Range(0, players).Select(_ => new List<Card>()).ToList(); bidCards = new Card?[players];
            bids = new int[players]; originalBids = new int[players]; tricks = new int[players]; scores = new int[players]; dealer = players - 1; StartDeal();
        }
        private void StartDeal()
        {
            foreach (List<Card> hand in hands) hand.Clear(); trick.Clear(); Array.Clear(tricks, 0, Players);
            for (int p = 0; p < Players; p++) bidCards[p] = null;
            IEnumerable<Card> cards = Cards.StandardDeck(); if (Players == 3) cards = cards.Where(card => card.Suit != Suit.Clubs);
            List<Card> deck = Cards.Shuffled(cards, rng); clockwise = dealsPlayed % 2 == 1;
            if (dealsPlayed > 0) dealer = Enumerable.Range(0, Players).OrderBy(player => scores[player]).First();
            else dealer = (dealer + 1) % Players;
            for (int round = 0; round < 13; round++)
                for (int offset = 1; offset <= Players; offset++) hands[Advance(dealer, offset)].Add(Pop(deck));
            bidsMade = 0; highBidder = -1; trumpBroken = false; phase = "bid"; CurrentPlayer = Advance(dealer, 1);
        }
        private int Advance(int player, int amount = 1) => (player + (clockwise ? amount : -amount) + Players * amount) % Players;
        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "bid") return hands[actual].Select(card => new Action("bid_card", card)).ToArray();
            if (phase == "adjust") return Enumerable.Range(1, 13).SelectMany(amount => new[]
            {
                new Action("increase_all", value: amount.ToString()), new Action("decrease_all", value: amount.ToString())
            }).ToArray();
            IEnumerable<Card> cards = hands[actual];
            if (trick.Count == 0 && !trumpBroken)
            {
                Card[] nonTrump = cards.Where(card => card.Suit != trump).ToArray(); if (nonTrump.Length > 0) cards = nonTrump;
            }
            else if (trick.Count > 0)
            {
                Card[] follow = cards.Where(card => card.Suit == trick[0].Item2.Suit).ToArray(); if (follow.Length > 0) cards = follow;
            }
            return cards.Select(card => new Action("play", card)).ToArray();
        }
        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "bid")
            {
                bidCards[player] = action.Card; bids[player] = originalBids[player] = BidValue(action.Card!.Value); bidsMade++;
                if (bidsMade < Players) { CurrentPlayer = Advance(player, 1); return; }
                DetermineHighBidder(); int total = bids.Sum();
                if (total == 13) { phase = "adjust"; CurrentPlayer = highBidder; }
                else { highMode = total > 13; phase = "play"; CurrentPlayer = highBidder; }
                return;
            }
            if (phase == "adjust")
            {
                int shift = int.Parse(action.Value!) * (action.Kind == "increase_all" ? 1 : -1);
                for (int p = 0; p < Players; p++) bids[p] += shift;
                highMode = shift > 0; phase = "play"; CurrentPlayer = highBidder; return;
            }
            Card card = action.Card!.Value; hands[player].Remove(card); trick.Add(Tuple.Create(player, card));
            if (card.Suit == trump) trumpBroken = true;
            if (trick.Count < Players) { CurrentPlayer = Advance(player, 1); return; }
            Suit led = trick[0].Item2.Suit; IEnumerable<Tuple<int, Card>> eligible = trick.Any(item => item.Item2.Suit == trump)
                ? trick.Where(item => item.Item2.Suit == trump) : trick.Where(item => item.Item2.Suit == led);
            int winner = eligible.OrderByDescending(item => Strength(item.Item2)).First().Item1; tricks[winner]++; trick.Clear();
            if (tricks.Sum() >= 13) FinishDeal(); else CurrentPlayer = winner;
        }
        private void DetermineHighBidder()
        {
            highBidder = Enumerable.Range(0, Players).OrderByDescending(player => bids[player])
                .ThenByDescending(player => SuitStrength(bidCards[player]!.Value.Suit)).First(); trump = bidCards[highBidder]!.Value.Suit;
        }
        private void FinishDeal()
        {
            for (int player = 0; player < Players; player++)
            {
                int difference = highMode ? tricks[player] - bids[player] : bids[player] - tricks[player];
                if (!highMode && originalBids[player] == 0 && bids[player] == 0 && tricks[player] == 0) scores[player] += 5;
                else scores[player] += difference > 0 ? difference * 2 : difference;
            }
            dealsPlayed++; if (dealsPlayed >= sessionDeals) finished = true; else StartDeal();
        }
        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "bid")
            {
                int estimate = Math.Min(10, hands[player].Count(card => Strength(card) >= 12));
                return actions.OrderBy(action => Math.Abs(BidValue(action.Card!.Value) - estimate)).First();
            }
            if (phase == "adjust") return actions.First(action => action.Kind == "decrease_all" && action.Value == "1");
            return highMode ? actions.OrderByDescending(action => Strength(action.Card!.Value)).First() : actions.OrderBy(action => Strength(action.Card!.Value)).First();
        }
        private static int BidValue(Card card) => card.Rank == 1 ? 1 : card.Rank >= 11 ? 0 : card.Rank;
        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static int SuitStrength(Suit suit) => suit == Suit.Spades ? 4 : suit == Suit.Hearts ? 3 : suit == Suit.Diamonds ? 2 : 1;
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over."); int high = scores.Max();
            return new GameResult(Enumerable.Range(0, Players).Where(player => scores[player] == high), scores.Select(value => (double)value), "thirteen Truf deals", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            string bidView = bidsMade < Players ? string.Join(",", bidCards.Select((card, p) => p == viewer && card.HasValue ? card.Value.ToString() : card.HasValue ? "XX" : "-")) : string.Join(",", bids);
            string trickView = string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + (item.Item2.Suit == trump ? "XX" : item.Item2.ToString())));
            return $"phase={phase} deal={dealsPlayed + 1}/{sessionDeals} direction={(clockwise ? "clockwise" : "counterclockwise")} bids=[{bidView}] " +
                $"mode={(phase == "bid" ? "hidden" : highMode ? "atas" : "bawah")} trump={(phase == "bid" ? "hidden" : Card.SuitCode(trump))} trick=[{trickView}] " +
                $"tricks=[{string.Join(",", tricks)}] scores=[{string.Join(",", scores)}] hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("truf", "トルフ", 3, 4, "simultaneous-card bid trick-taking",
                "各自が手札1枚を秘密bidし、最高bid札のsuitを切り札、合計13超をatas・未満をbawahとする。13なら最高bidderが全bidを同量増減する。切り札はbreak前lead不可・伏せ出しとし、正差2倍／負差そのままを13deal集計する。3人はclubを除く39枚。",
                "Pagat Truf", new Dictionary<string, string> { { "deals", "13" } }),
            (players, random, options) => new TrufGame(players, random, options));
    }
}
