using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TrumpLab.Games
{
    public sealed class NapoleonGame : GameBase
    {
        private sealed class NCard
        {
            public Card? Card { get; }
            public bool Joker => !Card.HasValue;
            public string Id => Joker ? "JOKER" : Card!.Value.ToString();
            public NCard(Card? card) { Card = card; }
            public override string ToString() => Id;
        }

        private sealed class Bid
        {
            public int Player { get; }
            public int Goal { get; }
            public Suit Trump { get; }
            public int Value => Goal * 4 + SuitBid(Trump);
            public Bid(int player, int goal, Suit trump)
            {
                Player = player; Goal = goal; Trump = trump;
            }
            public override string ToString() => Goal + ":" + Card.SuitCode(Trump);
        }

        private sealed class PlayedCard
        {
            public int Player { get; }
            public NCard Card { get; }
            public PlayedCard(int player, NCard card) { Player = player; Card = card; }
        }

        public const int DeckSize = 53;
        private readonly DeterministicRandom rng;
        private readonly List<List<NCard>> hands;
        private readonly List<List<NCard>> captured;
        private readonly List<NCard> widow = new List<NCard>();
        private readonly List<NCard> discarded = new List<NCard>();
        private readonly List<PlayedCard> trick = new List<PlayedCard>();
        private readonly int[] scores;
        private readonly int handSize;
        private readonly int targetScore;
        private readonly int minimumBid;
        private readonly bool yoromeki;
        private readonly bool sameTwo;
        private Bid? highBid;
        private int dealer;
        private int passesWithoutBid;
        private int passesSinceBid;
        private int dealNumber;
        private int trickNumber;
        private int napoleon = -1;
        private int adjutant = -1;
        private int goal;
        private int discardNeeded;
        private int lastCoalitionHonors;
        private Suit trump;
        private NCard? calledCard;
        private bool solo;
        private bool adjutantRevealed;
        private string phase = "bid";
        private bool finished;

        public override string GameId => "napoleon";
        public override string Name => "ナポレオン";
        public int SessionTarget => targetScore;
        public int MinimumBid => minimumBid;
        public bool Yoromeki => yoromeki;
        public bool SameTwo => sameTwo;
        public int DealNumber => dealNumber;

        public NapoleonGame(int players, DeterministicRandom rng,
            IReadOnlyDictionary<string, string> options)
        {
            Players = players;
            this.rng = rng;
            handSize = HandSizeFor(players);
            targetScore = options.Integer("target_score", 5);
            minimumBid = options.Integer("minimum_bid", 12);
            yoromeki = options.Boolean("yoromeki", true);
            sameTwo = options.Boolean("same_two", true);
            if (targetScore < 1 || targetScore > 100)
                throw new ArgumentOutOfRangeException(nameof(options), "target_score must be 1..100.");
            if (minimumBid < 10 || minimumBid > 20)
                throw new ArgumentOutOfRangeException(nameof(options), "minimum_bid must be 10..20.");
            hands = Enumerable.Range(0, players).Select(_ => new List<NCard>()).ToList();
            captured = Enumerable.Range(0, players).Select(_ => new List<NCard>()).ToList();
            scores = new int[players];
            dealer = players - 1;
            StartDeal();
        }

        public static int HandSizeFor(int players)
        {
            if (players < 4 || players > 7) throw new ArgumentOutOfRangeException(nameof(players));
            return players == 4 ? 12 : players == 5 ? 10 : players == 6 ? 8 : 7;
        }

        public static int WidowSizeFor(int players) => DeckSize - players * HandSizeFor(players);

        public static IReadOnlyDictionary<string, int> DeckComposition()
        {
            var result = new Dictionary<string, int>();
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
                for (int rank = 1; rank <= 13; rank++) result[new Card(suit, rank).ToString()] = 1;
            result["JOKER"] = 1;
            return result;
        }

        private void StartDeal()
        {
            foreach (List<NCard> pile in hands) pile.Clear();
            foreach (List<NCard> pile in captured) pile.Clear();
            widow.Clear(); discarded.Clear(); trick.Clear();
            var deck = Cards.StandardDeck().Select(card => new NCard(card)).ToList();
            deck.Add(new NCard(null));
            rng.Shuffle(deck);
            dealer = (dealer + 1) % Players;
            dealNumber++;
            for (int round = 0; round < handSize; round++)
                for (int offset = 1; offset <= Players; offset++)
                    hands[(dealer + offset) % Players].Add(Pop(deck));
            widow.AddRange(deck);
            highBid = null;
            passesWithoutBid = 0; passesSinceBid = 0; trickNumber = 0;
            napoleon = -1; adjutant = -1; goal = 0; discardNeeded = 0;
            calledCard = null; solo = false; adjutantRevealed = false;
            phase = "bid";
            CurrentPlayer = (dealer + 1) % Players;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "bid") return BidActions();
            if (phase == "call_adjutant")
            {
                var actions = Cards.StandardDeck()
                    .Select(card => new Action("call_adjutant", card, value: card.ToString())).ToList();
                actions.Add(new Action("call_joker", value: "JOKER"));
                return actions;
            }
            if (phase == "discard_widow")
                return hands[actual].Select(card => new Action("discard_widow", card.Card,
                    value: card.Id)).ToArray();
            return PlayActions(actual);
        }

        private IReadOnlyList<Action> BidActions()
        {
            int high = highBid?.Value ?? -1;
            var actions = new List<Action> { new Action("pass") };
            for (int bidGoal = minimumBid; bidGoal <= 20; bidGoal++)
                foreach (Suit suit in Enum.GetValues(typeof(Suit)))
                    if (bidGoal * 4 + SuitBid(suit) > high)
                        actions.Add(new Action("bid", value: bidGoal.ToString(CultureInfo.InvariantCulture)
                            + ":" + Card.SuitCode(suit)));
            return actions;
        }

        private IReadOnlyList<Action> PlayActions(int player)
        {
            IEnumerable<NCard> cards = hands[player];
            if (trick.Count == 0)
            {
                if (trickNumber == 0) cards = cards.Where(card => !card.Joker);
            }
            else if (trick[0].Card.Joker)
            {
                NCard[] trumps = cards.Where(card => !card.Joker
                    && card.Card!.Value.Suit == trump).ToArray();
                if (trumps.Length > 0) cards = trumps;
            }
            else
            {
                Suit led = trick[0].Card.Card!.Value.Suit;
                NCard[] follow = cards.Where(card => !card.Joker
                    && card.Card!.Value.Suit == led).ToArray();
                if (follow.Length > 0)
                    cards = follow.Concat(cards.Where(card => card.Joker));
            }
            var actions = new List<Action>();
            foreach (NCard card in cards)
            {
                if (trick.Count == 0 && card.Joker)
                    actions.Add(new Action("lead_joker", value: "JOKER"));
                else actions.Add(new Action("play", card.Card, value: card.Id));
            }
            return actions;
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null);
            Guard.Legal(action, LegalActions(player));
            TurnCount++;
            if (phase == "bid") { ApplyBid(player, action); return; }
            if (phase == "call_adjutant") { ApplyCall(action); return; }
            if (phase == "discard_widow") { ApplyDiscard(player, action); return; }
            ApplyPlay(player, action);
        }

        private void ApplyBid(int player, Action action)
        {
            if (action.Kind == "bid")
            {
                string[] parts = action.Value!.Split(':');
                highBid = new Bid(player, int.Parse(parts[0], CultureInfo.InvariantCulture),
                    Card.ParseSuit(parts[1]));
                passesSinceBid = 0;
            }
            else if (highBid == null)
            {
                passesWithoutBid++;
                if (passesWithoutBid >= Players) { StartDeal(); return; }
            }
            else
            {
                passesSinceBid++;
                if (passesSinceBid >= Players - 1)
                {
                    napoleon = highBid.Player;
                    goal = highBid.Goal;
                    trump = highBid.Trump;
                    phase = "call_adjutant";
                    CurrentPlayer = napoleon;
                    return;
                }
            }
            CurrentPlayer = (player + 1) % Players;
        }

        private void ApplyCall(Action action)
        {
            calledCard = action.Kind == "call_joker" ? new NCard(null) : new NCard(action.Card!.Value);
            int holder = Enumerable.Range(0, Players)
                .Where(player => hands[player].Any(card => card.Id == calledCard.Id))
                .DefaultIfEmpty(-1).First();
            solo = holder < 0 || holder == napoleon;
            adjutant = solo ? -1 : holder;
            hands[napoleon].AddRange(widow);
            widow.Clear();
            discardNeeded = WidowSizeFor(Players);
            phase = "discard_widow";
        }

        private void ApplyDiscard(int player, Action action)
        {
            NCard card = hands[player].Single(item => item.Id == action.Value);
            hands[player].Remove(card);
            discarded.Add(card);
            discardNeeded--;
            if (discardNeeded <= 0)
            {
                phase = "play";
                CurrentPlayer = napoleon;
            }
        }

        private void ApplyPlay(int player, Action action)
        {
            NCard played = action.Kind == "lead_joker"
                ? hands[player].Single(card => card.Joker)
                : hands[player].Single(card => card.Id == action.Value);
            hands[player].Remove(played);
            trick.Add(new PlayedCard(player, played));
            if (calledCard != null && played.Id == calledCard.Id)
            {
                adjutant = player == napoleon ? -1 : player;
                solo = player == napoleon;
                adjutantRevealed = !solo;
            }
            if (trick.Count < Players)
            {
                CurrentPlayer = (player + 1) % Players;
                return;
            }
            int winnerIndex = ResolveWinner(trick.Select(item => item.Card).ToArray(), trump,
                trickNumber == 0, sameTwo, yoromeki);
            int winner = trick[winnerIndex].Player;
            captured[winner].AddRange(trick.Select(item => item.Card));
            trick.Clear();
            trickNumber++;
            if (hands.All(hand => hand.Count == 0)) FinishDeal();
            else CurrentPlayer = winner;
        }

        private void FinishDeal()
        {
            int coalitionHonors = CapturedCoalitionHonors();
            lastCoalitionHonors = coalitionHonors;
            int[] deltas = DealWinDeltas(Players, napoleon, solo ? -1 : adjutant,
                goal, coalitionHonors);
            for (int player = 0; player < Players; player++) scores[player] += deltas[player];
            if (scores.Any(score => score >= targetScore)) finished = true;
            else StartDeal();
        }

        private int CapturedCoalitionHonors()
        {
            int count = captured[napoleon].Count(Honor);
            if (!solo && adjutant >= 0) count += captured[adjutant].Count(Honor);
            return count;
        }

        public static int[] DealWinDeltas(int players, int napoleon, int adjutant,
            int goal, int coalitionHonors)
        {
            if (players < 4 || players > 7) throw new ArgumentOutOfRangeException(nameof(players));
            if (napoleon < 0 || napoleon >= players) throw new ArgumentOutOfRangeException(nameof(napoleon));
            if (adjutant >= players || adjutant == napoleon) throw new ArgumentOutOfRangeException(nameof(adjutant));
            if (goal < 10 || goal > 20 || coalitionHonors < 0 || coalitionHonors > 20)
                throw new ArgumentOutOfRangeException();
            bool success = coalitionHonors >= goal;
            var result = new int[players];
            for (int player = 0; player < players; player++)
            {
                bool coalition = player == napoleon || player == adjutant;
                result[player] = coalition == success ? 1 : 0;
            }
            return result;
        }

        public static int ResolveTrickWinner(IReadOnlyList<string> cardIds, Suit trump,
            bool firstTrick = false, bool sameTwo = true, bool yoromeki = true)
        {
            if (cardIds == null || cardIds.Count == 0) throw new ArgumentException("A trick needs cards.");
            NCard[] cards = cardIds.Select(ParseNCard).ToArray();
            return ResolveWinner(cards, trump, firstTrick, sameTwo, yoromeki);
        }

        private static int ResolveWinner(IReadOnlyList<NCard> cards, Suit trump,
            bool firstTrick, bool useSameTwo, bool useYoromeki)
        {
            int mighty = Find(cards, new Card(Suit.Spades, 1));
            int stagger = Find(cards, new Card(Suit.Hearts, 12));
            if (useYoromeki && mighty >= 0 && stagger >= 0) return stagger;
            if (mighty >= 0) return mighty;
            if (cards[0].Joker) return 0;
            int right = Find(cards, new Card(trump, 11));
            if (right >= 0) return right;
            Suit leftSuit = Enum.GetValues(typeof(Suit)).Cast<Suit>()
                .Single(suit => suit != trump && Red(suit) == Red(trump));
            int left = Find(cards, new Card(leftSuit, 11));
            if (left >= 0) return left;
            bool containsJoker = cards.Any(card => card.Joker);
            Suit led = cards[0].Card!.Value.Suit;
            bool oneSuit = !containsJoker && cards.All(card => card.Card!.Value.Suit == led);
            if (useSameTwo && !firstTrick && oneSuit)
            {
                int two = Find(cards, new Card(led, 2));
                if (two >= 0) return two;
            }
            NCard[] trumpCards = cards.Where(card => !card.Joker
                && card.Card!.Value.Suit == trump).ToArray();
            IEnumerable<NCard> eligible = trumpCards.Length > 0
                ? trumpCards : cards.Where(card => !card.Joker && card.Card!.Value.Suit == led);
            NCard winning = eligible.OrderByDescending(card => Strength(card.Card!.Value)).First();
            for (int index = 0; index < cards.Count; index++)
                if (ReferenceEquals(cards[index], winning)) return index;
            throw new InvalidOperationException("No trick winner.");
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random,
            int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "bid") return ChooseBid(actions, player);
            if (phase == "call_adjutant") return ChooseCall(actions, player);
            if (phase == "discard_widow") return actions.OrderBy(DiscardCost)
                .ThenBy(action => action.Value, StringComparer.Ordinal).First();
            return ChoosePlay(actions, player);
        }

        private Action ChooseBid(IReadOnlyList<Action> actions, int player)
        {
            Suit preferred = Enum.GetValues(typeof(Suit)).Cast<Suit>()
                .OrderByDescending(suit => TrumpPotential(hands[player], suit))
                .ThenByDescending(SuitBid).First();
            int honors = hands[player].Count(Honor);
            int specials = hands[player].Count(card => card.Joker
                || card.Card == new Card(Suit.Spades, 1)
                || card.Card == new Card(preferred, 11)
                || card.Card == new Card(LeftSuit(preferred), 11));
            int estimate = Math.Min(20, Math.Max(minimumBid, 8 + honors + specials / 2));
            Action[] bids = actions.Where(action => action.Kind == "bid"
                && ParseBidGoal(action) <= estimate).ToArray();
            if (bids.Length == 0) return actions.First(action => action.Kind == "pass");
            return bids.OrderBy(action => ParseBidGoal(action))
                .ThenBy(action => Card.ParseSuit(action.Value!.Split(':')[1]) == preferred ? 0 : 1)
                .ThenBy(action => BidActionValue(action)).First();
        }

        private Action ChooseCall(IReadOnlyList<Action> actions, int player)
        {
            HashSet<string> own = new HashSet<string>(hands[player].Select(card => card.Id));
            Action? missing = actions.Where(action => !own.Contains(action.Value!))
                .OrderByDescending(CallValue)
                .ThenBy(action => action.Value, StringComparer.Ordinal)
                .Cast<Action?>().FirstOrDefault();
            return missing ?? actions.OrderByDescending(CallValue).First();
        }

        private Action ChoosePlay(IReadOnlyList<Action> actions, int player)
        {
            bool ownCoalition = PlayerKnowsTheyAreCoalition(player);
            int visibleHonors = VisibleCoalitionHonors(player);
            int needed = Math.Max(0, goal - visibleHonors);
            return actions.OrderByDescending(action => PlayUtility(action, player, ownCoalition, needed))
                .ThenBy(action => action.Value, StringComparer.Ordinal).First();
        }

        private int PlayUtility(Action action, int player, bool ownCoalition, int needed)
        {
            NCard candidate = ActionCard(action);
            var trial = trick.Select(item => item.Card).ToList();
            trial.Add(candidate);
            int winnerIndex = ResolveWinner(trial, trump, trickNumber == 0, sameTwo, yoromeki);
            int winnerSeat = winnerIndex < trick.Count ? trick[winnerIndex].Player : player;
            int tableHonors = trial.Count(Honor);
            bool coalitionWinner = KnownCoalitionSeat(winnerSeat, player);
            bool desiredWinner = ownCoalition ? coalitionWinner : !coalitionWinner;
            int value = desiredWinner ? 600 + tableHonors * 90 : -600 - tableHonors * 90;
            if (winnerSeat == player) value += ownCoalition == desiredWinner ? 80 : 20;
            if (Honor(candidate)) value += desiredWinner ? 55 : -80;
            int strength = candidate.Joker ? (action.Kind == "lead_joker" ? 15 : 0)
                : Strength(candidate.Card!.Value);
            if (trick.Count == 0)
                value += ownCoalition && needed > 0 ? strength * 5 : -strength * 2;
            else value -= strength;
            return value;
        }

        private bool PlayerKnowsTheyAreCoalition(int player)
        {
            if (player == napoleon) return true;
            if (calledCard == null) return false;
            if (adjutantRevealed) return player == adjutant;
            return hands[player].Any(card => card.Id == calledCard.Id);
        }

        private bool KnownCoalitionSeat(int seat, int viewer)
        {
            if (seat == napoleon) return true;
            if (adjutantRevealed && seat == adjutant) return true;
            return seat == viewer && PlayerKnowsTheyAreCoalition(viewer);
        }

        private int VisibleCoalitionHonors(int viewer)
        {
            int result = napoleon >= 0 ? captured[napoleon].Count(Honor) : 0;
            if (adjutantRevealed && adjutant >= 0) result += captured[adjutant].Count(Honor);
            else if (viewer != napoleon && PlayerKnowsTheyAreCoalition(viewer))
                result += captured[viewer].Count(Honor);
            return result;
        }

        private int DiscardCost(Action action)
        {
            NCard card = ActionCard(action);
            if (card.Joker) return 170;
            Card value = card.Card!.Value;
            int result = Strength(value);
            if (Honor(card)) result += 100;
            if (value == new Card(Suit.Spades, 1)) result += 200;
            if (value == new Card(trump, 11)) result += 180;
            if (value == new Card(LeftSuit(trump), 11)) result += 160;
            if (calledCard != null && card.Id == calledCard.Id) result += 120;
            return result;
        }

        private int CallValue(Action action)
        {
            if (action.Kind == "call_joker") return 900;
            Card card = action.Card!.Value;
            if (card == new Card(Suit.Spades, 1)) return 1000;
            if (card == new Card(trump, 11)) return 850;
            if (card == new Card(LeftSuit(trump), 11)) return 800;
            return Strength(card) * 10 + SuitBid(card.Suit);
        }

        public override bool IsTerminal => finished;

        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = scores.Max();
            return new GameResult(Enumerable.Range(0, Players).Where(player => scores[player] == high),
                scores.Select(value => (double)value), "first to " + targetScore + " Napoleon deal wins",
                TurnCount, new Dictionary<string, object>
                {
                    ["deals"] = dealNumber,
                    ["contract"] = goal,
                    ["coalition_honors"] = lastCoalitionHonors
                });
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            string adjutantView = AdjutantView(viewer);
            string role = ViewerRole(viewer);
            string publicDiscards = string.Join(" ", discarded.Where(Honor));
            string ownDiscards = viewer == napoleon ? string.Join(" ", discarded) : "hidden";
            return $"phase={phase} deal={dealNumber} dealer=P{dealer} high_bid={(highBid == null ? "-" : highBid.ToString())} " +
                $"napoleon={(napoleon < 0 ? "-" : "P" + napoleon)} goal={goal} trump={(napoleon < 0 ? "-" : Card.SuitCode(trump))} " +
                $"called={(calledCard == null ? "-" : calledCard.Id)} adjutant={adjutantView} your_role={role} " +
                $"trick={Math.Min(trickNumber + 1, handSize)}/{handSize} scores=[{string.Join(",", scores)}] " +
                $"honors=[{string.Join(",", captured.Select(pile => pile.Count(Honor)))}] discarded_honors=[{publicDiscards}] " +
                $"discard_count={discarded.Count} your_discard=[{ownDiscards}] hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}] " +
                $"table=[{string.Join(" ", trick.Select(item => "P" + item.Player + ":" + item.Card))}]\n" +
                $"your hand: {string.Join(" ", hands[viewer])}";
        }

        private string AdjutantView(int viewer)
        {
            if (calledCard == null) return "-";
            if (adjutantRevealed || finished) return solo || adjutant < 0 ? "solo" : "P" + adjutant;
            if (viewer == napoleon && solo) return "solo";
            if (viewer != napoleon && hands[viewer].Any(card => card.Id == calledCard.Id))
                return "P" + viewer;
            return "hidden";
        }

        private string ViewerRole(int viewer)
        {
            if (viewer == napoleon) return "napoleon";
            if (calledCard == null) return "unknown";
            if ((adjutantRevealed && viewer == adjutant)
                || hands[viewer].Any(card => card.Id == calledCard.Id)) return "adjutant";
            return "opposition";
        }

        private NCard ActionCard(Action action)
        {
            if (action.Kind == "lead_joker" || action.Value == "JOKER") return new NCard(null);
            return new NCard(action.Card ?? Card.Parse(action.Value!));
        }

        private static int TrumpPotential(IEnumerable<NCard> hand, Suit suit)
        {
            int result = hand.Count(card => !card.Joker && card.Card!.Value.Suit == suit);
            result += hand.Count(card => Honor(card) && card.Card!.Value.Suit == suit) * 2;
            if (hand.Any(card => card.Card == new Card(suit, 11))) result += 5;
            if (hand.Any(card => card.Card == new Card(LeftSuit(suit), 11))) result += 4;
            if (hand.Any(card => card.Card == new Card(Suit.Spades, 1))) result += 5;
            if (hand.Any(card => card.Joker)) result += 3;
            return result;
        }

        private static int ParseBidGoal(Action action) =>
            int.Parse(action.Value!.Split(':')[0], CultureInfo.InvariantCulture);
        private static int BidActionValue(Action action) => ParseBidGoal(action) * 4
            + SuitBid(Card.ParseSuit(action.Value!.Split(':')[1]));
        private static int Find(IReadOnlyList<NCard> cards, Card wanted)
        {
            for (int index = 0; index < cards.Count; index++)
                if (cards[index].Card == wanted) return index;
            return -1;
        }
        private static NCard ParseNCard(string id) => string.Equals(id, "JOKER",
            StringComparison.OrdinalIgnoreCase) ? new NCard(null) : new NCard(Card.Parse(id));
        private static bool Honor(NCard card) => card.Card.HasValue
            && (card.Card.Value.Rank == 1 || card.Card.Value.Rank >= 10);
        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static int SuitBid(Suit suit) => suit == Suit.Spades ? 3
            : suit == Suit.Hearts ? 2 : suit == Suit.Diamonds ? 1 : 0;
        private static bool Red(Suit suit) => suit == Suit.Diamonds || suit == Suit.Hearts;
        private static Suit LeftSuit(Suit trump) => Enum.GetValues(typeof(Suit)).Cast<Suit>()
            .Single(suit => suit != trump && Red(suit) == Red(trump));
        private static NCard Pop(List<NCard> cards)
        {
            NCard card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card;
        }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("napoleon", "ナポレオン", 4, 7,
                "contract hidden-partner honor-trick",
                "52枚＋Joker 1枚。4～7人（5人推奨）でsoft-pass競りを行い、任意cardで秘密の副官を指名する。通常must-follow、Mighty、正J、裏J、Joker切札請求、セイム2を用い、A/K/Q/J/10の20枚を契約数以上集める。",
                "Akagiri/Trump Game Encyclopedia (Gamefarm Napoleon)",
                new Dictionary<string, string>
                {
                    ["target_score"] = "5",
                    ["minimum_bid"] = "12",
                    ["yoromeki"] = "true",
                    ["same_two"] = "true"
                }),
            (players, random, options) => new NapoleonGame(players, random, options));
    }
}
