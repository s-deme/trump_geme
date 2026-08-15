using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TrumpLab.Games
{
    internal static class TrickClassicsGames
    {
        public static void RegisterGames(GameRegistry registry)
        {
            SpadesGame.Register(registry);
            OhHellGame.Register(registry);
            EuchreGame.Register(registry);
            PinochleGame.Register(registry);
        }
    }

    public sealed class PinochleGame : GameBase
    {
        private readonly DeterministicRandom rng;private readonly int targetScore;private readonly int[] teamScores=new int[2];private readonly int[] trickPoints=new int[2];private readonly int[] meldPoints=new int[4];
        private List<List<Card>> hands=new List<List<Card>>();private readonly List<Tuple<int,Card>> trick=new List<Tuple<int,Card>>();private readonly bool[] passed=new bool[4];
        private int dealer=3;private int highBid=19;private int bidder=-1;private Suit? trump;private string phase="bid";private bool finished;
        public override string GameId=>"pinochle";public override string Name=>"ピノクル";
        public PinochleGame(int players,DeterministicRandom rng,IReadOnlyDictionary<string,string> options)
        {Players=4;this.rng=rng;targetScore=options.Integer("target_score",150);StartHand();}
        private void StartHand()
        {
            dealer=(dealer+1)%4;List<Card> deck=Cards.Shuffled(Cards.StandardDeck(new[]{1,9,10,11,12,13},2),rng);hands=Enumerable.Range(0,4).Select(_=>new List<Card>()).ToList();
            for(int round=0;round<12;round++)for(int player=0;player<4;player++)hands[player].Add(Pop(deck));Array.Clear(passed,0,passed.Length);Array.Clear(trickPoints,0,trickPoints.Length);Array.Clear(meldPoints,0,meldPoints.Length);trick.Clear();
            highBid=19;bidder=-1;trump=null;phase="bid";CurrentPlayer=(dealer+1)%4;
        }
        private static int Strength(Card card)=>card.Rank==1?6:card.Rank==10?5:card.Rank==13?4:card.Rank==12?3:card.Rank==11?2:1;
        private static int Counter(Card card)=>card.Rank==1||card.Rank==10||card.Rank==13?1:0;
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player);
            if(phase=="bid")
            {
                var result=Enumerable.Range(highBid+1,Math.Max(0,60-highBid)).Select(value=>new Action("bid",value:value.ToString(CultureInfo.InvariantCulture))).ToList();
                if(bidder>=0||Enumerable.Range(0,4).Count(index=>!passed[index])>1)result.Insert(0,new Action("pass"));return result;
            }
            if(phase=="trump")return Enum.GetValues(typeof(Suit)).Cast<Suit>().Select(suit=>new Action("name_trump",value:Card.SuitCode(suit))).ToArray();
            IEnumerable<Card> cards=hands[actual];if(trick.Count>0)
            {
                Suit led=trick[0].Item2.Suit;Card[] follow=cards.Where(card=>card.Suit==led).ToArray();
                if(follow.Length>0)
                {
                    bool ledCurrentlyWinning=!trump.HasValue||!trick.Any(item=>item.Item2.Suit==trump.Value);int winning=trick.Where(item=>item.Item2.Suit==led).Max(item=>Strength(item.Item2));
                    Card[] crawl=ledCurrentlyWinning?follow.Where(card=>Strength(card)>winning).ToArray():Array.Empty<Card>();cards=crawl.Length>0?crawl:follow;
                }
                else if(trump.HasValue)
                {
                    Card[] trumps=cards.Where(card=>card.Suit==trump.Value).ToArray();if(trumps.Length>0)
                    {int winning=trick.Where(item=>item.Item2.Suit==trump.Value).Select(item=>Strength(item.Item2)).DefaultIfEmpty(0).Max();Card[] over=trumps.Where(card=>Strength(card)>winning).ToArray();cards=over.Length>0?over:trumps;}
                }
            }
            return cards.Select(card=>new Action("play",card)).ToArray();
        }
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));TurnCount++;
            if(phase=="bid")
            {
                if(action.Kind=="pass")passed[player]=true;else{highBid=int.Parse(action.Value!,CultureInfo.InvariantCulture);bidder=player;}
                if(bidder>=0&&Enumerable.Range(0,4).All(index=>index==bidder||passed[index])){phase="trump";CurrentPlayer=bidder;return;}
                CurrentPlayer=NextBidder(player);return;
            }
            if(phase=="trump")
            {trump=Card.ParseSuit(action.Value!);for(int index=0;index<4;index++)meldPoints[index]=MeldScore(hands[index],trump.Value);phase="play";CurrentPlayer=bidder;return;}
            Card card=action.Card!.Value;hands[player].Remove(card);trick.Add(Tuple.Create(player,card));if(trick.Count<4){CurrentPlayer=(player+1)%4;return;}
            Suit led=trick[0].Item2.Suit;IEnumerable<Tuple<int,Card>> eligible=trump.HasValue&&trick.Any(item=>item.Item2.Suit==trump.Value)?trick.Where(item=>item.Item2.Suit==trump.Value):trick.Where(item=>item.Item2.Suit==led);
            int winner=eligible.OrderByDescending(item=>Strength(item.Item2)).First().Item1;trickPoints[winner%2]+=trick.Sum(item=>Counter(item.Item2));bool last=hands.All(hand=>hand.Count==0);if(last)trickPoints[winner%2]++;
            trick.Clear();CurrentPlayer=winner;if(last)ScoreHand();
        }
        private int NextBidder(int player){for(int offset=1;offset<=4;offset++){int next=(player+offset)%4;if(!passed[next]&&next!=bidder)return next;}return bidder>=0?bidder:player;}
        private void ScoreHand()
        {
            int bidderTeam=bidder%2;int[] teamMeld={meldPoints[0]+meldPoints[2],meldPoints[1]+meldPoints[3]};int made=teamMeld[bidderTeam]+trickPoints[bidderTeam];
            if(made>=highBid)teamScores[bidderTeam]+=made;else teamScores[bidderTeam]-=highBid;int defenders=1-bidderTeam;teamScores[defenders]+=teamMeld[defenders]+trickPoints[defenders];
            if(teamScores.Max()>=targetScore)finished=true;else StartHand();
        }
        public static int MeldScore(IEnumerable<Card> cards,Suit trumpSuit)
        {
            Card[] hand=cards.ToArray();Func<Suit,int,int> count=(suit,rank)=>hand.Count(card=>card.Suit==suit&&card.Rank==rank);int score=0;
            int runs=new[]{1,10,13,12,11}.Min(rank=>count(trumpSuit,rank));if(runs>=2)score+=150;else if(runs==1)score+=15;
            foreach(Suit suit in Enum.GetValues(typeof(Suit)).Cast<Suit>()){int marriages=Math.Min(count(suit,13),count(suit,12));if(suit==trumpSuit)marriages=Math.Max(0,marriages-runs);score+=marriages*(suit==trumpSuit?4:2);}
            int pinochles=Math.Min(count(Suit.Spades,12),count(Suit.Diamonds,11));score+=pinochles>=2?30:pinochles*4;
            int[] ranks={1,13,12,11},single={10,8,6,4},doubles={100,80,60,40};for(int index=0;index<ranks.Length;index++)
            {int around=Enum.GetValues(typeof(Suit)).Cast<Suit>().Min(suit=>count(suit,ranks[index]));score+=around>=2?doubles[index]:around*single[index];}
            return score;
        }
        public override Action ChooseCpuAction(int player,DeterministicRandom random,int difficulty=1)
        {
            IReadOnlyList<Action> actions=LegalActions(player);
            if(phase=="bid")
            {int potential=Enum.GetValues(typeof(Suit)).Cast<Suit>().Max(suit=>MeldScore(hands[player],suit))+12;Action[] bidActions=actions.Where(action=>action.Kind=="bid"&&int.Parse(action.Value!,CultureInfo.InvariantCulture)<=potential).ToArray();return bidActions.Length>0?bidActions[bidActions.Length-1]:actions.First();}
            if(phase=="trump")
            {Suit best=Enum.GetValues(typeof(Suit)).Cast<Suit>().OrderByDescending(suit=>MeldScore(hands[player],suit)+hands[player].Count(card=>card.Suit==suit)).First();return actions.First(action=>action.Value==Card.SuitCode(best));}
            return actions.OrderBy(action=>Strength(action.Card!.Value)).First();
        }
        public override bool IsTerminal=>finished;
        public override GameResult Result(){if(!finished)throw new InvalidOperationException("Game is not over.");int high=teamScores.Max();return new GameResult(Enumerable.Range(0,4).Where(i=>teamScores[i%2]==high),Enumerable.Range(0,4).Select(i=>(double)teamScores[i%2]),"pinochle partnership score",TurnCount);}
        public override string View(int? player=null){int viewer=player??CurrentPlayer;return $"phase={phase} dealer={dealer} bid={highBid} bidder={bidder} trump={(trump.HasValue?Card.SuitCode(trump.Value):"-")} scores=[{string.Join(",",teamScores)}] melds=[{string.Join(",",meldPoints)}] counters=[{string.Join(",",trickPoints)}]\ntrick: {string.Join(" ",trick.Select(item=>item.Item2))}\nyour hand: {string.Join(" ",hands[viewer])}";}
        private static Card Pop(List<Card> cards){Card card=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return card;}
        public static void Register(GameRegistry registry)=>registry.Register(new GameInfo("pinochle","ピノクル",4,4,"meld-team-trick","48枚の固定ペア戦で競り、切り札メルド、マストフォロー・マストトランプを行い契約を判定する。","Pagat Single Deck Pinochle",new Dictionary<string,string>{{"target_score","勝利点（既定150）"}}),(p,r,o)=>new PinochleGame(p,r,o));
    }

    public sealed class EuchreGame : GameBase
    {
        private readonly DeterministicRandom rng;private readonly int targetScore;private readonly int[] teamScores=new int[2];
        private List<List<Card>> hands=new List<List<Card>>();private readonly List<Tuple<int,Card>> trick=new List<Tuple<int,Card>>();private readonly int[] tricks=new int[4];
        private int dealer=3;private int offers;private int auctionRound=1;private int maker=-1;private int inactive=-1;private Suit? trump;private Card upcard;private string phase="order";private bool alone;private bool finished;
        public override string GameId=>"euchre";public override string Name=>"ユーカー";
        public EuchreGame(int players,DeterministicRandom rng,IReadOnlyDictionary<string,string> options)
        {Players=4;this.rng=rng;targetScore=options.Integer("target_score",10);StartHand();}
        private void StartHand()
        {
            dealer=(dealer+1)%4;List<Card> deck=Cards.Shuffled(Cards.StandardDeck(new[]{1,9,10,11,12,13}),rng);hands=Enumerable.Range(0,4).Select(_=>new List<Card>()).ToList();
            for(int round=0;round<5;round++)for(int player=0;player<4;player++)hands[player].Add(Pop(deck));upcard=Pop(deck);
            Array.Clear(tricks,0,tricks.Length);trick.Clear();offers=0;auctionRound=1;maker=-1;inactive=-1;trump=null;alone=false;phase="order";CurrentPlayer=(dealer+1)%4;
        }
        private static bool SameColor(Suit left,Suit right)=>(left==Suit.Clubs||left==Suit.Spades)==(right==Suit.Clubs||right==Suit.Spades);
        private Suit EffectiveSuit(Card card)=>trump.HasValue&&card.Rank==11&&card.Suit!=trump.Value&&SameColor(card.Suit,trump.Value)?trump.Value:card.Suit;
        private int Strength(Card card)
        {if(trump.HasValue&&card.Rank==11&&card.Suit==trump.Value)return 100;if(trump.HasValue&&card.Rank==11&&card.Suit!=trump.Value&&SameColor(card.Suit,trump.Value))return 99;return card.Rank==1?14:card.Rank;}
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player);
            if(phase=="order")return new[]{new Action("pass"),new Action("order_up"),new Action("order_up_alone")};
            if(phase=="call")
            {
                var actions=Enum.GetValues(typeof(Suit)).Cast<Suit>().Where(suit=>suit!=upcard.Suit).SelectMany(suit=>new[]{
                    new Action("call_trump",value:Card.SuitCode(suit)),new Action("call_trump_alone",value:Card.SuitCode(suit))}).ToList();actions.Insert(0,new Action("pass"));return actions;
            }
            if(phase=="discard")return hands[actual].Select(card=>new Action("discard",card)).ToArray();
            IEnumerable<Card> cards=hands[actual];if(trick.Count>0){Suit led=EffectiveSuit(trick[0].Item2);Card[] follow=cards.Where(card=>EffectiveSuit(card)==led).ToArray();if(follow.Length>0)cards=follow;}return cards.Select(card=>new Action("play",card)).ToArray();
        }
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));TurnCount++;
            if(phase=="order")
            {
                if(action.Kind!="pass"){maker=player;trump=upcard.Suit;alone=action.Kind=="order_up_alone";hands[dealer].Add(upcard);phase="discard";CurrentPlayer=dealer;return;}
                offers++;if(offers==4){offers=0;auctionRound=2;phase="call";CurrentPlayer=(dealer+1)%4;}else CurrentPlayer=(player+1)%4;return;
            }
            if(phase=="call")
            {
                if(action.Kind!="pass"){maker=player;trump=Card.ParseSuit(action.Value!);alone=action.Kind=="call_trump_alone";BeginPlay();return;}
                offers++;if(offers==4)StartHand();else CurrentPlayer=(player+1)%4;return;
            }
            if(phase=="discard"){hands[player].Remove(action.Card!.Value);BeginPlay();return;}
            Card played=action.Card!.Value;hands[player].Remove(played);trick.Add(Tuple.Create(player,played));int participants=alone?3:4;
            if(trick.Count<participants){CurrentPlayer=NextActive(player);return;}Suit ledSuit=EffectiveSuit(trick[0].Item2);Suit trumpSuit=trump??throw new InvalidOperationException("Trump is not set.");IEnumerable<Tuple<int,Card>> eligible=trick.Any(item=>EffectiveSuit(item.Item2)==trumpSuit)?trick.Where(item=>EffectiveSuit(item.Item2)==trumpSuit):trick.Where(item=>EffectiveSuit(item.Item2)==ledSuit);
            int winner=eligible.OrderByDescending(item=>Strength(item.Item2)).First().Item1;tricks[winner]++;trick.Clear();CurrentPlayer=winner;if(hands.Where((hand,index)=>index!=inactive).All(hand=>hand.Count==0))ScoreHand();
        }
        private void BeginPlay(){inactive=alone?(maker+2)%4:-1;phase="play";CurrentPlayer=NextActive(dealer);}
        private int NextActive(int player){for(int offset=1;offset<=4;offset++){int next=(player+offset)%4;if(next!=inactive)return next;}return player;}
        private void ScoreHand()
        {
            int makerTeam=maker%2,won=tricks[makerTeam]+tricks[makerTeam+2];if(won>=3)teamScores[makerTeam]+=won==5?(alone?4:2):1;else teamScores[1-makerTeam]+=2;
            if(teamScores.Max()>=targetScore)finished=true;else StartHand();
        }
        public override Action ChooseCpuAction(int player,DeterministicRandom random,int difficulty=1)
        {
            IReadOnlyList<Action> actions=LegalActions(player);
            if(phase=="order")
            {int count=hands[player].Count(card=>card.Suit==upcard.Suit||card.Rank==11&&SameColor(card.Suit,upcard.Suit));if(count>=4)return actions.First(action=>action.Kind=="order_up_alone");return count>=3?actions.First(action=>action.Kind=="order_up"):actions[0];}
            if(phase=="call")
            {Suit best=Enum.GetValues(typeof(Suit)).Cast<Suit>().Where(suit=>suit!=upcard.Suit).OrderByDescending(suit=>hands[player].Count(card=>card.Suit==suit||card.Rank==11&&SameColor(card.Suit,suit))).First();int count=hands[player].Count(card=>card.Suit==best||card.Rank==11&&SameColor(card.Suit,best));if(count>=2||offers==3)return actions.First(action=>action.Kind=="call_trump"&&action.Value==Card.SuitCode(best));return actions[0];}
            if(phase=="discard")return actions.OrderBy(action=>Strength(action.Card!.Value)).First();return actions.OrderBy(action=>Strength(action.Card!.Value)).First();
        }
        public override bool IsTerminal=>finished;
        public override GameResult Result(){if(!finished)throw new InvalidOperationException("Game is not over.");int high=teamScores.Max();return new GameResult(Enumerable.Range(0,4).Where(i=>teamScores[i%2]==high),Enumerable.Range(0,4).Select(i=>(double)teamScores[i%2]),"euchre partnership score",TurnCount);}
        public override string View(int? player=null){int viewer=player??CurrentPlayer;return $"phase={phase} auction={auctionRound} dealer={dealer} upcard={upcard} trump={(trump.HasValue?Card.SuitCode(trump.Value):"-")} maker={maker} alone={alone} scores=[{string.Join(",",teamScores)}] tricks=[{string.Join(",",tricks)}]\ntrick: {string.Join(" ",trick.Select(item=>item.Item2))}\nyour hand: {string.Join(" ",hands[viewer])}";}
        private static Card Pop(List<Card> cards){Card card=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return card;}
        public static void Register(GameRegistry registry)=>registry.Register(new GameInfo("euchre","ユーカー",4,4,"team-trick","24枚・5枚手札で2段階に切り札を決め、左右のバウアーと単独プレイを含む5トリックを競う。","Bicycle Euchre",new Dictionary<string,string>{{"target_score","勝利点（既定10）"}}),(p,r,o)=>new EuchreGame(p,r,o));
    }

    public sealed class SpadesGame : GameBase
    {
        private readonly DeterministicRandom rng;private readonly int targetScore;private readonly int[] teamScores=new int[2];private readonly int[] bags=new int[2];
        private List<List<Card>> hands=new List<List<Card>>();private readonly List<Tuple<int,Card>> trick=new List<Tuple<int,Card>>();
        private readonly int[] bids=new int[4];private readonly int[] tricks=new int[4];private int dealer=3;private string phase="bid";private bool spadesBroken;private bool finished;
        public override string GameId=>"spades";public override string Name=>"スペード";
        public SpadesGame(int players,DeterministicRandom rng,IReadOnlyDictionary<string,string> options)
        {Players=4;this.rng=rng;targetScore=options.Integer("target_score",200);StartHand();}
        private void StartHand()
        {
            dealer=(dealer+1)%4;List<Card> deck=Cards.Shuffled(Cards.StandardDeck(),rng);hands=Enumerable.Range(0,4).Select(_=>new List<Card>()).ToList();
            for(int round=0;round<13;round++)for(int player=0;player<4;player++)hands[player].Add(Pop(deck));
            Array.Fill(bids,-1);Array.Clear(tricks,0,tricks.Length);trick.Clear();spadesBroken=false;phase="bid";CurrentPlayer=(dealer+1)%4;
        }
        private static int Strength(Card card)=>card.Rank==1?14:card.Rank;
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player);if(phase=="bid")return Enumerable.Range(1,13).Select(value=>new Action("bid",value:value.ToString(CultureInfo.InvariantCulture))).ToArray();
            IEnumerable<Card> cards=hands[actual];if(trick.Count>0){Suit led=trick[0].Item2.Suit;Card[] follow=cards.Where(card=>card.Suit==led).ToArray();if(follow.Length>0)cards=follow;}
            else if(!spadesBroken){Card[] plain=cards.Where(card=>card.Suit!=Suit.Spades).ToArray();if(plain.Length>0)cards=plain;}
            return cards.Select(card=>new Action("play",card)).ToArray();
        }
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));TurnCount++;
            if(phase=="bid")
            {bids[player]=int.Parse(action.Value!,CultureInfo.InvariantCulture);int next=Next(player,index=>bids[index]<0);if(next>=0){CurrentPlayer=next;return;}phase="play";CurrentPlayer=(dealer+1)%4;return;}
            Card card=action.Card!.Value;hands[player].Remove(card);trick.Add(Tuple.Create(player,card));if(card.Suit==Suit.Spades)spadesBroken=true;
            if(trick.Count<4){CurrentPlayer=(player+1)%4;return;}Suit led=trick[0].Item2.Suit;Tuple<int,Card> winner=trick.Where(item=>item.Item2.Suit==Suit.Spades).DefaultIfEmpty()
                .Any(item=>item!=null)?trick.Where(item=>item.Item2.Suit==Suit.Spades).OrderByDescending(item=>Strength(item.Item2)).First():trick.Where(item=>item.Item2.Suit==led).OrderByDescending(item=>Strength(item.Item2)).First();
            tricks[winner.Item1]++;trick.Clear();CurrentPlayer=winner.Item1;if(hands.All(hand=>hand.Count==0))ScoreHand();
        }
        private void ScoreHand()
        {
            for(int team=0;team<2;team++)
            {int contract=bids[team]+bids[team+2],won=tricks[team]+tricks[team+2];if(won>=contract){int over=won-contract;teamScores[team]+=10*contract+over;bags[team]+=over;if(bags[team]>=10){teamScores[team]-=100;bags[team]-=10;}}}
            if(teamScores.Max()>=targetScore)finished=true;else StartHand();
        }
        private int Next(int player,Func<int,bool> predicate){for(int offset=1;offset<=4;offset++){int next=(player+offset)%4;if(predicate(next))return next;}return -1;}
        public override Action ChooseCpuAction(int player,DeterministicRandom random,int difficulty=1)
        {
            IReadOnlyList<Action> actions=LegalActions(player);if(phase=="bid")
            {int estimate=Math.Max(1,hands[player].Count(card=>Strength(card)>=12)+hands[player].Count(card=>card.Suit==Suit.Spades&&Strength(card)>=9)/2);return actions[Math.Min(12,estimate)-1];}
            if(trick.Count==0)return actions.OrderBy(action=>Strength(action.Card!.Value)).First();Suit led=trick[0].Item2.Suit;int current=trick.Where(item=>item.Item2.Suit==Suit.Spades).Any()?trick.Where(item=>item.Item2.Suit==Suit.Spades).Max(item=>Strength(item.Item2)):trick.Where(item=>item.Item2.Suit==led).Max(item=>Strength(item.Item2));
            Action[] winning=actions.Where(action=>(action.Card!.Value.Suit==Suit.Spades||action.Card.Value.Suit==led)&&Strength(action.Card.Value)>current).OrderBy(action=>Strength(action.Card!.Value)).ToArray();
            return winning.Length>0?winning[0]:actions.OrderBy(action=>Strength(action.Card!.Value)).First();
        }
        public override bool IsTerminal=>finished;
        public override GameResult Result(){if(!finished)throw new InvalidOperationException("Game is not over.");int best=teamScores.Max();return new GameResult(Enumerable.Range(0,4).Where(i=>teamScores[i%2]==best),Enumerable.Range(0,4).Select(i=>(double)teamScores[i%2]),"partnership score",TurnCount,new Dictionary<string,object>{{"team_scores",teamScores.ToArray()},{"bags",bags.ToArray()}});}
        public override string View(int? player=null){int viewer=player??CurrentPlayer;return $"phase={phase} dealer={dealer} scores=[{string.Join(",",teamScores)}] bags=[{string.Join(",",bags)}] bids=[{string.Join(",",bids)}] tricks=[{string.Join(",",tricks)}] broken={spadesBroken}\ntrick: {string.Join(" ",trick.Select(item=>item.Item2))}\nyour hand: {string.Join(" ",hands[viewer])}";}
        private static Card Pop(List<Card> cards){Card card=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return card;}
        public static void Register(GameRegistry registry)=>registry.Register(new GameInfo("spades","スペード",4,4,"team-exact-trick","固定ペアでビッドし、スペード固定切り札の13トリックを行い、契約点とバッグを記録する。","Bicycle Spades",new Dictionary<string,string>{{"target_score","勝利点（既定200）"}}),(p,r,o)=>new SpadesGame(p,r,o));
    }

    public sealed class OhHellGame : GameBase
    {
        private readonly DeterministicRandom rng;private readonly int[] scores;private readonly int[] bids;private readonly int[] tricks;private readonly int[] handSizes;
        private List<List<Card>> hands=new List<List<Card>>();private readonly List<Tuple<int,Card>> trick=new List<Tuple<int,Card>>();private int dealer=-1;private int handIndex=-1;private Suit? trump;private string phase="bid";private bool finished;
        public override string GameId=>"oh_hell";public override string Name=>"オーヘル";
        public OhHellGame(int players,DeterministicRandom rng)
        {
            Players=players;this.rng=rng;scores=new int[players];bids=new int[players];tricks=new int[players];int maximum=Math.Min(10,51/players);
            handSizes=Enumerable.Range(1,maximum).Reverse().Concat(Enumerable.Range(2,maximum-1)).ToArray();StartHand();
        }
        private void StartHand()
        {
            handIndex++;if(handIndex>=handSizes.Length){finished=true;return;}dealer=(dealer+1)%Players;int size=handSizes[handIndex];List<Card> deck=Cards.Shuffled(Cards.StandardDeck(),rng);
            hands=Enumerable.Range(0,Players).Select(_=>new List<Card>()).ToList();for(int round=0;round<size;round++)for(int player=0;player<Players;player++)hands[player].Add(Pop(deck));
            trump=deck.Count>0?Pop(deck).Suit:(Suit?)null;Array.Fill(bids,-1);Array.Clear(tricks,0,tricks.Length);trick.Clear();phase="bid";CurrentPlayer=(dealer+1)%Players;
        }
        private static int Strength(Card card)=>card.Rank==1?14:card.Rank;
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player);if(phase=="bid")
            {IEnumerable<int> values=Enumerable.Range(0,handSizes[handIndex]+1);if(actual==dealer&&bids.Count(value=>value>=0)==Players-1){int forbidden=handSizes[handIndex]-bids.Where(value=>value>=0).Sum();values=values.Where(value=>value!=forbidden);}return values.Select(value=>new Action("bid",value:value.ToString(CultureInfo.InvariantCulture))).ToArray();}
            IEnumerable<Card> cards=hands[actual];if(trick.Count>0){Suit led=trick[0].Item2.Suit;Card[] follow=cards.Where(card=>card.Suit==led).ToArray();if(follow.Length>0)cards=follow;}return cards.Select(card=>new Action("play",card)).ToArray();
        }
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));TurnCount++;
            if(phase=="bid"){bids[player]=int.Parse(action.Value!,CultureInfo.InvariantCulture);int next=Next(player,index=>bids[index]<0);if(next>=0){CurrentPlayer=next;return;}phase="play";CurrentPlayer=(dealer+1)%Players;return;}
            Card card=action.Card!.Value;hands[player].Remove(card);trick.Add(Tuple.Create(player,card));if(trick.Count<Players){CurrentPlayer=(player+1)%Players;return;}
            Suit led=trick[0].Item2.Suit;IEnumerable<Tuple<int,Card>> eligible=trump.HasValue&&trick.Any(item=>item.Item2.Suit==trump.Value)?trick.Where(item=>item.Item2.Suit==trump.Value):trick.Where(item=>item.Item2.Suit==led);
            int winner=eligible.OrderByDescending(item=>Strength(item.Item2)).First().Item1;tricks[winner]++;trick.Clear();CurrentPlayer=winner;if(hands.All(hand=>hand.Count==0))ScoreHand();
        }
        private void ScoreHand(){for(int player=0;player<Players;player++)scores[player]+=tricks[player]+(tricks[player]==bids[player]?10:0);StartHand();}
        private int Next(int player,Func<int,bool> predicate){for(int offset=1;offset<=Players;offset++){int next=(player+offset)%Players;if(predicate(next))return next;}return -1;}
        public override Action ChooseCpuAction(int player,DeterministicRandom random,int difficulty=1)
        {
            IReadOnlyList<Action> actions=LegalActions(player);if(phase=="bid")
            {int estimate=hands[player].Count(card=>Strength(card)>=13||trump.HasValue&&card.Suit==trump.Value&&Strength(card)>=11);return actions.OrderBy(action=>Math.Abs(int.Parse(action.Value!,CultureInfo.InvariantCulture)-estimate)).First();}
            if(trick.Count==0)return actions.OrderByDescending(action=>Strength(action.Card!.Value)).First();return actions.OrderBy(action=>Strength(action.Card!.Value)).First();
        }
        public override bool IsTerminal=>finished;
        public override GameResult Result(){if(!finished)throw new InvalidOperationException("Game is not over.");int high=scores.Max();return new GameResult(Enumerable.Range(0,Players).Where(i=>scores[i]==high),scores.Select(v=>(double)v),"exact bid series",TurnCount);}
        public override string View(int? player=null){int viewer=player??CurrentPlayer;return $"hand={handIndex+1}/{handSizes.Length} size={handSizes[Math.Min(handIndex,handSizes.Length-1)]} phase={phase} dealer={dealer} trump={(trump.HasValue?Card.SuitCode(trump.Value):"-")} scores=[{string.Join(",",scores)}] bids=[{string.Join(",",bids)}] tricks=[{string.Join(",",tricks)}]\ntrick: {string.Join(" ",trick.Select(item=>item.Item2))}\nyour hand: {string.Join(" ",hands[viewer])}";}
        private static Card Pop(List<Card> cards){Card card=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return card;}
        public static void Register(GameRegistry registry)=>registry.Register(new GameInfo("oh_hell","オーヘル",3,7,"exact-bid","手札枚数を降順・昇順に変え、ディーラーのフック付きビッドと10点の的中ボーナスで競う。","Pagat Oh Hell"),(p,r,o)=>new OhHellGame(p,r));
    }
}
