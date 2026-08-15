using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TrumpLab.Games
{
    internal static class TwoPlayerCandidateGames
    {
        public static void RegisterGames(GameRegistry registry)
        {
            CrispGame.Register(registry);
            DurakGame.Register(registry);
            SchnapsenGame.Register(registry);
            CribbageGame.Register(registry);
        }
    }

    public sealed class CrispGame : GameBase
    {
        private enum ComboKind { Single, Pair, Run, PairRun, Triple, Quad }

        private sealed class Combo
        {
            public ComboKind Kind { get; }
            public Card[] Cards { get; }
            public int Top { get; }
            public bool Special => Kind == ComboKind.Triple || Kind == ComboKind.Quad;
            public Combo(ComboKind kind, IEnumerable<Card> cards)
            {
                Kind = kind;
                Cards = cards.OrderBy(card => card.Rank).ThenBy(card => card.Suit).ToArray();
                Top = Cards.Max(card => card.Rank);
            }
        }

        private readonly DeterministicRandom rng;
        private readonly List<List<Card>> hands = new List<List<Card>>
        {
            new List<Card>(), new List<Card>()
        };
        private readonly List<Card> stock = new List<Card>();
        private readonly int[] matchPoints = new int[2];
        private Card? faceUp;
        private Combo? currentCombo;
        private int lastPlayer;
        // Crisps has no dealer role.  The starter for later deals is determined by
        // the running match score; a tie goes to the previous non-starter.
        private int starter = 1;
        private bool firstDeal = true;
        private string phase = "play";
        private bool finished;

        public override string GameId => "crisp";
        public override string Name => "Crisp";

        public CrispGame(int players, DeterministicRandom rng)
        {
            Players = 2;
            this.rng = rng;
            StartDeal();
        }

        private void StartDeal()
        {
            hands[0].Clear(); hands[1].Clear(); stock.Clear();
            List<Card> deck = Cards.Shuffled(Cards.StandardDeck(
                new[] { 2, 3, 4, 5, 6, 7, 8, 9, 10, 12 }), rng);
            for (int round = 0; round < 12; round++)
                for (int player = 0; player < 2; player++)
                    hands[player].Add(Pop(deck));
            for (int index = 0; index < 4; index++) Pop(deck);
            stock.AddRange(deck);
            faceUp = stock.Count > 0 ? Pop(stock) : (Card?)null;
            currentCombo = null;
            phase = "play";
            if (firstDeal)
            {
                // The physical rule leaves the first starter to an arbitrary
                // method.  The deterministic runtime normalises that choice to P0.
                starter = 0;
                firstDeal = false;
            }
            else if (matchPoints[0] < matchPoints[1]) starter = 0;
            else if (matchPoints[1] < matchPoints[0]) starter = 1;
            else starter = 1 - starter;
            CurrentPlayer = starter;
            lastPlayer = starter;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "reward")
                return new[] { new Action("take_face_up"), new Action("take_face_down") };

            List<Action> actions = Combinations(hands[actual])
                .Where(CanBeat)
                .Select(combo => new Action("play", value: Encode(combo.Cards)))
                .ToList();
            if (currentCombo != null) actions.Add(new Action("pass"));
            return actions;
        }

        private bool CanBeat(Combo candidate)
        {
            if (currentCombo == null) return true;
            if (candidate.Special)
            {
                if (!currentCombo.Special) return currentCombo.Cards.Any(card => card.Rank == 12);
                return candidate.Cards.Length > currentCombo.Cards.Length ||
                    candidate.Cards.Length == currentCombo.Cards.Length && candidate.Top >= currentCombo.Top;
            }
            return !currentCombo.Special && candidate.Kind == currentCombo.Kind &&
                candidate.Cards.Length == currentCombo.Cards.Length && candidate.Top >= currentCombo.Top;
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null);
            Guard.Legal(action, LegalActions(player));
            TurnCount++;
            if (phase == "reward")
            {
                AwardStock(player, action.Kind == "take_face_up");
                return;
            }
            if (action.Kind == "pass")
            {
                BeginReward(lastPlayer, player);
                return;
            }

            Card[] selected = Decode(action.Value!);
            Combo combo = Classify(selected) ?? throw new InvalidOperationException("Invalid Crisp combination.");
            foreach (Card card in selected) hands[player].Remove(card);
            currentCombo = combo;
            lastPlayer = player;
            if (hands[player].Count == 0)
            {
                matchPoints[player]++;
                if (matchPoints[player] >= 3) finished = true;
                else StartDeal();
                return;
            }
            CurrentPlayer = 1 - player;
        }

        private void BeginReward(int winner, int passer)
        {
            currentCombo = null;
            if (faceUp.HasValue && stock.Count > 0)
            {
                phase = "reward";
                CurrentPlayer = winner;
                return;
            }
            if (faceUp.HasValue)
            {
                hands[winner].Add(faceUp.Value);
                faceUp = null;
            }
            phase = "play";
            CurrentPlayer = winner;
            lastPlayer = winner;
        }

        private void AwardStock(int winner, bool takeFaceUp)
        {
            int loser = 1 - winner;
            Card hidden = Pop(stock);
            if (takeFaceUp)
            {
                hands[winner].Add(faceUp!.Value);
                hands[loser].Add(hidden);
            }
            else
            {
                hands[winner].Add(hidden);
                hands[loser].Add(faceUp!.Value);
            }
            faceUp = stock.Count > 0 ? Pop(stock) : (Card?)null;
            phase = "play";
            CurrentPlayer = winner;
            lastPlayer = winner;
        }

        private static IEnumerable<Combo> Combinations(IReadOnlyList<Card> hand)
        {
            int possibilities = 1 << hand.Count;
            for (int mask = 1; mask < possibilities; mask++)
            {
                var selected = new List<Card>();
                for (int index = 0; index < hand.Count; index++)
                    if ((mask & (1 << index)) != 0) selected.Add(hand[index]);
                Combo? combo = Classify(selected);
                if (combo != null) yield return combo;
            }
        }

        private static Combo? Classify(IEnumerable<Card> source)
        {
            Card[] cards = source.ToArray();
            if (cards.Length == 0) return null;
            int[] ranks = cards.Select(card => card.Rank).OrderBy(rank => rank).ToArray();
            if (cards.Length == 1) return new Combo(ComboKind.Single, cards);
            if (ranks.All(rank => rank == ranks[0]))
            {
                if (cards.Length == 2) return new Combo(ComboKind.Pair, cards);
                if (cards.Length == 3) return new Combo(ComboKind.Triple, cards);
                if (cards.Length == 4) return new Combo(ComboKind.Quad, cards);
                return null;
            }
            int[] distinct = ranks.Distinct().ToArray();
            bool consecutive = distinct[distinct.Length - 1] <= 10 &&
                distinct[distinct.Length - 1] - distinct[0] + 1 == distinct.Length;
            if (cards.Length >= 3 && distinct.Length == cards.Length && consecutive)
                return new Combo(ComboKind.Run, cards);
            if (distinct.Length >= 2 && cards.Length == distinct.Length * 2 && consecutive &&
                distinct.All(rank => ranks.Count(value => value == rank) == 2))
                return new Combo(ComboKind.PairRun, cards);
            return null;
        }

        public static bool IsCombination(IEnumerable<Card> cards) => Classify(cards) != null;

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "reward")
            {
                if (!faceUp.HasValue) return actions[0];
                int sameRank = hands[player].Count(card => card.Rank == faceUp.Value.Rank);
                return actions[sameRank > 0 ? 0 : 1];
            }
            Action[] plays = actions.Where(candidate => candidate.Kind == "play").ToArray();
            if (plays.Length == 0) return actions.Single(candidate => candidate.Kind == "pass");
            return plays.OrderBy(candidate => Decode(candidate.Value!).Length)
                .ThenBy(candidate => Decode(candidate.Value!).Max(card => card.Rank)).First();
        }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = matchPoints.Max();
            return new GameResult(Enumerable.Range(0, 2).Where(player => matchPoints[player] == high),
                matchPoints.Select(score => (double)score), "first to three deals", TurnCount);
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            string combo = currentCombo == null ? "-" : Encode(currentCombo.Cards);
            return $"phase={phase} face_up={(faceUp.HasValue ? faceUp.Value.ToString() : "-")} stock={stock.Count} " +
                $"last_combo={combo} match=[{string.Join(",", matchPoints)}] hand_counts=[{hands[0].Count},{hands[1].Count}]\n" +
                $"your hand: {string.Join(" ", hands[viewer])}";
        }

        private static string Encode(IEnumerable<Card> cards) =>
            string.Join(",", cards.OrderBy(card => card.Rank).ThenBy(card => card.Suit));
        private static Card[] Decode(string value) => value.Split(',').Select(Card.Parse).ToArray();
        private static Card Pop(List<Card> cards)
        {
            Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card;
        }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("crisp", "Crisp", 2, 2, "climbing",
                "40枚から12枚ずつを配り、単札・ペア・ラン・ペアランと3/4枚組で応酬する3ディール先取戦。",
                "Gokurakism Crisps"),
            (players, random, options) => new CrispGame(players, random));
    }

    public sealed class DurakGame : GameBase
    {
        private sealed class TablePair
        {
            public Card Attack { get; }
            public Card? Defense { get; set; }
            public TablePair(Card attack) { Attack = attack; }
        }

        private readonly List<List<Card>> hands;
        private readonly List<Card> stock;
        private readonly List<TablePair> table = new List<TablePair>();
        private readonly Suit trump;
        private int attacker;
        private int defender;
        private int attackLimit;
        private string phase = "attack";
        private bool defenderTaking;
        private bool finished;

        public override string GameId => "durak";
        public override string Name => "デュラック";

        public DurakGame(int players, DeterministicRandom rng)
        {
            Players = 2;
            stock = Cards.Shuffled(Cards.StandardDeck(
                new[] { 1, 6, 7, 8, 9, 10, 11, 12, 13 }), rng);
            trump = stock[0].Suit;
            hands = new List<List<Card>> { new List<Card>(), new List<Card>() };
            for (int round = 0; round < 6; round++)
                for (int player = 0; player < 2; player++) hands[player].Add(Pop(stock));
            Card lowestTrump = hands.SelectMany(hand => hand).Where(card => card.Suit == trump)
                .OrderBy(Strength).FirstOrDefault();
            attacker = lowestTrump.Equals(default(Card)) ? 0 :
                (hands[0].Contains(lowestTrump) ? 0 : 1);
            defender = 1 - attacker;
            CurrentPlayer = attacker;
            attackLimit = Math.Min(6, hands[defender].Count);
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "defend")
            {
                Card attack = table.Last(pair => !pair.Defense.HasValue).Attack;
                var actions = hands[actual].Where(card => Covers(card, attack))
                    .Select(card => new Action("cover", card)).ToList();
                actions.Add(new Action("take"));
                return actions;
            }

            IEnumerable<Card> cards = hands[actual];
            if (table.Count > 0)
            {
                var ranks = new HashSet<int>(table.SelectMany(pair => pair.Defense.HasValue
                    ? new[] { pair.Attack.Rank, pair.Defense.Value.Rank }
                    : new[] { pair.Attack.Rank }));
                cards = cards.Where(card => ranks.Contains(card.Rank));
            }
            var attacks = table.Count < attackLimit
                ? cards.Select(card => new Action("attack", card)).ToList()
                : new List<Action>();
            if (table.Count > 0 && table.All(pair => pair.Defense.HasValue || defenderTaking))
                attacks.Add(new Action("pass"));
            return attacks;
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null);
            Guard.Legal(action, LegalActions(player));
            TurnCount++;
            if (phase == "defend")
            {
                if (action.Kind == "take")
                {
                    defenderTaking = true;
                    phase = "attack";
                    CurrentPlayer = attacker;
                    return;
                }
                Card card = action.Card!.Value;
                hands[player].Remove(card);
                table.Last(pair => !pair.Defense.HasValue).Defense = card;
                if (table.Count >= attackLimit) EndBout(false);
                else { phase = "attack"; CurrentPlayer = attacker; }
                return;
            }

            if (action.Kind == "pass")
            {
                EndBout(defenderTaking);
                return;
            }
            Card attackCard = action.Card!.Value;
            hands[player].Remove(attackCard);
            table.Add(new TablePair(attackCard));
            if (defenderTaking)
            {
                if (table.Count >= attackLimit) EndBout(true);
                return;
            }
            phase = "defend";
            CurrentPlayer = defender;
        }

        private void EndBout(bool pickedUp)
        {
            if (pickedUp)
            {
                foreach (TablePair pair in table)
                {
                    hands[defender].Add(pair.Attack);
                    if (pair.Defense.HasValue) hands[defender].Add(pair.Defense.Value);
                }
            }
            table.Clear();
            Refill(attacker);
            Refill(defender);
            if (!pickedUp)
            {
                int oldAttacker = attacker;
                attacker = defender;
                defender = oldAttacker;
            }
            if (stock.Count == 0 && (hands[0].Count == 0 || hands[1].Count == 0))
            {
                finished = true;
                return;
            }
            phase = "attack";
            defenderTaking = false;
            attackLimit = Math.Min(6, hands[defender].Count);
            CurrentPlayer = attacker;
        }

        private void Refill(int player)
        {
            while (hands[player].Count < 6 && stock.Count > 0) hands[player].Add(Pop(stock));
        }

        private bool Covers(Card defense, Card attack)
        {
            if (defense.Suit == attack.Suit) return Strength(defense) > Strength(attack);
            return defense.Suit == trump && attack.Suit != trump;
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom rng, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (defenderTaking && actions.Any(action => action.Kind == "pass"))
                return actions.First(action => action.Kind == "pass");
            Action[] cards = actions.Where(action => action.Card.HasValue).ToArray();
            if (cards.Length > 0) return cards.OrderBy(action => action.Card!.Value.Suit == trump ? 1 : 0)
                .ThenBy(action => Strength(action.Card!.Value)).First();
            return actions[0];
        }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int[] winners = Enumerable.Range(0, 2).Where(player => hands[player].Count == 0).ToArray();
            return new GameResult(winners, hands.Select(hand => (double)-hand.Count),
                winners.Length == 2 ? "draw: no durak" : "first player out after the talon emptied",
                TurnCount, new Dictionary<string, object> { { "durak", winners.Length == 1 ? 1 - winners[0] : -1 } });
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            string publicTable = string.Join(" ", table.Select(pair =>
                pair.Attack + "/" + (pair.Defense.HasValue ? pair.Defense.Value.ToString() : "-")));
            return $"phase={phase} attacker=P{attacker} defender=P{defender} trump={Card.SuitCode(trump)} " +
                $"face_up_trump={(stock.Count > 0 ? stock[0].ToString() : "-")} stock={stock.Count} " +
                $"taking={defenderTaking} table=[{publicTable}] hand_counts=[{hands[0].Count},{hands[1].Count}]\n" +
                $"your hand: {string.Join(" ", hands[viewer])}";
        }

        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static Card Pop(List<Card> cards)
        {
            Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card;
        }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("durak", "デュラック", 2, 2, "attack-defense shedding",
                "36枚、6枚手札。攻撃札を同スート上位または切り札で覆い、山切れ後に先に上がる。",
                "Gokurakism Durak"),
            (players, random, options) => new DurakGame(players, random));
    }

    public sealed class SchnapsenGame : GameBase
    {
        private readonly DeterministicRandom rng;
        private readonly List<List<Card>> hands = new List<List<Card>>
        {
            new List<Card>(), new List<Card>()
        };
        private readonly List<Card> stock = new List<Card>();
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly int[] cardPoints = new int[2];
        private readonly int[] pendingMarriage = new int[2];
        private readonly int[] tricksWon = new int[2];
        private readonly int[] gamePoints = new int[2];
        private readonly HashSet<Tuple<int, Suit>> marriages = new HashSet<Tuple<int, Suit>>();
        private Card? trumpCard;
        private Suit trump;
        private int leader;
        private int dealer = 1;
        private int? closedBy;
        private string phase = "lead";
        private bool finished;

        public override string GameId => "schnapsen";
        public override string Name => "シュナプセン";

        public SchnapsenGame(int players, DeterministicRandom rng)
        {
            Players = 2;
            this.rng = rng;
            StartHand();
        }

        private void StartHand()
        {
            hands[0].Clear(); hands[1].Clear(); stock.Clear(); trick.Clear(); marriages.Clear();
            Array.Clear(cardPoints, 0, cardPoints.Length);
            Array.Clear(pendingMarriage, 0, pendingMarriage.Length);
            Array.Clear(tricksWon, 0, tricksWon.Length);
            stock.AddRange(Cards.Shuffled(Cards.StandardDeck(new[] { 1, 10, 11, 12, 13 }), rng));
            dealer = 1 - dealer;
            for (int round = 0; round < 5; round++)
                for (int offset = 1; offset <= 2; offset++)
                {
                    int player = (dealer + offset) % 2;
                    hands[player].Add(Pop(stock));
                }
            trumpCard = Pop(stock);
            trump = trumpCard.Value.Suit;
            leader = 1 - dealer;
            CurrentPlayer = leader;
            closedBy = null;
            phase = "lead";
        }

        private bool TalonOpen => !closedBy.HasValue && (stock.Count > 0 || trumpCard.HasValue);
        private bool StrictPlay => closedBy.HasValue || !TalonOpen;

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "last_check")
            {
                var actions = new List<Action> { new Action("settle_last_trick") };
                if (cardPoints[actual] >= 66) actions.Insert(0, new Action("claim_66"));
                return actions;
            }
            if (phase == "lead")
            {
                var actions = hands[actual].Select(card => new Action("play", card)).ToList();
                foreach (Card card in hands[actual].Where(card => card.Rank == 12 || card.Rank == 13))
                {
                    int mate = card.Rank == 12 ? 13 : 12;
                    var key = Tuple.Create(actual, card.Suit);
                    if (hands[actual].Any(other => other.Suit == card.Suit && other.Rank == mate) &&
                        !marriages.Contains(key)) actions.Add(new Action("marriage", card));
                }
                if (TalonOpen && tricksWon[actual] > 0 && trumpCard.HasValue &&
                    hands[actual].Contains(new Card(trump, 11))) actions.Add(new Action("exchange_trump"));
                if (TalonOpen && stock.Count > 0) actions.Add(new Action("close_talon"));
                if (cardPoints[actual] >= 66) actions.Add(new Action("claim_66"));
                return actions;
            }

            Card led = trick[0].Item2;
            IEnumerable<Card> candidates = hands[actual];
            if (StrictPlay)
            {
                Card[] sameSuit = candidates.Where(card => card.Suit == led.Suit).ToArray();
                Card[] winners = sameSuit.Where(card => Strength(card) > Strength(led)).ToArray();
                if (winners.Length > 0) candidates = winners;
                else if (sameSuit.Length > 0) candidates = sameSuit;
                else
                {
                    Card[] trumps = hands[actual].Where(card => card.Suit == trump).ToArray();
                    if (trumps.Length > 0) candidates = trumps;
                }
            }
            return candidates.Select(card => new Action("play", card)).ToArray();
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null);
            Guard.Legal(action, LegalActions(player));
            TurnCount++;
            if (phase == "last_check")
            {
                if (action.Kind == "claim_66") AwardHand(player, GamePointValue(player));
                else AwardHand(player, 1);
                return;
            }
            if (action.Kind == "claim_66")
            {
                AwardHand(player, GamePointValue(player));
                return;
            }
            if (action.Kind == "close_talon")
            {
                closedBy = player;
                return;
            }
            if (action.Kind == "exchange_trump")
            {
                Card jack = new Card(trump, 11);
                hands[player].Remove(jack);
                hands[player].Add(trumpCard!.Value);
                trumpCard = jack;
                return;
            }

            Card played = action.Card!.Value;
            hands[player].Remove(played);
            if (action.Kind == "marriage")
            {
                int value = played.Suit == trump ? 40 : 20;
                marriages.Add(Tuple.Create(player, played.Suit));
                if (tricksWon[player] > 0) cardPoints[player] += value;
                else pendingMarriage[player] += value;
            }
            trick.Add(Tuple.Create(player, played));
            if (trick.Count == 1)
            {
                phase = "follow";
                CurrentPlayer = 1 - player;
                return;
            }
            ResolveTrick();
        }

        private void ResolveTrick()
        {
            int winner = TrickWinner();
            cardPoints[winner] += trick.Sum(item => PointValue(item.Item2));
            tricksWon[winner]++;
            if (tricksWon[winner] == 1)
            {
                cardPoints[winner] += pendingMarriage[winner];
                pendingMarriage[winner] = 0;
            }
            trick.Clear();
            if (TalonOpen) DrawAfterTrick(winner);
            leader = winner;
            CurrentPlayer = winner;
            phase = "lead";
            if (hands[0].Count == 0 && hands[1].Count == 0)
            {
                // The tournament rules give the winner of the very last trick a
                // final opportunity to check out.  If they do not, the last-trick
                // winner receives exactly one game point; no Dix de Dernier bonus
                // is part of the adopted Schnapsen variant.
                phase = "last_check";
            }
        }

        private void DrawAfterTrick(int winner)
        {
            int loser = 1 - winner;
            if (stock.Count > 0) hands[winner].Add(Pop(stock));
            else if (trumpCard.HasValue)
            {
                hands[winner].Add(trumpCard.Value);
                trumpCard = null;
                return;
            }
            if (stock.Count > 0) hands[loser].Add(Pop(stock));
            else if (trumpCard.HasValue)
            {
                hands[loser].Add(trumpCard.Value);
                trumpCard = null;
            }
        }

        private int TrickWinner()
        {
            Tuple<int, Card> led = trick[0];
            Tuple<int, Card> followed = trick[1];
            if (followed.Item2.Suit == led.Item2.Suit)
                return Strength(followed.Item2) > Strength(led.Item2) ? followed.Item1 : led.Item1;
            if (followed.Item2.Suit == trump && led.Item2.Suit != trump) return followed.Item1;
            return led.Item1;
        }

        private int GamePointValue(int winner)
        {
            int opponent = 1 - winner;
            if (tricksWon[opponent] == 0) return 3;
            return cardPoints[opponent] < 33 ? 2 : 1;
        }

        private void AwardHand(int winner, int points)
        {
            gamePoints[winner] += points;
            if (gamePoints[winner] >= 7) finished = true;
            else StartHand();
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (actions.Any(action => action.Kind == "claim_66"))
                return actions.First(action => action.Kind == "claim_66");
            if (actions.Any(action => action.Kind == "marriage"))
                return actions.First(action => action.Kind == "marriage");
            if (actions.Any(action => action.Kind == "exchange_trump"))
                return actions.First(action => action.Kind == "exchange_trump");
            Action[] plays = actions.Where(action => action.Kind == "play").ToArray();
            if (plays.Length > 0) return plays.OrderBy(action => Strength(action.Card!.Value)).First();
            return actions[0];
        }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = gamePoints.Max();
            return new GameResult(Enumerable.Range(0, 2).Where(player => gamePoints[player] == high),
                gamePoints.Select(value => (double)value), "first to seven game points", TurnCount,
                new Dictionary<string, object> { { "card_points", cardPoints.ToArray() } });
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            string publicTrick = string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2));
            return $"phase={phase} trump={Card.SuitCode(trump)} trump_card={(trumpCard.HasValue ? trumpCard.Value.ToString() : "-")} " +
                $"talon={stock.Count} closed_by={(closedBy.HasValue ? "P" + closedBy.Value : "-")} trick=[{publicTrick}] " +
                $"card_points=[{string.Join(",", cardPoints)}] game_points=[{string.Join(",", gamePoints)}] " +
                $"hand_counts=[{hands[0].Count},{hands[1].Count}]\n" +
                $"your hand: {string.Join(" ", hands[viewer])}";
        }

        private static int Strength(Card card) => card.Rank == 1 ? 5 :
            card.Rank == 10 ? 4 : card.Rank == 13 ? 3 : card.Rank == 12 ? 2 : 1;
        private static int PointValue(Card card) => card.Rank == 1 ? 11 :
            card.Rank == 10 ? 10 : card.Rank == 13 ? 4 : card.Rank == 12 ? 3 : 2;
        private static Card Pop(List<Card> cards)
        {
            Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card;
        }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("schnapsen", "シュナプセン", 2, 2, "point trick-taking",
                "20枚で66カード点を宣言し、マリッジ、切り札J交換、山札クローズを使う7ゲーム点先取戦。",
                "Gokurakism Schnapsen"),
            (players, random, options) => new SchnapsenGame(players, random));
    }

    public sealed class CribbageGame : GameBase
    {
        private readonly DeterministicRandom rng;
        private readonly int targetScore;
        private readonly List<List<Card>> hands = new List<List<Card>>
        {
            new List<Card>(), new List<Card>()
        };
        private readonly List<List<Card>> keptHands = new List<List<Card>>
        {
            new List<Card>(), new List<Card>()
        };
        private readonly List<List<Card>> peggingHands = new List<List<Card>>
        {
            new List<Card>(), new List<Card>()
        };
        private readonly List<Card> crib = new List<Card>();
        private readonly List<Card> deck = new List<Card>();
        private readonly List<Card> sequence = new List<Card>();
        private readonly int[] scores = new int[2];
        private readonly bool[] saidGo = new bool[2];
        private Card? starter;
        private int dealer = 1;
        private int discardsMade;
        private int runningTotal;
        private int? lastPlayer;
        private string phase = "discard";
        private bool finished;

        public override string GameId => "cribbage";
        public override string Name => "クリベッジ";

        public CribbageGame(int players, DeterministicRandom rng,
            IReadOnlyDictionary<string, string> options)
        {
            Players = 2;
            this.rng = rng;
            targetScore = Math.Max(1, options.Integer("target_score", 121));
            StartDeal();
        }

        private void StartDeal()
        {
            hands[0].Clear(); hands[1].Clear(); keptHands[0].Clear(); keptHands[1].Clear();
            peggingHands[0].Clear(); peggingHands[1].Clear(); crib.Clear(); deck.Clear(); sequence.Clear();
            deck.AddRange(Cards.Shuffled(Cards.StandardDeck(), rng));
            dealer = 1 - dealer;
            for (int round = 0; round < 6; round++)
                for (int offset = 1; offset <= 2; offset++)
                    hands[(dealer + offset) % 2].Add(Pop(deck));
            starter = null;
            discardsMade = 0;
            runningTotal = 0;
            lastPlayer = null;
            saidGo[0] = false; saidGo[1] = false;
            phase = "discard";
            CurrentPlayer = 1 - dealer;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "discard")
            {
                var result = new List<Action>();
                for (int left = 0; left < hands[actual].Count - 1; left++)
                    for (int right = left + 1; right < hands[actual].Count; right++)
                        result.Add(new Action("discard_two", value:
                            Encode(new[] { hands[actual][left], hands[actual][right] })));
                return result;
            }
            Action[] plays = peggingHands[actual].Where(card => runningTotal + PipValue(card) <= 31)
                .Select(card => new Action("peg", card)).ToArray();
            return plays.Length > 0 ? plays : new[] { new Action("go") };
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null);
            Guard.Legal(action, LegalActions(player));
            TurnCount++;
            if (phase == "discard")
            {
                foreach (Card card in Decode(action.Value!))
                {
                    hands[player].Remove(card);
                    crib.Add(card);
                }
                keptHands[player].AddRange(hands[player]);
                discardsMade++;
                if (discardsMade == 1) CurrentPlayer = 1 - player;
                else BeginPegging();
                return;
            }
            if (action.Kind == "go")
            {
                ApplyGo(player);
                return;
            }
            ApplyPeg(player, action.Card!.Value);
        }

        private void BeginPegging()
        {
            starter = Pop(deck);
            if (starter.Value.Rank == 11 && AddScore(dealer, 2)) return;
            peggingHands[0].AddRange(keptHands[0]);
            peggingHands[1].AddRange(keptHands[1]);
            phase = "pegging";
            CurrentPlayer = 1 - dealer;
        }

        private void ApplyPeg(int player, Card card)
        {
            peggingHands[player].Remove(card);
            runningTotal += PipValue(card);
            sequence.Add(card);
            lastPlayer = player;
            saidGo[player] = false;
            int points = PeggingScore(sequence, runningTotal);
            if (points > 0 && AddScore(player, points)) return;
            if (runningTotal == 31)
            {
                ResetCount();
                if (BothPeggingHandsEmpty()) { ScoreShow(); return; }
                CurrentPlayer = peggingHands[1 - player].Count > 0 ? 1 - player : player;
                return;
            }
            if (BothPeggingHandsEmpty())
            {
                if (AddScore(player, 1)) return;
                ScoreShow();
                return;
            }
            int other = 1 - player;
            if (saidGo[other])
            {
                if (CanPeg(player)) CurrentPlayer = player;
                else CloseCount();
            }
            else CurrentPlayer = other;
        }

        private void ApplyGo(int player)
        {
            saidGo[player] = true;
            int other = 1 - player;
            if (CanPeg(other)) CurrentPlayer = other;
            else CloseCount();
        }

        private void CloseCount()
        {
            int previous = lastPlayer ?? 0;
            if (runningTotal > 0 && AddScore(previous, 1)) return;
            ResetCount();
            if (BothPeggingHandsEmpty()) { ScoreShow(); return; }
            int next = 1 - previous;
            CurrentPlayer = peggingHands[next].Count > 0 ? next : previous;
        }

        private void ResetCount()
        {
            runningTotal = 0;
            sequence.Clear();
            saidGo[0] = false; saidGo[1] = false;
            lastPlayer = null;
        }

        private bool CanPeg(int player) => peggingHands[player]
            .Any(card => runningTotal + PipValue(card) <= 31);
        private bool BothPeggingHandsEmpty() => peggingHands[0].Count == 0 && peggingHands[1].Count == 0;

        private void ScoreShow()
        {
            phase = "show";
            int nonDealer = 1 - dealer;
            if (AddScore(nonDealer, HandScore(keptHands[nonDealer], starter!.Value, false))) return;
            if (AddScore(dealer, HandScore(keptHands[dealer], starter.Value, false))) return;
            if (AddScore(dealer, HandScore(crib, starter.Value, true))) return;
            StartDeal();
        }

        private bool AddScore(int player, int points)
        {
            scores[player] += points;
            if (scores[player] < targetScore) return false;
            finished = true;
            CurrentPlayer = player;
            return true;
        }

        public static int PeggingScore(IReadOnlyList<Card> played, int total)
        {
            int score = total == 15 || total == 31 ? 2 : 0;
            int same = 1;
            for (int index = played.Count - 2; index >= 0 &&
                played[index].Rank == played[played.Count - 1].Rank; index--) same++;
            if (same >= 2) score += same * (same - 1);
            for (int length = played.Count; length >= 3; length--)
            {
                int[] ranks = played.Skip(played.Count - length).Select(card => card.Rank).ToArray();
                if (ranks.Distinct().Count() == length && ranks.Max() - ranks.Min() == length - 1)
                {
                    score += length;
                    break;
                }
            }
            return score;
        }

        public static int HandScore(IReadOnlyList<Card> hand, Card starter, bool isCrib)
        {
            Card[] all = hand.Concat(new[] { starter }).ToArray();
            int score = 0;
            for (int mask = 1; mask < (1 << all.Length); mask++)
            {
                int sum = 0;
                for (int index = 0; index < all.Length; index++)
                    if ((mask & (1 << index)) != 0) sum += PipValue(all[index]);
                if (sum == 15) score += 2;
            }
            foreach (IGrouping<int, Card> group in all.GroupBy(card => card.Rank))
                score += group.Count() * (group.Count() - 1);

            int longest = 0;
            int runCount = 0;
            for (int mask = 1; mask < (1 << all.Length); mask++)
            {
                Card[] subset = Enumerable.Range(0, all.Length)
                    .Where(index => (mask & (1 << index)) != 0).Select(index => all[index]).ToArray();
                if (subset.Length < 3 || subset.Select(card => card.Rank).Distinct().Count() != subset.Length)
                    continue;
                int[] ranks = subset.Select(card => card.Rank).ToArray();
                if (ranks.Max() - ranks.Min() != subset.Length - 1) continue;
                if (subset.Length > longest) { longest = subset.Length; runCount = 1; }
                else if (subset.Length == longest) runCount++;
            }
            score += longest * runCount;

            bool fourFlush = hand.Count == 4 && hand.All(card => card.Suit == hand[0].Suit);
            if (fourFlush && (!isCrib || starter.Suit == hand[0].Suit))
                score += starter.Suit == hand[0].Suit ? 5 : 4;
            if (hand.Any(card => card.Rank == 11 && card.Suit == starter.Suit)) score++;
            return score;
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "discard")
                return actions.OrderBy(action => Decode(action.Value!).Sum(PipValue)).First();
            Action[] plays = actions.Where(action => action.Kind == "peg").ToArray();
            if (plays.Length == 0) return actions[0];
            return plays.OrderByDescending(action =>
                    PeggingScore(sequence.Concat(new[] { action.Card!.Value }).ToArray(),
                        runningTotal + PipValue(action.Card.Value)))
                .ThenBy(action => PipValue(action.Card!.Value)).First();
        }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 2).Where(player => scores[player] == high),
                scores.Select(score => (double)score), "first to " + targetScore, TurnCount,
                new Dictionary<string, object> { { "dealer", dealer } });
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            return $"phase={phase} dealer=P{dealer} starter={(starter.HasValue ? starter.Value.ToString() : "-")} " +
                $"count={runningTotal} sequence=[{string.Join(" ", sequence)}] scores=[{string.Join(",", scores)}] " +
                $"crib_count={crib.Count} hand_counts=[{hands[0].Count},{hands[1].Count}] " +
                $"pegging_counts=[{peggingHands[0].Count},{peggingHands[1].Count}]\n" +
                $"your hand: {string.Join(" ", phase == "pegging" ? peggingHands[viewer] : hands[viewer])}";
        }

        private static int PipValue(Card card) => card.Rank == 1 ? 1 : Math.Min(card.Rank, 10);
        private static string Encode(IEnumerable<Card> cards) =>
            string.Join(",", cards.OrderBy(card => card.Rank).ThenBy(card => card.Suit));
        private static Card[] Decode(string value) => value.Split(',').Select(Card.Parse).ToArray();
        private static Card Pop(List<Card> cards)
        {
            Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card;
        }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("cribbage", "クリベッジ", 2, 2, "counting",
                "6枚から各2枚をcribへ捨て、31までのペギングと手札・cribの15、ペア、ラン等を得点化する121点戦。",
                "Pagat Six Card Cribbage", new Dictionary<string, string> { { "target_score", "121" } }),
            (players, random, options) => new CribbageGame(players, random, options));
    }
}
