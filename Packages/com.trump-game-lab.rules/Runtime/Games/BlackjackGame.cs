using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    public sealed class BlackjackGame : GameBase
    {
        private sealed class PlayerHand
        {
            public List<Card> Cards { get; } = new List<Card>();
            public double Bet { get; set; } = 1;
            public bool Stood { get; set; }
            public bool FromSplit { get; set; }
            public bool SplitAces { get; set; }
        }

        private readonly List<Card> deck;
        private readonly List<List<PlayerHand>> playerHands;
        private readonly List<Card> dealer = new List<Card>();
        private readonly bool dealerHitsSoft17;
        private readonly bool allowDouble;
        private readonly bool allowSplit;
        private readonly bool allowInsurance;
        private readonly int maxSplitHands;
        private readonly double[] insuranceBets;
        private int activeHand;
        private string phase;
        private bool finished;

        public override string GameId => "blackjack";
        public override string Name => "ブラックジャック";

        public BlackjackGame(int players, DeterministicRandom rng,
            IReadOnlyDictionary<string, string> options)
        {
            Players = players;
            dealerHitsSoft17 = options.Boolean("dealer_hits_soft_17", false);
            allowDouble = options.Boolean("allow_double", true);
            allowSplit = options.Boolean("allow_split", true);
            allowInsurance = options.Boolean("allow_insurance", true);
            maxSplitHands = options.Integer("max_split_hands", 4);
            if (maxSplitHands < 1) throw new ArgumentOutOfRangeException("max_split_hands");
            deck = Cards.Shuffled(Cards.StandardDeck(copies: options.Integer("decks", 1)), rng);
            playerHands = Enumerable.Range(0, players)
                .Select(_ => new List<PlayerHand> { new PlayerHand() }).ToList();
            insuranceBets = new double[players];
            for (int round = 0; round < 2; round++)
            {
                foreach (List<PlayerHand> hands in playerHands) hands[0].Cards.Add(Pop(deck));
                dealer.Add(Pop(deck));
            }

            if (DealerNaturalPossible() && allowInsurance && dealer[0].Rank == 1)
                phase = "insurance";
            else
            {
                phase = "play";
                if (DealerHasNatural()) finished = true;
                else MoveToNextPlayable(-1, -1);
            }
        }

        public static int HandValue(IEnumerable<Card> cards)
        {
            Card[] hand = cards.ToArray();
            int value = hand.Sum(card => card.Rank == 1 ? 11 : Math.Min(card.Rank, 10));
            int aces = hand.Count(card => card.Rank == 1);
            while (value > 21 && aces-- > 0) value -= 10;
            return value;
        }

        public static bool IsSoft(IEnumerable<Card> cards)
        {
            Card[] hand = cards.ToArray();
            int hard = hand.Sum(card => card.Rank == 1 ? 1 : Math.Min(card.Rank, 10));
            return hand.Any(card => card.Rank == 1) && hard + 10 <= 21;
        }

        private static bool Natural(PlayerHand hand) =>
            !hand.FromSplit && hand.Cards.Count == 2 && HandValue(hand.Cards) == 21;

        private bool DealerNaturalPossible() => dealer[0].Rank == 1 || dealer[0].Rank >= 10;
        private bool DealerHasNatural() => dealer.Count == 2 && HandValue(dealer) == 21;

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "insurance")
                return new[] { new Action("decline_insurance"), new Action("insurance") };

            PlayerHand hand = playerHands[actual][activeHand];
            var actions = new List<Action>();
            if (HandValue(hand.Cards) < 21 && !hand.SplitAces) actions.Add(new Action("hit"));
            actions.Add(new Action("stand"));
            if (allowDouble && hand.Cards.Count == 2 && hand.Bet == 1 &&
                HandValue(hand.Cards) >= 9 && HandValue(hand.Cards) <= 11 && !hand.SplitAces)
                actions.Add(new Action("double"));
            if (allowSplit && hand.Cards.Count == 2 &&
                hand.Cards[0].Rank == hand.Cards[1].Rank &&
                playerHands[actual].Count < maxSplitHands)
                actions.Add(new Action("split"));
            return actions;
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null);
            Guard.Legal(action, LegalActions(player));
            TurnCount++;
            if (phase == "insurance")
            {
                insuranceBets[player] = action.Kind == "insurance" ? 0.5 : 0;
                if (player + 1 < Players)
                {
                    CurrentPlayer = player + 1;
                    return;
                }
                phase = "play";
                if (DealerHasNatural()) { finished = true; return; }
                MoveToNextPlayable(-1, -1);
                return;
            }

            PlayerHand hand = playerHands[player][activeHand];
            switch (action.Kind)
            {
                case "hit":
                    hand.Cards.Add(Pop(deck));
                    if (HandValue(hand.Cards) >= 21) hand.Stood = true;
                    break;
                case "stand":
                    hand.Stood = true;
                    break;
                case "double":
                    hand.Bet = 2;
                    hand.Cards.Add(Pop(deck));
                    hand.Stood = true;
                    break;
                case "split":
                    Split(player, hand);
                    break;
            }
            if (!hand.Stood) return;
            MoveToNextPlayable(player, activeHand);
        }

        private void Split(int player, PlayerHand left)
        {
            Card moved = left.Cards[1];
            left.Cards.RemoveAt(1);
            var right = new PlayerHand { FromSplit = true };
            left.FromSplit = true;
            right.Cards.Add(moved);
            bool aces = left.Cards[0].Rank == 1;
            left.SplitAces = aces; right.SplitAces = aces;
            left.Cards.Add(Pop(deck)); right.Cards.Add(Pop(deck));
            if (aces) { left.Stood = true; right.Stood = true; }
            playerHands[player].Insert(activeHand + 1, right);
        }

        private void MoveToNextPlayable(int previousPlayer, int previousHand)
        {
            int startPlayer = previousPlayer < 0 ? 0 : previousPlayer;
            int startHand = previousPlayer < 0 ? 0 : previousHand + 1;
            for (int player = startPlayer; player < Players; player++)
            {
                int handStart = player == startPlayer ? startHand : 0;
                for (int handIndex = handStart; handIndex < playerHands[player].Count; handIndex++)
                {
                    PlayerHand candidate = playerHands[player][handIndex];
                    if (!candidate.Stood && !Natural(candidate) && HandValue(candidate.Cards) < 21)
                    {
                        CurrentPlayer = player; activeHand = handIndex; return;
                    }
                }
            }
            DealerPlay();
        }

        private void DealerPlay()
        {
            while (HandValue(dealer) < 17 ||
                (HandValue(dealer) == 17 && IsSoft(dealer) && dealerHitsSoft17))
                dealer.Add(Pop(deck));
            finished = true;
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom rng, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "insurance") return actions[0];
            PlayerHand hand = playerHands[player][activeHand];
            int value = HandValue(hand.Cards);
            if (actions.Any(a => a.Kind == "split") &&
                (hand.Cards[0].Rank == 1 || hand.Cards[0].Rank == 8)) return new Action("split");
            if (actions.Any(a => a.Kind == "double") && value >= 10) return new Action("double");
            int dealerUp = dealer[0].Rank == 1 ? 11 : Math.Min(dealer[0].Rank, 10);
            int stand = dealerUp >= 7 ? 17 : 12;
            return new Action(value < stand ? "hit" : "stand");
        }

        public override bool IsTerminal => finished;

        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int dealerValue = HandValue(dealer);
            bool dealerNatural = DealerHasNatural();
            double[] scores = Enumerable.Range(0, Players).Select(player =>
            {
                double total = insuranceBets[player] > 0
                    ? (dealerNatural ? insuranceBets[player] * 2 : -insuranceBets[player]) : 0;
                foreach (PlayerHand hand in playerHands[player])
                {
                    int value = HandValue(hand.Cards);
                    if (value > 21) total -= hand.Bet;
                    else if (Natural(hand) && !dealerNatural) total += hand.Bet * 1.5;
                    else if (dealerNatural) total -= hand.Bet;
                    else if (dealerValue > 21 || value > dealerValue) total += hand.Bet;
                    else if (value < dealerValue) total -= hand.Bet;
                }
                return total;
            }).ToArray();
            return new GameResult(Enumerable.Range(0, Players).Where(i => scores[i] > 0),
                scores, "dealer=" + dealerValue, TurnCount,
                new Dictionary<string, object> { ["dealer_cards"] = dealer.ToArray() });
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            string own = string.Join(" | ", playerHands[viewer].Select((hand, index) =>
                $"H{index}:{string.Join(",", hand.Cards)}({HandValue(hand.Cards)}) bet={hand.Bet}"));
            string others = string.Join(",", playerHands.Select((hands, index) =>
                index == viewer ? "-" : hands.Sum(hand => hand.Cards.Count).ToString()));
            IEnumerable<Card> visible = finished ? dealer : dealer.Take(1);
            return $"phase={phase} active_hand={activeHand} dealer={string.Join(",", visible)} " +
                $"other_card_counts=[{others}]\nyour hands: {own}";
        }

        private static Card Pop(List<Card> cards)
        {
            if (cards.Count == 0) throw new InvalidOperationException("Blackjack shoe is empty.");
            Card value = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return value;
        }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("blackjack", "ブラックジャック", 1, 5, "banking",
                "ヒット、スタンド、ダブル、スプリット、保険を選び、21を超えずディーラーを上回る。",
                "Bicycle Blackjack",
                new Dictionary<string, string>
                {
                    ["decks"] = "使用デッキ数（既定1）",
                    ["dealer_hits_soft_17"] = "ディーラーがソフト17でヒットする",
                    ["allow_double"] = "ダブルダウンを許可する（既定true）",
                    ["allow_split"] = "ペアのスプリットを許可する（既定true）",
                    ["allow_insurance"] = "保険を許可する（既定true）",
                    ["max_split_hands"] = "1人の最大ハンド数（既定4）"
                }),
            (players, rng, options) => new BlackjackGame(players, rng, options));
    }
}
