using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TrumpLab.Games
{
    internal static class RemainingTwoPlayerGames
    {
        public static void RegisterGames(GameRegistry registry)
        {
            BohemianSchneiderGame.Register(registry);
            NorwegianWhistGame.Register(registry);
            GoldmineGame.Register(registry);
        }
    }

    public sealed class BohemianSchneiderGame : GameBase
    {
        private readonly DeterministicRandom rng;
        private readonly int targetScore;
        private readonly List<List<Card>> hands = new List<List<Card>>
        {
            new List<Card>(), new List<Card>()
        };
        private readonly List<Card> stock = new List<Card>();
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly List<Card> captured = new List<Card>();
        private readonly int[] gamePoints = new int[2];
        private readonly int[] honors = new int[2];
        private int dealer = 1;
        private bool finished;

        public override string GameId => "bohemian_schneider";
        public override string Name => "ボヘミアン・シュナイダー";

        public BohemianSchneiderGame(int players, DeterministicRandom rng,
            IReadOnlyDictionary<string, string> options)
        {
            Players = 2;
            this.rng = rng;
            targetScore = Math.Max(1, options.Integer("target_score", 7));
            StartDeal();
        }

        private void StartDeal()
        {
            hands[0].Clear(); hands[1].Clear(); stock.Clear(); trick.Clear(); captured.Clear();
            honors[0] = 0; honors[1] = 0;
            stock.AddRange(Cards.Shuffled(Cards.StandardDeck(new[] { 1, 7, 8, 9, 10, 11, 12, 13 }), rng));
            dealer = 1 - dealer;
            for (int packet = 0; packet < 2; packet++)
                for (int playerOffset = 1; playerOffset <= 2; playerOffset++)
                    for (int card = 0; card < 3; card++) hands[(dealer + playerOffset) % 2].Add(Pop(stock));
            CurrentPlayer = 1 - dealer;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            return hands[actual].Select(card => new Action("play", card)).ToArray();
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null);
            Guard.Legal(action, LegalActions(player));
            TurnCount++;
            Card card = action.Card!.Value;
            hands[player].Remove(card);
            trick.Add(Tuple.Create(player, card));
            if (trick.Count == 1) { CurrentPlayer = 1 - player; return; }
            int winner = BeatsByOne(trick[1].Item2, trick[0].Item2) ? trick[1].Item1 : trick[0].Item1;
            foreach (Tuple<int, Card> item in trick)
            {
                captured.Add(item.Item2);
                if (IsHonor(item.Item2)) honors[winner]++;
            }
            trick.Clear();
            if (stock.Count > 0)
            {
                hands[winner].Add(Pop(stock));
                hands[1 - winner].Add(Pop(stock));
            }
            if (hands[0].Count == 0) FinishDeal();
            else CurrentPlayer = winner;
        }

        private void FinishDeal()
        {
            if (honors[0] == honors[1])
            {
                StartDeal();
                return;
            }
            int winner = honors[0] > honors[1] ? 0 : 1;
            int won = honors[winner];
            int tier = won == 20 ? 3 : won >= 16 ? 2 : 1;
            gamePoints[winner] += tier;
            if (gamePoints[winner] >= targetScore) finished = true;
            else StartDeal();
        }

        public static bool BeatsByOne(Card response, Card lead) =>
            response.Suit == lead.Suit && RankIndex(response.Rank) == RankIndex(lead.Rank) + 1;

        public override Action ChooseCpuAction(int player, DeterministicRandom rng, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (trick.Count > 0)
            {
                Action[] winning = actions.Where(action => BeatsByOne(action.Card!.Value, trick[0].Item2)).ToArray();
                if (winning.Length > 0) return winning.OrderBy(action => IsHonor(action.Card!.Value) ? 1 : 0).First();
            }
            return actions.OrderBy(action => IsHonor(action.Card!.Value) ? 1 : 0)
                .ThenBy(action => RankIndex(action.Card!.Value.Rank)).First();
        }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = gamePoints.Max();
            return new GameResult(Enumerable.Range(0, 2).Where(player => gamePoints[player] == high),
                gamePoints.Select(value => (double)value), "honors with Schneider and Schwarz bonuses", TurnCount);
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            return $"stock={stock.Count} trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] " +
                $"honors=[{string.Join(",", honors)}] game_points=[{string.Join(",", gamePoints)}] " +
                $"hand_counts=[{hands[0].Count},{hands[1].Count}]\n" +
                $"your hand: {string.Join(" ", hands[viewer])}";
        }

        private static bool IsHonor(Card card) => card.Rank == 1 || card.Rank >= 10;
        private static int RankIndex(int rank) => rank == 1 ? 7 : rank - 7;
        private static Card Pop(List<Card> cards)
        {
            Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card;
        }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("bohemian_schneider", "ボヘミアン・シュナイダー", 2, 2, "trick-and-draw",
                "7～Aの32枚から6枚ずつ持ち、応手が同スートの直上rankのときだけリードを奪う。絵札20枚の多数を7ゲーム点まで争う。",
                "Bohemian Schneider rules", new Dictionary<string, string> { { "target_score", "7" } }),
            (players, random, options) => new BohemianSchneiderGame(players, random, options));
    }

    public sealed class NorwegianWhistGame : GameBase
    {
        private readonly DeterministicRandom rng;
        private readonly int targetScore;
        private readonly List<List<Card>> hands = new List<List<Card>>
        {
            new List<Card>(), new List<Card>()
        };
        private readonly List<List<List<Card>>> layouts = new List<List<List<Card>>>
        {
            new List<List<Card>>(), new List<List<Card>>()
        };
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly int[] tricks = new int[2];
        private readonly int[] scores = new int[2];
        private int dealer = 1;
        private int? highBidder;
        private bool highGame;
        private string phase = "bid_non_dealer";
        private bool finished;

        public override string GameId => "norwegian_whist";
        public override string Name => "ノルウェージャンホイスト";

        public NorwegianWhistGame(int players, DeterministicRandom rng,
            IReadOnlyDictionary<string, string> options)
        {
            Players = 2;
            this.rng = rng;
            targetScore = Math.Max(1, options.Integer("target_score", 13));
            StartDeal();
        }

        private void StartDeal()
        {
            hands[0].Clear(); hands[1].Clear(); layouts[0].Clear(); layouts[1].Clear(); trick.Clear();
            tricks[0] = 0; tricks[1] = 0;
            List<Card> deck = Cards.Shuffled(Cards.StandardDeck(), rng);
            dealer = 1 - dealer;
            for (int player = 0; player < 2; player++)
                for (int column = 0; column < 8; column++) layouts[player].Add(new List<Card>());
            for (int column = 0; column < 8; column++)
                for (int offset = 1; offset <= 2; offset++) layouts[(dealer + offset) % 2][column].Add(Pop(deck));
            for (int column = 0; column < 8; column++)
                for (int offset = 1; offset <= 2; offset++) layouts[(dealer + offset) % 2][column].Add(Pop(deck));
            for (int round = 0; round < 10; round++)
                for (int offset = 1; offset <= 2; offset++) hands[(dealer + offset) % 2].Add(Pop(deck));
            highBidder = null;
            highGame = false;
            phase = "bid_non_dealer";
            CurrentPlayer = 1 - dealer;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase.StartsWith("bid", StringComparison.Ordinal))
                return new[] { new Action("bid_high"), new Action("bid_low") };
            IEnumerable<Card> cards = Available(actual);
            if (trick.Count > 0)
            {
                Suit led = trick[0].Item2.Suit;
                Card[] follow = cards.Where(card => card.Suit == led).ToArray();
                if (follow.Length > 0) cards = follow;
            }
            return cards.Select(card => new Action("play", card)).ToArray();
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null);
            Guard.Legal(action, LegalActions(player));
            TurnCount++;
            if (phase == "bid_non_dealer")
            {
                if (action.Kind == "bid_high") { highGame = true; highBidder = player; BeginPlay(); }
                else { phase = "bid_dealer"; CurrentPlayer = dealer; }
                return;
            }
            if (phase == "bid_dealer")
            {
                if (action.Kind == "bid_high") { highGame = true; highBidder = player; }
                BeginPlay();
                return;
            }
            Card card = action.Card!.Value;
            if (!hands[player].Remove(card))
            {
                List<Card> column = layouts[player].Single(pile => pile.Count > 0 && pile[pile.Count - 1] == card);
                column.RemoveAt(column.Count - 1);
            }
            trick.Add(Tuple.Create(player, card));
            if (trick.Count < 4) { CurrentPlayer = 1 - player; return; }
            Suit ledSuit = trick[0].Item2.Suit;
            int winner = trick.Where(item => item.Item2.Suit == ledSuit)
                .OrderByDescending(item => Strength(item.Item2)).First().Item1;
            tricks[winner]++;
            trick.Clear();
            if (tricks.Sum() == 13) FinishDeal();
            else CurrentPlayer = winner;
        }

        private void BeginPlay()
        {
            phase = "play";
            if (!highGame) CurrentPlayer = 1 - dealer;
            else CurrentPlayer = 1 - highBidder!.Value;
        }

        private IEnumerable<Card> Available(int player) => hands[player].Concat(layouts[player]
            .Where(column => column.Count > 0).Select(column => column[column.Count - 1]));

        private void FinishDeal()
        {
            if (highGame)
            {
                int bidder = highBidder!.Value;
                if (tricks[bidder] >= 7) scores[bidder] += tricks[bidder] - 6;
                else scores[1 - bidder] += (tricks[1 - bidder] - 6) * 2;
            }
            else
            {
                int winner = tricks[0] < tricks[1] ? 0 : 1;
                scores[winner] += 7 - tricks[winner];
            }
            if (scores.Max() >= targetScore) finished = true;
            else StartDeal();
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase.StartsWith("bid", StringComparison.Ordinal))
            {
                int highCards = Available(player).Count(card => Strength(card) >= 11);
                return actions.First(action => action.Kind == (highCards >= 5 ? "bid_high" : "bid_low"));
            }
            return highGame
                ? actions.OrderByDescending(action => Strength(action.Card!.Value)).First()
                : actions.OrderBy(action => Strength(action.Card!.Value)).First();
        }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 2).Where(player => scores[player] == high),
                scores.Select(value => (double)value), "first to thirteen high/low game points", TurnCount);
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            string publicLayouts = string.Join(" ", Enumerable.Range(0, 2).Select(player => "P" + player + "[" +
                string.Join(" ", layouts[player].Select(column => column.Count == 0 ? "-" :
                    column[column.Count - 1] + (column.Count > 1 ? "/?" : ""))) + "]"));
            return $"phase={phase} contract={(highGame ? "high" : "low")} bidder={(highBidder.HasValue ? "P" + highBidder.Value : "-")} " +
                $"trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] tricks=[{string.Join(",", tricks)}] " +
                $"scores=[{string.Join(",", scores)}] hand_counts=[{hands[0].Count},{hands[1].Count}]\n" +
                $"layouts: {publicLayouts}\nyour hand: {string.Join(" ", hands[viewer])}";
        }

        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static Card Pop(List<Card> cards)
        {
            Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card;
        }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("norwegian_whist", "ノルウェージャンホイスト", 2, 2, "high-low open trick-taking",
                "各自10枚の手札と8組の伏札＋表札を使い、high/lowをビッドして1人2枚ずつの13トリックを行う13点戦。",
                "Pagat Two Player Whist", new Dictionary<string, string> { { "target_score", "13" } }),
            (players, random, options) => new NorwegianWhistGame(players, random, options));
    }

    public sealed class GoldmineGame : GameBase
    {
        private readonly DeterministicRandom rng;
        private readonly int targetScore;
        private readonly List<List<Card>> hands = new List<List<Card>>
        {
            new List<Card>(), new List<Card>()
        };
        private readonly List<Card> stock = new List<Card>();
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly Card?[,] knowledge = new Card?[2, 6];
        private readonly int[] scores = new int[2];
        private Card[] prizes = Array.Empty<Card>();
        private Suit trump;
        private int dealer = 1;
        private int prizeIndex;
        private int firstActor;
        private string phase = "a_first";
        private string? firstChoice;
        private bool finished;

        public override string GameId => "goldmine";
        public override string Name => "ゴールドマイン";

        public GoldmineGame(int players, DeterministicRandom rng,
            IReadOnlyDictionary<string, string> options)
        {
            Players = 2;
            this.rng = rng;
            targetScore = Math.Max(1, options.Integer("target_score", 30));
            StartDeal();
        }

        private void StartDeal()
        {
            hands[0].Clear(); hands[1].Clear(); stock.Clear(); trick.Clear();
            for (int player = 0; player < 2; player++) for (int index = 0; index < 6; index++) knowledge[player, index] = null;
            prizes = Cards.Shuffled(Enumerable.Range(2, 6).Select(rank => new Card(Suit.Spades, rank)), rng).ToArray();
            stock.AddRange(Cards.Shuffled(new[] { Suit.Hearts, Suit.Clubs, Suit.Diamonds }
                .SelectMany(suit => Enumerable.Range(2, 6).Select(rank => new Card(suit, rank))), rng));
            dealer = 1 - dealer;
            for (int round = 0; round < 6; round++)
                for (int offset = 1; offset <= 2; offset++) hands[(dealer + offset) % 2].Add(Pop(stock));
            Card indicator = Pop(stock);
            trump = indicator.Suit;
            stock.Insert(0, indicator);
            prizeIndex = 0;
            firstActor = 1 - dealer;
            CurrentPlayer = firstActor;
            firstChoice = null;
            phase = "a_first";
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "a_first")
                return InspectActions().Concat(hands[actual].Select(card => new Action("exchange", card))).ToArray();
            if (phase == "a_second")
                return firstChoice == "inspect" ? hands[actual].Select(card => new Action("exchange", card)).ToArray() :
                    InspectActions().ToArray();
            IEnumerable<Card> cards = hands[actual];
            return cards.Select(card => new Action("play", card)).ToArray();
        }

        private IEnumerable<Action> InspectActions() => Enumerable.Range(prizeIndex, 6 - prizeIndex)
            .Select(index => new Action("inspect", target: index));

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null);
            Guard.Legal(action, LegalActions(player));
            TurnCount++;
            if (phase == "a_first" || phase == "a_second")
            {
                if (action.Kind == "inspect") knowledge[player, action.Target!.Value] = prizes[action.Target.Value];
                else
                {
                    hands[player].Remove(action.Card!.Value);
                    hands[player].Add(Pop(stock));
                }
                if (phase == "a_first")
                {
                    firstChoice = action.Kind;
                    phase = "a_second";
                    CurrentPlayer = 1 - player;
                }
                else
                {
                    phase = "play";
                    CurrentPlayer = player;
                }
                return;
            }
            Card card = action.Card!.Value;
            hands[player].Remove(card);
            trick.Add(Tuple.Create(player, card));
            if (trick.Count == 1) { CurrentPlayer = 1 - player; return; }
            int winner = TrickWinner();
            int loser = 1 - winner;
            scores[winner] += prizes[prizeIndex].Rank;
            knowledge[0, prizeIndex] = prizes[prizeIndex];
            knowledge[1, prizeIndex] = prizes[prizeIndex];
            prizeIndex++;
            trick.Clear();
            if (prizeIndex == 6)
            {
                if (scores.Max() >= targetScore) finished = true;
                else StartDeal();
                return;
            }
            firstActor = loser;
            CurrentPlayer = loser;
            firstChoice = null;
            phase = "a_first";
        }

        private int TrickWinner()
        {
            Card first = trick[0].Item2, second = trick[1].Item2;
            if (first.Suit == second.Suit) return second.Rank > first.Rank ? trick[1].Item1 : trick[0].Item1;
            if (second.Suit == trump && first.Suit != trump) return trick[1].Item1;
            return trick[0].Item1;
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            Action[] inspect = actions.Where(action => action.Kind == "inspect").ToArray();
            if (inspect.Length > 0) return inspect.FirstOrDefault(action => !knowledge[player, action.Target!.Value].HasValue).Kind != null
                ? inspect.First(action => !knowledge[player, action.Target!.Value].HasValue) : inspect[0];
            if (actions[0].Kind == "exchange")
                return actions.OrderBy(action => action.Card!.Value.Suit == trump ? 1 : 0)
                    .ThenBy(action => action.Card!.Value.Rank).First();
            return actions.OrderByDescending(action => action.Card!.Value.Suit == trump ? 100 + action.Card.Value.Rank : action.Card.Value.Rank).First();
        }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 2).Where(player => scores[player] == high),
                scores.Select(value => (double)value), "first to " + targetScore + " gold points", TurnCount);
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            string prizeView = string.Join(" ", Enumerable.Range(0, 6).Select(index => index < prizeIndex
                ? prizes[index].Rank.ToString(CultureInfo.InvariantCulture)
                : knowledge[viewer, index].HasValue ? knowledge[viewer, index]!.Value.Rank.ToString(CultureInfo.InvariantCulture) : "?"));
            return $"phase={phase} trump={Card.SuitCode(trump)} stock={stock.Count} prizes=[{prizeView}] " +
                $"trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] scores=[{string.Join(",", scores)}] " +
                $"hand_counts=[{hands[0].Count},{hands[1].Count}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }

        private static Card Pop(List<Card> cards)
        {
            Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card;
        }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("goldmine", "ゴールドマイン", 2, 2, "information trick-taking",
                "2～7の3スートで、毎トリック前に一方が金塊調査、他方が手札交換を行い、メイフォローで伏せた2～7点札を争う30点戦。",
                "Gokurakism Goldmine", new Dictionary<string, string> { { "target_score", "30" } }),
            (players, random, options) => new GoldmineGame(players, random, options));
    }
}
