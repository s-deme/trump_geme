using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TrumpLab.Games
{
    internal static class PokerAndBankingGames
    {
        public static void RegisterGames(GameRegistry registry)
        {
            TexasHoldemGame.Register(registry);
            FiveCardDrawGame.Register(registry);
            BaccaratGame.Register(registry);
        }
    }

    public readonly struct PokerRank : IComparable<PokerRank>
    {
        public int Category { get; }
        public IReadOnlyList<int> Tiebreakers { get; }
        public string Label { get; }
        public PokerRank(int category,IEnumerable<int> tiebreakers,string label)
        {Category=category;Tiebreakers=tiebreakers.ToArray();Label=label;}
        public int CompareTo(PokerRank other)
        {
            int category=Category.CompareTo(other.Category);if(category!=0)return category;
            int length=Math.Max(Tiebreakers.Count,other.Tiebreakers.Count);
            for(int index=0;index<length;index++)
            {int left=index<Tiebreakers.Count?Tiebreakers[index]:0,right=index<other.Tiebreakers.Count?other.Tiebreakers[index]:0;
             int value=left.CompareTo(right);if(value!=0)return value;}
            return 0;
        }
        public override string ToString()=>$"{Label}({string.Join(",",Tiebreakers)})";
    }

    public static class PokerHandEvaluator
    {
        private static int Strength(Card card)=>card.Rank==1?14:card.Rank;
        public static PokerRank EvaluateBest(IEnumerable<Card> cards)
        {
            Card[] values=cards.ToArray();if(values.Length<5)throw new ArgumentException("At least five cards are required.");PokerRank? best=null;
            for(int a=0;a<values.Length-4;a++)for(int b=a+1;b<values.Length-3;b++)for(int c=b+1;c<values.Length-2;c++)
            for(int d=c+1;d<values.Length-1;d++)for(int e=d+1;e<values.Length;e++)
            {PokerRank rank=EvaluateFive(new[]{values[a],values[b],values[c],values[d],values[e]});if(!best.HasValue||rank.CompareTo(best.Value)>0)best=rank;}
            return best!.Value;
        }
        public static PokerRank EvaluateFive(IEnumerable<Card> cards)
        {
            Card[] hand=cards.ToArray();if(hand.Length!=5)throw new ArgumentException("Exactly five cards are required.");
            int[] ranks=hand.Select(Strength).OrderByDescending(value=>value).ToArray();
            var groups=ranks.GroupBy(value=>value).OrderByDescending(group=>group.Count()).ThenByDescending(group=>group.Key).ToArray();
            bool flush=hand.Select(card=>card.Suit).Distinct().Count()==1;int[] distinct=ranks.Distinct().ToArray();int straightHigh=0;
            if(distinct.Length==5&&distinct[0]-distinct[4]==4)straightHigh=distinct[0];else if(distinct.SequenceEqual(new[]{14,5,4,3,2}))straightHigh=5;
            if(flush&&straightHigh>0)return new PokerRank(8,new[]{straightHigh},"straight-flush");
            if(groups[0].Count()==4)return new PokerRank(7,new[]{groups[0].Key,groups[1].Key},"four-kind");
            if(groups[0].Count()==3&&groups[1].Count()==2)return new PokerRank(6,new[]{groups[0].Key,groups[1].Key},"full-house");
            if(flush)return new PokerRank(5,ranks,"flush");if(straightHigh>0)return new PokerRank(4,new[]{straightHigh},"straight");
            if(groups[0].Count()==3)return new PokerRank(3,new[]{groups[0].Key}.Concat(groups.Skip(1).Select(g=>g.Key).OrderByDescending(v=>v)),"three-kind");
            if(groups[0].Count()==2&&groups[1].Count()==2)
            {int high=Math.Max(groups[0].Key,groups[1].Key),low=Math.Min(groups[0].Key,groups[1].Key);return new PokerRank(2,new[]{high,low,groups[2].Key},"two-pair");}
            if(groups[0].Count()==2)return new PokerRank(1,new[]{groups[0].Key}.Concat(groups.Skip(1).Select(g=>g.Key).OrderByDescending(v=>v)),"pair");
            return new PokerRank(0,ranks,"high-card");
        }
    }

    public sealed class TexasHoldemGame : GameBase
    {
        private readonly List<List<Card>> hands;private readonly List<Card> deck;private readonly List<Card> board=new List<Card>();
        private readonly bool[] folded;private readonly bool[] acted;private readonly int[] stacks;private readonly int[] streetContributions;
        private string street="preflop";private int currentBet;private int raises;private int pot;private bool finished;
        public override string GameId=>"texas_holdem";public override string Name=>"テキサスホールデム";
        public TexasHoldemGame(int players,DeterministicRandom rng,IReadOnlyDictionary<string,string> options)
        {
            Players=players;deck=Cards.Shuffled(Cards.StandardDeck(),rng);hands=Enumerable.Range(0,players).Select(_=>new List<Card>()).ToList();
            for(int round=0;round<2;round++)for(int player=0;player<players;player++)hands[player].Add(Pop(deck));
            folded=new bool[players];acted=new bool[players];stacks=Enumerable.Repeat(options.Integer("starting_stack",20),players).ToArray();streetContributions=new int[players];
            int small=players==2?0:1,big=players==2?1:2;PostBlind(small,1);PostBlind(big,2);currentBet=2;CurrentPlayer=players==2?0:(big+1)%players;
        }
        private void PostBlind(int player,int amount){int paid=Math.Min(amount,stacks[player]);stacks[player]-=paid;streetContributions[player]+=paid;pot+=paid;}
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player);var result=new List<Action>();int owed=currentBet-streetContributions[actual];
            if(owed==0)result.Add(new Action("check"));else{result.Add(new Action("fold"));result.Add(new Action("call"));}
            int limit=street=="turn"||street=="river"?4:2;if(raises<3&&stacks[actual]>owed)result.Add(new Action("raise",value:limit.ToString(CultureInfo.InvariantCulture)));return result;
        }
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));TurnCount++;int owed=currentBet-streetContributions[player];
            if(action.Kind=="fold")folded[player]=true;else if(action.Kind=="call")Pay(player,owed);else if(action.Kind=="raise")
            {Pay(player,owed);Pay(player,int.Parse(action.Value!,CultureInfo.InvariantCulture));currentBet=streetContributions[player];raises++;for(int i=0;i<Players;i++)if(!folded[i])acted[i]=false;}
            acted[player]=true;Advance(player);
        }
        private void Pay(int player,int amount){int paid=Math.Min(amount,stacks[player]);stacks[player]-=paid;streetContributions[player]+=paid;pot+=paid;}
        private void Advance(int player)
        {
            int[] active=Enumerable.Range(0,Players).Where(i=>!folded[i]).ToArray();if(active.Length==1){stacks[active[0]]+=pot;pot=0;finished=true;return;}
            int next=FindNext(player,i=>!folded[i]&&stacks[i]>0&&(!acted[i]||streetContributions[i]<currentBet));if(next>=0){CurrentPlayer=next;return;}
            if(street=="river"){Showdown();return;}street=street=="preflop"?"flop":street=="flop"?"turn":"river";
            if(street=="flop"){Pop(deck);for(int i=0;i<3;i++)board.Add(Pop(deck));}else{Pop(deck);board.Add(Pop(deck));}
            Array.Clear(acted,0,acted.Length);Array.Clear(streetContributions,0,streetContributions.Length);currentBet=0;raises=0;
            next=FindNext(0,i=>!folded[i]&&stacks[i]>0);if(next<0)Showdown();else CurrentPlayer=next;
        }
        private int FindNext(int player,Func<int,bool> predicate){for(int offset=1;offset<=Players;offset++){int next=(player+offset)%Players;if(predicate(next))return next;}return -1;}
        private void Showdown()
        {
            int[] active=Enumerable.Range(0,Players).Where(i=>!folded[i]).ToArray();PokerRank best=active.Select(i=>PokerHandEvaluator.EvaluateBest(hands[i].Concat(board))).Max();
            int[] winners=active.Where(i=>PokerHandEvaluator.EvaluateBest(hands[i].Concat(board)).CompareTo(best)==0).ToArray();int share=pot/winners.Length;
            foreach(int winner in winners)stacks[winner]+=share;pot-=share*winners.Length;if(pot>0){stacks[winners[0]]+=pot;pot=0;}finished=true;
        }
        public override Action ChooseCpuAction(int player,DeterministicRandom rng,int difficulty=1)
        {
            IReadOnlyList<Action> actions=LegalActions(player);PokerRank rank=board.Count+2>=5?PokerHandEvaluator.EvaluateBest(hands[player].Concat(board)):default;
            if(actions.Any(a=>a.Kind=="raise")&&(rank.Category>=2||board.Count==0&&hands[player][0].Rank==hands[player][1].Rank))return actions.First(a=>a.Kind=="raise");
            if(actions.Any(a=>a.Kind=="check"))return actions.First(a=>a.Kind=="check");if(rank.Category==0&&currentBet-streetContributions[player]>=4)return actions.First(a=>a.Kind=="fold");return actions.First(a=>a.Kind=="call");
        }
        public override bool IsTerminal=>finished;
        public override GameResult Result(){if(!finished)throw new InvalidOperationException("Game is not over.");int high=stacks.Max();return new GameResult(Enumerable.Range(0,Players).Where(i=>stacks[i]==high),stacks.Select(v=>(double)v),"chip stacks",TurnCount,new Dictionary<string,object>{{"board",board.ToArray()}});}
        public override string View(int? player=null){int viewer=player??CurrentPlayer;string shown=finished?" hands="+string.Join(" ",Enumerable.Range(0,Players).Select(i=>$"P{i}[{string.Join(" ",hands[i])}]")):"";return $"street={street} pot={pot} bet={currentBet} board=[{string.Join(" ",board)}] stacks=[{string.Join(",",stacks)}] folded=[{string.Join(",",folded)}]{shown}\nyour hand: {string.Join(" ",hands[viewer])}";}
        private static Card Pop(List<Card> cards){Card card=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return card;}
        public static void Register(GameRegistry registry)=>registry.Register(new GameInfo("texas_holdem","テキサスホールデム",2,10,"community-poker","ブラインド、4回のベッティング、5枚の共通札を備え、7枚から最強の5枚役を作る。","Bicycle Texas Hold'em",new Dictionary<string,string>{{"starting_stack","開始チップ（既定20）"}}),(p,r,o)=>new TexasHoldemGame(p,r,o));
    }

    public sealed class FiveCardDrawGame : GameBase
    {
        private readonly List<List<Card>> hands;private readonly List<Card> deck;private readonly bool[] folded;private readonly bool[] acted;private readonly bool[] drew;
        private readonly int[] stacks;private readonly int[] contributions;private readonly int[] drawnCounts;private string phase="bet1";private int currentBet;private int raises;private int pot;private bool finished;
        public override string GameId=>"five_card_draw";public override string Name=>"ファイブカードドロー";
        public FiveCardDrawGame(int players,DeterministicRandom rng,IReadOnlyDictionary<string,string> options)
        {
            Players=players;deck=Cards.Shuffled(Cards.StandardDeck(),rng);hands=Enumerable.Range(0,players).Select(_=>new List<Card>()).ToList();
            for(int round=0;round<5;round++)for(int player=0;player<players;player++)hands[player].Add(Pop(deck));
            folded=new bool[players];acted=new bool[players];drew=new bool[players];stacks=Enumerable.Repeat(options.Integer("starting_stack",20),players).ToArray();
            contributions=new int[players];drawnCounts=new int[players];for(int player=0;player<players;player++){stacks[player]--;pot++;}CurrentPlayer=1%players;
        }
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player);
            if(phase=="draw")
            {
                var result=new List<Action>{DrawAction()};
                for(int a=0;a<5;a++){result.Add(DrawAction(a));for(int b=a+1;b<5;b++){result.Add(DrawAction(a,b));for(int c=b+1;c<5;c++)result.Add(DrawAction(a,b,c));}}return result;
            }
            var betting=new List<Action>();int owed=currentBet-contributions[actual];if(owed==0)betting.Add(new Action("check"));else{betting.Add(new Action("fold"));betting.Add(new Action("call"));}
            if(raises<3&&stacks[actual]>owed)betting.Add(new Action("raise",value:(phase=="bet1"?1:2).ToString(CultureInfo.InvariantCulture)));return betting;
        }
        private static Action DrawAction(params int[] indexes)=>new Action("draw",value:string.Join(",",indexes));
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));TurnCount++;
            if(phase=="draw")
            {
                int[] indexes=string.IsNullOrEmpty(action.Value)?Array.Empty<int>():action.Value!.Split(',').Select(int.Parse).OrderByDescending(v=>v).ToArray();
                foreach(int index in indexes)hands[player].RemoveAt(index);for(int index=0;index<indexes.Length;index++)hands[player].Add(Pop(deck));drawnCounts[player]=indexes.Length;drew[player]=true;
                int next=FindNext(player,index=>!folded[index]&&!drew[index]);if(next>=0){CurrentPlayer=next;return;}StartSecondBet();return;
            }
            int owed=currentBet-contributions[player];if(action.Kind=="fold")folded[player]=true;else if(action.Kind=="call")Pay(player,owed);else if(action.Kind=="raise")
            {Pay(player,owed);Pay(player,int.Parse(action.Value!,CultureInfo.InvariantCulture));currentBet=contributions[player];raises++;for(int i=0;i<Players;i++)if(!folded[i])acted[i]=false;}
            acted[player]=true;AdvanceBet(player);
        }
        private void Pay(int player,int amount){int paid=Math.Min(amount,stacks[player]);stacks[player]-=paid;contributions[player]+=paid;pot+=paid;}
        private void AdvanceBet(int player)
        {
            int[] active=Enumerable.Range(0,Players).Where(i=>!folded[i]).ToArray();if(active.Length==1){stacks[active[0]]+=pot;pot=0;finished=true;return;}
            int next=FindNext(player,index=>!folded[index]&&(!acted[index]||contributions[index]<currentBet));if(next>=0){CurrentPlayer=next;return;}
            if(phase=="bet2"){Showdown();return;}phase="draw";CurrentPlayer=FindNext(0,index=>!folded[index]);
        }
        private void StartSecondBet(){phase="bet2";Array.Clear(acted,0,acted.Length);Array.Clear(contributions,0,contributions.Length);currentBet=0;raises=0;CurrentPlayer=FindNext(0,index=>!folded[index]);}
        private int FindNext(int player,Func<int,bool> predicate){for(int offset=1;offset<=Players;offset++){int next=(player+offset)%Players;if(predicate(next))return next;}return -1;}
        private void Showdown(){int[] active=Enumerable.Range(0,Players).Where(i=>!folded[i]).ToArray();PokerRank best=active.Select(i=>PokerHandEvaluator.EvaluateFive(hands[i])).Max();int[] winners=active.Where(i=>PokerHandEvaluator.EvaluateFive(hands[i]).CompareTo(best)==0).ToArray();int share=pot/winners.Length;foreach(int winner in winners)stacks[winner]+=share;pot-=share*winners.Length;if(pot>0){stacks[winners[0]]+=pot;pot=0;}finished=true;}
        public override Action ChooseCpuAction(int player,DeterministicRandom rng,int difficulty=1)
        {
            IReadOnlyList<Action> actions=LegalActions(player);PokerRank rank=PokerHandEvaluator.EvaluateFive(hands[player]);
            if(phase=="draw")
            {int[] keep=hands[player].Select((card,index)=>Tuple.Create(index,card)).GroupBy(item=>item.Item2.Rank).Where(group=>group.Count()>1).SelectMany(group=>group.Select(item=>item.Item1)).ToArray();return DrawAction(Enumerable.Range(0,5).Where(index=>!keep.Contains(index)).Take(3).ToArray());}
            if(actions.Any(a=>a.Kind=="raise")&&rank.Category>=2)return actions.First(a=>a.Kind=="raise");if(actions.Any(a=>a.Kind=="check"))return actions.First(a=>a.Kind=="check");return rank.Category==0&&currentBet-contributions[player]>=2?actions.First(a=>a.Kind=="fold"):actions.First(a=>a.Kind=="call");
        }
        public override bool IsTerminal=>finished;
        public override GameResult Result(){if(!finished)throw new InvalidOperationException("Game is not over.");int high=stacks.Max();return new GameResult(Enumerable.Range(0,Players).Where(i=>stacks[i]==high),stacks.Select(v=>(double)v),"chip stacks",TurnCount);}
        public override string View(int? player=null){int viewer=player??CurrentPlayer;string shown=finished?" showdown="+string.Join(" ",Enumerable.Range(0,Players).Where(i=>!folded[i]).Select(i=>$"P{i}[{string.Join(" ",hands[i])}]")):"";return $"phase={phase} pot={pot} stacks=[{string.Join(",",stacks)}] draws=[{string.Join(",",drawnCounts)}]{shown}\nyour hand: {string.Join(" ",hands[viewer])}";}
        private static Card Pop(List<Card> cards){Card card=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return card;}
        public static void Register(GameRegistry registry)=>registry.Register(new GameInfo("five_card_draw","ファイブカードドロー",2,6,"draw-poker","アンティ、2回のベッティング、0～3枚の交換を行い、5枚ポーカー役を比較する。","Pagat Five Card Draw",new Dictionary<string,string>{{"starting_stack","開始チップ（既定20）"}}),(p,r,o)=>new FiveCardDrawGame(p,r,o));
    }

    public sealed class BaccaratGame : GameBase
    {
        private readonly List<Card> deck;private readonly string?[] bets;private readonly double[] scores;private readonly List<Card> playerHand=new List<Card>();private readonly List<Card> bankerHand=new List<Card>();private string outcome="";private bool finished;
        public override string GameId=>"baccarat";public override string Name=>"バカラ";
        public BaccaratGame(int players,DeterministicRandom rng){Players=players;deck=Cards.Shuffled(Enumerable.Range(0,8).SelectMany(_=>Cards.StandardDeck()),rng);bets=new string?[players];scores=new double[players];}
        public override IReadOnlyList<Action> LegalActions(int? player=null){ValidateTurn(player);return new[]{new Action("bet_player"),new Action("bet_banker"),new Action("bet_tie")};}
        public override void Apply(Action action){int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));TurnCount++;bets[player]=action.Kind;if(player+1<Players){CurrentPlayer=player+1;return;}DealAndSettle();}
        private void DealAndSettle()
        {
            playerHand.Add(Pop(deck));bankerHand.Add(Pop(deck));playerHand.Add(Pop(deck));bankerHand.Add(Pop(deck));int playerTotal=Total(playerHand),bankerTotal=Total(bankerHand);int? third=null;
            if(playerTotal<8&&bankerTotal<8)
            {
                if(playerTotal<=5){Card card=Pop(deck);playerHand.Add(card);third=BaccaratValue(card);}bankerTotal=Total(bankerHand);
                bool draw=ShouldBankerDraw(bankerTotal,third);
                if(draw)bankerHand.Add(Pop(deck));
            }
            playerTotal=Total(playerHand);bankerTotal=Total(bankerHand);outcome=playerTotal>bankerTotal?"player":bankerTotal>playerTotal?"banker":"tie";
            for(int i=0;i<Players;i++){if(outcome=="tie")scores[i]=bets[i]=="bet_tie"?8:0;else if(bets[i]=="bet_"+outcome)scores[i]=outcome=="banker"?0.95:1;else scores[i]=-1;}finished=true;
        }
        private static int BaccaratValue(Card card)=>card.Rank>=10?0:card.Rank;private static int Total(IEnumerable<Card> cards)=>cards.Sum(BaccaratValue)%10;
        public static bool ShouldBankerDraw(int bankerTotal,int? playerThirdCard)
        {
            if(!playerThirdCard.HasValue)return bankerTotal<=5;int third=playerThirdCard.Value;
            return bankerTotal<=2||bankerTotal==3&&third!=8||bankerTotal==4&&third>=2&&third<=7||
                bankerTotal==5&&third>=4&&third<=7||bankerTotal==6&&third>=6&&third<=7;
        }
        public override Action ChooseCpuAction(int player,DeterministicRandom rng,int difficulty=1)=>new Action("bet_banker");public override bool IsTerminal=>finished;
        public override GameResult Result(){if(!finished)throw new InvalidOperationException("Game is not over.");double high=scores.Max();return new GameResult(Enumerable.Range(0,Players).Where(i=>Math.Abs(scores[i]-high)<0.000001),scores,"Punto Banco wager",TurnCount,new Dictionary<string,object>{{"outcome",outcome}});}
        public override string View(int? player=null)=>finished?$"outcome={outcome} player=[{string.Join(" ",playerHand)}] total={Total(playerHand)} banker=[{string.Join(" ",bankerHand)}] total={Total(bankerHand)} scores=[{string.Join(",",scores)}]":$"bets_placed={bets.Count(value=>value!=null)}/{Players} your_bet={bets[player??CurrentPlayer]??"-"}";
        private static Card Pop(List<Card> cards){Card card=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return card;}
        public static void Register(GameRegistry registry)=>registry.Register(new GameInfo("baccarat","バカラ",1,8,"banking","Punto Bancoの第三札表でプレイヤー・バンカー・タイの固定額ベットを決着する。","Pagat Baccarat"),(p,r,o)=>new BaccaratGame(p,r));
    }
}
