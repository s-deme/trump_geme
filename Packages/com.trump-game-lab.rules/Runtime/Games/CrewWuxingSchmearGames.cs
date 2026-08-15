using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    internal static class CrewWuxingSchmearGames
    {
        public static void RegisterGames(GameRegistry registry)
        { TrumpCrewGame.Register(registry); WuxingXiangkeGame.Register(registry); SchmearGame.Register(registry); }
    }

    public sealed class TrumpCrewGame : GameBase
    {
        private sealed class CrewCard
        {
            public Card? Card { get; }
            public bool Joker => !Card.HasValue;
            public string Id => Joker ? "JOKER" : Card!.Value.ToString();
            public CrewCard(Card? card) { Card = card; }
            public override string ToString() => Id;
        }
        private readonly DeterministicRandom rng;
        private readonly List<List<CrewCard>> hands;
        private readonly List<Tuple<int, CrewCard>> trick = new List<Tuple<int, CrewCard>>();
        private readonly int[] bids;
        private readonly int[] tricks;
        private readonly int finalStage;
        private readonly int maxAttempts;
        private int dealer;
        private int stage = 1;
        private int bidderCount;
        private int attempts;
        private Suit? trump;
        private Suit? jokerLeadSuit;
        private string dealerStrength = "-";
        private string phase = "strength";
        private bool finished;
        private bool cleared;
        public override string GameId => "trump_crew";
        public override string Name => "トランプクルー";
        public TrumpCrewGame(int players, DeterministicRandom rng, IReadOnlyDictionary<string, string> options)
        {
            Players = players; this.rng = rng; hands = Enumerable.Range(0, players).Select(_ => new List<CrewCard>()).ToList();
            bids = new int[players]; tricks = new int[players]; dealer = players - 1;
            finalStage = Math.Max(1, Math.Min(52 / players, options.Integer("final_stage", 52 / players)));
            maxAttempts = Math.Max(0, options.Integer("max_attempts", 0)); StartStage();
        }
        private void StartStage()
        {
            foreach (List<CrewCard> hand in hands) hand.Clear(); trick.Clear(); Array.Clear(bids, 0, Players); Array.Clear(tricks, 0, Players);
            var deck = Cards.StandardDeck().Select(card => new CrewCard(card)).ToList(); deck.Add(new CrewCard(null)); rng.Shuffle(deck);
            dealer = (dealer + 1) % Players;
            for (int round = 0; round < stage; round++) for (int offset = 1; offset <= Players; offset++) hands[(dealer + offset) % Players].Add(Pop(deck));
            CrewCard indicator = Pop(deck); trump = indicator.Card?.Suit; jokerLeadSuit = null;
            bidderCount = 0; dealerStrength = "-"; phase = "strength"; CurrentPlayer = dealer;
        }
        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "strength") return new[] { new Action("announce_strength", value: "weak"), new Action("announce_strength", value: "middle"), new Action("announce_strength", value: "strong") };
            if (phase == "bid")
            {
                int remaining = stage - bids.Sum();
                if (actual == dealer) return new[] { new Action("bid", value: Math.Max(0, remaining).ToString()) };
                return Enumerable.Range(0, Math.Max(0, remaining) + 1).Select(value => new Action("bid", value: value.ToString())).ToArray();
            }
            IEnumerable<CrewCard> cards = hands[actual];
            Suit? ledSuit = LedSuit();
            if (trick.Count > 0 && ledSuit.HasValue)
            {
                CrewCard[] follow = cards.Where(card => !card.Joker && card.Card!.Value.Suit == ledSuit.Value).ToArray();
                if (follow.Length > 0) cards = follow.Concat(hands[actual].Where(card => card.Joker));
            }
            return cards.SelectMany(card => PlayActions(card, trick.Count == 0)).ToArray();
        }
        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "strength") { dealerStrength = action.Value!; phase = "bid"; CurrentPlayer = (dealer + 1) % Players; return; }
            if (phase == "bid")
            {
                bids[player] = int.Parse(action.Value!); bidderCount++;
                if (bidderCount >= Players) { phase = "play"; CurrentPlayer = (dealer + 1) % Players; }
                else CurrentPlayer = (player + 1) % Players;
                return;
            }
            string cardId = action.Value!.StartsWith("JOKER", StringComparison.Ordinal) ? "JOKER" : action.Value;
            CrewCard card = hands[player].Single(item => item.Id == cardId);
            if (trick.Count == 0 && card.Joker) jokerLeadSuit = JokerSuit(action.Value);
            hands[player].Remove(card); trick.Add(Tuple.Create(player, card));
            if (trick.Count < Players) { CurrentPlayer = (player + 1) % Players; return; }
            int winner = TrickWinner(); tricks[winner]++; trick.Clear(); jokerLeadSuit = null;
            if (tricks.Sum() >= stage) FinishStage(); else CurrentPlayer = winner;
        }
        private int TrickWinner()
        {
            Tuple<int, CrewCard>? joker = trick.FirstOrDefault(item => item.Item2.Joker); if (joker != null) return joker.Item1;
            Suit led = LedSuit()!.Value;
            IEnumerable<Tuple<int, CrewCard>> eligible = trump.HasValue && trick.Any(item => item.Item2.Card!.Value.Suit == trump.Value)
                ? trick.Where(item => item.Item2.Card!.Value.Suit == trump.Value) : trick.Where(item => item.Item2.Card!.Value.Suit == led);
            return eligible.OrderByDescending(item => Strength(item.Item2.Card!.Value)).First().Item1;
        }
        private void FinishStage()
        {
            bool success = Enumerable.Range(0, Players).All(player => bids[player] == tricks[player]);
            if (success)
            {
                attempts = 0; if (stage >= finalStage) { cleared = finished = true; return; } stage++;
            }
            else if (++attempts >= maxAttempts && maxAttempts > 0) { finished = true; return; }
            StartStage();
        }
        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "strength")
            {
                double quality = hands[player].Average(CardQuality);
                return actions[quality >= 1.05 ? 2 : quality >= 0.65 ? 1 : 0];
            }
            if (phase == "bid")
            {
                if (player == dealer) return actions[0];
                double average = hands[player].Average(CardQuality);
                double estimateValue = (double)stage / Players + (average - 0.82) * stage * 0.7;
                double dealerAdjustment = (double)stage / (Players * 4);
                if (dealerStrength == "weak") estimateValue += dealerAdjustment;
                else if (dealerStrength == "strong") estimateValue -= dealerAdjustment;
                int estimate = Math.Max(0, (int)Math.Round(estimateValue, MidpointRounding.AwayFromZero));
                return actions.OrderBy(action => Math.Abs(int.Parse(action.Value!) - estimate)).First();
            }
            return ChooseCooperativePlay(player, actions);
        }
        private Action ChooseCooperativePlay(int player, IReadOnlyList<Action> actions)
        {
            Action[] distinctCards = actions.GroupBy(ActionCardId).Select(group =>
                group.Key == "JOKER" ? group.First(action => action.Value == "JOKER") : group.First()).ToArray();
            bool selfNeedsTrick = tricks[player] < bids[player];
            if (trick.Count == 0)
                return selfNeedsTrick
                    ? distinctCards.OrderByDescending(ActionPower).First()
                    : distinctCards.OrderBy(ActionPower).First();

            int currentWinner = CurrentTrickWinner();
            bool winnerNeedsTrick = tricks[currentWinner] < bids[currentWinner];
            Action[] winning = distinctCards.Where(WouldCurrentlyWin).ToArray();
            Action[] losing = distinctCards.Where(action => !WouldCurrentlyWin(action)).ToArray();
            if (winnerNeedsTrick && losing.Length > 0)
                return losing.OrderByDescending(ActionPower).First();
            if (!winnerNeedsTrick && selfNeedsTrick && winning.Length > 0)
                return winning.OrderBy(ActionPower).First();
            if (losing.Length > 0)
                return (selfNeedsTrick ? losing.OrderBy(ActionPower) : losing.OrderByDescending(ActionPower)).First();
            return winning.OrderBy(ActionPower).First();
        }
        private bool WouldCurrentlyWin(Action action)
        {
            if (ActionCardId(action) == "JOKER") return !trick.Any(item => item.Item2.Joker);
            if (trick.Any(item => item.Item2.Joker)) return false;
            Card candidate = action.Card!.Value;
            Tuple<int, CrewCard> winner = trick.First(item => item.Item1 == CurrentTrickWinner());
            return TrickPower(candidate) > TrickPower(winner.Item2.Card!.Value);
        }
        private int CurrentTrickWinner()
        {
            Tuple<int, CrewCard>? joker = trick.FirstOrDefault(item => item.Item2.Joker);
            if (joker != null) return joker.Item1;
            return trick.OrderByDescending(item => TrickPower(item.Item2.Card!.Value)).First().Item1;
        }
        private int TrickPower(Card card)
        {
            Suit? led = LedSuit();
            int suitPower = trump.HasValue && card.Suit == trump.Value ? 2 : led.HasValue && card.Suit == led.Value ? 1 : 0;
            return suitPower * 100 + Strength(card);
        }
        private double CardQuality(CrewCard card)
        {
            if (card.Joker) return 2;
            double quality = (double)Strength(card.Card!.Value) / 14;
            if (trump.HasValue && card.Card.Value.Suit == trump.Value) quality += 1;
            return quality;
        }
        private int ActionPower(Action action)
        {
            if (ActionCardId(action) == "JOKER") return 1000;
            Card card = action.Card!.Value;
            return (trump.HasValue && card.Suit == trump.Value ? 100 : 0) + Strength(card);
        }
        private static string ActionCardId(Action action) =>
            action.Value!.StartsWith("JOKER", StringComparison.Ordinal) ? "JOKER" : action.Value;
        private static IEnumerable<Action> PlayActions(CrewCard card, bool leading)
        {
            if (!card.Joker || !leading) return new[] { new Action("play", card.Card, value: card.Id) };
            return new[] { "JOKER", "JOKER:C", "JOKER:D", "JOKER:H", "JOKER:S" }
                .Select(value => new Action("play", value: value));
        }
        private Suit? LedSuit() => trick.Count == 0 ? null : trick[0].Item2.Joker ? jokerLeadSuit : trick[0].Item2.Card!.Value.Suit;
        private static Suit? JokerSuit(string value) => value == "JOKER" ? (Suit?)null : Card.ParseSuit(value.Substring("JOKER:".Length));
        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static CrewCard Pop(List<CrewCard> cards) { CrewCard card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over."); double value = cleared ? finalStage : stage - 1;
            return new GameResult(cleared ? Enumerable.Range(0, Players) : Array.Empty<int>(), Enumerable.Repeat(value, Players), cleared ? "all cooperative stages cleared" : "attempt limit reached", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer; string attemptLimit = maxAttempts == 0 ? "unlimited" : maxAttempts.ToString();
            return $"phase={phase} stage={stage}/{finalStage} attempt={attempts + 1}/{attemptLimit} dealer=P{dealer} dealer_strength={dealerStrength} trump={(trump.HasValue ? Card.SuitCode(trump.Value) : "none")} " +
                $"bids=[{string.Join(",", bids)}] tricks=[{string.Join(",", tricks)}] table=[{string.Join(" ", trick.Select((item, index) => "P" + item.Item1 + ":" + item.Item2 + (index == 0 && item.Item2.Joker ? ">" + (jokerLeadSuit.HasValue ? Card.SuitCode(jokerLeadSuit.Value) : "any") : "")))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("trump_crew", "トランプクルー", 3, 5, "cooperative exact-trick", "52枚＋Joker。人数別stage上限まで手札を1枚ずつ増やす。余り札がtrump（Jokerならno-trump）。dealerの強度公開後、他者がbidしdealerが残数を引き受ける。Jokerは常時最強で、lead時はsuit指定または無指定。全員exactなら次stage、失敗は同stageを再挑戦する。", "草場純『トランプクルーのルール』/ ゴクラキズム", new Dictionary<string, string> { { "final_stage", "既定52÷人数。短縮時の最終stage" }, { "max_attempts", "既定0（無制限）。正整数なら同一stageの試行上限" } }),
            (players, random, options) => new TrumpCrewGame(players, random, options));
    }

    public sealed class WuxingXiangkeGame : GameBase
    {
        private readonly DeterministicRandom rng;
        private readonly List<List<Card>> hands = Enumerable.Range(0, 5).Select(_ => new List<Card>()).ToList();
        private readonly List<List<Card>> captured = Enumerable.Range(0, 5).Select(_ => new List<Card>()).ToList();
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly int[] scores = new int[5];
        private readonly List<Card> kitty = new List<Card>();
        private int dealer = 4;
        private int deals;
        private bool twoPartners;
        private Suit firstSuit;
        private bool finished;
        public override string GameId => "wuxing_xiangke";
        public override string Name => "五行相克";
        public WuxingXiangkeGame(int players, DeterministicRandom rng) { Players = 5; this.rng = rng; StartDeal(); }
        private void StartDeal()
        {
            foreach (List<Card> pile in hands) pile.Clear(); foreach (List<Card> pile in captured) pile.Clear(); trick.Clear(); kitty.Clear();
            List<Card> deck = Cards.Shuffled(Cards.StandardDeck(), rng); dealer = (dealer + 1) % 5;
            for (int round = 0; round < 10; round++) for (int offset = 1; offset <= 5; offset++) hands[(dealer + offset) % 5].Add(Pop(deck));
            kitty.Add(Pop(deck)); kitty.Add(Pop(deck)); firstSuit = kitty[0].Suit; twoPartners = kitty.Count(PointCard) == 2; CurrentPlayer = (dealer + 1) % 5;
        }
        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player); IEnumerable<Card> cards = hands[actual]; Suit led = trick.Count == 0 ? firstSuit : trick[0].Item2.Suit;
            if (trick.Count > 0 || captured.Sum(pile => pile.Count) == 0)
            { Card[] follow = cards.Where(card => card.Suit == led).ToArray(); if (follow.Length > 0) cards = follow; }
            return cards.Select(card => new Action("play", card)).ToArray();
        }
        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++; Card card = action.Card!.Value;
            hands[player].Remove(card); trick.Add(Tuple.Create(player, card));
            if (trick.Count < 5) { CurrentPlayer = (player + 1) % 5; return; }
            Suit led = trick[0].Item2.Suit; IEnumerable<Tuple<int, Card>> eligible = trick.Any(item => item.Item2.Suit == Suit.Spades)
                ? trick.Where(item => item.Item2.Suit == Suit.Spades) : trick.Where(item => item.Item2.Suit == led);
            int winner = eligible.OrderByDescending(item => Strength(item.Item2)).First().Item1; captured[winner].AddRange(trick.Select(item => item.Item2));
            if (captured.Sum(pile => pile.Count) == 5) captured[winner].AddRange(kitty); trick.Clear();
            if (hands.All(hand => hand.Count == 0)) FinishDeal(); else CurrentPlayer = winner;
        }
        private void FinishDeal()
        {
            int[] points = captured.Select(pile => pile.Count(PointCard)).ToArray();
            for (int player = 0; player < 5; player++)
            {
                if (twoPartners) scores[player] -= Math.Abs(12 - points[player] - points[(player + 2) % 5] - points[(player + 3) % 5]);
                else { int partnerPoints = points[(player + 3) % 5]; scores[player] += points[player] <= partnerPoints ? points[player] : -(points[player] - partnerPoints); }
            }
            deals++; if (deals >= 5) finished = true; else StartDeal();
        }
        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1) => LegalActions(player).OrderBy(action => Strength(action.Card!.Value)).First();
        private static bool PointCard(Card card) => card.Rank == 1 || card.Rank >= 10;
        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over."); int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 5).Where(player => scores[player] == high), scores.Select(value => (double)value), "five directed-partner deals", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer; return $"deal={deals + 1}/5 dealer=P{dealer} mode={(twoPartners ? "two-partners" : "one-partner")} start_suit={Card.SuitCode(firstSuit)} kitty=[{string.Join(" ", kitty)}] " +
                $"public_points=[{string.Join(",", captured.Select(pile => pile.Count(PointCard)))}] scores=[{string.Join(",", scores)}] table=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("wuxing_xiangke", "五行相克", 5, 5, "directed-partner point-trick", "52枚を各10枚＋公開2枚。最初の公開札suitを仮leadとし、spade固定trumpのmust-followを行う。A/K/Q/J/10が各1点。公開札の得点札が2枚なら非隣接2人、0～1枚なら2席先1人を一方向partnerとし、12との差またはpartner点との関係で5deal採点する。", "gokurakism/Gogyo"),
            (players, random, options) => new WuxingXiangkeGame(players, random));
    }

    public sealed class SchmearGame : GameBase
    {
        private sealed class SCard
        {
            public Card? Card { get; }
            public bool Joker => !Card.HasValue;
            public string Id => Joker ? "JOKER" : Card!.Value.ToString();
            public SCard(Card? card) { Card = card; }
            public override string ToString() => Id;
        }
        private readonly DeterministicRandom rng;
        private readonly List<List<SCard>> hands;
        private readonly List<List<SCard>> captured;
        private readonly List<Tuple<int, SCard>> trick = new List<Tuple<int, SCard>>();
        private readonly List<SCard> stock = new List<SCard>();
        private readonly int[] scores;
        private readonly int[] dealSpecial;
        private readonly int targetScore;
        private int dealer;
        private int bidsMade;
        private int highBid;
        private int declarer = -1;
        private int partner = -1;
        private int exchangePlayer;
        private int discarded;
        private Suit trump;
        private Card calledCard;
        private bool partnerRevealed;
        private IReadOnlyList<int> winners = Array.Empty<int>();
        private string phase = "bid";
        private bool finished;
        public override string GameId => "schmear";
        public override string Name => "シュミア";
        public SchmearGame(int players, DeterministicRandom rng, IReadOnlyDictionary<string, string> options)
        {
            Players = players; this.rng = rng; hands = Enumerable.Range(0, players).Select(_ => new List<SCard>()).ToList();
            captured = Enumerable.Range(0, players).Select(_ => new List<SCard>()).ToList(); scores = new int[players]; dealSpecial = new int[players];
            targetScore = Math.Max(1, options.Integer("target_score", 21)); dealer = players - 1; StartDeal();
        }
        private void StartDeal()
        {
            foreach (List<SCard> pile in hands) pile.Clear(); foreach (List<SCard> pile in captured) pile.Clear(); trick.Clear(); stock.Clear(); Array.Clear(dealSpecial, 0, Players);
            IEnumerable<Card> pack = Players == 5
                ? Cards.StandardDeck(new[] { 1 }.Concat(Enumerable.Range(4, 10)))
                : Cards.StandardDeck();
            stock.AddRange(pack.Select(card => new SCard(card))); stock.Add(new SCard(null)); rng.Shuffle(stock); dealer = (dealer + 1) % Players;
            for (int round = 0; round < 6; round++) for (int offset = 1; offset <= Players; offset++) hands[(dealer + offset) % Players].Add(Pop(stock));
            bidsMade = 0; highBid = 0; declarer = partner = -1; partnerRevealed = false; winners = Array.Empty<int>(); phase = "bid"; CurrentPlayer = (dealer + 1) % Players;
        }
        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "bid") return new[] { new Action("pass") }.Concat(Enumerable.Range(Math.Max(3, highBid + 1), Math.Max(0, 6 - Math.Max(3, highBid + 1) + 1)).Select(value => new Action("bid", value: value.ToString()))).ToArray();
            if (phase == "choose_trump") return Enum.GetValues(typeof(Suit)).Cast<Suit>().Select(suit => new Action("choose_trump", value: Card.SuitCode(suit))).ToArray();
            if (phase == "exchange")
            {
                var actions = new List<Action>();
                if (discarded < 3) actions.AddRange(hands[actual].Where(card => !IsTrump(card)).Select(card => new Action("discard_exchange", card.Card, value: card.Id)));
                actions.Add(new Action("finish_exchange")); return actions;
            }
            if (phase == "dealer_discard")
            {
                IEnumerable<SCard> cards = hands[actual].Where(card => !IsTrump(card)); if (!cards.Any()) cards = hands[actual].Where(card => !ProtectedTrump(card));
                return cards.Select(card => new Action("dealer_discard", card.Card, value: card.Id)).ToArray();
            }
            if (phase == "call_partner")
            {
                var actions = new List<Action> { new Action("play_solo") };
                foreach (Card card in Cards.StandardDeck(new[] { 1 }.Concat(Enumerable.Range(4, 10))).Where(card => hands[actual].All(item => item.Card != card))) actions.Add(new Action("call_partner", card, value: card.ToString()));
                return actions;
            }
            IEnumerable<SCard> playable = hands[actual];
            if (trick.Count > 0)
            {
                SCard lead = trick[0].Item2; SCard[] follow = IsTrump(lead) ? playable.Where(IsTrump).ToArray()
                    : playable.Where(card => !IsTrump(card) && !card.Joker && card.Card!.Value.Suit == lead.Card!.Value.Suit).ToArray();
                if (follow.Length > 0) playable = follow;
            }
            return playable.Select(card => new Action("play", card.Card, value: card.Id)).ToArray();
        }
        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "bid")
            {
                bidsMade++; if (action.Kind == "bid") { highBid = int.Parse(action.Value!); declarer = player; }
                if (bidsMade < Players) { CurrentPlayer = (player + 1) % Players; return; }
                if (declarer < 0) { dealer = (dealer - 1 + Players) % Players; StartDeal(); return; }
                phase = "choose_trump"; CurrentPlayer = declarer; return;
            }
            if (phase == "choose_trump")
            {
                trump = Card.ParseSuit(action.Value!); exchangePlayer = (dealer + 1) % Players; discarded = 0; phase = "exchange"; CurrentPlayer = exchangePlayer; return;
            }
            if (phase == "exchange")
            {
                if (action.Kind == "discard_exchange") { hands[player].Remove(Find(hands[player], action.Value!)); discarded++; return; }
                for (int i = 0; i < discarded && stock.Count > 0; i++) hands[player].Add(Pop(stock)); discarded = 0;
                exchangePlayer = (player + 1) % Players;
                if (exchangePlayer == dealer) { hands[dealer].AddRange(stock); stock.Clear(); phase = hands[dealer].Count > 6 ? "dealer_discard" : NextAfterExchange(); CurrentPlayer = dealer; }
                else CurrentPlayer = exchangePlayer;
                return;
            }
            if (phase == "dealer_discard")
            {
                hands[player].Remove(Find(hands[player], action.Value!)); if (hands[player].Count <= 6) { phase = NextAfterExchange(); CurrentPlayer = phase == "call_partner" ? declarer : declarer; } return;
            }
            if (phase == "call_partner")
            {
                if (action.Kind == "call_partner") { calledCard = action.Card!.Value; partner = Enumerable.Range(0, Players).Where(p => hands[p].Any(card => card.Card == calledCard)).DefaultIfEmpty(-1).First(); }
                phase = "play"; CurrentPlayer = declarer; return;
            }
            SCard played = Find(hands[player], action.Value!); hands[player].Remove(played); trick.Add(Tuple.Create(player, played));
            if (Players == 5 && played.Card == calledCard) partnerRevealed = true;
            if (IsLowTrump(played)) dealSpecial[player]++;
            if (trick.Count < Players) { CurrentPlayer = (player + 1) % Players; return; }
            int winner = TrickWinner(); captured[winner].AddRange(trick.Select(item => item.Item2)); trick.Clear();
            if (hands.All(hand => hand.Count == 0)) FinishDeal(); else CurrentPlayer = winner;
        }
        private string NextAfterExchange() { if (Players == 5) return "call_partner"; partner = (declarer + 2) % Players; return "play"; }
        private bool IsTrump(SCard card)
        {
            if (card.Joker) return true; Card value = card.Card!.Value; return value.Suit == trump || IsLeftBower(value);
        }
        private bool IsLeftBower(Card card) => card.Rank == 11 && card.Suit != trump && Red(card.Suit) == Red(trump);
        private int TrumpStrength(SCard card)
        {
            if (card.Joker) return 0; Card value = card.Card!.Value;
            if (value.Rank == 1) return 20; if (value.Rank == 13) return 19; if (value.Rank == 12) return 18;
            if (value.Rank == 11 && value.Suit == trump) return 17; if (IsLeftBower(value)) return 16;
            return value.Rank + 5;
        }
        private int TrickWinner()
        {
            SCard lead = trick[0].Item2; IEnumerable<Tuple<int, SCard>> eligible = trick.Any(item => IsTrump(item.Item2)) ? trick.Where(item => IsTrump(item.Item2))
                : trick.Where(item => !item.Item2.Joker && item.Item2.Card!.Value.Suit == lead.Card!.Value.Suit);
            return eligible.OrderByDescending(item => IsTrump(item.Item2) ? TrumpStrength(item.Item2) : PlainStrength(item.Item2.Card!.Value)).First().Item1;
        }
        private void FinishDeal()
        {
            var declarerTeam = Players == 6
                ? new HashSet<int>(Enumerable.Range(0, Players).Where(player => (player - declarer + Players) % 2 == 0))
                : new HashSet<int> { declarer };
            if (Players == 5 && partner >= 0) declarerTeam.Add(partner);
            int highOwner = OwnerOfCaptured(new Card(trump, 1)); if (highOwner >= 0) dealSpecial[highOwner]++;
            int rightOwner = OwnerOfCaptured(new Card(trump, 11)); if (rightOwner >= 0) dealSpecial[rightOwner]++;
            Suit leftSuit = Enum.GetValues(typeof(Suit)).Cast<Suit>().Single(suit => suit != trump && Red(suit) == Red(trump));
            int leftOwner = OwnerOfCaptured(new Card(leftSuit, 11)); if (leftOwner >= 0) dealSpecial[leftOwner]++;
            int jokerOwner = Enumerable.Range(0, Players).Where(p => captured[p].Any(card => card.Joker)).DefaultIfEmpty(-1).First(); if (jokerOwner >= 0) dealSpecial[jokerOwner]++;
            int[] gameValues = captured.Select(pile => pile.Sum(GameValue)).ToArray();
            int declarerGame = declarerTeam.Sum(player => gameValues[player]);
            int opponentGame = Enumerable.Range(0, Players).Where(player => !declarerTeam.Contains(player)).Sum(player => gameValues[player]);
            if (declarerGame > opponentGame) dealSpecial[declarer]++;
            else if (opponentGame > declarerGame) dealSpecial[Enumerable.Range(0, Players).First(player => !declarerTeam.Contains(player))]++;
            int teamPoints = declarerTeam.Sum(player => dealSpecial[player]);
            int opponentPoints = Enumerable.Range(0, Players).Where(player => !declarerTeam.Contains(player)).Sum(player => dealSpecial[player]);
            bool success = teamPoints >= highBid;
            if (success) foreach (int player in declarerTeam) scores[player] += teamPoints; else foreach (int player in declarerTeam) scores[player] -= highBid;
            foreach (int player in Enumerable.Range(0, Players).Where(player => !declarerTeam.Contains(player))) scores[player] += opponentPoints;
            if (scores.Any(score => score >= targetScore))
            {
                int high = scores.Max(); int[] tied = Enumerable.Range(0, Players).Where(player => scores[player] == high).ToArray();
                int[] biddingSide = tied.Where(declarerTeam.Contains).ToArray(); winners = biddingSide.Length > 0 ? biddingSide : tied; finished = true;
            }
            else StartDeal();
        }
        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "bid") return highBid < 3 && player == (dealer + 1) % Players ? actions.First(action => action.Value == "3") : actions[0];
            if (phase == "choose_trump") return actions.OrderByDescending(action => hands[player].Count(card => card.Card.HasValue && card.Card.Value.Suit == Card.ParseSuit(action.Value!))).First();
            if (phase == "exchange") return actions.Last();
            if (phase == "dealer_discard") return actions.OrderBy(action => action.Card.HasValue ? PlainStrength(action.Card.Value) : 20).First();
            if (phase == "call_partner") return actions.Count > 1 ? actions[1] : actions[0];
            return actions.OrderBy(action => action.Card.HasValue ? PlainStrength(action.Card.Value) : 20).First();
        }
        private int OwnerOfCaptured(Card card) => Enumerable.Range(0, Players).Where(player => captured[player].Any(item => item.Card == card)).DefaultIfEmpty(-1).First();
        private bool IsLowTrump(SCard card) => card.Card == new Card(trump, Players == 5 ? 4 : 2);
        private bool ProtectedTrump(SCard card) => card.Joker || card.Card == new Card(trump, 1) ||
            card.Card == new Card(trump, 11) || card.Card == new Card(trump, Players == 5 ? 4 : 2) ||
            card.Card.HasValue && IsLeftBower(card.Card.Value);
        private static bool Red(Suit suit) => suit == Suit.Diamonds || suit == Suit.Hearts;
        private static int PlainStrength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static int GameValue(SCard card) => card.Joker ? 1 : card.Card!.Value.Rank == 1 ? 4 : card.Card.Value.Rank == 13 ? 3 : card.Card.Value.Rank == 12 ? 2 : card.Card.Value.Rank == 11 ? 1 : card.Card.Value.Rank == 10 ? 10 : 0;
        private static SCard Find(List<SCard> cards, string id) => cards.Single(card => card.Id == id);
        private static SCard Pop(List<SCard> cards) { SCard card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over."); return new GameResult(winners, scores.Select(value => (double)value), "Schmear target score", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer; return $"phase={phase} dealer=P{dealer} bid={highBid} declarer={(declarer < 0 ? "-" : "P" + declarer)} trump={(phase == "bid" ? "-" : Card.SuitCode(trump))} called_card={(phase == "play" && Players == 5 ? calledCard.ToString() : "-")} stock={stock.Count} " +
                $"partner={(partnerRevealed || Players == 6 ? partner < 0 ? "solo" : "P" + partner : "hidden")} special=[{string.Join(",", dealSpecial)}] scores=[{string.Join(",", scores)}] table=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("schmear", "シュミア", 5, 6, "bid point-trick", "Pagat St Paul版。6人は52枚＋Joker、5人は2・3を除く44枚＋Jokerで各6枚。3～6を1巡bidし、trump決定後に非trumpを最大3枚交換、dealerが残りを取り6枚へ戻す。5人はcard指名partner、6人は交互3対3。High/Low/正J/裏J/Joker/Gameのteam6点をbid以上集め21点を争う。", "Pagat/Schmier", new Dictionary<string, string> { { "target_score", "21" } }),
            (players, random, options) => new SchmearGame(players, random, options));
    }
}
