using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    public sealed class SkatGame : GameBase
    {
        private static readonly int[] BidValues = new[] { 23, 35, 46, 59 }
            .Concat(new[] { 9, 10, 11, 12, 24 }.SelectMany(baseValue =>
                Enumerable.Range(2, 18).Select(multiplier => baseValue * multiplier)))
            .Distinct().OrderBy(value => value).ToArray();

        private readonly DeterministicRandom rng;
        private readonly int sessionDeals;
        private readonly List<List<Card>> hands = new List<List<Card>>
        {
            new List<Card>(), new List<Card>(), new List<Card>()
        };
        private readonly List<List<Card>> captured = new List<List<Card>>
        {
            new List<Card>(), new List<Card>(), new List<Card>()
        };
        private readonly List<Card> skat = new List<Card>();
        private readonly List<Card> discard = new List<Card>();
        private readonly List<Card> declarerOwned = new List<Card>();
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly bool[] passed = new bool[3];
        private readonly int[] tricks = new int[3];
        private readonly int[] scores = new int[3];
        private int dealer = 2;
        private int declarer = -1;
        private int highBidder = -1;
        private int? highBid;
        private int dealsPlayed;
        private bool handGame;
        private string contract = "";
        private string phase = "auction";
        private bool finished;

        public override string GameId => "skat";
        public override string Name => "スカート";

        public SkatGame(int players, DeterministicRandom rng,
            IReadOnlyDictionary<string, string> options)
        {
            Players = 3; this.rng = rng;
            sessionDeals = Math.Max(1, options.Integer("deals", 18)); StartDeal();
        }

        private void StartDeal()
        {
            foreach (List<Card> hand in hands) hand.Clear();
            foreach (List<Card> pile in captured) pile.Clear();
            skat.Clear(); discard.Clear(); declarerOwned.Clear(); trick.Clear();
            Array.Clear(passed, 0, 3); Array.Clear(tricks, 0, 3);
            List<Card> deck = Cards.Shuffled(Cards.StandardDeck(new[] { 1, 7, 8, 9, 10, 11, 12, 13 }), rng);
            dealer = (dealer + 1) % 3;
            DealBatch(deck, 3); skat.Add(Pop(deck)); skat.Add(Pop(deck)); DealBatch(deck, 4); DealBatch(deck, 3);
            declarer = -1; highBidder = -1; highBid = null; handGame = false; contract = "";
            phase = "auction"; CurrentPlayer = (dealer + 1) % 3;
        }

        private void DealBatch(List<Card> deck, int count)
        {
            for (int round = 0; round < count; round++)
                for (int offset = 1; offset <= 3; offset++) hands[(dealer + offset) % 3].Add(Pop(deck));
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "auction")
            {
                var actions = new List<Action> { new Action("pass") };
                actions.AddRange(BidValues.Where(value => !highBid.HasValue || value > highBid.Value)
                    .Select(value => new Action("bid", value: value.ToString())));
                return actions;
            }
            if (phase == "choose_skat") return new[] { new Action("take_skat"), new Action("hand_game") };
            if (phase == "discard")
            {
                if (discard.Count == 2) return new[] { new Action("finish_discard") };
                return hands[actual].Select(card => new Action("discard_to_skat", card)).ToArray();
            }
            if (phase == "declare_contract") return ContractActions();
            IEnumerable<Card> cards = hands[actual];
            if (trick.Count > 0)
            {
                Card lead = trick[0].Item2;
                Card[] follow = IsTrump(lead)
                    ? cards.Where(IsTrump).ToArray()
                    : cards.Where(card => !IsTrump(card) && card.Suit == lead.Suit).ToArray();
                if (follow.Length > 0) cards = follow;
            }
            return cards.Select(card => new Action("play", card)).ToArray();
        }

        private IReadOnlyList<Action> ContractActions()
        {
            var values = new List<string> { "D", "H", "S", "C", "G", "N", "NO" };
            if (handGame)
            {
                foreach (string game in new[] { "D", "H", "S", "C", "G" })
                {
                    values.Add(game + ":SCH"); values.Add(game + ":SW"); values.Add(game + ":O");
                }
            }
            return values.Select(value => new Action("declare_contract", value: value)).ToArray();
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "auction") { ApplyAuction(player, action); return; }
            if (phase == "choose_skat")
            {
                handGame = action.Kind == "hand_game";
                if (handGame) { phase = "declare_contract"; CurrentPlayer = declarer; }
                else
                {
                    hands[declarer].AddRange(skat); skat.Clear(); phase = "discard"; CurrentPlayer = declarer;
                }
                return;
            }
            if (phase == "discard")
            {
                if (action.Kind == "discard_to_skat")
                {
                    Card card = action.Card!.Value; hands[player].Remove(card); discard.Add(card); return;
                }
                skat.AddRange(discard); discard.Clear(); phase = "declare_contract"; CurrentPlayer = declarer; return;
            }
            if (phase == "declare_contract")
            {
                contract = action.Value!; declarerOwned.AddRange(hands[declarer]); declarerOwned.AddRange(skat);
                phase = "play"; CurrentPlayer = (dealer + 1) % 3; return;
            }
            Card played = action.Card!.Value; hands[player].Remove(played); trick.Add(Tuple.Create(player, played));
            if (trick.Count < 3) { CurrentPlayer = (player + 1) % 3; return; }
            int winner = TrickWinner(); tricks[winner]++; captured[winner].AddRange(trick.Select(item => item.Item2)); trick.Clear();
            if (IsNull && winner == declarer) { FinishPlayedDeal(); return; }
            if (hands.All(hand => hand.Count == 0)) FinishPlayedDeal();
            else CurrentPlayer = winner;
        }

        private void ApplyAuction(int player, Action action)
        {
            if (action.Kind == "pass") passed[player] = true;
            else { highBid = int.Parse(action.Value!); highBidder = player; }
            int passedCount = passed.Count(value => value);
            if (!highBid.HasValue && passedCount == 3) { CompletePassedDeal(); return; }
            if (highBid.HasValue && passedCount >= 2)
            {
                declarer = highBidder; phase = "choose_skat"; CurrentPlayer = declarer; return;
            }
            CurrentPlayer = NextActive(player);
        }

        private int NextActive(int player)
        {
            for (int offset = 1; offset <= 3; offset++)
            {
                int candidate = (player + offset) % 3;
                if (!passed[candidate]) return candidate;
            }
            throw new InvalidOperationException("No active bidder.");
        }

        private void CompletePassedDeal()
        {
            dealsPlayed++;
            if (dealsPlayed >= sessionDeals) finished = true;
            else StartDeal();
        }

        private bool IsNull => contract.StartsWith("N", StringComparison.Ordinal);
        private string ContractCode => contract.Split(':')[0];
        private bool AnnouncedSchneider => contract.Contains(":SCH") || contract.Contains(":SW") || contract.Contains(":O");
        private bool AnnouncedSchwarz => contract.Contains(":SW") || contract.Contains(":O");
        private bool AnnouncedOpen => contract.Contains(":O");

        private int TrickWinner()
        {
            Card lead = trick[0].Item2;
            bool trumped = !IsNull && trick.Any(item => IsTrump(item.Item2));
            IEnumerable<Tuple<int, Card>> eligible = trumped
                ? trick.Where(item => IsTrump(item.Item2))
                : trick.Where(item => !IsTrump(item.Item2) && item.Item2.Suit == lead.Suit);
            return eligible.OrderByDescending(item => Strength(item.Item2)).First().Item1;
        }

        private bool IsTrump(Card card)
        {
            if (IsNull) return false;
            if (card.Rank == 11) return true;
            return ContractCode != "G" && card.Suit == Card.ParseSuit(ContractCode);
        }

        private int Strength(Card card)
        {
            if (IsNull) return card.Rank == 1 ? 14 : card.Rank;
            if (card.Rank == 11)
                return card.Suit == Suit.Clubs ? 100 : card.Suit == Suit.Spades ? 99 : card.Suit == Suit.Hearts ? 98 : 97;
            return card.Rank == 1 ? 96 : card.Rank == 10 ? 95 : card.Rank == 13 ? 94 :
                card.Rank == 12 ? 93 : card.Rank;
        }

        private void FinishPlayedDeal()
        {
            bool won;
            int value;
            if (IsNull)
            {
                won = tricks[declarer] == 0;
                value = ContractCode == "NO" ? (handGame ? 59 : 46) : (handGame ? 35 : 23);
            }
            else
            {
                int declarerPoints = captured[declarer].Sum(CardPoints) + skat.Sum(CardPoints);
                bool basicWin = declarerPoints >= 61;
                won = basicWin && (!AnnouncedSchneider || declarerPoints >= 90) &&
                    (!AnnouncedSchwarz || tricks[declarer] == 10);
                value = SuitGrandValue(declarerPoints, won);
            }
            int required = highBid ?? 18;
            bool overbid = value < required;
            if (won && !overbid) scores[declarer] += value;
            else
            {
                int lossValue = IsNull ? Math.Max(value, required) :
                    Math.Max(value, ((required + BaseValue() - 1) / BaseValue()) * BaseValue());
                scores[declarer] -= 2 * lossValue;
            }
            dealsPlayed++;
            if (dealsPlayed >= sessionDeals) finished = true;
            else StartDeal();
        }

        private int SuitGrandValue(int declarerPoints, bool won)
        {
            int multiplier = Matadors() + 1;
            if (handGame) multiplier++;
            bool declarerSchneider = declarerPoints <= 30;
            bool opponentsSchneider = 120 - declarerPoints <= 30;
            bool schneider = declarerSchneider || opponentsSchneider || AnnouncedSchneider;
            bool schwarz = tricks[declarer] == 0 || tricks[declarer] == 10 || AnnouncedSchwarz;
            if (schneider) multiplier++;
            if (AnnouncedSchneider) multiplier++;
            if (schwarz) multiplier++;
            if (AnnouncedSchwarz) multiplier++;
            if (AnnouncedOpen) multiplier++;
            return BaseValue() * multiplier;
        }

        private int Matadors()
        {
            List<Card> order = new List<Card>
            {
                new Card(Suit.Clubs, 11), new Card(Suit.Spades, 11),
                new Card(Suit.Hearts, 11), new Card(Suit.Diamonds, 11)
            };
            if (ContractCode != "G")
            {
                Suit suit = Card.ParseSuit(ContractCode);
                foreach (int rank in new[] { 1, 10, 13, 12, 9, 8, 7 }) order.Add(new Card(suit, rank));
            }
            bool withTop = declarerOwned.Contains(order[0]);
            int count = 0;
            foreach (Card card in order)
            {
                if (declarerOwned.Contains(card) != withTop) break;
                count++;
            }
            return Math.Max(1, count);
        }

        private int BaseValue() => ContractCode == "D" ? 9 : ContractCode == "H" ? 10 :
            ContractCode == "S" ? 11 : ContractCode == "C" ? 12 : 24;

        private static int CardPoints(Card card) => card.Rank == 1 ? 11 : card.Rank == 10 ? 10 :
            card.Rank == 13 ? 4 : card.Rank == 12 ? 3 : card.Rank == 11 ? 2 : 0;

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "auction")
            {
                if (!highBid.HasValue) return actions.First(action => action.Kind == "bid" && action.Value == "18");
                return actions.First(action => action.Kind == "pass");
            }
            if (phase == "choose_skat") return actions.First(action => action.Kind == "take_skat");
            if (phase == "discard") return discard.Count == 2 ? actions[0]
                : actions.OrderBy(action => CardPoints(action.Card!.Value)).ThenBy(action => StrengthForChoice(action.Card!.Value)).First();
            if (phase == "declare_contract")
            {
                string bestSuit = new[] { "D", "H", "S", "C" }
                    .OrderByDescending(code => hands[player].Count(card => card.Suit == Card.ParseSuit(code) || card.Rank == 11)).First();
                return actions.First(action => action.Value == bestSuit);
            }
            return player == declarer
                ? actions.OrderByDescending(action => Strength(action.Card!.Value)).First()
                : actions.OrderBy(action => Strength(action.Card!.Value)).First();
        }

        private static int StrengthForChoice(Card card) => card.Rank == 1 ? 14 : card.Rank;

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 3).Where(player => scores[player] == high),
                scores.Select(value => (double)value), sessionDeals + "-deal Skat session", TurnCount);
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            string openHand = AnnouncedOpen && declarer >= 0 ? $" open_hand_P{declarer}=[{string.Join(" ", hands[declarer])}]" : "";
            return $"phase={phase} deal={dealsPlayed + 1}/{sessionDeals} dealer=P{dealer} bid={(highBid.HasValue ? highBid.Value.ToString() : "-")} " +
                $"declarer={(declarer < 0 ? "-" : "P" + declarer)} hand={handGame} contract={(contract == "" ? "-" : contract)} skat={skat.Count} discard={discard.Count}/2 " +
                $"trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] tricks=[{string.Join(",", tricks)}] " +
                $"scores=[{string.Join(",", scores)}] hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]{openHand}\n" +
                $"your hand: {string.Join(" ", hands[viewer])}";
        }

        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("skat", "スカート", 3, 3, "auction trick-taking",
                "7～Aの32枚を10枚ずつ＋skat2枚に配る。数値auction後にskat/handとsuit/grand/nullを選び、120 card pointsとmatador倍率で採点する。",
                "International Skat rules / Pagat", new Dictionary<string, string> { { "deals", "18" } }),
            (players, random, options) => new SkatGame(players, random, options));
    }
}
