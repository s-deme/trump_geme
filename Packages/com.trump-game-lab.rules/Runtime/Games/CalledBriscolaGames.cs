using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    internal static class CalledBriscolaGames
    {
        public static void RegisterGames(GameRegistry registry)
        { BriscolaChiamataGame.Register(registry); BriscolaBugiardaGame.Register(registry); }
    }

    public abstract class CalledBriscolaGameBase : GameBase
    {
        private static readonly int[] RankOrder = { 1, 3, 13, 12, 11, 7, 6, 5, 4, 2 };
        private readonly DeterministicRandom rng;
        private readonly bool mustFollow;
        private readonly bool detailedSettlement;
        private readonly List<List<Card>> hands = Enumerable.Range(0, 5).Select(_ => new List<Card>()).ToList();
        private readonly List<List<Card>> captured = Enumerable.Range(0, 5).Select(_ => new List<Card>()).ToList();
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly int[] scores = new int[5];
        private readonly bool[] active = new bool[5];
        private int dealer = 4;
        private int declarer = -1;
        private int partner = -1;
        private int currentRankIndex = -1;
        private int calledRank;
        private Suit trump;
        private int deals;
        private bool partnerRevealed;
        private string phase = "bid";
        private bool finished;
        protected CalledBriscolaGameBase(DeterministicRandom rng, bool mustFollow, bool detailedSettlement)
        { Players = 5; this.rng = rng; this.mustFollow = mustFollow; this.detailedSettlement = detailedSettlement; StartDeal(); }

        private void StartDeal()
        {
            foreach (List<Card> pile in hands) pile.Clear(); foreach (List<Card> pile in captured) pile.Clear(); trick.Clear();
            List<Card> deck = Cards.Shuffled(Cards.StandardDeck(RankOrder), rng); dealer = (dealer + 1) % 5;
            for (int round = 0; round < 8; round++) for (int offset = 1; offset <= 5; offset++) hands[(dealer + offset) % 5].Add(Pop(deck));
            Array.Fill(active, true); declarer = partner = -1; currentRankIndex = -1; calledRank = 0; partnerRevealed = false; phase = "bid"; CurrentPlayer = (dealer + 1) % 5;
        }
        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "bid")
            {
                var actions = new List<Action> { new Action("pass") };
                for (int index = currentRankIndex + 1; index < RankOrder.Length; index++) actions.Add(new Action("bid_rank", value: RankOrder[index].ToString()));
                return actions;
            }
            if (phase == "choose_trump") return Enum.GetValues(typeof(Suit)).Cast<Suit>().Select(suit => new Action("choose_trump", value: Card.SuitCode(suit))).ToArray();
            IEnumerable<Card> cards = hands[actual];
            if (mustFollow && trick.Count > 0)
            {
                Suit led = trick[0].Item2.Suit; Card[] follow = cards.Where(card => card.Suit == led).ToArray(); if (follow.Length > 0) cards = follow;
            }
            return cards.Select(card => new Action("play", card)).ToArray();
        }
        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "bid")
            {
                if (action.Kind == "pass") active[player] = false;
                else { calledRank = int.Parse(action.Value!); currentRankIndex = Array.IndexOf(RankOrder, calledRank); declarer = player; }
                if (active.Count(value => value) == 1 && declarer >= 0) { phase = "choose_trump"; CurrentPlayer = declarer; return; }
                if (active.All(value => !value)) { StartDeal(); return; }
                CurrentPlayer = NextActive(player); return;
            }
            if (phase == "choose_trump")
            {
                trump = Card.ParseSuit(action.Value!); Card called = new Card(trump, calledRank);
                partner = Enumerable.Range(0, 5).Where(p => hands[p].Contains(called)).DefaultIfEmpty(-1).First();
                if (partner == declarer) partner = -1; phase = "play"; CurrentPlayer = declarer; return;
            }
            Card played = action.Card!.Value; hands[player].Remove(played); trick.Add(Tuple.Create(player, played));
            if (played == new Card(trump, calledRank)) partnerRevealed = true;
            if (trick.Count < 5) { CurrentPlayer = (player + 1) % 5; return; }
            Suit ledSuit = trick[0].Item2.Suit; IEnumerable<Tuple<int, Card>> eligible = trick.Any(item => item.Item2.Suit == trump)
                ? trick.Where(item => item.Item2.Suit == trump) : trick.Where(item => item.Item2.Suit == ledSuit);
            int winner = eligible.OrderBy(item => Array.IndexOf(RankOrder, item.Item2.Rank)).First().Item1; captured[winner].AddRange(trick.Select(item => item.Item2)); trick.Clear();
            if (hands.All(hand => hand.Count == 0)) FinishDeal(); else CurrentPlayer = winner;
        }
        private int NextActive(int player) { int next = (player + 1) % 5; while (!active[next]) next = (next + 1) % 5; return next; }
        private void FinishDeal()
        {
            var side = new HashSet<int> { declarer }; if (partner >= 0) side.Add(partner);
            int points = side.Sum(player => captured[player].Sum(CardPoints)); bool wonAll = side.Sum(player => captured[player].Count) == 40;
            if (!detailedSettlement)
            {
                int unit = points >= 61 ? 1 : -1; if (wonAll) unit *= 2;
                if (partner < 0) { scores[declarer] += 4 * unit; foreach (int player in Enumerable.Range(0, 5).Where(p => p != declarer)) scores[player] -= unit; }
                else { scores[declarer] += 2 * unit; scores[partner] += unit; foreach (int player in Enumerable.Range(0, 5).Where(p => !side.Contains(p))) scores[player] -= unit; }
                if (scores.Any(score => score >= 11)) finished = true; else StartDeal();
                return;
            }
            int scale = SettlementUnit(points);
            if (partner < 0) { scores[declarer] += 4 * scale; foreach (int player in Enumerable.Range(0, 5).Where(p => p != declarer)) scores[player] -= scale; }
            else { scores[declarer] += 2 * scale; scores[partner] += scale; foreach (int player in Enumerable.Range(0, 5).Where(p => !side.Contains(p))) scores[player] -= scale; }
            deals++; if (deals >= 5) finished = true; else StartDeal();
        }
        private static int SettlementUnit(int points)
        {
            if (points == 120) return 12; if (points >= 61) return (points - 61) / 10 + 1;
            if (points == 0) return -12; return -((60 - points) / 10 + 1);
        }
        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "bid") return declarer < 0 ? actions.Last() : actions[0];
            if (phase == "choose_trump") return actions.OrderBy(action => hands[player].Contains(new Card(Card.ParseSuit(action.Value!), calledRank)))
                .ThenByDescending(action => hands[player].Count(card => card.Suit == Card.ParseSuit(action.Value!))).First();
            return actions.OrderBy(action => CardPoints(action.Card!.Value)).ThenByDescending(action => Array.IndexOf(RankOrder, action.Card!.Value.Rank)).First();
        }
        private static int CardPoints(Card card) => card.Rank == 1 ? 11 : card.Rank == 3 ? 10 : card.Rank == 13 ? 4 : card.Rank == 12 ? 3 : card.Rank == 11 ? 2 : 0;
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over."); int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 5).Where(player => scores[player] == high), scores.Select(value => (double)value), detailedSettlement ? "five Bugiarda deals" : "Chiamata target eleven", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer; string role = viewer == declarer ? "declarer" : viewer == partner ? "partner" : "opposition";
            return $"phase={phase} dealer=P{dealer} called_rank={(calledRank == 0 ? "-" : calledRank.ToString())} trump={(phase == "bid" ? "-" : Card.SuitCode(trump))} " +
                $"declarer={(declarer < 0 ? "-" : "P" + declarer)} partner={(partnerRevealed ? partner < 0 ? "solo" : "P" + partner : "hidden")} your_role={role} " +
                $"scores=[{string.Join(",", scores)}] captured_points=[{string.Join(",", captured.Select(pile => pile.Sum(CardPoints)))}] table=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }
    }

    public sealed class BriscolaChiamataGame : CalledBriscolaGameBase
    {
        public override string GameId => "briscola_chiamata";
        public override string Name => "ブリスコラ・キアマタ";
        public BriscolaChiamataGame(DeterministicRandom rng) : base(rng, true, false) { }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("briscola_chiamata", "ブリスコラ・キアマタ", 5, 5, "called-partner point-trick", "8・9・10を除く40枚。A>3>K>Q>J>7>6>5>4>2のrankを弱い方向へhard-pass auctionし、declarerがtrumpを決める。bid rankのtrump所持者を秘密partnerとしてmust-followで120点中61点を狙い、単独±4／2対3は±2・±1、全trickで倍、11点を争う。", "gokurakism/Briscola Chiamata"),
            (players, random, options) => new BriscolaChiamataGame(random));
    }

    public sealed class BriscolaBugiardaGame : CalledBriscolaGameBase
    {
        public override string GameId => "briscola_bugiarda";
        public override string Name => "ブリスコラ・ブジャルダ";
        public BriscolaBugiardaGame(DeterministicRandom rng) : base(rng, false, true) { }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("briscola_bugiarda", "ブリスコラ・ブジャルダ", 5, 5, "called-partner may-follow", "キアマタと同じ40枚・rank auction・秘密partnerを使うが、follow義務なし。declarer側のcard pointを61～70から120まで7段階（敗北側も対称）でchip精算し、採用仕様では5deal合計を競う。", "gokurakism/Briscola Bugiarda"),
            (players, random, options) => new BriscolaBugiardaGame(random));
    }
}
