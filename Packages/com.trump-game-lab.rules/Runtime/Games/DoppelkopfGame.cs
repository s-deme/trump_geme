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
            public int Owner { get; set; }
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
        private readonly List<Tuple<int,Tuple<int,DCard>[]>> completedTricks=new List<Tuple<int,Tuple<int,DCard>[]>>();
        private readonly HashSet<int> normalReTeam=new HashSet<int>();
        private readonly int[] announcedLevel=new int[2];
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
            trick.Clear();completedTricks.Clear();normalReTeam.Clear();choices.Clear(); povertyCards.Clear(); Array.Clear(tricks, 0, 4);Array.Clear(announcedLevel,0,2);
            var deck = new List<DCard>();
            for (int copy = 0; copy < 2; copy++)
                deck.AddRange(Cards.StandardDeck(new[] { 1, 9, 10, 11, 12, 13 }).Select(card => new DCard(card, copy)));
            rng.Shuffle(deck); dealer = (dealer + 1) % 4;
            for (int round = 0; round < 12; round++) for (int offset = 1; offset <= 4; offset++) hands[(dealer + offset) % 4].Add(Pop(deck));
            for(int player=0;player<4;player++)foreach(DCard card in hands[player])card.Owner=player;
            foreach(int player in Enumerable.Range(0,4).Where(player=>hands[player].Any(card=>card.Card==new Card(Suit.Clubs,12))))normalReTeam.Add(player);
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
            if(phase=="announce")
            {
                var actions=new List<Action>{new Action("decline_announcement")};
                if(contract!="marriage"||partner>=0)
                {
                    int side=TeamIndex(actual),level=announcedLevel[side];int cards=hands[actual].Count;
                    if(level==0&&cards>=(announcedLevel[1-side]>0?10:11))actions.Add(new Action(side==0?"announce_re":"announce_kontra"));
                    else if(level==1&&cards>=10)actions.Add(new Action("announce_no90"));
                    else if(level==2&&cards>=9)actions.Add(new Action("announce_no60"));
                    else if(level==3&&cards>=8)actions.Add(new Action("announce_no30"));
                    else if(level==4&&cards>=7)actions.Add(new Action("announce_schwarz"));
                }
                return actions;
            }

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
                phase = "announce"; CurrentPlayer = (dealer + 1) % 4; return;
            }
            if(phase=="announce")
            {if(action.Kind!="decline_announcement")announcedLevel[TeamIndex(player)]=action.Kind=="announce_re"||action.Kind=="announce_kontra"?1:action.Kind=="announce_no90"?2:action.Kind=="announce_no60"?3:action.Kind=="announce_no30"?4:5;phase="play";return;}
            DCard played = Find(hands[player], action.Value!); hands[player].Remove(played); trick.Add(Tuple.Create(player, played));
            if (trick.Count < 4) { CurrentPlayer = (player + 1) % 4;phase="announce"; return; }
            int winner = TrickWinner(); tricks[winner]++; captured[winner].AddRange(trick.Select(item => item.Item2));completedTricks.Add(Tuple.Create(winner,trick.ToArray())); trick.Clear();
            if (contract == "marriage" && partner < 0 && marriageSearchTricks < 3)
            {
                marriageSearchTricks++; if (winner != declarer) partner = winner;
            }
            if (tricks.Sum() >= 12) FinishDeal(); else{CurrentPlayer = winner;phase="announce";}
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
            if (contract == "marriage") { phase = "announce"; CurrentPlayer = (dealer + 1) % 4; return; }
            if (contract == "poverty")
            {
                povertyPartner = (declarer + 1) % 4; partner = povertyPartner; phase = "poverty_offer"; CurrentPlayer = declarer; return;
            }
            phase = "announce"; CurrentPlayer = (dealer + 1) % 4;
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
            HashSet<int> reTeam = ReTeam(); int rePoints = reTeam.Sum(player => captured[player].Sum(card => Points(card.Card)));int[] teamPoints={rePoints,240-rePoints};
            bool reWins=rePoints>=(announcedLevel[1]>0&&announcedLevel[0]==0?120:121);int winningSide=reWins?0:1;
            for(int side=0;side<2;side++)if(announcedLevel[side]>1&&!AnnouncementMade(side,teamPoints)){winningSide=1-side;break;}
            int losingPoints=teamPoints[1-winningSide];int actualTiers=(losingPoints<90?1:0)+(losingPoints<60?1:0)+(losingPoints<30?1:0)+(losingPoints==0?1:0);
            int declaredTiers=Math.Max(0,announcedLevel[0]-1)+Math.Max(0,announcedLevel[1]-1);int named=(announcedLevel[0]>0?2:0)+(announcedLevel[1]>0?2:0);
            int gamePoints=1+(winningSide==1?1:0)+named+Math.Max(actualTiers,Math.Max(announcedLevel[0]-1,announcedLevel[1]-1))+declaredTiers;
            int[] bonus=SpecialBonuses(reTeam);ApplyTeamScore(reTeam,winningSide==0?gamePoints:-gamePoints);ApplyTeamScore(reTeam,bonus[0]-bonus[1]);
            dealsPlayed++; if (dealsPlayed >= sessionDeals) finished = true; else StartDeal();
        }
        private bool AnnouncementMade(int side,int[] points)
        {int level=announcedLevel[side];if(level<=1)return true;int opponent=points[1-side];return level==2?opponent<90:level==3?opponent<60:level==4?opponent<30:opponent==0;}
        private int[] SpecialBonuses(HashSet<int> reTeam)
        {
            int[] bonus=new int[2];bool solo=contract=="solo"||contract.EndsWith("_solo",StringComparison.Ordinal);
            for(int index=0;index<completedTricks.Count;index++)
            {
                int winner=completedTricks[index].Item1;Tuple<int,DCard>[] cards=completedTricks[index].Item2;int side=reTeam.Contains(winner)?0:1;
                if(cards.All(item=>item.Item2.Card.Rank==1||item.Item2.Card.Rank==10))bonus[side]++;
                if(!solo)
                {
                    bonus[side]+=cards.Count(item=>item.Item2.Card==new Card(Suit.Diamonds,1)&&reTeam.Contains(item.Item2.Owner)!=(side==0));
                    if(index==completedTricks.Count-1)
                    {Tuple<int,DCard>? charlie=cards.FirstOrDefault(item=>item.Item2.Card==new Card(Suit.Clubs,11));if(charlie!=null){int ownerSide=reTeam.Contains(charlie.Item2.Owner)?0:1;if(ownerSide!=side||charlie.Item1==winner)bonus[side]++;}}
                }
            }
            return bonus;
        }
        private void ApplyTeamScore(HashSet<int> reTeam,int amount)
        {if(reTeam.Count==1){int solo=reTeam.Single();scores[solo]+=3*amount;for(int player=0;player<4;player++)if(player!=solo)scores[player]-=amount;}else for(int player=0;player<4;player++)scores[player]+=reTeam.Contains(player)?amount:-amount;}
        private HashSet<int> ReTeam()
        {
            if (contract == "solo" || contract == "queen_solo" || contract == "jack_solo") return new HashSet<int> { declarer };
            if (contract == "poverty") return new HashSet<int> { declarer, partner };
            if (contract == "marriage") { var team = new HashSet<int> { declarer }; if (partner >= 0) team.Add(partner); return team; }
            return new HashSet<int>(normalReTeam);
        }
        private int TeamIndex(int player)=>ReTeam().Contains(player)?0:1;
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
            if(phase=="announce")return actions[0];
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
                $"declarer={(declarer < 0 ? "-" : "P" + declarer)} partner={(partner < 0 ? "hidden" : "P" + partner)} your_role={knownRole} announcements=[{string.Join(",",announcedLevel)}] " +
                $"trick=[{string.Join(" ", trick.Select(item => "P" + item.Item1 + ":" + item.Item2))}] tricks=[{string.Join(",", tricks)}] scores=[{string.Join(",", scores)}] " +
                $"hand_counts=[{string.Join(",", hands.Select(hand => hand.Count))}]\nyour hand: {string.Join(" ", hands[viewer])}";
        }
        public static void Register(GameRegistry registry) => registry.Register(
            new GameInfo("doppelkopf", "ドッペルコップ", 4, 4, "hidden-team point-trick",
                "9～Aを2組使う48枚。club Q所持者をRe陣営とし、Marriage、Poverty、Solo、Re/Kontra～Schwarz宣言、Fox・Charlie・Doppelkopf bonusを含む8deal戦。",
                "gokurakism/Doppelkopf", new Dictionary<string, string> { { "deals", "8" } }),
            (players, random, options) => new DoppelkopfGame(players, random, options));
    }
}
