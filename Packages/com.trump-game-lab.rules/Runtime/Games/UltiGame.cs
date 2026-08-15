using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    public sealed class UltiGame : GameBase
    {
        private sealed class ContractDef
        {
            public string Id { get; }
            public int Rank { get; }
            public bool Heart { get; }
            public bool NoTrump { get; }
            public bool Open { get; }
            public bool Betli { get; }
            public bool Simple { get; }
            public bool Forty { get; }
            public bool Twenty { get; }
            public bool Ulti { get; }
            public bool Durch { get; }

            public ContractDef(string id, int rank, bool heart = false, bool noTrump = false,
                bool open = false, bool betli = false, bool simple = false, bool forty = false,
                bool twenty = false, bool ulti = false, bool durch = false)
            {
                Id = id; Rank = rank; Heart = heart; NoTrump = noTrump; Open = open;
                Betli = betli; Simple = simple; Forty = forty; Twenty = twenty;
                Ulti = ulti; Durch = durch;
            }
        }

        private static readonly ContractDef[] Contracts = BuildContracts();
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
        private readonly List<HashSet<Suit>> marriages = new List<HashSet<Suit>>
        {
            new HashSet<Suit>(), new HashSet<Suit>(), new HashSet<Suit>()
        };
        private readonly List<Card> talon = new List<Card>();
        private readonly List<Card> pendingDiscard = new List<Card>();
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly int[] tricks = new int[3];
        private readonly int[] scores = new int[3];
        private int dealer = 0;
        private int declarer = -1;
        private int highBidder = -1;
        private int consecutivePasses;
        private int marriagePlayers;
        private int dealsPlayed;
        private ContractDef? highContract;
        private Suit? trump;
        private bool openExposed;
        private bool lastUltiWon;
        private bool lastTrumpSevenPlayed;
        private int lastWinner = -1;
        private string phase = "initial_bid";
        private bool finished;

        public override string GameId => "ulti";
        public override string Name => "ウルティ";

        public UltiGame(int players, DeterministicRandom rng,
            IReadOnlyDictionary<string, string> options)
        {
            Players = 3; this.rng = rng;
            sessionDeals = Math.Max(1, options.Integer("deals", 12)); StartDeal();
        }

        private static ContractDef[] BuildContracts()
        {
            var contracts = new List<ContractDef>
            {
                new ContractDef("minor_game", 1, simple: true),
                new ContractDef("heart_game", 2, heart: true, simple: true),
                new ContractDef("minor_40_100", 4, forty: true),
                new ContractDef("minor_ulti", 5, simple: true, ulti: true),
                new ContractDef("betli", 5, noTrump: true, betli: true),
                new ContractDef("durchmars", 6, noTrump: true, durch: true),
                new ContractDef("minor_40_100_ulti", 8, forty: true, ulti: true),
                new ContractDef("heart_40_100", 8, heart: true, forty: true),
                new ContractDef("minor_20_100", 8, twenty: true),
                new ContractDef("heart_ulti", 10, heart: true, simple: true, ulti: true),
                new ContractDef("minor_40_100_durch", 10, forty: true, durch: true),
                new ContractDef("minor_ulti_durch", 10, ulti: true, durch: true),
                new ContractDef("heart_betli", 10, noTrump: true, betli: true, heart: true),
                new ContractDef("minor_20_100_ulti", 12, twenty: true, ulti: true),
                new ContractDef("heart_durchmars", 12, noTrump: true, durch: true, heart: true),
                new ContractDef("minor_40_100_ulti_durch", 14, forty: true, ulti: true, durch: true),
                new ContractDef("minor_20_100_durch", 14, twenty: true, durch: true),
                new ContractDef("heart_40_100_ulti", 16, heart: true, forty: true, ulti: true),
                new ContractDef("heart_20_100", 16, heart: true, twenty: true),
                new ContractDef("minor_20_100_ulti_durch", 18, twenty: true, ulti: true, durch: true),
                new ContractDef("heart_40_100_durch", 20, heart: true, forty: true, durch: true),
                new ContractDef("heart_ulti_durch", 20, heart: true, ulti: true, durch: true),
                new ContractDef("open_betli", 20, noTrump: true, open: true, betli: true),
                new ContractDef("heart_20_100_ulti", 24, heart: true, twenty: true, ulti: true),
                new ContractDef("open_durchmars", 24, noTrump: true, open: true, durch: true),
                new ContractDef("heart_40_100_ulti_durch", 28, heart: true, forty: true, ulti: true, durch: true),
                new ContractDef("heart_20_100_durch", 28, heart: true, twenty: true, durch: true),
                new ContractDef("minor_40_100_open_durch", 28, open: true, forty: true, durch: true),
                new ContractDef("minor_ulti_open_durch", 28, open: true, ulti: true, durch: true),
                new ContractDef("minor_40_100_ulti_open_durch", 32, open: true, forty: true, ulti: true, durch: true),
                new ContractDef("heart_40_100_open_durch", 32, heart: true, open: true, forty: true, durch: true),
                new ContractDef("heart_ulti_open_durch", 32, heart: true, open: true, ulti: true, durch: true),
                new ContractDef("minor_20_100_open_durch", 32, open: true, twenty: true, durch: true),
                new ContractDef("heart_20_100_ulti_durch", 36, heart: true, twenty: true, ulti: true, durch: true),
                new ContractDef("minor_20_100_ulti_open_durch", 36, open: true, twenty: true, ulti: true, durch: true),
                new ContractDef("heart_40_100_ulti_open_durch", 40, heart: true, open: true, forty: true, ulti: true, durch: true),
                new ContractDef("heart_20_100_open_durch", 40, heart: true, open: true, twenty: true, durch: true),
                new ContractDef("heart_20_100_ulti_open_durch", 48, heart: true, open: true, twenty: true, ulti: true, durch: true)
            };
            return contracts.OrderBy(contract => contract.Rank).ToArray();
        }

        private void StartDeal()
        {
            foreach (List<Card> hand in hands) hand.Clear();
            foreach (List<Card> pile in captured) pile.Clear();
            foreach (HashSet<Suit> set in marriages) set.Clear();
            talon.Clear(); pendingDiscard.Clear(); trick.Clear(); Array.Clear(tricks, 0, 3);
            List<Card> deck = Cards.Shuffled(Cards.StandardDeck(new[] { 1, 7, 8, 9, 10, 11, 12, 13 }), rng);
            dealer = (dealer + 2) % 3;
            int first = (dealer + 2) % 3;
            for (int round = 0; round < 10; round++)
                for (int offset = 1; offset <= 3; offset++) hands[(dealer - offset + 6) % 3].Add(Pop(deck));
            hands[first].Add(Pop(deck)); hands[first].Add(Pop(deck));
            declarer = -1; highBidder = -1; highContract = null; consecutivePasses = 0;
            marriagePlayers = 0; trump = null; openExposed = false; lastUltiWon = false;
            lastTrumpSevenPlayed = false; lastWinner = -1; phase = "initial_bid"; CurrentPlayer = first;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "initial_bid" || phase == "raise_bid")
                return HigherContracts().Select(contract => new Action("bid", value: contract.Id)).ToArray();
            if (phase == "auction")
            {
                var actions = new List<Action> { new Action("pass") };
                if (HigherContracts().Any()) actions.Add(new Action("take_talon"));
                actions.AddRange(HigherContracts().Select(contract => new Action("bid_without_talon", value: contract.Id)));
                return actions;
            }
            if (phase == "discard")
            {
                if (pendingDiscard.Count == 2) return new[] { new Action("finish_discard") };
                return hands[actual].Select(card => new Action("discard_to_talon", card)).ToArray();
            }
            if (phase == "choose_trump")
            {
                IEnumerable<Suit> suits = highContract!.Heart
                    ? new[] { Suit.Hearts }
                    : new[] { Suit.Clubs, Suit.Diamonds, Suit.Spades };
                return suits.Select(suit => new Action("choose_trump", value: Card.SuitCode(suit))).ToArray();
            }
            if (phase == "marriages")
            {
                var actions = new List<Action> { new Action("finish_marriages") };
                if (!highContract!.NoTrump && MarriageDeclarationsAllowed(actual))
                    actions.AddRange(Enum.GetValues(typeof(Suit)).Cast<Suit>()
                        .Where(suit => !marriages[actual].Contains(suit) && HasMarriage(actual, suit))
                        .Select(suit => new Action("declare_marriage", value: Card.SuitCode(suit))));
                return actions;
            }
            return LegalPlays(actual);
        }

        private IEnumerable<ContractDef> HigherContracts() => Contracts
            .Where(contract => highContract == null || contract.Rank > highContract.Rank);

        private bool MarriageDeclarationsAllowed(int player) => player == declarer ||
            !(highContract!.Forty || highContract.Twenty || highContract.Durch);

        private bool HasMarriage(int player, Suit suit) =>
            hands[player].Contains(new Card(suit, 13)) && hands[player].Contains(new Card(suit, 12));

        private IReadOnlyList<Action> LegalPlays(int player)
        {
            IEnumerable<Card> cards = hands[player];
            if (trick.Count == 0) return cards.Select(card => new Action("play", card)).ToArray();
            Suit led = trick[0].Item2.Suit;
            Card[] follow = cards.Where(card => card.Suit == led).ToArray();
            if (follow.Length > 0)
            {
                Card winningLed = trick.Where(item => item.Item2.Suit == led)
                    .OrderByDescending(item => Strength(item.Item2)).First().Item2;
                Card[] beating = follow.Where(card => Strength(card) > Strength(winningLed)).ToArray();
                cards = beating.Length > 0 ? beating : follow;
            }
            else if (trump.HasValue)
            {
                Card[] trumps = cards.Where(card => card.Suit == trump.Value).ToArray();
                if (trumps.Length > 0)
                {
                    Card? winningTrump = trick.Where(item => item.Item2.Suit == trump.Value)
                        .Select(item => (Card?)item.Item2).OrderByDescending(card => card.HasValue ? Strength(card.Value) : -1).FirstOrDefault();
                    Card[] beating = winningTrump.HasValue
                        ? trumps.Where(card => Strength(card) > Strength(winningTrump.Value)).ToArray()
                        : trumps;
                    cards = beating.Length > 0 ? beating : trumps;
                }
            }
            if (player == declarer && highContract!.Ulti && trump.HasValue)
            {
                Card trumpSeven = new Card(trump.Value, 7);
                Card[] alternatives = cards.Where(card => card != trumpSeven).ToArray();
                if (alternatives.Length > 0) cards = alternatives;
            }
            return cards.Select(card => new Action("play", card)).ToArray();
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "initial_bid" || phase == "raise_bid")
            {
                highContract = FindContract(action.Value!); highBidder = player;
                phase = "discard"; pendingDiscard.Clear(); CurrentPlayer = player; return;
            }
            if (phase == "auction")
            {
                if (action.Kind == "pass")
                {
                    consecutivePasses++;
                    if (consecutivePasses >= 3) { FinishAuction(); return; }
                    CurrentPlayer = NextCounterClockwise(player); return;
                }
                if (action.Kind == "take_talon")
                {
                    hands[player].AddRange(talon); talon.Clear(); phase = "raise_bid"; CurrentPlayer = player; return;
                }
                highContract = FindContract(action.Value!); highBidder = player; consecutivePasses = 0;
                CurrentPlayer = NextCounterClockwise(player); return;
            }
            if (phase == "discard")
            {
                if (action.Kind == "discard_to_talon")
                {
                    Card card = action.Card!.Value; hands[player].Remove(card); pendingDiscard.Add(card); return;
                }
                talon.AddRange(pendingDiscard); pendingDiscard.Clear(); consecutivePasses = 0;
                phase = "auction"; CurrentPlayer = NextCounterClockwise(player); return;
            }
            if (phase == "choose_trump")
            {
                trump = Card.ParseSuit(action.Value!); BeginMarriages(); return;
            }
            if (phase == "marriages")
            {
                if (action.Kind == "declare_marriage")
                {
                    marriages[player].Add(Card.ParseSuit(action.Value!)); return;
                }
                marriagePlayers++;
                if (marriagePlayers >= 3) { phase = "play"; CurrentPlayer = declarer; }
                else CurrentPlayer = NextCounterClockwise(player);
                return;
            }
            Card played = action.Card!.Value; hands[player].Remove(played); trick.Add(Tuple.Create(player, played));
            if (trick.Count < 3) { CurrentPlayer = NextCounterClockwise(player); return; }
            int winner = TrickWinner(); tricks[winner]++; captured[winner].AddRange(trick.Select(item => item.Item2));
            bool last = hands.All(hand => hand.Count == 0);
            if (last)
            {
                lastWinner = winner;
                Tuple<int, Card>? seven = trick.FirstOrDefault(item => trump.HasValue && item.Item2.Suit == trump.Value && item.Item2.Rank == 7);
                lastTrumpSevenPlayed = seven != null;
                lastUltiWon = seven != null && seven.Item1 == declarer && winner == declarer;
            }
            trick.Clear();
            if (tricks.Sum() == 1 && highContract!.Open) openExposed = true;
            if (last) FinishDeal(); else CurrentPlayer = winner;
        }

        private void FinishAuction()
        {
            declarer = highBidder;
            if (highContract!.NoTrump) { trump = null; BeginMarriages(); }
            else { phase = "choose_trump"; CurrentPlayer = declarer; }
        }

        private void BeginMarriages()
        {
            phase = "marriages"; marriagePlayers = 0; CurrentPlayer = declarer;
        }

        private int NextCounterClockwise(int player) => (player + 2) % 3;

        private int TrickWinner()
        {
            Suit led = trick[0].Item2.Suit;
            IEnumerable<Tuple<int, Card>> eligible = trump.HasValue && trick.Any(item => item.Item2.Suit == trump.Value)
                ? trick.Where(item => item.Item2.Suit == trump.Value)
                : trick.Where(item => item.Item2.Suit == led);
            return eligible.OrderByDescending(item => Strength(item.Item2)).First().Item1;
        }

        private int Strength(Card card)
        {
            if (highContract != null && highContract.NoTrump)
                return card.Rank == 1 ? 14 : card.Rank;
            return card.Rank == 1 ? 14 : card.Rank == 10 ? 13 : card.Rank == 13 ? 12 :
                card.Rank == 12 ? 11 : card.Rank == 11 ? 10 : card.Rank;
        }

        private void FinishDeal()
        {
            ContractDef bid = highContract!;
            int declarerBase = captured[declarer].Sum(CardPoints) + (lastWinner == declarer ? 10 : 0);
            int defendersBase = Enumerable.Range(0, 3).Where(player => player != declarer)
                .Sum(player => captured[player].Sum(CardPoints)) + talon.Sum(CardPoints) + (lastWinner == declarer ? 0 : 10);
            int declarerMarriage = MarriagePoints(declarer);
            int defendersMarriage = Enumerable.Range(0, 3).Where(player => player != declarer).Sum(MarriagePoints);
            int declarerPoints = declarerBase + declarerMarriage;
            int defendersPoints = defendersBase + defendersMarriage;
            int net = 0;

            if (bid.Betli) net += tricks[declarer] == 0 ? bid.Rank : -bid.Rank;
            else
            {
                if (bid.Simple)
                {
                    int value = bid.Heart ? 2 : 1;
                    if (Math.Max(declarerPoints, defendersPoints) >= 100) value *= 2;
                    if (tricks[declarer] == 10) net += bid.Heart ? 6 : 3;
                    else if (tricks[declarer] == 0) net -= bid.Heart ? 6 : 3;
                    else net += declarerPoints > defendersPoints ? value : -value;
                }
                if (bid.Forty)
                {
                    int value = bid.Heart ? 8 : 4;
                    bool hasForty = trump.HasValue && marriages[declarer].Contains(trump.Value);
                    net += hasForty && declarerBase + 40 >= 100 ? value : -value;
                }
                if (bid.Twenty)
                {
                    int value = bid.Heart ? 16 : 8;
                    bool hasTwenty = trump.HasValue && marriages[declarer].Any(suit => suit != trump.Value);
                    net += hasTwenty && declarerBase + 20 >= 100 ? value : -value;
                }
                if (bid.Ulti)
                {
                    int value = bid.Heart ? 8 : 4;
                    net += lastUltiWon ? value : -2 * value;
                }
                else if (lastTrumpSevenPlayed)
                    net += lastUltiWon ? (bid.Heart ? 4 : 2) : -(bid.Heart ? 8 : 4);
                if (bid.Durch)
                {
                    int value = bid.Open ? 24 : bid.Heart ? 12 : 6;
                    net += tricks[declarer] == 10 ? value : -value;
                }
            }
            scores[declarer] += 2 * net;
            for (int player = 0; player < 3; player++) if (player != declarer) scores[player] -= net;
            dealsPlayed++;
            if (dealsPlayed >= sessionDeals) finished = true;
            else StartDeal();
        }

        private int MarriagePoints(int player) => marriages[player].Sum(suit => trump.HasValue && suit == trump.Value ? 40 : 20);
        private static int CardPoints(Card card) => card.Rank == 1 || card.Rank == 10 ? 10 : 0;

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "initial_bid") return actions.First(action => action.Value == "minor_game");
            if (phase == "auction") return actions.First(action => action.Kind == "pass");
            if (phase == "raise_bid") return actions.OrderBy(action => FindContract(action.Value!).Rank).First();
            if (phase == "discard") return pendingDiscard.Count == 2 ? actions[0]
                : actions.OrderBy(action => CardPoints(action.Card!.Value)).ThenBy(action => ChoiceStrength(action.Card!.Value)).First();
            if (phase == "choose_trump")
                return actions.OrderByDescending(action => hands[player].Count(card => card.Suit == Card.ParseSuit(action.Value!))).First();
            if (phase == "marriages")
            {
                Action[] declarations = actions.Where(action => action.Kind == "declare_marriage").ToArray();
                return declarations.Length > 0 ? declarations[0] : actions.First(action => action.Kind == "finish_marriages");
            }
            bool avoid = highContract!.Betli;
            return avoid ? actions.OrderBy(action => Strength(action.Card!.Value)).First()
                : actions.OrderByDescending(action => Strength(action.Card!.Value)).First();
        }

        private static int ChoiceStrength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static ContractDef FindContract(string id) => Contracts.Single(contract => contract.Id == id);

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 3).Where(player => scores[player] == high),
                scores.Select(value => (double)value), sessionDeals + "-deal Ulti session", TurnCount);
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            string openHands = openExposed ? " open_hands=" + string.Join(" | ",
                Enumerable.Range(0, 3).Select(playerIndex => "P" + playerIndex + "[" + string.Join(" ", hands[playerIndex]) + "]")) : "";
            return $"phase={phase} deal={dealsPlayed + 1}/{sessionDeals} dealer=P{dealer} bid={(highContract == null ? "-" : highContract.Id)} " +
                $"declarer={(declarer < 0 ? "-" : "P" + declarer)} trump={(trump.HasValue ? Card.SuitCode(trump.Value) : "N")} talon={talon.Count} discard={pendingDiscard.Count}/2 " +
                $"marriages=[{string.Join(" | ", marriages.Select(set => string.Join("", set.Select(Card.SuitCode))))}] " +
                $"trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] tricks=[{string.Join(",", tricks)}] " +
                $"scores=[{string.Join(",", scores)}] hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]{openHands}\n" +
                $"your hand: {string.Join(" ", hands[viewer])}";
        }

        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("ulti", "ウルティ", 3, 3, "marriage auction trick-taking",
                "7～Aの32枚。12枚のfirst bidderが2枚talonを作り、以後talonを奪って複合contractを競る。must-follow/trump/beatとmarriage・Ulti・Betli・Durchmarsを採点する。",
                "Pagat / gokurakism", new Dictionary<string, string> { { "deals", "12" } }),
            (players, random, options) => new UltiGame(players, random, options));
    }
}
