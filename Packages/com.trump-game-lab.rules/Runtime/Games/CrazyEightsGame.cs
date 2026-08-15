using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    public sealed class CrazyEightsGame : GameBase, IGamePresentationProvider
    {
        private readonly DeterministicRandom rng;
        private readonly int wildRank;
        private readonly List<List<Card>> hands;
        private List<Card> stock;
        private readonly List<Card> discard;
        private Suit? calledSuit;
        private int? winner;
        private readonly int dealer;
        private string phase="play";

        public override string GameId => "crazy_eights";
        public override string Name => "クレイジーエイト";
        public int WildRank => wildRank;

        public CrazyEightsGame(int players, DeterministicRandom rng,
            IReadOnlyDictionary<string, string> options)
        {
            Players = players; this.rng = rng; wildRank = options.Integer("wild_rank", 8);dealer=players-1;
            List<Card> deck = Cards.Shuffled(Cards.StandardDeck(), rng);
            hands = Enumerable.Range(0, players).Select(_ => new List<Card>()).ToList();
            int size = players==2?7:5;
            for (int round = 0; round < size; round++)
                foreach (List<Card> hand in hands) hand.Add(Pop(deck));
            Card first = Pop(deck);
            stock = deck;
            discard = new List<Card> { first };
            if(first.Rank==wildRank){phase="choose_starter_suit";CurrentPlayer=dealer;}
            else CurrentPlayer=(dealer+1)%Players;
        }

        private bool Playable(Card card)
        {
            Card top = discard[discard.Count - 1];
            Suit suit = calledSuit ?? top.Suit;
            return card.Rank == wildRank || card.Suit == suit || card.Rank == top.Rank;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if(phase=="choose_starter_suit")return Enum.GetValues(typeof(Suit)).Cast<Suit>()
                .Select(suit=>new Action("choose_starter_suit",value:Card.SuitCode(suit))).ToArray();
            var actions = new List<Action>();
            foreach (Card card in hands[actual].Where(Playable))
            {
                if (card.Rank == wildRank)
                    foreach (Suit suit in Enum.GetValues(typeof(Suit)))
                        actions.Add(new Action(hands[actual].Count==2?"play_last_card":"play",
                            card, value: Card.SuitCode(suit)));
                else actions.Add(new Action(hands[actual].Count==2?"play_last_card":"play", card));
            }
            if (stock.Count > 0 || discard.Count > 1) actions.Add(new Action("draw"));
            else if (actions.Count == 0) actions.Add(new Action("pass"));
            return actions;
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null);
            Guard.Legal(action, LegalActions(player));
            if(action.Kind=="choose_starter_suit")
            {calledSuit=Card.ParseSuit(action.Value!);phase="play";TurnCount++;CurrentPlayer=(dealer+1)%Players;return;}
            if (action.Kind == "draw")
            {
                Refill();
                if (stock.Count > 0) hands[player].Add(Pop(stock));
                TurnCount++;
                CurrentPlayer = (player + 1) % Players;
                return;
            }
            else if(action.Kind == "play"||action.Kind=="play_last_card")
            {
                Card card = action.Card!.Value;
                hands[player].Remove(card);
                discard.Add(card);
                calledSuit = card.Rank == wildRank ? Card.ParseSuit(action.Value!) : (Suit?)null;
                if(hands[player].Count==1&&action.Kind!="play_last_card")DrawPenalty(player,2);
                if (hands[player].Count == 0) winner = player;
            }
            TurnCount++;
            if (!winner.HasValue) CurrentPlayer = (player + 1) % Players;
        }

        private void Refill()
        {
            if (stock.Count != 0 || discard.Count <= 1) return;
            Card top = Pop(discard);
            stock = Cards.Shuffled(discard, rng);
            discard.Clear(); discard.Add(top);
        }
        private void DrawPenalty(int player,int count){for(int i=0;i<count;i++){Refill();if(stock.Count==0)return;hands[player].Add(Pop(stock));}}

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if(phase=="choose_starter_suit")
            {Suit starterSuit=Enum.GetValues(typeof(Suit)).Cast<Suit>().OrderByDescending(suit=>hands[player].Count(card=>card.Suit==suit)).First();return actions.First(action=>action.Value==Card.SuitCode(starterSuit));}
            Action[] plays = actions.Where(action => action.Kind == "play"||action.Kind=="play_last_card").ToArray();
            if (plays.Length == 0) return actions[0];
            Action[] nonWild = plays.Where(action => action.Card!.Value.Rank != wildRank).ToArray();
            if (nonWild.Length > 0)
                return nonWild.OrderByDescending(action =>
                    hands[player].Count(card => card.Suit == action.Card!.Value.Suit)).First();
            Suit best = Enum.GetValues(typeof(Suit)).Cast<Suit>()
                .OrderByDescending(suit => hands[player].Count(card => card.Suit == suit)).First();
            return plays.First(action => action.Value == Card.SuitCode(best));
        }

        public override bool IsTerminal => winner.HasValue;
        public override GameResult Result()
        {
            if (!winner.HasValue) throw new InvalidOperationException("Game is not over.");
            double[] scores = hands.Select(hand => -(double)hand.Sum(CardPenalty)).ToArray();
            scores[winner.Value] = -scores.Sum();
            return new GameResult(new[] { winner.Value }, scores, "empty hand", TurnCount);
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            return $"top={discard[discard.Count - 1]} called={(calledSuit.HasValue ? Card.SuitCode(calledSuit.Value) : "-")} " +
                $"stock={stock.Count} hands=[{string.Join(",", hands.Select(hand => hand.Count))}]\n" +
                "your hand: " + string.Join(" ", hands[viewer]);
        }

        public GamePresentation Present(int? viewer = null)
        {
            int actualViewer = viewer ?? CurrentPlayer;
            if (actualViewer < 0 || actualViewer >= Players)
                throw new ArgumentOutOfRangeException(nameof(viewer));

            PlayerPresentation[] presentedPlayers = Enumerable.Range(0, Players)
                .Select(player => new PlayerPresentation(
                    player, player == CurrentPlayer, player == actualViewer))
                .ToArray();

            var zones = new List<CardZonePresentation>();
            for (int player = 0; player < Players; player++)
            {
                bool isViewer = player == actualViewer;
                zones.Add(new CardZonePresentation(
                    "hand_" + player,
                    "hand",
                    player,
                    isViewer ? CardZoneVisibility.FaceUp : CardZoneVisibility.FaceDown,
                    hands[player].Count,
                    isViewer ? hands[player] : null));
            }
            zones.Add(new CardZonePresentation(
                "stock", "stock", null, CardZoneVisibility.CountOnly, stock.Count));
            zones.Add(new CardZonePresentation(
                "discard", "discard", null, CardZoneVisibility.FaceUp, discard.Count, discard));

            var fields = new List<GameFieldPresentation>();
            if (calledSuit.HasValue)
                fields.Add(new GameFieldPresentation(
                    "called_suit", PresentationValue.FromSuit(calledSuit.Value)));

            ActionPresentation[] actions = Array.Empty<ActionPresentation>();
            if (!IsTerminal && actualViewer == CurrentPlayer)
            {
                actions = LegalActions(actualViewer)
                    .Select((action, index) => new ActionPresentation(
                        "action_" + index,
                        action,
                        "action." + GameId + "." + action.Kind))
                    .ToArray();
            }

            GameResultPresentation? presentedResult = null;
            if (IsTerminal)
            {
                GameResult result = Result();
                presentedResult = new GameResultPresentation(
                    result.Winners, result.Scores, result.Reason, result.Turns);
            }

            return new GamePresentation(
                GameId,
                phase,
                actualViewer,
                CurrentPlayer,
                TurnCount,
                IsTerminal,
                presentedPlayers,
                zones,
                fields,
                actions,
                presentedResult);
        }

        private static Card Pop(List<Card> cards)
        {
            Card value = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return value;
        }

        private int CardPenalty(Card card) => card.Rank == wildRank ? 50 : Math.Min(card.Rank, 10);

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("crazy_eights", "クレイジーエイト", 2, 5, "shedding",
                "Pagat基本版。2人は7枚、3人以上は5枚。場札と同じsuitかrankを出し、8は任意suitになる。play可能でも1枚drawを選べ、stock枯渇時はtop以外の捨札を再利用する。残り1枚宣言を含む。", "Pagat Crazy Eights basic game",
                new Dictionary<string, string> { ["wild_rank"] = "ワイルドとして扱うランク（既定8）" }),
            (players, rng, options) => new CrazyEightsGame(players, rng, options));
    }
}
