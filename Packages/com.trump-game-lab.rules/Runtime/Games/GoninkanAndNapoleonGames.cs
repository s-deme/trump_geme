using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    internal static class GoninkanAndNapoleonGames
    {
        public static void RegisterGames(GameRegistry registry)
        {
            GoninkanGame.Register(registry);
            NapoleonGame.Register(registry);
        }
    }

    public sealed class GoninkanGame : GameBase
    {
        private sealed class GCard
        {
            public Card? Card { get; }
            public bool Joker => !Card.HasValue;
            public string Id => Joker ? "JOKER" : Card!.Value.ToString();
            public GCard(Card? card) { Card = card; }
            public override string ToString() => Id;
        }
        private readonly DeterministicRandom rng;
        private readonly List<List<GCard>> hands = Enumerable.Range(0, 5).Select(_ => new List<GCard>()).ToList();
        private readonly List<List<GCard>> captured = Enumerable.Range(0, 5).Select(_ => new List<GCard>()).ToList();
        private readonly List<Tuple<int, GCard>> trick = new List<Tuple<int, GCard>>();
        private readonly int[] scores = new int[5];
        private readonly int[] roundDelta=new int[5];
        private readonly HashSet<int> relationship = new HashSet<int>();
        private readonly List<int> playOrder=Enumerable.Range(0,5).ToList();
        private readonly List<GCard> shownTrumpCards=new List<GCard>();
        private int round;
        private int match = 1;
        private int trickNumber;
        private int doubleHolder=-1;
        private int trumpChooser=-1;
        private string specialContract="";
        private Suit trump;
        private Suit? jokerLeadSuit;
        private string phase = "play";
        private bool finished;
        public override string GameId => "goninkan";
        public override string Name => "ゴニンカン";
        public GoninkanGame(int players, DeterministicRandom rng) { Players = 5; this.rng = rng; StartRound(); }
        private void StartRound()
        {
            match = 1;trump = round == 9 ? Suit.Spades : (Suit)(round % 3);Array.Clear(roundDelta,0,5);DealCards(true);
        }
        private void DealCards(bool determineTeam)
        {
            foreach (List<GCard> pile in hands) pile.Clear(); foreach (List<GCard> pile in captured) pile.Clear(); trick.Clear();
            var deck = Cards.StandardDeck().Where(card => card.Rank != 2 || card.Suit == Suit.Spades).Select(card => new GCard(card)).ToList(); deck.Add(new GCard(null)); rng.Shuffle(deck);
            for (int card = 0; card < 10; card++) for (int player = 0; player < 5; player++) hands[player].Add(Pop(deck));
            if (determineTeam)
            {
                relationship.Clear(); int jokerHolder = Enumerable.Range(0, 5).Single(player => hands[player].Any(card => card.Joker));
                int aceHolder = Enumerable.Range(0, 5).Single(player => hands[player].Any(card => card.Card == new Card(trump, 1)));
                relationship.Add(jokerHolder);doubleHolder=aceHolder==jokerHolder?jokerHolder:-1;relationship.Add(aceHolder == jokerHolder ? (jokerHolder + 2) % 5 : aceHolder);ArrangeSeats();
            }
            trickNumber = 0; jokerLeadSuit = null;specialContract="";shownTrumpCards.Clear();
            if(determineTeam&&doubleHolder>=0){phase="double_relation_exchange";CurrentPlayer=relationship.Single(player=>player!=doubleHolder);}
            else BeginPlay(relationship.First());
        }
        private void ArrangeSeats()
        {playOrder.Clear();int first=relationship.First();int second=relationship.Single(player=>player!=first);int[] others=Enumerable.Range(0,5).Where(player=>!relationship.Contains(player)).ToArray();playOrder.Add(first);playOrder.Add(others[0]);playOrder.Add(second);playOrder.Add(others[1]);playOrder.Add(others[2]);}
        private int Next(int player){int index=playOrder.IndexOf(player);return playOrder[(index+1)%5];}
        private void BeginPlay(int leader){phase="play";CurrentPlayer=leader;}
        public override IReadOnlyList<Action> LegalActions(int? player = null)
        {
            int actual = ValidateTurn(player);
            if(phase=="double_relation_exchange")return hands[actual].Select(card=>new Action("exchange_double_relation",card.Card,value:card.Id)).ToArray();
            if(phase=="show_trump_cards")
            {var showActions=new List<Action>();for(int left=0;left<hands[actual].Count-1;left++)for(int right=left+1;right<hands[actual].Count;right++)showActions.Add(new Action("show_two",value:left+","+right));return showActions;}
            if (phase == "choose_trump") return Enum.GetValues(typeof(Suit)).Cast<Suit>().Select(suit => new Action("choose_trump", value: Card.SuitCode(suit))).ToArray();
            if(phase=="special_offer")return relationship.Contains(actual)?new[]{new Action("stop_sukonku"),new Action("declare_juuroku")}:new[]{new Action("stop_gyaku_sukonku"),new Action("declare_gyaku_juuroku")};
            IEnumerable<GCard> cards = hands[actual];
            if (trick.Count > 0)
            {
                Suit? led = jokerLeadSuit ?? trick.First(item => !item.Item2.Joker).Item2.Card!.Value.Suit;
                GCard[] follow = cards.Where(card => !card.Joker && card.Card!.Value.Suit == led.Value).ToArray(); if (follow.Length > 0) cards = follow.Concat(cards.Where(card => card.Joker));
            }
            if ((trickNumber == 0 || trickNumber == 9) && cards.Count() > 1) cards = cards.Where(card => !card.Joker);
            var actions = new List<Action>();
            foreach (GCard card in cards)
            {
                if (trick.Count == 0 && card.Joker && trickNumber > 0 && trickNumber < 9)
                    foreach (Suit suit in Enum.GetValues(typeof(Suit))) actions.Add(new Action("lead_joker", value: Card.SuitCode(suit)));
                else actions.Add(new Action("play", card.Card, value: card.Id));
            }
            return actions;
        }
        public override void Apply(Action action)
        {
            int player = ValidateTurn(null); Guard.Legal(action, LegalActions(player)); TurnCount++;
            if(phase=="double_relation_exchange")
            {GCard offered=hands[player].Single(card=>card.Id==action.Value);GCard special=rng.Choice(hands[doubleHolder].Where(card=>card.Joker||card.Card==new Card(trump,1)).ToArray());hands[player].Remove(offered);hands[doubleHolder].Remove(special);hands[player].Add(special);hands[doubleHolder].Add(offered);BeginPlay(relationship.First());return;}
            if(phase=="show_trump_cards")
            {int[] indexes=action.Value!.Split(',').Select(int.Parse).ToArray();shownTrumpCards.Add(hands[player][indexes[0]]);shownTrumpCards.Add(hands[player][indexes[1]]);phase="choose_trump";CurrentPlayer=trumpChooser;return;}
            if (phase == "choose_trump") { trump = Card.ParseSuit(action.Value!); BeginPlay(trumpChooser); return; }
            if(phase=="special_offer")
            {if(action.Kind.StartsWith("stop_",StringComparison.Ordinal)){FinishMatch(relationship.Contains(player),true);return;}specialContract=action.Kind=="declare_juuroku"?"juuroku":"gyaku_juuroku";BeginPlay(player);return;}
            GCard card = action.Kind == "lead_joker" ? hands[player].Single(item => item.Joker) : hands[player].Single(item => item.Id == action.Value);
            hands[player].Remove(card);
            if (action.Kind == "lead_joker") jokerLeadSuit = Card.ParseSuit(action.Value!);
            else if (trick.Count == 0 && card.Joker) jokerLeadSuit = trump;
            trick.Add(Tuple.Create(player, card));
            if (trick.Count < 5) { CurrentPlayer = Next(player); return; }
            int winner = TrickWinner(); captured[winner].AddRange(trick.Select(item => item.Item2)); trick.Clear(); jokerLeadSuit = null; trickNumber++;
            CheckMatchBoundary(winner);
        }
        private int TrickWinner()
        {
            Tuple<int, GCard>? joker = trick.FirstOrDefault(item => item.Item2.Joker); if (joker != null) return joker.Item1;
            Suit led = jokerLeadSuit ?? trick[0].Item2.Card!.Value.Suit; IEnumerable<Tuple<int, GCard>> eligible = trick.Any(item => item.Item2.Card!.Value.Suit == trump)
                ? trick.Where(item => item.Item2.Card!.Value.Suit == trump) : trick.Where(item => item.Item2.Card!.Value.Suit == led);
            return eligible.OrderByDescending(item => Strength(item.Item2.Card!.Value)).First().Item1;
        }
        private void CheckMatchBoundary(int winner)
        {
            int honors = relationship.Sum(player => captured[player].Count(card => card.Card.HasValue && (card.Card.Value.Rank == 1 || card.Card.Value.Rank >= 11)));
            if(!string.IsNullOrEmpty(specialContract)){if(trickNumber>=10)FinishSpecial(honors);else BeginPlay(winner);return;}
            int opponentHonors=Enumerable.Range(0,5).Where(player=>!relationship.Contains(player)).Sum(player=>captured[player].Count(card=>card.Card.HasValue&&(card.Card.Value.Rank==1||card.Card.Value.Rank>=11)));
            int target=match==2?8:9,opponentTarget=17-target;bool? success=honors>=target?true:opponentHonors>=opponentTarget?false:(bool?)null;
            if(!success.HasValue&&trickNumber<10){BeginPlay(winner);return;}bool won=success??honors>=target;bool early=trickNumber<10;
            if(early&&match>=2){phase="special_offer";CurrentPlayer=winner;return;}FinishMatch(won,early);
        }
        private void FinishSpecial(int honors)
        {
            bool success=honors>=(match==2?8:9);bool all=specialContract=="juuroku"?honors==16:honors==0;
            if(specialContract=="juuroku"){AddRound(success);AddTeamPoints(all?8:-16);if(success&&match<3&&!all){PrepareNextMatch();return;}}
            else if(all){Array.Clear(roundDelta,0,5);AddTeamPoints(-16);EndRound();return;}
            else{AddRound(success);AddTeamPoints(32);}
            if(success&&match<3){PrepareNextMatch();return;}if(success&&match==3)AddTeamPoints(1);EndRound();
        }
        private void FinishMatch(bool success,bool sukonku)
        {
            if(!success&&sukonku)
            {if(match==1){Array.Clear(roundDelta,0,5);AddTeamPoints(-10);}else{Array.Clear(roundDelta,0,5);AddTeamPoints(-3);}EndRound();return;}
            AddRound(success);if(success&&sukonku)AddTeamPoints(1);
            if(success&&match<3){PrepareNextMatch();return;}if(success&&match==3)AddTeamPoints(1);EndRound();
        }
        private void AddRound(bool relationshipWon)=>AddTeamPoints(relationshipWon?1:-1);
        private void AddTeamPoints(int relationValue){for(int player=0;player<5;player++)roundDelta[player]+=relationship.Contains(player)?relationValue:-relationValue;}
        private void PrepareNextMatch(){match++;DealCards(false);trumpChooser=relationship.First();int shower=relationship.Single(player=>player!=trumpChooser);phase="show_trump_cards";CurrentPlayer=shower;}
        private void EndRound(){for(int player=0;player<5;player++)scores[player]+=roundDelta[player];round++;if(round>=10)finished=true;else StartRound();}
        public override Action ChooseCpuAction(int player, DeterministicRandom random, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player); if(phase=="double_relation_exchange"||phase=="show_trump_cards")return actions[0];if (phase == "choose_trump") return actions[round % 4];if(phase=="special_offer")return actions[0];
            return actions.OrderBy(action => action.Card.HasValue ? Strength(action.Card.Value) : 15).First();
        }
        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static GCard Pop(List<GCard> cards) { GCard card = cards[cards.Count - 1]; cards.RemoveAt(cards.Count - 1); return card; }
        public override bool IsTerminal => finished;
        public override GameResult Result()
        {
            if (!finished) throw new InvalidOperationException("Game is not over."); int high = scores.Max(); return new GameResult(Enumerable.Range(0, 5).Where(player => scores[player] == high), scores.Select(value => (double)value), "ten Goninkan rounds", TurnCount);
        }
        public override string View(int? player = null)
        {
            int viewer = player ?? CurrentPlayer;string shown=relationship.Contains(viewer)?string.Join(" ",shownTrumpCards):shownTrumpCards.Count==0?"-":"hidden"; return $"phase={phase} round={round + 1}/10 match={match}/3 trump={Card.SuitCode(trump)} kankei=[{string.Join(",", relationship.Select(p => "P" + p))}] order=[{string.Join(",",playOrder.Select(p=>"P"+p))}] shown=[{shown}] special={(specialContract==""?"-":specialContract)} " +
                $"honors=[{string.Join(",", captured.Select(pile => pile.Count(card => card.Card.HasValue && (card.Card.Value.Rank == 1 || card.Card.Value.Rank >= 11))))}] scores=[{string.Join(",", scores)}] table=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("goninkan", "ゴニンカン", 5, 5, "two-versus-three honor-trick", "49枚＋Jokerの公式5人版。隣接関係の席順正規化、二重関の伏せ札交換、2枚提示によるtrump選択、スコンク・じゅうろく・逆じゅうろくと取消しを含む公式配点で10roundを競う。", "Goshogawara Goninkan official rules / Pagat Kan"),
            (players, random, options) => new GoninkanGame(players, random));
    }
}
