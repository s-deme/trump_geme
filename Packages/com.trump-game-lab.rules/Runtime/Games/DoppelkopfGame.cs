using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    public sealed class DoppelkopfGame : GameBase
    {
        private sealed class DCard
        {
            public Card Card { get; }
            public int Copy { get; }
            public string Id => Card + "#" + Copy;
            public DCard(Card card, int copy) { Card = card; Copy = copy; }
            public override string ToString() => Id;
        }

        private sealed class ContractChoice
        {
            public int Player { get; }
            public string Contract { get; }
            public int Rank { get; }
            public Suit? Suit { get; }
            public ContractChoice(int player, string contract, int rank, Suit? suit = null)
            { Player = player; Contract = contract; Rank = rank; Suit = suit; }
        }

        private readonly DeterministicRandom rng;
        private readonly int sessionDeals;
        private readonly List<List<DCard>> hands = NewPiles();
        private readonly List<List<DCard>> captured = NewPiles();
        private readonly List<Tuple<int, DCard>> trick = new List<Tuple<int, DCard>>();
        private readonly List<ContractChoice> choices = new List<ContractChoice>();
        private readonly List<DCard> povertyCards = new List<DCard>();
        private readonly int[] tricks = new int[4];
        private readonly int[] scores = new int[4];
        private int dealer = 3;
        private int declarations;
        private int dealsPlayed;
        private int declarer = -1;
        private int partner = -1;
        private int marriageSearchTricks;
        private int povertyPartner = -1;
        private string contract = "normal";
        private Suit? soloSuit;
        private string phase = "declare";
        private bool finished;

        public override string GameId => "doppelkopf";
        public override string Name => "ドッペルコップ";
        public DoppelkopfGame(int players, DeterministicRandom rng, IReadOnlyDictionary<string, string> options)
        { Players = 4; this.rng = rng; sessionDeals = Math.Max(1, options.Integer("deals", 8)); StartDeal(); }
        private static List<List<DCard>> NewPiles() => Enumerable.Range(0, 4).Select(_ => new List<DCard>()).ToList();

        private void StartDeal()
        {
            foreach (List<DCard> pile in hands) pile.Clear(); foreach (List<DCard> pile in captured) pile.Clear();
            trick.Clear(); choices.Clear(); povertyCards.Clear(); Array.Clear(tricks, 0, 4);
            var deck = new List<DCard>();
            for (int copy = 0; copy < 2; copy++)
                deck.AddRange(Cards.StandardDeck(new[] { 1, 9, 10, 11, 12, 13 }).Select(card => new DCard(card, copy)));
            rng.Shuffle(deck); dealer = (dealer + 1) % 4;
            for (int round = 0; round < 12; round++) for (int offset = 1; offset <= 4; offset++) hands[(dealer + offset) % 4].Add(Pop(deck));
            declarations = 0; declarer = partner = povertyPartner = -1; marriageSearchTricks = 0;
            contract = "normal"; soloSuit = null; phase = "declare"; CurrentPlayer = (dealer + 1) % 4;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "declare")
            {
                var actions = new List<Action> { new Action("contract", value: "normal") };
                int clubQueens = hands[actual].Count(card => card.Card == new Card(Suit.Clubs, 12));
                if (clubQueens == 2) actions.Add(new Action("contract", value: "marriage"));
                if (hands[actual].Count(IsNormalTrump) <= 3) actions.Add(new Action("contract", value: "poverty"));
                foreach (Suit suit in Enum.GetValues(typeof(Suit))) actions.Add(new Action("contract", value: "solo:" + Card.SuitCode(suit)));
                actions.Add(new Action("contract", value: "queen_solo")); actions.Add(new Action("contract", value: "jack_solo"));
                return actions;
            }
            if (phase == "poverty_offer")
            {
                int remainingTrumps = hands[actual].Count(IsNormalTrump);
                IEnumerable<DCard> cards = remainingTrumps > 0 ? hands[actual].Where(IsNormalTrump) : hands[actual];
                return cards.Select(card => CardAction("offer_poverty", card)).ToArray();
            }
            if (phase == "poverty_return") return hands[actual].Select(card => CardAction("return_poverty", card)).ToArray();

            IEnumerable<DCard> playable = hands[actual];
            if (trick.Count > 0)
            {
                DCard lead = trick[0].Item2;
                DCard[] follow = IsTrump(lead) ? playable.Where(IsTrump).ToArray()
                    : playable.Where(card => !IsTrump(card) && card.Card.Suit == lead.Card.Suit).ToArray();
                if (follow.Length > 0) playable = follow;
            }
            return playable.Select(card => CardAction("play", card)).ToArray();
        }

        private static Action CardAction(string kind, DCard card) => new Action(kind, card.Card, value: card.Id);

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "declare")
            {
                choices.Add(ParseChoice(player, action.Value!)); declarations++;
                if (declarations < 4) { CurrentPlayer = (player + 1) % 4; return; }
                SelectContract(); return;
            }
            if (phase == "poverty_offer")
            {
                DCard card = Find(hands[player], action.Value!); hands[player].Remove(card); povertyCards.Add(card);
                if (povertyCards.Count < 3) return;
                hands[povertyPartner].AddRange(povertyCards); phase = "poverty_return"; CurrentPlayer = povertyPartner; return;
            }
            if (phase == "poverty_return")
            {
                DCard card = Find(hands[player], action.Value!); hands[player].Remove(card); hands[declarer].Add(card);
                if (hands[declarer].Count < 12) return;
                phase = "play"; CurrentPlayer = (dealer + 1) % 4; return;
            }
            DCard played = Find(hands[player], action.Value!); hands[player].Remove(played); trick.Add(Tuple.Create(player, played));
            if (trick.Count < 4) { CurrentPlayer = (player + 1) % 4; return; }
            int winner = TrickWinner(); tricks[winner]++; captured[winner].AddRange(trick.Select(item => item.Item2)); trick.Clear();
            if (contract == "marriage" && partner < 0 && marriageSearchTricks < 3)
            {
                marriageSearchTricks++; if (winner != declarer) partner = winner;
            }
            if (tricks.Sum() >= 12) FinishDeal(); else CurrentPlayer = winner;
        }

        private void SelectContract()
        {
            ContractChoice selected = choices.OrderByDescending(choice => choice.Rank).First();
            contract = selected.Contract; soloSuit = selected.Suit; declarer = selected.Rank > 0 ? selected.Player : -1;
            if (contract == "normal")
            {
                int[] holders = Enumerable.Range(0, 4).Where(player => hands[player].Any(card => card.Card == new Card(Suit.Clubs, 12))).ToArray();
                if (holders.Length == 1) { contract = "marriage"; declarer = holders[0]; }
            }
            if (contract == "marriage") { phase = "play"; CurrentPlayer = (dealer + 1) % 4; return; }
            if (contract == "poverty")
            {
                povertyPartner = (declarer + 1) % 4; partner = povertyPartner; phase = "poverty_offer"; CurrentPlayer = declarer; return;
            }
            phase = "play"; CurrentPlayer = (dealer + 1) % 4;
        }

        private static ContractChoice ParseChoice(int player, string value)
        {
            string[] parts = value.Split(':'); string name = parts[0]; Suit? suit = parts.Length > 1 ? Card.ParseSuit(parts[1]) : (Suit?)null;
            int rank = name == "normal" ? 0 : name == "marriage" ? 1 : name == "poverty" ? 2 : name == "solo" ? 3 : 4;
            return new ContractChoice(player, name, rank, suit);
        }
        private bool IsTrump(DCard card)
        {
            if (contract == "queen_solo") return card.Card.Rank == 12;
            if (contract == "jack_solo") return card.Card.Rank == 11;
            if (card.Card == new Card(Suit.Hearts, 10) || card.Card.Rank == 12 || card.Card.Rank == 11) return true;
            Suit trumpSuit = contract == "solo" ? soloSuit.GetValueOrDefault() : Suit.Diamonds;
            return card.Card.Suit == trumpSuit;
        }
        private static bool IsNormalTrump(DCard card) => card.Card == new Card(Suit.Hearts, 10) || card.Card.Rank == 12 ||
            card.Card.Rank == 11 || card.Card.Suit == Suit.Diamonds;
        private int TrumpStrength(DCard card)
        {
            if (contract != "queen_solo" && contract != "jack_solo" && card.Card == new Card(Suit.Hearts, 10)) return 120;
            int suit = card.Card.Suit == Suit.Clubs ? 4 : card.Card.Suit == Suit.Spades ? 3 : card.Card.Suit == Suit.Hearts ? 2 : 1;
            if (card.Card.Rank == 12) return 100 + suit;
            if (card.Card.Rank == 11) return 90 + suit;
            return PlainStrength(card.Card);
        }
        private int TrickWinner()
        {
            DCard lead = trick[0].Item2;
            IEnumerable<Tuple<int, DCard>> eligible = trick.Any(item => IsTrump(item.Item2)) ? trick.Where(item => IsTrump(item.Item2))
                : trick.Where(item => !IsTrump(item.Item2) && item.Item2.Card.Suit == lead.Card.Suit);
            return eligible.OrderByDescending(item => IsTrump(item.Item2) ? TrumpStrength(item.Item2) : PlainStrength(item.Item2.Card)).First().Item1;
        }
        private void FinishDeal()
        {
            HashSet<int> reTeam = ReTeam(); int rePoints = reTeam.Sum(player => captured[player].Sum(card => Points(card.Card)));
            bool reWins = rePoints >= 121; int losingPoints = reWins ? 240 - rePoints : rePoints;
            int gamePoints = 1 + (losingPoints < 90 ? 1 : 0) + (losingPoints < 60 ? 1 : 0) + (losingPoints < 30 ? 1 : 0) + (losingPoints == 0 ? 1 : 0);
            if (contract == "solo" || contract.EndsWith("_solo", StringComparison.Ordinal)) gamePoints *= 3;
            for (int player = 0; player < 4; player++) scores[player] += (reTeam.Contains(player) == reWins ? gamePoints : -gamePoints);
            dealsPlayed++; if (dealsPlayed >= sessionDeals) finished = true; else StartDeal();
        }
        private HashSet<int> ReTeam()
        {
            if (contract == "solo" || contract == "queen_solo" || contract == "jack_solo") return new HashSet<int> { declarer };
            if (contract == "poverty") return new HashSet<int> { declarer, partner };
            if (contract == "marriage") { var team = new HashSet<int> { declarer }; if (partner >= 0) team.Add(partner); return team; }
            return new HashSet<int>(Enumerable.Range(0, 4).Where(player =>
                hands[player].Concat(captured[player]).Any(card => card.Card == new Card(Suit.Clubs, 12))));
        }
        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "declare")
            {
                Action marriage = actions.FirstOrDefault(action => action.Value == "marriage"); if (marriage.Value != null) return marriage;
                Action poverty = actions.FirstOrDefault(action => action.Value == "poverty"); if (poverty.Value != null) return poverty;
                return actions[0];
            }
            if (phase == "poverty_offer" || phase == "poverty_return") return actions.OrderBy(action => PlainStrength(action.Card.GetValueOrDefault())).First();
            return actions.OrderBy(action => IsTrump(Find(hands[player], action.Value!)) ? TrumpStrength(Find(hands[player], action.Value!)) : PlainStrength(action.Card.GetValueOrDefault())).First();
        }
        private static int PlainStrength(Card card) => card.Rank == 1 ? 6 : card.Rank == 10 ? 5 : card.Rank == 13 ? 4 : card.Rank == 12 ? 3 : card.Rank == 11 ? 2 : 1;
        private static int Points(Card card) => card.Rank == 1 ? 11 : card.Rank == 10 ? 10 : card.Rank == 13 ? 4 : card.Rank == 12 ? 3 : card.Rank == 11 ? 2 : 0;
        private static DCard Find(List<DCard> cards, string id) => cards.Single(card => card.Id == id);
        private static DCard Pop(List<DCard> cards) { DCard card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over."); int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 4).Where(player => scores[player] == high), scores.Select(value => (double)value), "Doppelkopf session points", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer; string knownRole = contract == "normal"
                ? (hands[viewer].Concat(captured[viewer]).Any(card => card.Card == new Card(Suit.Clubs, 12)) ? "Re" : "Kontra")
                : viewer == declarer || viewer == partner ? "declarer-side" : "defender";
            return $"phase={phase} deal={dealsPlayed + 1}/{sessionDeals} dealer=P{dealer} contract={contract}{(soloSuit.HasValue ? ":" + Card.SuitCode(soloSuit.Value) : "")} " +
                $"declarer={(declarer < 0 ? "-" : "P" + declarer)} partner={(partner < 0 ? "hidden" : "P" + partner)} your_role={knownRole} " +
                $"trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] tricks=[{string.Join(",", tricks)}] scores=[{string.Join(",", scores)}] " +
                $"hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("doppelkopf", "ドッペルコップ", 4, 4, "hidden-team point-trick",
                "9～Aを2組使う48枚。通常はheart10、全Q/J、diamondが切り札でclub Q所持者がRe陣営。Marriage、Povertyの3枚交換、suit/queen/jack soloを宣言し、240 card pointの121点とSchneider段階を8deal精算する（採用仕様ではRe/Kontra宣言と特殊札bonusなし）。",
                "gokurakism/Doppelkopf", new Dictionary<string, string> { { "deals", "8" } }),
            (players, random, options) => new DoppelkopfGame(players, random, options));
    }
}
