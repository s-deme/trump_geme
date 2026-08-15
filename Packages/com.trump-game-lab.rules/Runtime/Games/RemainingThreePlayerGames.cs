using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    internal static class RemainingThreePlayerGames
    {
        public static void RegisterGames(GameRegistry registry)
        {
            ItalianWhistGame.Register(registry);
            KaedamaTrickGame.Register(registry);
            TrickOfTheDeadGame.Register(registry);
            CorpoGame.Register(registry);
        }
    }

    public sealed class ItalianWhistGame : GameBase
    {
        private sealed class ItalianCard
        {
            public Card? Card { get; }
            public bool RedJoker { get; }
            public bool Joker => !Card.HasValue;
            public bool Red => Joker ? RedJoker : Card!.Value.Suit == Suit.Diamonds || Card.Value.Suit == Suit.Hearts;
            public string Id => Joker ? (RedJoker ? "XR" : "XB") : Card!.Value.ToString();
            public ItalianCard(Card card) { Card = card; }
            public ItalianCard(bool redJoker) { RedJoker = redJoker; }
            public override string ToString() => Id;
        }

        private readonly DeterministicRandom rng;
        private readonly int sessionDeals;
        private readonly List<List<ItalianCard>> hands = NewHands();
        private readonly List<List<ItalianCard>> reserved = NewHands();
        private readonly List<List<ItalianCard>> secondHands = NewHands();
        private readonly List<Tuple<int, ItalianCard>> trick = new List<Tuple<int, ItalianCard>>();
        private readonly Dictionary<int, Suit> jokerSuits = new Dictionary<int, Suit>();
        private readonly Dictionary<int, int> jokerRanks = new Dictionary<int, int>();
        private readonly int[] firstTricks = new int[3];
        private readonly int[] secondTricks = new int[3];
        private readonly int[] scores = new int[3];
        private readonly int[] reserveCounts = new int[3];
        private int dealer = 2;
        private int dealsPlayed;
        private int tricksPlayed;
        private string phase = "reserve";
        private int jokerChoiceIndex = -1;
        private bool finished;

        public override string GameId => "italian_whist";
        public override string Name => "イタリアン・ホイスト";

        public ItalianWhistGame(int players, DeterministicRandom rng,
            IReadOnlyDictionary<string, string> options)
        {
            Players = 3; this.rng = rng;
            sessionDeals = Math.Max(1, options.Integer("deals", 6)); StartDeal();
        }

        private static List<List<ItalianCard>> NewHands() => new List<List<ItalianCard>>
        {
            new List<ItalianCard>(), new List<ItalianCard>(), new List<ItalianCard>()
        };

        private bool HasTrump => dealsPlayed % 6 >= 3;

        private void StartDeal()
        {
            foreach (List<ItalianCard> hand in hands) hand.Clear();
            foreach (List<ItalianCard> pile in reserved) pile.Clear();
            foreach (List<ItalianCard> pile in secondHands) pile.Clear();
            trick.Clear(); jokerSuits.Clear(); jokerRanks.Clear();
            Array.Clear(firstTricks, 0, 3); Array.Clear(secondTricks, 0, 3);
            Array.Clear(reserveCounts, 0, 3);
            var deck = Cards.StandardDeck().Select(card => new ItalianCard(card)).ToList();
            deck.Add(new ItalianCard(true)); deck.Add(new ItalianCard(false)); rng.Shuffle(deck);
            dealer = (dealer + 1) % 3;
            for (int round = 0; round < 18; round++)
                for (int offset = 1; offset <= 3; offset++) hands[(dealer + offset) % 3].Add(Pop(deck));
            phase = "reserve"; tricksPlayed = 0; CurrentPlayer = (dealer + 1) % 3;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "choose_joker_suit")
            {
                ItalianCard joker = trick[jokerChoiceIndex].Item2;
                Suit[] suits = joker.Red
                    ? new[] { Suit.Diamonds, Suit.Hearts }
                    : new[] { Suit.Clubs, Suit.Spades };
                return suits.Select(suit => new Action("choose_joker_suit",
                    value: Card.SuitCode(suit))).ToArray();
            }
            if (phase == "choose_joker_rank")
            {
                Suit suit = jokerSuits[jokerChoiceIndex];
                var used = new HashSet<int>(trick.Where(item => !item.Item2.Joker &&
                    item.Item2.Card!.Value.Suit == suit).Select(item => item.Item2.Card!.Value.Rank));
                return Enumerable.Range(1, 13).Where(rank => !used.Contains(rank))
                    .Select(rank => new Action("choose_joker_rank", value: rank.ToString())).ToArray();
            }
            if (phase == "reserve") return hands[actual]
                .Select(card => new Action("reserve_for_second_half", card.Card, value: card.Id)).ToArray();
            IEnumerable<ItalianCard> cards = hands[actual];
            if (trick.Count > 0)
            {
                ItalianCard lead = trick[0].Item2;
                ItalianCard[] follow = lead.Joker
                    ? cards.Where(card => card.Red == lead.Red).ToArray()
                    : cards.Where(card => (!card.Joker && card.Card!.Value.Suit == lead.Card!.Value.Suit) ||
                        (card.Joker && card.Red == lead.Red)).ToArray();
                if (follow.Length > 0) cards = follow;
            }
            return cards.Select(card => new Action("play", card.Card, value: card.Id)).ToArray();
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "choose_joker_suit")
            {
                jokerSuits[jokerChoiceIndex] = Card.ParseSuit(action.Value!);
                ContinueJokerResolution(); return;
            }
            if (phase == "choose_joker_rank")
            {
                jokerRanks[jokerChoiceIndex] = int.Parse(action.Value!);
                ContinueJokerResolution(); return;
            }
            ItalianCard card = hands[player].First(item => item.Id == action.Value);
            hands[player].Remove(card);
            if (phase == "reserve")
            {
                reserved[player].Add(card); reserveCounts[player]++;
                if (reserveCounts[player] < 9) return;
                if (reserveCounts.Sum() < 27) { CurrentPlayer = (player + 1) % 3; return; }
                TransferSecondHands(); phase = "first_half"; CurrentPlayer = (dealer + 1) % 3; return;
            }
            trick.Add(Tuple.Create(player, card));
            if (trick.Count < 3) { CurrentPlayer = (player + 1) % 3; return; }
            BeginJokerResolution(); return;
        }

        private void BeginJokerResolution()
        {
            jokerSuits.Clear(); jokerRanks.Clear();
            for (int index = 0; index < trick.Count; index++)
            {
                if (!trick[index].Item2.Joker) continue;
                Suit? forced = ForcedJokerSuit(index);
                if (forced.HasValue) jokerSuits[index] = forced.Value;
            }
            ContinueJokerResolution();
        }

        private void ContinueJokerResolution()
        {
            int unresolvedSuit = Enumerable.Range(0, trick.Count).Where(index =>
                trick[index].Item2.Joker && !jokerSuits.ContainsKey(index)).DefaultIfEmpty(-1).First();
            if (unresolvedSuit >= 0)
            {
                jokerChoiceIndex = unresolvedSuit; phase = "choose_joker_suit";
                CurrentPlayer = trick[unresolvedSuit].Item1; return;
            }
            int unresolvedRank = Enumerable.Range(0, trick.Count).Where(index =>
                trick[index].Item2.Joker && !jokerRanks.ContainsKey(index)).DefaultIfEmpty(-1).First();
            if (unresolvedRank >= 0)
            {
                jokerChoiceIndex = unresolvedRank; phase = "choose_joker_rank";
                CurrentPlayer = trick[unresolvedRank].Item1; return;
            }
            phase = tricksPlayed < 9 ? "first_half" : "second_half";
            ResolveCompletedTrick();
        }

        private Suit? ForcedJokerSuit(int index)
        {
            ItalianCard joker = trick[index].Item2;
            if (index > 0 && !trick[0].Item2.Joker && trick[0].Item2.Red == joker.Red)
                return trick[0].Item2.Card!.Value.Suit;
            ItalianCard? matching = trick.Where((item, other) => other != index)
                .Select(item => item.Item2).FirstOrDefault(card => !card.Joker && card.Red == joker.Red);
            return matching?.Card!.Value.Suit;
        }

        private void ResolveCompletedTrick()
        {
            int winner = TrickWinner();
            if (phase == "first_half") firstTricks[winner]++; else secondTricks[winner]++;
            trick.Clear(); jokerSuits.Clear(); jokerRanks.Clear(); jokerChoiceIndex = -1; tricksPlayed++;
            if (tricksPlayed == 9 && phase == "first_half")
            {
                for (int p = 0; p < 3; p++) hands[p].AddRange(secondHands[p]);
                phase = "second_half"; CurrentPlayer = (dealer + 2) % 3; return;
            }
            if (tricksPlayed == 18) { FinishDeal(); return; }
            CurrentPlayer = winner;
        }

        private void TransferSecondHands()
        {
            int mode = dealsPlayed % 6;
            int shift = mode % 3 == 0 ? 1 : mode % 3 == 1 ? 2 : 0;
            for (int source = 0; source < 3; source++)
                secondHands[(source + shift) % 3].AddRange(reserved[source]);
        }

        private int TrickWinner()
        {
            Suit[] suits = Enumerable.Range(0, trick.Count).Select(EffectiveSuit).ToArray();
            int[] strengths = Enumerable.Range(0, trick.Count).Select(index => EffectiveStrength(index, suits)).ToArray();
            Suit led = suits[0];
            IEnumerable<int> eligible = HasTrump && suits.Contains(Suit.Spades)
                ? Enumerable.Range(0, trick.Count).Where(index => suits[index] == Suit.Spades)
                : Enumerable.Range(0, trick.Count).Where(index => suits[index] == led);
            int best = eligible.First();
            foreach (int index in eligible.Skip(1)) if (strengths[index] > strengths[best]) best = index;
            return trick[best].Item1;
        }

        private Suit EffectiveSuit(int index)
        {
            ItalianCard card = trick[index].Item2;
            if (!card.Joker) return card.Card!.Value.Suit;
            return jokerSuits[index];
        }

        private int EffectiveStrength(int index, IReadOnlyList<Suit> suits)
        {
            ItalianCard card = trick[index].Item2;
            if (!card.Joker) return Strength(card.Card!.Value);
            int rank = jokerRanks[index];
            return rank == 1 ? 14 : rank;
        }

        private void FinishDeal()
        {
            for (int player = 0; player < 3; player++) scores[player] += firstTricks[player] - secondTricks[player];
            dealsPlayed++;
            if (dealsPlayed >= sessionDeals) finished = true; else StartDeal();
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "choose_joker_suit")
                return HasTrump && actions.Any(action => action.Value == "S")
                    ? actions.First(action => action.Value == "S") : actions[0];
            if (phase == "choose_joker_rank")
                return tricksPlayed < 9 ? actions.Last() : actions[0];
            if (phase == "reserve") return actions.OrderBy(action => ActionStrength(action)).First();
            return phase == "first_half"
                ? actions.OrderByDescending(action => ActionStrength(action)).First()
                : actions.OrderBy(action => ActionStrength(action)).First();
        }

        private static int ActionStrength(Action action) => action.Card.HasValue ? Strength(action.Card.Value) : 15;
        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static ItalianCard Pop(List<ItalianCard> cards)
        { ItalianCard card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 3).Where(player => scores[player] == high),
                scores.Select(value => (double)value), "six-deal first-half minus second-half tricks", TurnCount);
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            string hiddenSecond = phase == "second_half" ? "revealed" : secondHands[viewer].Count + " hidden";
            string declarations = string.Join(" ", Enumerable.Range(0, trick.Count)
                .Where(index => trick[index].Item2.Joker && jokerSuits.ContainsKey(index))
                .Select(index => trick[index].Item2.Id + "=" + Card.SuitCode(jokerSuits[index]) +
                    (jokerRanks.ContainsKey(index) ? ":" + jokerRanks[index] : ":?")));
            return $"phase={phase} deal={dealsPlayed + 1}/{sessionDeals} dealer=P{dealer} trump={(HasTrump ? "S" : "none")} " +
                $"trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] " +
                $"joker_declarations=[{declarations}] " +
                $"first_tricks=[{string.Join(",", firstTricks)}] second_tricks=[{string.Join(",", secondTricks)}] " +
                $"scores=[{string.Join(",", scores)}] reserved=[{string.Join(",", reserveCounts)}] your_second_half={hiddenSecond} " +
                $"hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("italian_whist", "イタリアン・ホイスト", 3, 3, "two-half trick-taking",
                "54枚を18枚ずつ配り、各自が後半用9枚を左・右・自分へ順に渡す。前半勝数－後半勝数を、no-trumpとspade-trump各3ディールで競う。Joker所有者は必要なsuitと場にないrankを全員のplay後に宣言する。",
                "gokurakism/Italian Whist", new Dictionary<string, string> { { "deals", "6" } }),
            (players, random, options) => new ItalianWhistGame(players, random, options));
    }

    public sealed class KaedamaTrickGame : GameBase
    {
        private sealed class KaedamaCard
        {
            public Card? Card { get; }
            public int JokerIndex { get; }
            public bool Joker => !Card.HasValue;
            public string Id => Joker ? "X" + JokerIndex : Card!.Value.ToString();
            public KaedamaCard(Card card) { Card = card; JokerIndex = -1; }
            public KaedamaCard(int jokerIndex) { JokerIndex = jokerIndex; }
            public override string ToString() => Id;
        }

        private readonly DeterministicRandom rng;
        private readonly int sessionDeals;
        private readonly List<List<KaedamaCard>> hands = NewHands();
        private readonly List<Tuple<int, KaedamaCard>> trick = new List<Tuple<int, KaedamaCard>>();
        private readonly int[] cardPoints = new int[3];
        private readonly int[] scores = new int[3];
        private readonly int[] initialJokers = new int[3];
        private int dealer = 2;
        private int dealsPlayed;
        private int tricksPlayed;
        private int soloist = -1;
        private int akechi = -1;
        private int kobayashi = -1;
        private int jokersPlayed;
        private bool soloHadBoth;
        private bool finished;

        public override string GameId => "kaedama_trick";
        public override string Name => "替え玉トリック";

        public KaedamaTrickGame(int players, DeterministicRandom rng,
            IReadOnlyDictionary<string, string> options)
        {
            Players = 3; this.rng = rng;
            sessionDeals = Math.Max(1, options.Integer("deals", 9)); StartDeal();
        }

        private static List<List<KaedamaCard>> NewHands() => new List<List<KaedamaCard>>
        {
            new List<KaedamaCard>(), new List<KaedamaCard>(), new List<KaedamaCard>()
        };

        private void StartDeal()
        {
            foreach (List<KaedamaCard> hand in hands) hand.Clear();
            trick.Clear(); Array.Clear(cardPoints, 0, 3); Array.Clear(initialJokers, 0, 3);
            var deck = Cards.StandardDeck(new[] { 1, 8, 9, 10, 11, 12, 13 })
                .Select(card => new KaedamaCard(card)).ToList();
            deck.Add(new KaedamaCard(0)); deck.Add(new KaedamaCard(1)); rng.Shuffle(deck);
            dealer = (dealer + 1) % 3;
            for (int round = 0; round < 10; round++)
                for (int offset = 1; offset <= 3; offset++) hands[(dealer + offset) % 3].Add(Pop(deck));
            for (int player = 0; player < 3; player++) initialJokers[player] = hands[player].Count(card => card.Joker);
            tricksPlayed = 0; soloist = akechi = kobayashi = -1; jokersPlayed = 0; soloHadBoth = false;
            CurrentPlayer = (dealer + 1) % 3;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player); IEnumerable<KaedamaCard> cards = hands[actual];
            if (trick.Count > 0)
            {
                KaedamaCard lead = trick[0].Item2;
                KaedamaCard[] follow = IsTrump(lead)
                    ? cards.Where(IsTrump).ToArray()
                    : cards.Where(card => !card.Joker && card.Card!.Value.Suit == lead.Card!.Value.Suit).ToArray();
                if (follow.Length > 0) cards = follow;
            }
            return cards.Select(card => new Action("play", card.Card, value: card.Id)).ToArray();
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            KaedamaCard card = hands[player].First(item => item.Id == action.Value); hands[player].Remove(card);
            if (card.Joker)
            {
                jokersPlayed++;
                if (soloist < 0)
                {
                    soloist = player; soloHadBoth = initialJokers[player] == 2;
                    if (!soloHadBoth)
                    {
                        akechi = Enumerable.Range(0, 3).Single(p => p != soloist && initialJokers[p] == 1);
                        kobayashi = Enumerable.Range(0, 3).Single(p => p != soloist && initialJokers[p] == 0);
                    }
                }
            }
            trick.Add(Tuple.Create(player, card));
            if (trick.Count < 3) { CurrentPlayer = (player + 1) % 3; return; }
            int winner = TrickWinner(); cardPoints[winner] += trick.Sum(item => PointValue(item.Item2));
            trick.Clear(); tricksPlayed++;
            if (tricksPlayed >= 10) FinishDeal(); else CurrentPlayer = winner;
        }

        private int TrickWinner()
        {
            KaedamaCard lead = trick[0].Item2;
            IEnumerable<Tuple<int, KaedamaCard>> eligible = trick.Any(item => IsTrump(item.Item2))
                ? trick.Where(item => IsTrump(item.Item2))
                : trick.Where(item => item.Item2.Card!.Value.Suit == lead.Card!.Value.Suit);
            Tuple<int, KaedamaCard> best = eligible.First();
            foreach (Tuple<int, KaedamaCard> item in eligible.Skip(1))
                if (Strength(item.Item2) > Strength(best.Item2)) best = item;
            return best.Item1;
        }

        private void FinishDeal()
        {
            if (soloist < 0) throw new InvalidOperationException("No joker was played.");
            if (!soloHadBoth)
            {
                bool soloWins = cardPoints[soloist] >= 76 || cardPoints[kobayashi] >= cardPoints[akechi] ||
                    Math.Abs(cardPoints[akechi] - cardPoints[kobayashi]) >= 30;
                if (soloWins) scores[soloist] += Math.Max(10, cardPoints[kobayashi]);
                else
                {
                    int award = Math.Max(10, Math.Min(cardPoints[soloist], cardPoints[kobayashi]));
                    scores[akechi] += award; scores[kobayashi] += award;
                }
            }
            else
            {
                int[] detectives = Enumerable.Range(0, 3).Where(player => player != soloist).ToArray();
                int low = Math.Min(cardPoints[detectives[0]], cardPoints[detectives[1]]);
                bool soloWins = cardPoints[soloist] <= 100 &&
                    (cardPoints[soloist] >= 76 || Math.Abs(cardPoints[detectives[0]] - cardPoints[detectives[1]]) >= 30);
                int award = Math.Max(10, low);
                if (soloWins) scores[soloist] += award;
                else { scores[detectives[0]] += award; scores[detectives[1]] += award; }
            }
            dealsPlayed++;
            if (dealsPlayed >= sessionDeals) finished = true; else StartDeal();
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1) =>
            LegalActions(player).OrderBy(action => action.Card.HasValue ? Strength(new KaedamaCard(action.Card.Value)) : 20).First();

        private static bool IsTrump(KaedamaCard card) => card.Joker || card.Card!.Value.Suit == Suit.Spades;
        private static int Strength(KaedamaCard card)
        {
            if (card.Joker) return 20;
            int rank = card.Card!.Value.Rank;
            return rank == 1 ? 17 : rank == 10 ? 16 : rank == 13 ? 15 : rank == 12 ? 14 :
                rank == 11 ? 13 : rank;
        }
        private static int PointValue(KaedamaCard card)
        {
            if (card.Joker) return 15;
            switch (card.Card!.Value.Rank)
            {
                case 1: return 11;
                case 10: return 10;
                case 13: return 4;
                case 12: return 3;
                case 11: return 2;
                default: return 0;
            }
        }
        private static KaedamaCard Pop(List<KaedamaCard> cards)
        { KaedamaCard card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 3).Where(player => scores[player] == high),
                scores.Select(value => (double)value), "nine hidden-role deals", TurnCount);
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            string roles = soloist < 0 ? "unrevealed" : jokersPlayed < 2 ? "solo=P" + soloist + ",partners hidden" :
                soloHadBoth ? "solo=P" + soloist + ",two junior detectives" :
                "solo=P" + soloist + ",akechi=P" + akechi + ",kobayashi=P" + kobayashi;
            return $"deal={dealsPlayed + 1}/{sessionDeals} trick_no={tricksPlayed + 1}/10 roles={roles} " +
                $"trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] " +
                $"card_points=[{string.Join(",", cardPoints)}] scores=[{string.Join(",", scores)}] " +
                $"hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("kaedama_trick", "替え玉トリック", 3, 3, "hidden-role trick-taking",
                "8～Aの28枚とJoker2枚を使うspade切り札戦。最初のJokerを出した怪人二十面相と、残るJokerの明智探偵・Jokerなしの小林少年の条件別勝敗とカード点を9ディール集計する。",
                "gokurakism/Kaedama Trick", new Dictionary<string, string> { { "deals", "9" } }),
            (players, random, options) => new KaedamaTrickGame(players, random, options));
    }

    public sealed class TrickOfTheDeadGame : GameBase
    {
        private readonly List<List<Card>> hands = NewHands();
        private readonly List<List<Card>> zombies = NewHands();
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly List<Card> pendingCards = new List<Card>();
        private readonly Queue<int> pickOrder = new Queue<int>();
        private readonly int[] points = new int[3];
        private int firstTricks;
        private int secondTricks;
        private int pendingLeader;
        private string phase = "first_half";
        private bool finished;

        public override string GameId => "trick_of_the_dead";
        public override string Name => "Trick of the Dead";

        public TrickOfTheDeadGame(int players, DeterministicRandom rng)
        {
            Players = 3;
            List<Card> deck = Cards.Shuffled(Cards.StandardDeck(new[] { 3, 4, 5, 6, 7, 8, 9, 13 })
                .Where(card => card.Suit != Suit.Spades), rng);
            for (int round = 0; round < 7; round++)
                for (int player = 0; player < 3; player++) hands[player].Add(Pop(deck));
            CurrentPlayer = 0;
        }

        private static List<List<Card>> NewHands() => new List<List<Card>>
        {
            new List<Card>(), new List<Card>(), new List<Card>()
        };

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "zombie_pick") return pendingCards.Select(card => new Action("take_zombie", card)).ToArray();
            IEnumerable<Card> cards = hands[actual];
            if (phase == "second_half" && trick.Count > 0)
            {
                Card lead = trick[0].Item2;
                Card[] follow = lead.Rank == 13
                    ? cards.Where(card => card.Rank == 13).ToArray()
                    : cards.Where(card => card.Rank != 13 && card.Suit == lead.Suit).ToArray();
                if (follow.Length > 0) cards = follow;
            }
            return cards.Select(card => new Action("play", card)).ToArray();
        }

        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if (phase == "zombie_pick")
            {
                Card picked = action.Card!.Value; pendingCards.Remove(picked); zombies[player].Add(picked);
                pickOrder.Dequeue();
                if (pickOrder.Count > 0) { CurrentPlayer = pickOrder.Peek(); return; }
                if (firstTricks >= 6)
                {
                    for (int p = 0; p < 3; p++) { hands[p].AddRange(zombies[p]); zombies[p].Clear(); }
                    phase = "second_half";
                }
                else phase = "first_half";
                CurrentPlayer = pendingLeader; return;
            }
            Card card = action.Card!.Value; hands[player].Remove(card); trick.Add(Tuple.Create(player, card));
            if (trick.Count < 3) { CurrentPlayer = (player + 1) % 3; return; }
            int winner = TrickWinner(); pendingLeader = winner;
            if (phase == "first_half")
            {
                points[winner]++; firstTricks++;
                foreach (Tuple<int, Card> item in trick.OrderBy(item => Strength(item.Item2))
                    .ThenBy(item => trick.IndexOf(item))) pickOrder.Enqueue(item.Item1);
                pendingCards.AddRange(trick.Select(item => item.Item2)); trick.Clear();
                phase = "zombie_pick"; CurrentPlayer = pickOrder.Peek(); return;
            }
            points[winner] += 2; secondTricks++; trick.Clear();
            if (secondTricks >= 7) finished = true; else CurrentPlayer = winner;
        }

        private int TrickWinner()
        {
            if (phase == "first_half")
                return trick.OrderByDescending(item => Strength(item.Item2)).First().Item1;
            Tuple<int, Card>? firstKing = trick.FirstOrDefault(item => item.Item2.Rank == 13);
            if (firstKing != null) return firstKing.Item1;
            Suit led = trick[0].Item2.Suit; Tuple<int, Card> best = trick[0];
            foreach (Tuple<int, Card> item in trick.Skip(1).Where(item => item.Item2.Suit == led))
                if (Strength(item.Item2) > Strength(best.Item2)) best = item;
            return best.Item1;
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "zombie_pick") return actions.OrderByDescending(action => Strength(action.Card!.Value)).First();
            return actions.OrderBy(action => Strength(action.Card!.Value)).First();
        }

        private static int Strength(Card card) => card.Rank;
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int[] final = points.Select(value => value % 10).ToArray(); int high = final.Max();
            return new GameResult(Enumerable.Range(0, 3).Where(player => final[player] == high),
                final.Select(value => (double)value), "largest units digit after zombie half", TurnCount,
                new Dictionary<string, object> { { "raw_points", points.ToArray() } });
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            return $"phase={phase} first_tricks={firstTricks}/6 second_tricks={secondTricks}/7 " +
                $"trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] " +
                $"zombie_pool={pendingCards.Count} zombie_counts=[{string.Join(",", zombies.Select(pile => pile.Count))}] " +
                $"points=[{string.Join(",", points)}] hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("trick_of_the_dead", "Trick of the Dead", 3, 3, "two-half trick-taking",
                "3スートの3～9・Kから7枚ずつ配る。前半6trickはメイフォロー・高rank勝ちで1点、低rank順に場札を1枚ずつ伏せて回収し、残り1枚とZombie札で後半7trickのK固定切り札・マストフォローを2点で行い、合計の1の位を競う。",
                "gokurakism/Trick of the Dead"),
            (players, random, options) => new TrickOfTheDeadGame(players, random));
    }

    public sealed class CorpoGame : GameBase
    {
        private readonly DeterministicRandom rng;
        private readonly int targetScore;
        private readonly List<List<Card>> hands = NewHands();
        private readonly List<List<Card>> pokerHands = NewHands();
        private readonly List<List<Card>> revealedPokerHands = NewHands();
        private readonly List<Tuple<int, Card>> trick = new List<Tuple<int, Card>>();
        private readonly int[] tricks = new int[3];
        private readonly int[] scores = new int[3];
        private readonly int[] reserved = new int[3];
        private int dealer = 2;
        private int bidsMade;
        private int bidder = -1;
        private int tricksPlayed;
        private string phase = "reserve_poker";
        private bool hasPokerShowdown;
        private bool finished;

        public override string GameId => "corpo";
        public override string Name => "コルポ";

        public CorpoGame(int players, DeterministicRandom rng,
            IReadOnlyDictionary<string, string> options)
        {
            Players = 3; this.rng = rng; targetScore = Math.Max(1, options.Integer("target_score", 15)); StartDeal();
        }

        private static List<List<Card>> NewHands() => new List<List<Card>>
        {
            new List<Card>(), new List<Card>(), new List<Card>()
        };

        private void StartDeal()
        {
            foreach (List<Card> hand in hands) hand.Clear(); foreach (List<Card> hand in pokerHands) hand.Clear();
            trick.Clear(); Array.Clear(tricks, 0, 3); Array.Clear(reserved, 0, 3);
            List<Card> deck = Cards.Shuffled(Cards.StandardDeck(new[] { 1, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 }), rng);
            dealer = (dealer + 1) % 3;
            for (int round = 0; round < 14; round++)
                for (int offset = 1; offset <= 3; offset++) hands[(dealer + offset) % 3].Add(Pop(deck));
            bidsMade = 0; bidder = -1; tricksPlayed = 0; phase = "reserve_poker"; CurrentPlayer = (dealer + 1) % 3;
        }

        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if (phase == "reserve_poker") return hands[actual].Select(card => new Action("reserve_for_poker", card)).ToArray();
            if (phase == "bid") return new[] { new Action("pass"), new Action("colpo") };
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
            if (phase == "reserve_poker")
            {
                Card card = action.Card!.Value; hands[player].Remove(card); pokerHands[player].Add(card); reserved[player]++;
                if (reserved[player] < 5) return;
                if (reserved.Sum() < 15) { CurrentPlayer = (player + 1) % 3; return; }
                phase = "bid"; CurrentPlayer = (dealer + 1) % 3; return;
            }
            if (phase == "bid")
            {
                bidsMade++;
                if (action.Kind == "colpo") { bidder = player; phase = "play"; CurrentPlayer = player; return; }
                if (bidsMade >= 3) { phase = "play"; CurrentPlayer = (dealer + 1) % 3; }
                else CurrentPlayer = (player + 1) % 3;
                return;
            }
            Card played = action.Card!.Value; hands[player].Remove(played); trick.Add(Tuple.Create(player, played));
            if (trick.Count < 3) { CurrentPlayer = (player + 1) % 3; return; }
            int winner = TrickWinner(); tricks[winner]++; tricksPlayed++; trick.Clear();
            if (tricks[winner] >= 7 || tricksPlayed >= 9) FinishDeal(); else CurrentPlayer = winner;
        }

        private int TrickWinner()
        {
            Suit led = trick[0].Item2.Suit;
            IEnumerable<Tuple<int, Card>> eligible = trick.Any(item => item.Item2.Suit == Suit.Spades)
                ? trick.Where(item => item.Item2.Suit == Suit.Spades)
                : trick.Where(item => item.Item2.Suit == led);
            return eligible.OrderByDescending(item => Strength(item.Item2)).First().Item1;
        }

        private void FinishDeal()
        {
            if (bidder >= 0)
            {
                if (tricks[bidder] >= 7) scores[bidder] += tricks[bidder];
                else
                {
                    scores[bidder] -= 7;
                    for (int player = 0; player < 3; player++) if (player != bidder) scores[player] += tricks[player];
                }
            }
            else
            {
                int sevenWinner = Enumerable.Range(0, 3).Where(player => tricks[player] >= 7)
                    .DefaultIfEmpty(-1).First();
                if (sevenWinner >= 0) scores[sevenWinner] += 5;
                else ScorePoker();
            }
            if (scores.Max() >= targetScore) finished = true; else StartDeal();
        }

        private void ScorePoker()
        {
            for (int player = 0; player < 3; player++)
            {
                revealedPokerHands[player].Clear();
                revealedPokerHands[player].AddRange(pokerHands[player]);
            }
            hasPokerShowdown = true;
            int[][] values = pokerHands.Select(PokerValue).ToArray();
            int[] winners = Enumerable.Range(0, 3).Where(player =>
                Enumerable.Range(0, 3).All(other => Compare(values[player], values[other]) >= 0)).ToArray();
            if (winners.Length == 3) return;
            foreach (int winner in winners) scores[winner] += tricks[winner] == 0 ? 3 : tricks[winner];
        }

        private static int[] PokerValue(List<Card> hand)
        {
            var groups = hand.GroupBy(card => Strength(card)).Select(group => new { Rank = group.Key, Count = group.Count() })
                .OrderByDescending(group => group.Count).ThenByDescending(group => group.Rank).ToArray();
            int category = groups[0].Count == 4 ? 5 : groups[0].Count == 3 && groups[1].Count == 2 ? 4 :
                groups[0].Count == 3 ? 3 : groups[0].Count == 2 && groups[1].Count == 2 ? 2 :
                groups[0].Count == 2 ? 1 : 0;
            return new[] { category }.Concat(groups.Select(group => group.Rank)).ToArray();
        }

        private static int Compare(IReadOnlyList<int> left, IReadOnlyList<int> right)
        {
            int count = Math.Max(left.Count, right.Count);
            for (int i = 0; i < count; i++)
            {
                int l = i < left.Count ? left[i] : 0; int r = i < right.Count ? right[i] : 0;
                if (l != r) return l.CompareTo(r);
            }
            return 0;
        }

        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (phase == "reserve_poker") return actions.OrderByDescending(action =>
                pokerHands[player].Count(card => card.Rank == action.Card.GetValueOrDefault().Rank) * 20 +
                Strength(action.Card.GetValueOrDefault())).First();
            if (phase == "bid")
            {
                int likely = hands[player].Count(card => card.Suit == Suit.Spades || Strength(card) >= 12);
                return actions.First(action => action.Kind == (likely >= 7 ? "colpo" : "pass"));
            }
            return actions.OrderByDescending(action => Strength(action.Card!.Value)).First();
        }

        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static Card Pop(List<Card> cards) { Card card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }

        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over.");
            int high = scores.Max();
            return new GameResult(Enumerable.Range(0, 3).Where(player => scores[player] == high),
                scores.Select(value => (double)value), "first to " + targetScore + " Colpo points", TurnCount);
        }

        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;
            string poker = pokerHands[viewer].Count + " reserved";
            string revealedPoker = hasPokerShowdown ? string.Join(" | ", Enumerable.Range(0, 3)
                .Select(index => "P" + index + ":" + string.Join(" ", revealedPokerHands[index]))) : "none";
            return $"phase={phase} dealer=P{dealer} bidder={(bidder < 0 ? "none" : "P" + bidder)} tricks_played={tricksPlayed}/9 " +
                $"trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] tricks=[{string.Join(",", tricks)}] " +
                $"scores=[{string.Join(",", scores)}] poker={poker} revealed_poker=[{revealedPoker}] " +
                $"hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }

        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("corpo", "コルポ", 3, 3, "trick-taking/poker",
                "4～Aの44枚から各14枚を受け、5枚を限定Poker用に伏せる。残る9枚でspade切り札戦を行い、Colpo宣言の7勝、無宣言7勝、またはstraight/flushなしのPokerを採点して15点を争う。",
                "gokurakism/Colpo", new Dictionary<string, string> { { "target_score", "15" } }),
            (players, random, options) => new CorpoGame(players, random, options));
    }
}
