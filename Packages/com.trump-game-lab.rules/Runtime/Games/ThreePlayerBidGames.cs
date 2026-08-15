using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    internal static class ThreePlayerBidGames
    {
        public static void RegisterGames(GameRegistry registry)
        {
            NinetyNineGame.Register(registry);
            FiveHundredGame.Register(registry);
        }
    }

    public sealed class NinetyNineGame : GameBase
    {
        private readonly DeterministicRandom rng;
        private readonly int sessionDeals;
        private readonly int? targetScore;
        private readonly List<List<Card>> hands = new List<List<Card>>
        {
            new List<Card>(), new List<Card>(), new List<Card>()
        };
        private readonly List<List<Card>> bidCards = new List<List<Card>>
        {
            new List<Card>(), new List<Card>(), new List<Card>()
        };
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly int[] tricks = new int[3];
        private readonly int[] scores = new int[3];
        private readonly int?[] revealedBids = new int?[3];
        private readonly bool[] declared = new bool[3];
        private int dealer = 2;
        private int completedDeals;
        private int completedBids;
        private int premiumLevel;
        private int premiumHolder = -1;
        private int premiumPasses;
        private int premiumActions;
        private Suit? trump;
        private string phase = "choose_bid";
        private bool finished;

        public override string GameId => "ninety_nine";
        public override string Name => "ナインティナイン";

        public NinetyNineGame(int players, DeterministicRandom rng,
            IReadOnlyDictionary<string, string> options)
        {
            Players = 3; this.rng = rng;
            sessionDeals = Math.Max(1, options.Integer("deals", 9));
            targetScore = options.ContainsKey("target_score")
                ? Math.Max(1, options.Integer("target_score", 100)) : (int?)null;
            StartDeal(null);
        }

        private void StartDeal(int? previousSuccesses)
        {
            foreach (List<Card> hand in hands) hand.Clear();
            foreach (List<Card> bid in bidCards) bid.Clear();
            trick.Clear(); Array.Clear(tricks, 0, 3);
            Array.Clear(declared, 0, 3);
            List<Card> deck = Cards.Shuffled(Cards.StandardDeck(new[] { 1, 6, 7, 8, 9, 10, 11, 12, 13 }), rng);
            dealer = (dealer + 1) % 3;
            for (int round = 0; round < 12; round++)
                for (int offset = 1; offset <= 3; offset++) hands[(dealer + offset) % 3].Add(Pop(deck));
            trump = previousSuccesses.HasValue
                ? previousSuccesses.Value == 3 ? Suit.Clubs : previousSuccesses.Value == 2 ? Suit.Hearts
                    : previousSuccesses.Value == 1 ? Suit.Spades : Suit.Diamonds
                : (Suit?)null;
            completedBids = 0; premiumLevel = 0; premiumHolder = -1;
            premiumPasses = 0; premiumActions = 0; phase = "choose_bid"; CurrentPlayer = (dealer + 1) % 3;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "choose_bid") return hands[actual]
                .Select(card => new Action("set_bid_card", card)).ToArray();
            if (phase == "premium")
            {
                var actions = new List<Action> { new Action("pass_premium") };
                if (premiumLevel < 1) actions.Add(new Action("declare"));
                if (premiumLevel < 2 || premiumLevel == 2 && declared[actual] &&
                    Priority(actual) < Priority(premiumHolder)) actions.Add(new Action("reveal"));
                return actions;
            }
            IEnumerable<Card> cards = hands[actual];
            if (trick.Count > 0)
            {
                Suit led = trick[0].Item2.Suit; Card[] follow = cards.Where(card => card.Suit == led).ToArray();
                if (follow.Length > 0) cards = follow;
            }
            return cards.Select(card => new Action("play", card)).ToArray();
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "choose_bid")
            {
                Card card = action.Card!.Value; hands[player].Remove(card); bidCards[player].Add(card);
                if (bidCards[player].Count < 3) return;
                completedBids++;
                if (completedBids < 3) CurrentPlayer = (player + 1) % 3;
                else { phase = "premium"; CurrentPlayer = (dealer + 1) % 3; }
                return;
            }
            if (phase == "premium")
            {
                premiumActions++;
                if (action.Kind == "pass_premium") premiumPasses++;
                else
                {
                    if (action.Kind == "declare") declared[player] = true;
                    premiumLevel = action.Kind == "declare" ? 1 : 2;
                    premiumHolder = player; premiumPasses = 0;
                }
                int needed = premiumLevel == 0 ? 3 : 2;
                if (premiumPasses >= needed || premiumLevel == 2 && premiumActions >= 3 && premiumPasses >= 2)
                {
                    phase = "play"; CurrentPlayer = (dealer + 1) % 3;
                }
                else CurrentPlayer = (player + 1) % 3;
                return;
            }
            Card played = action.Card!.Value; hands[player].Remove(played); trick.Add(Tuple.Create(player, played));
            if (trick.Count < 3) { CurrentPlayer = (player + 1) % 3; return; }
            int winner = TrickWinner(); tricks[winner]++; trick.Clear();
            if (hands.All(hand => hand.Count == 0)) FinishDeal();
            else CurrentPlayer = winner;
        }

        private int TrickWinner()
        {
            Suit led = trick[0].Item2.Suit;
            IEnumerable<Tuple<int, Card>> eligible = trump.HasValue && trick.Any(item => item.Item2.Suit == trump.Value)
                ? trick.Where(item => item.Item2.Suit == trump.Value)
                : trick.Where(item => item.Item2.Suit == led);
            return eligible.OrderByDescending(item => Strength(item.Item2)).First().Item1;
        }

        private void FinishDeal()
        {
            bool[] succeeded = Enumerable.Range(0, 3).Select(player => Bid(player) == tricks[player]).ToArray();
            int successCount = succeeded.Count(value => value);
            int contractPoints = successCount == 3 ? 10 : successCount == 2 ? 20 : successCount == 1 ? 30 : 0;
            for (int player = 0; player < 3; player++)
            {
                revealedBids[player] = succeeded[player] ? Bid(player) : (int?)null;
                scores[player] += tricks[player] + (succeeded[player] ? contractPoints : 0);
            }
            if (premiumHolder >= 0)
            {
                int premium = premiumLevel == 1 ? 30 : 60;
                if (succeeded[premiumHolder]) scores[premiumHolder] += premium;
                else for (int player = 0; player < 3; player++) if (player != premiumHolder) scores[player] += premium;
            }
            completedDeals++;
            if (targetScore.HasValue && scores.Max() >= targetScore.Value)
            {
                for (int player = 0; player < 3; player++)
                    if (scores[player] >= targetScore.Value) scores[player] += 100;
                phase = "finished"; finished = true;
            }
            else if (!targetScore.HasValue && completedDeals >= sessionDeals)
            {
                phase = "finished"; finished = true;
            }
            else StartDeal(successCount);
        }

        private int Bid(int player) => bidCards[player].Sum(card =>
            card.Suit == Suit.Clubs ? 3 : card.Suit == Suit.Hearts ? 2 : card.Suit == Suit.Spades ? 1 : 0);

        private int Priority(int player) => (player - (dealer + 1) % 3 + 3) % 3;

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "choose_bid")
            {
                int desired = Math.Min(9, hands[player].Concat(bidCards[player]).Count(card => Strength(card) >= 12));
                int current = Bid(player);
                return actions.OrderBy(action => Math.Abs(current + BidValue(action.Card!.Value) - desired))
                    .ThenBy(action => Strength(action.Card!.Value)).First();
            }
            if (phase == "premium") return actions.First(action => action.Kind == "pass_premium");
            int target = Bid(player);
            return tricks[player] < target
                ? actions.OrderByDescending(action => Strength(action.Card!.Value)).First()
                : actions.OrderBy(action => Strength(action.Card!.Value)).First();
        }

        private static int BidValue(Card card) => card.Suit == Suit.Clubs ? 3 : card.Suit == Suit.Hearts ? 2 : card.Suit == Suit.Spades ? 1 : 0;

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 3).Where(player => scores[player] == high),
                scores.Select(value => (double)value), targetScore.HasValue
                    ? "first to " + targetScore.Value + " plus game bonus"
                    : sessionDeals + "-deal session", TurnCount);
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            string[] shownBids = Enumerable.Range(0, 3).Select(player => player == viewer || player == premiumHolder && premiumLevel > 0
                ? string.Join(" ", bidCards[player]) : bidCards[player].Count == 3 ? "hidden" : bidCards[player].Count + "/3").ToArray();
            string openHand = premiumLevel == 2 && premiumHolder >= 0 ?
                $" open_hand_P{premiumHolder}=[{string.Join(" ", hands[premiumHolder])}]" : "";
            string[] claims = Enumerable.Range(0, 3).Select(index => completedDeals == 0 ? "-" :
                revealedBids[index].HasValue ? revealedBids[index]!.Value.ToString() : "hidden").ToArray();
            int shownDeal = finished ? completedDeals : completedDeals + 1;
            string session = targetScore.HasValue ? $"deal={shownDeal} target_score={targetScore.Value}" :
                $"deal={shownDeal}/{sessionDeals}";
            return $"phase={phase} {session} dealer=P{dealer} trump={(trump.HasValue ? Card.SuitCode(trump.Value) : "N")} " +
                $"bids=[{string.Join(" | ", shownBids)}] premium={(premiumLevel == 0 ? "none" : premiumLevel == 1 ? "declare" : "reveal")} " +
                $"trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] tricks=[{string.Join(",", tricks)}] " +
                $"scores=[{string.Join(",", scores)}] revealed_bids=[{string.Join(",", claims)}] " +
                $"hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]{openHand}\n" +
                $"your hand: {string.Join(" ", hands[viewer])}";
        }

        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("ninety_nine", "ナインティナイン", 3, 3, "exact-bid trick-taking",
                "6～Aの36枚から3枚のスート値で秘密bidし、残る9枚でexact tricksを狙う。成功claim、declare/reveal、成功人数連動の切り札・得点を含む9ディール戦。",
                "David Parlett / Pagat", new Dictionary<string, string>
                {
                    { "deals", "9" }, { "target_score", "100" }
                }),
            (players, random, options) => new NinetyNineGame(players, random, options));
    }

    public sealed class FiveHundredGame : GameBase
    {
        private sealed class FiveCard
        {
            public Card? Card { get; }
            public bool Joker => !Card.HasValue;
            public string Id => Joker ? "X" : Card!.Value.ToString();
            public FiveCard(Card? card) { Card = card; }
            public override string ToString() => Id;
        }

        private static readonly string[] RegularBids = Enumerable.Range(6, 5)
            .SelectMany(tricks => new[] { "S", "C", "D", "H", "N" }.Select(suit => tricks + suit)).ToArray();
        private readonly DeterministicRandom rng;
        private readonly int targetScore;
        private readonly List<List<FiveCard>> hands = new List<List<FiveCard>>
        {
            new List<FiveCard>(), new List<FiveCard>(), new List<FiveCard>()
        };
        private readonly List<FiveCard> kitty = new List<FiveCard>();
        private readonly List<FiveCard> discard = new List<FiveCard>();
        private readonly List<Tuple<int, FiveCard>> trick = new List<Tuple<int, FiveCard>>();
        private readonly HashSet<Suit> ledSuits=new HashSet<Suit>();
        private readonly int[] tricks = new int[3];
        private readonly int[] scores = new int[3];
        private int dealer = 2;
        private int declarer = -1;
        private int highBidder = -1;
        private string? highBid;
        private int consecutivePasses;
        private int totalPasses;
        private Suit? jokerSuit;
        private Suit? jokerLeadSuit;
        private string phase = "auction";
        private bool finished;

        public override string GameId => "five_hundred";
        public override string Name => "ファイブハンドレッド";

        public FiveHundredGame(int players, DeterministicRandom rng,
            IReadOnlyDictionary<string, string> options)
        {
            Players = 3; this.rng = rng;
            targetScore = Math.Max(1, options.Integer("target_score", 500)); StartDeal();
        }

        private void StartDeal()
        {
            foreach (List<FiveCard> hand in hands) hand.Clear();
            kitty.Clear(); discard.Clear(); trick.Clear();ledSuits.Clear(); Array.Clear(tricks, 0, 3);
            var deck = Cards.StandardDeck(new[] { 1, 7, 8, 9, 10, 11, 12, 13 })
                .Select(card => new FiveCard(card)).ToList();
            deck.Add(new FiveCard(null)); rng.Shuffle(deck); dealer = (dealer + 1) % 3;
            for (int round = 0; round < 3; round++)
                for (int offset = 1; offset <= 3; offset++) hands[(dealer + offset) % 3].Add(Pop(deck));
            for (int count = 0; count < 3; count++) kitty.Add(Pop(deck));
            for (int round = 0; round < 3; round++)
                for (int offset = 1; offset <= 3; offset++) hands[(dealer + offset) % 3].Add(Pop(deck));
            for (int round = 0; round < 4; round++)
                for (int offset = 1; offset <= 3; offset++) hands[(dealer + offset) % 3].Add(Pop(deck));
            declarer = -1; highBidder = -1; highBid = null; consecutivePasses = 0; totalPasses = 0;jokerSuit=null;jokerLeadSuit=null;
            phase = "auction"; CurrentPlayer = (dealer + 1) % 3;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "auction")
            {
                var actions = new List<Action> { new Action("pass") };
                actions.AddRange(RegularBids.Concat(new[] { "M", "OM" })
                    .Where(bid => highBid == null || BidRank(bid) > BidRank(highBid))
                    .Select(bid => new Action("bid", value: bid)));
                return actions;
            }
            if (phase == "discard")
            {
                if (discard.Count == 3) return new[] { new Action("finish_discard") };
                return hands[actual].Select(card => new Action("discard_to_kitty", card.Card, value: card.Id)).ToArray();
            }
            if(phase=="joker_nomination")
            {var actions=new List<Action>{new Action("leave_joker_wild")};actions.AddRange(Enum.GetValues(typeof(Suit)).Cast<Suit>().Select(suit=>new Action("nominate_joker",value:Card.SuitCode(suit))));return actions;}
            IEnumerable<FiveCard> cards = hands[actual];
            Suit? led=trick.Count==0?(Suit?)null:jokerLeadSuit??EffectiveSuit(trick[0].Item2);
            if (led.HasValue)
            {
                FiveCard[] follow = cards.Where(card => EffectiveSuit(card)==led.Value).ToArray();
                if (follow.Length > 0) cards = follow;
                else if((highBid=="M"||highBid=="OM")&&!jokerSuit.HasValue&&cards.Any(card=>card.Joker))cards=cards.Where(card=>card.Joker);
            }
            var plays=new List<Action>();foreach(FiveCard card in cards)
            {if(trick.Count==0&&card.Joker&&!ContractTrump().HasValue&&!jokerSuit.HasValue)
                plays.AddRange(Enum.GetValues(typeof(Suit)).Cast<Suit>().Where(suit=>!ledSuits.Contains(suit)||hands[actual].Count==1).Select(suit=>new Action("lead_joker",value:Card.SuitCode(suit))));
             else plays.Add(new Action("play",card.Card,value:card.Id));}return plays;
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "auction")
            {
                if (action.Kind == "pass") { consecutivePasses++; totalPasses++; }
                else { highBid = action.Value; highBidder = player; consecutivePasses = 0; }
                bool allPassed = highBid == null && totalPasses >= 3;
                bool auctionWon = highBid != null && consecutivePasses >= 2;
                if (allPassed || auctionWon) FinishAuction();
                else CurrentPlayer = (player + 1) % 3;
                return;
            }
            if (phase == "discard")
            {
                if (action.Kind == "discard_to_kitty")
                {
                    FiveCard card = hands[player].Single(item => item.Id == action.Value);
                    hands[player].Remove(card); discard.Add(card); return;
                }
                if(!ContractTrump().HasValue&&hands[declarer].Any(card=>card.Joker)){phase="joker_nomination";CurrentPlayer=declarer;}else{phase="play";CurrentPlayer=declarer;}return;
            }
            if(phase=="joker_nomination")
            {if(action.Kind=="nominate_joker")jokerSuit=Card.ParseSuit(action.Value!);phase="play";CurrentPlayer=declarer;return;}
            FiveCard played = action.Kind=="lead_joker"?hands[player].Single(item=>item.Joker):hands[player].Single(item => item.Id == action.Value);
            hands[player].Remove(played); trick.Add(Tuple.Create(player, played));
            if(action.Kind=="lead_joker")jokerLeadSuit=Card.ParseSuit(action.Value!);
            if (trick.Count < 3) { CurrentPlayer = (player + 1) % 3; return; }
            int winner = TrickWinner(); tricks[winner]++;Suit led=jokerLeadSuit??EffectiveSuit(trick[0].Item2)!.Value;ledSuits.Add(led);trick.Clear();jokerLeadSuit=null;
            if (hands.All(hand => hand.Count == 0)) FinishDeal();
            else CurrentPlayer = winner;
        }

        private void FinishAuction()
        {
            if (highBid == null)
            {
                phase = "play"; CurrentPlayer = (dealer + 1) % 3; return;
            }
            declarer = highBidder; hands[declarer].AddRange(kitty); kitty.Clear();
            phase = "discard"; CurrentPlayer = declarer;
        }

        private Suit? ContractTrump()
        {
            if (highBid == null || highBid == "M" || highBid == "OM" || highBid.EndsWith("N")) return null;
            return Card.ParseSuit(highBid.Substring(highBid.Length - 1));
        }

        private Suit EffectiveSuit(Card card)
        {
            Suit? trump = ContractTrump();
            if (trump.HasValue && card.Rank == 11 && card.Suit == SameColor(trump.Value)) return trump.Value;
            return card.Suit;
        }
        private Suit? EffectiveSuit(FiveCard card)=>card.Joker?(ContractTrump()??jokerSuit):EffectiveSuit(card.Card!.Value);

        private int TrickWinner()
        {
            Suit led=jokerLeadSuit??EffectiveSuit(trick[0].Item2)!.Value;
            Suit? trump = ContractTrump();
            IEnumerable<Tuple<int, FiveCard>> eligible = trump.HasValue && trick.Any(item => EffectiveSuit(item.Item2) == trump.Value)
                ? trick.Where(item => EffectiveSuit(item.Item2) == trump.Value)
                : trick.Where(item => EffectiveSuit(item.Item2) == led||item.Item2.Joker&&!jokerSuit.HasValue);
            return eligible.OrderByDescending(item => item.Item2.Joker?100:CardStrength(item.Item2.Card!.Value)).First().Item1;
        }

        private int CardStrength(Card card)
        {
            Suit? trump = ContractTrump();
            if (trump.HasValue && card.Rank == 11 && card.Suit == trump.Value) return 99;
            if (trump.HasValue && card.Rank == 11 && card.Suit == SameColor(trump.Value)) return 98;
            return card.Rank == 1 ? 14 : card.Rank;
        }

        private void FinishDeal()
        {
            if (declarer < 0)
            {
                for (int player = 0; player < 3; player++) scores[player] += tricks[player] * 10;
            }
            else
            {
                bool misere = highBid == "M" || highBid == "OM";
                int required = misere ? 0 : int.Parse(highBid!.Substring(0, highBid.Length - 1));
                bool success = misere ? tricks[declarer] == 0 : tricks[declarer] >= required;
                int value = BidScore(highBid!);
                if (success && !misere && tricks[declarer] == 10) value = Math.Max(value, 250);
                scores[declarer] += success ? value : -value;
                for (int player = 0; player < 3; player++) if (player != declarer) scores[player] += tricks[player] * 10;
            }
            bool wonContract=declarer>=0&&(highBid=="M"||highBid=="OM"?tricks[declarer]==0:tricks[declarer]>=int.Parse(highBid!.Substring(0,highBid.Length-1)));
            if (scores.Min() <= -targetScore||wonContract&&scores[declarer]>=targetScore) finished = true;
            else StartDeal();
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "auction")
            {
                int strength = hands[player].Count(card => card.Joker || card.Card.HasValue && CardStrength(card.Card.Value) >= 13);
                Action[] affordable = actions.Where(action => action.Kind == "bid" && BidScore(action.Value!) <= 160).ToArray();
                return strength >= 4 && affordable.Length > 0
                    ? affordable.OrderBy(action => BidScore(action.Value!)).First()
                    : actions.First(action => action.Kind == "pass");
            }
            if (phase == "discard")
            {
                return discard.Count == 3 ? actions[0]
                    : actions.OrderBy(action => action.Card.HasValue ? CardStrength(action.Card.Value) : 100).First();
            }
            if(phase=="joker_nomination")return actions.First(action=>action.Kind=="nominate_joker");
            bool avoid = declarer == player && (highBid == "M" || highBid == "OM");
            return avoid ? actions.OrderBy(action => action.Card.HasValue ? CardStrength(action.Card.Value) : 100).First()
                : actions.OrderByDescending(action => action.Card.HasValue ? CardStrength(action.Card.Value) : 100).First();
        }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 3).Where(player => scores[player] == high),
                scores.Select(value => (double)value), "500 or minus 500 boundary", TurnCount);
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            string openHand = highBid == "OM" && declarer >= 0 ? $" open_hand_P{declarer}=[{string.Join(" ", hands[declarer])}]" : "";
            return $"phase={phase} dealer=P{dealer} high_bid={(highBid ?? "-")} declarer={(declarer < 0 ? "-" : "P" + declarer)} " +
                $"kitty={kitty.Count} discarded={discard.Count}/3 trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] " +
                $"tricks=[{string.Join(",", tricks)}] scores=[{string.Join(",", scores)}] hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]{openHand}\n" +
                $"your hand: {string.Join(" ", hands[viewer])}";
        }

        private static int BidScore(string bid)
        {
            if (bid == "M") return 250;
            if (bid == "OM") return 500;
            int tricks = int.Parse(bid.Substring(0, bid.Length - 1));
            string suit = bid.Substring(bid.Length - 1);
            int baseValue = suit == "S" ? 40 : suit == "C" ? 60 : suit == "D" ? 80 : suit == "H" ? 100 : 120;
            return baseValue + (tricks - 6) * 100;
        }
        private static int BidRank(string bid)=>bid=="M"?230:bid=="OM"?490:BidScore(bid);

        private static Suit SameColor(Suit suit) => suit == Suit.Hearts ? Suit.Diamonds : suit == Suit.Diamonds ? Suit.Hearts
            : suit == Suit.Clubs ? Suit.Spades : Suit.Clubs;
        private static FiveCard Pop(List<FiveCard> cards) { FiveCard card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("five_hundred", "ファイブハンドレッド", 3, 3, "auction trick-taking",
                "32枚とJokerを10枚ずつ＋kitty3枚に配るPagat 3人版。6～10 tricks、Misere/Open Misere、No Trump Joker suit指定、bower、契約成功又は-500の終了を扱う。",
                "Pagat Three-player Five Hundred", new Dictionary<string, string> { { "target_score", "500" } }),
            (players, random, options) => new FiveHundredGame(players, random, options));
    }
}
