using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    internal static class FinesseAndSchafkopfGames
    {
        public static void RegisterGames(GameRegistry registry)
        {
            FinesseGame.Register(registry);
            SchafkopfGame.Register(registry);
        }
    }

    public sealed class FinesseGame : GameBase
    {
        private sealed class FCard
        {
            public Card Card { get; }
            public int Copy { get; }
            public string Id => Card + "#" + Copy;
            public FCard(Card card, int copy) { Card = card; Copy = copy; }
            public override string ToString() => Id;
        }

        private readonly DeterministicRandom rng;
        private readonly List<List<FCard>> hands = NewPiles();
        private readonly List<List<FCard>> tableCards = NewPiles();
        private readonly List<Tuple<int, FCard>> trick = new List<Tuple<int, FCard>>();
        private readonly Queue<int> refillOwners = new Queue<int>();
        private readonly int[] pairTricks = new int[2];
        private readonly int[] scores = new int[2];
        private int dealer = 3;
        private int trickNumber;
        private int pendingLeader;
        private Suit? trump;
        private bool firstLead;
        private bool dealEndsAfterRefill;
        private string phase = "play";
        private bool finished;

        public override string GameId => "finesse";
        public override string Name => "フィネス";
        public FinesseGame(int players, DeterministicRandom rng) { Players = 4; this.rng = rng; StartDeal(); }
        private static List<List<FCard>> NewPiles() => Enumerable.Range(0, 4).Select(_ => new List<FCard>()).ToList();
        private static int Partner(int player) => (player + 2) % 4;

        private void StartDeal()
        {
            foreach (List<FCard> pile in hands) pile.Clear(); foreach (List<FCard> pile in tableCards) pile.Clear();
            trick.Clear(); refillOwners.Clear(); Array.Clear(pairTricks, 0, 2);
            var deck = Cards.StandardDeck().Select(card => new FCard(card, 0)).ToList();
            deck.AddRange(Cards.StandardDeck(new[] { 11, 12, 13 }).Select(card => new FCard(card, 1))); rng.Shuffle(deck);
            var reserve = new List<FCard>(); for (int i = 0; i < 12; i++) reserve.Add(Pop(deck));
            dealer = (dealer + 1) % 4;
            for (int round = 0; round < 13; round++) for (int offset = 1; offset <= 4; offset++) hands[(dealer + offset) % 4].Add(Pop(deck));
            for (int round = 0; round < 3; round++) for (int offset = 1; offset <= 4; offset++) tableCards[(dealer + offset) % 4].Add(Pop(reserve));
            trickNumber = 0; trump = null; firstLead = true; dealEndsAfterRefill = false; phase = "play"; CurrentPlayer = (dealer + 1) % 4;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "refill") return hands[actual].Select(card => new Action("refill_table", card.Card, value: card.Id)).ToArray();
            if (trick.Count == 0)
            {
                var actions = new List<Action>();
                foreach (FCard card in hands[actual]) AddLeadActions(actions, "lead_hand", card, actual);
                foreach (FCard card in tableCards[Partner(actual)]) AddLeadActions(actions, "lead_partner_table", card, Partner(actual));
                return actions;
            }
            IEnumerable<FCard> cards = hands[actual]; Card led = trick[0].Item2.Card;
            FCard[] follow = cards.Where(card => card.Card.Suit == led.Suit).ToArray(); if (follow.Length > 0) cards = follow;
            return cards.Select(card => new Action("play", card.Card, value: card.Id)).ToArray();
        }

        private void AddLeadActions(List<Action> actions, string kind, FCard card, int owner)
        {
            string value = owner + ":" + card.Id;
            if (firstLead && card.Card.Rank == 1)
            {
                actions.Add(new Action(kind + "_trump", card.Card, value: value));
                actions.Add(new Action(kind + "_no_trump", card.Card, value: value));
            }
            else actions.Add(new Action(kind, card.Card, value: value));
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "refill")
            {
                FCard card = hands[player].Single(item => item.Id == action.Value); hands[player].Remove(card); tableCards[player].Add(card);
                refillOwners.Dequeue();
                if (refillOwners.Count > 0) CurrentPlayer = refillOwners.Peek();
                else if (dealEndsAfterRefill) FinishDeal();
                else { phase = "play"; CurrentPlayer = pendingLeader; }
                return;
            }
            if (trick.Count == 0)
            {
                string[] parts = action.Value!.Split(new[] { ':' }, 2); int owner = int.Parse(parts[0]);
                FCard card = owner == player ? hands[player].Single(item => item.Id == parts[1]) : tableCards[owner].Single(item => item.Id == parts[1]);
                if (owner == player) hands[player].Remove(card); else { tableCards[owner].Remove(card); refillOwners.Enqueue(owner); }
                if (firstLead)
                {
                    trump = action.Kind.EndsWith("_no_trump", StringComparison.Ordinal) ? (Suit?)null : card.Card.Suit;
                    firstLead = false;
                }
                trick.Add(Tuple.Create(owner, card)); CurrentPlayer = NextUnplayed(owner); return;
            }
            FCard played = hands[player].Single(item => item.Id == action.Value); hands[player].Remove(played); trick.Add(Tuple.Create(player, played));
            if (trick.Count < 4) { CurrentPlayer = NextUnplayed(player); return; }
            ResolveTrick();
        }

        private int NextUnplayed(int owner)
        {
            int next = (owner + 1) % 4;
            while (trick.Any(item => item.Item1 == next)) next = (next + 1) % 4;
            return next;
        }
        private void ResolveTrick()
        {
            Suit led = trick[0].Item2.Card.Suit;
            IEnumerable<Tuple<int, FCard>> eligible = trump.HasValue && trick.Any(item => item.Item2.Card.Suit == trump.Value)
                ? trick.Where(item => item.Item2.Card.Suit == trump.Value) : trick.Where(item => item.Item2.Card.Suit == led);
            int winner = eligible.OrderByDescending(item => Strength(item.Item2.Card)).First().Item1;
            pairTricks[winner % 2]++; pendingLeader = winner; trickNumber++; trick.Clear();
            dealEndsAfterRefill = trickNumber >= 13;
            if (refillOwners.Count > 0) { phase = "refill"; CurrentPlayer = refillOwners.Peek(); }
            else if (dealEndsAfterRefill) FinishDeal(); else CurrentPlayer = winner;
        }

        private void FinishDeal()
        {
            int lastTeam = pendingLeader % 2;
            for (int team = 0; team < 2; team++)
            {
                int dealScore = team == lastTeam ? 4 : 0; int won = pairTricks[team];
                if (won >= 7) dealScore += won == 7 ? 2 : won == 8 ? 5 : won == 9 ? 10 : won == 10 ? 20 : won == 11 ? 10 : won == 12 ? 5 : 2;
                int penalty = Enumerable.Range(0, 4).Where(player => player % 2 == team).Sum(player =>
                    tableCards[player].Count(card => trump.HasValue ? card.Card.Suit == trump.Value : card.Card.Rank >= 11)) * 3;
                scores[team] += Math.Max(0, dealScore - penalty);
            }
            int high = scores.Max(), low = scores.Min();
            if (high >= 60 || high >= 42 && high - low >= 5) finished = true; else StartDeal();
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "refill") return actions.OrderBy(action => Strength(action.Card!.Value)).First();
            return actions.Where(action => !action.Kind.Contains("partner_table"))
                .DefaultIfEmpty(actions[0]).OrderBy(action => Strength(action.Card!.Value)).First();
        }
        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static FCard Pop(List<FCard> cards) { FCard card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over."); int high = scores.Max(); double[] result = { scores[0], scores[1], scores[0], scores[1] };
            return new GameResult(Enumerable.Range(0, 4).Where(player => result[player] == high), result, "Finesse partnership target", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            return $"phase={phase} dealer=P{dealer} trick_no={trickNumber + 1}/13 trump={(firstLead ? "undecided" : trump.HasValue ? Card.SuitCode(trump.Value) : "none")} " +
                $"trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] pair_tricks=[{string.Join(",", pairTricks)}] scores=[{string.Join(",", scores)}] " +
                $"tables=[{string.Join(" | ", tableCards.Select((pile, p) => "P" + p + ":" + string.Join(" ", pile)))}] hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("finesse", "フィネス", 4, 4, "team open-table trick-taking",
                "52枚＋J/Q/K複製12枚を、各13枚手札＋公開table3枚に分ける。lead時は自分の手札かpartnerのtable札を指定でき、初lead suitが切り札（Aならno-trump可）。table使用後は所有者が手札から補充し、勝数曲線・最終4点・残table罰点で42点差5または60点を争う。",
                "gokurakism/Finesse"),
            (players, random, options) => new FinesseGame(players, random));
    }

    public sealed class SchafkopfGame : GameBase
    {
        private sealed class Bid
        {
            public int Player { get; }
            public string Contract { get; }
            public Suit? Suit { get; }
            public int Rank { get; }
            public Bid(int player, string contract, int rank, Suit? suit = null) { Player = player; Contract = contract; Rank = rank; Suit = suit; }
        }

        private readonly DeterministicRandom rng;
        private readonly int sessionDeals;
        private readonly List<List<Card>> hands = Enumerable.Range(0, 4).Select(_ => new List<Card>()).ToList();
        private readonly List<List<Card>> captured = Enumerable.Range(0, 4).Select(_ => new List<Card>()).ToList();
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly List<Bid> bids = new List<Bid>();
        private readonly int[] tricks = new int[4];
        private readonly int[] scores = new int[4];
        private int dealer = 3;
        private int bidsMade;
        private int dealsPlayed;
        private int declarer = -1;
        private int partner = -1;
        private string contract = "partner";
        private Suit? soloSuit;
        private Card? calledAce;
        private string phase = "bid";
        private bool finished;

        public override string GameId => "schafkopf";
        public override string Name => "シャーフコップ";
        public SchafkopfGame(int players, DeterministicRandom rng, IReadOnlyDictionary<string, string> options)
        { Players = 4; this.rng = rng; sessionDeals = Math.Max(1, options.Integer("deals", 8)); StartDeal(); }

        private void StartDeal()
        {
            foreach (List<Card> hand in hands) hand.Clear(); foreach (List<Card> pile in captured) pile.Clear();
            trick.Clear(); bids.Clear(); Array.Clear(tricks, 0, 4);
            List<Card> deck = Cards.Shuffled(Cards.StandardDeck(new[] { 1, 7, 8, 9, 10, 11, 12, 13 }), rng); dealer = (dealer + 1) % 4;
            for (int round = 0; round < 8; round++) for (int offset = 1; offset <= 4; offset++) hands[(dealer + offset) % 4].Add(Pop(deck));
            bidsMade = 0; declarer = partner = -1; contract = "partner"; soloSuit = null; calledAce = null; phase = "bid"; CurrentPlayer = (dealer + 1) % 4;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "bid")
            {
                var actions = new List<Action> { new Action("pass") };
                if (CallableSuits(actual).Any()) actions.Add(new Action("bid", value: "partner"));
                actions.Add(new Action("bid", value: "wenz")); actions.Add(new Action("bid", value: "wenz_tout"));
                foreach (Suit suit in Enum.GetValues(typeof(Suit)))
                { actions.Add(new Action("bid", value: "solo:" + Card.SuitCode(suit))); actions.Add(new Action("bid", value: "solo_tout:" + Card.SuitCode(suit))); }
                return actions;
            }
            if (phase == "call_ace") return CallableSuits(actual).Select(suit => new Action("call_ace", value: Card.SuitCode(suit))).ToArray();
            IEnumerable<Card> cards = hands[actual];
            if (trick.Count == 0)
            {
                if (contract == "partner" && actual == partner && calledAce.HasValue && hands[actual].Contains(calledAce.Value))
                {
                    Suit calledSuit = calledAce.Value.Suit; int suitCount = hands[actual].Count(card => !IsTrump(card) && card.Suit == calledSuit);
                    if (suitCount < 4) cards = cards.Where(card => card.Suit != calledSuit || card == calledAce.Value);
                }
            }
            else
            {
                Card lead = trick[0].Item2;
                Card[] follow = IsTrump(lead) ? cards.Where(IsTrump).ToArray() : cards.Where(card => !IsTrump(card) && card.Suit == lead.Suit).ToArray();
                if (follow.Length > 0) cards = follow;
                if (contract == "partner" && actual == partner && calledAce.HasValue && lead.Suit == calledAce.Value.Suit && !IsTrump(lead) && cards.Contains(calledAce.Value))
                    cards = new[] { calledAce.Value };
            }
            return cards.Select(card => new Action("play", card)).ToArray();
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "bid")
            {
                bidsMade++;
                if (action.Kind == "bid") bids.Add(ParseBid(player, action.Value!));
                if (bidsMade < 4) { CurrentPlayer = (player + 1) % 4; return; }
                if (bids.Count == 0) { StartDeal(); return; }
                Bid winner = bids.OrderByDescending(bid => bid.Rank).First(); declarer = winner.Player; contract = winner.Contract; soloSuit = winner.Suit;
                if (contract == "partner") { phase = "call_ace"; CurrentPlayer = declarer; }
                else { phase = "play"; CurrentPlayer = (dealer + 1) % 4; }
                return;
            }
            if (phase == "call_ace")
            {
                calledAce = new Card(Card.ParseSuit(action.Value!), 1);
                partner = Enumerable.Range(0, 4).Single(p => hands[p].Contains(calledAce.Value));
                phase = "play"; CurrentPlayer = (dealer + 1) % 4; return;
            }
            Card card = action.Card!.Value; hands[player].Remove(card); trick.Add(Tuple.Create(player, card));
            if (trick.Count < 4) { CurrentPlayer = (player + 1) % 4; return; }
            int winnerPlayer = TrickWinner(); tricks[winnerPlayer]++; captured[winnerPlayer].AddRange(trick.Select(item => item.Item2)); trick.Clear();
            if (tricks.Sum() >= 8) FinishDeal(); else CurrentPlayer = winnerPlayer;
        }

        private Bid ParseBid(int player, string value)
        {
            string[] parts = value.Split(':'); string name = parts[0]; Suit? suit = parts.Length > 1 ? Card.ParseSuit(parts[1]) : (Suit?)null;
            int rank = name == "partner" ? 1 : name == "wenz" ? 2 : name == "solo" ? 3 : name == "wenz_tout" ? 4 : 5;
            return new Bid(player, name, rank, suit);
        }
        private IEnumerable<Suit> CallableSuits(int player) => new[] { Suit.Clubs, Suit.Spades, Suit.Diamonds }
            .Where(suit => !hands[player].Contains(new Card(suit, 1)));
        private bool IsTrump(Card card)
        {
            if (contract == "wenz" || contract == "wenz_tout") return card.Rank == 11;
            if (card.Rank == 12 || card.Rank == 11) return true;
            Suit suit = contract == "partner" ? Suit.Hearts : soloSuit!.Value;
            return card.Suit == suit;
        }
        private int TrumpStrength(Card card)
        {
            int suitOrder = card.Suit == Suit.Clubs ? 4 : card.Suit == Suit.Spades ? 3 : card.Suit == Suit.Hearts ? 2 : 1;
            if (card.Rank == 12) return 100 + suitOrder;
            if (card.Rank == 11) return 90 + suitOrder;
            return PlainStrength(card);
        }
        private int TrickWinner()
        {
            Card lead = trick[0].Item2; IEnumerable<Tuple<int, Card>> eligible = trick.Any(item => IsTrump(item.Item2))
                ? trick.Where(item => IsTrump(item.Item2)) : trick.Where(item => !IsTrump(item.Item2) && item.Item2.Suit == lead.Suit);
            return eligible.OrderByDescending(item => IsTrump(item.Item2) ? TrumpStrength(item.Item2) : PlainStrength(item.Item2)).First().Item1;
        }
        private void FinishDeal()
        {
            var team = new HashSet<int> { declarer }; if (contract == "partner") team.Add(partner);
            int points = team.Sum(player => captured[player].Sum(CardPoints)); int teamTricks = team.Sum(player => tricks[player]);
            bool tout = contract.EndsWith("tout", StringComparison.Ordinal); bool success = tout ? teamTricks == 8 : points >= 61;
            int stake = contract == "partner" ? 10 : tout ? 180 : 50;
            int losingPoints = success ? 120 - points : points; int losingTricks = success ? 8 - teamTricks : teamTricks;
            if (losingPoints <= 30) stake += 10; if (losingTricks == 0) stake += 10;
            for (int player = 0; player < 4; player++) scores[player] += (team.Contains(player) == success ? stake : -stake);
            dealsPlayed++; if (dealsPlayed >= sessionDeals) finished = true; else StartDeal();
        }
        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "bid") return actions.FirstOrDefault(action => action.Value == "partner").Value == "partner"
                ? actions.First(action => action.Value == "partner") : actions.First(action => action.Value == "wenz");
            if (phase == "call_ace") return actions[0];
            return actions.OrderBy(action => IsTrump(action.Card.GetValueOrDefault())
                ? TrumpStrength(action.Card.GetValueOrDefault()) : PlainStrength(action.Card.GetValueOrDefault())).First();
        }
        private static int PlainStrength(Card card) => card.Rank == 1 ? 8 : card.Rank == 10 ? 7 : card.Rank == 13 ? 6 : card.Rank - 6;
        private static int CardPoints(Card card) => card.Rank == 1 ? 11 : card.Rank == 10 ? 10 : card.Rank == 13 ? 4 : card.Rank == 12 ? 3 : card.Rank == 11 ? 2 : 0;
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over."); int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 4).Where(player => scores[player] == high), scores.Select(value => (double)value), "eight Bavarian Schafkopf deals", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer; bool partnerPublic = calledAce.HasValue && captured.Any(pile => pile.Contains(calledAce.Value));
            string role = viewer == declarer ? "declarer" : viewer == partner ? "partner" : "defender";
            return $"phase={phase} deal={dealsPlayed + 1}/{sessionDeals} dealer=P{dealer} contract={contract} declarer={(declarer < 0 ? "-" : "P" + declarer)} " +
                $"partner={(partnerPublic ? "P" + partner : "hidden")} your_role={role} called_ace={(calledAce.HasValue ? calledAce.Value.ToString() : "-")} " +
                $"trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] tricks=[{string.Join(",", tricks)}] scores=[{string.Join(",", scores)}] " +
                $"hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("schafkopf", "シャーフコップ", 4, 4, "contract team point-trick",
                "7～Aの32枚でPartner、Wenz、Suit Soloと各Toutをauctionする。PartnerはQ/J＋heart固定切り札と非切り札A指名、SoloはQ/J＋指定suit、WenzはJのみ。A/10/K/Q/J点の61、Tout全勝、Schneider/Schwarzを基礎stake（Stossなし）で8deal精算する。",
                "gokurakism/Bavarian Schafkopf", new Dictionary<string, string> { { "deals", "8" } }),
            (players, random, options) => new SchafkopfGame(players, random, options));
    }
}
