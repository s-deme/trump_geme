using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TrumpLab.Games
{
    internal static class ClassicCandidateGames
    {
        public static void RegisterGames(GameRegistry registry)
        {
            GopsGame.Register(registry);
            SevensGame.Register(registry);
            ConcentrationGame.Register(registry);
            HeartsGame.Register(registry);
            TwentyFourGame.Register(registry);
        }
    }

    public sealed class GopsGame : GameBase
    {
        private readonly List<List<Card>> hands;
        private readonly List<Card> prizes;
        private readonly int[] scores = new int[2];
        private readonly Card?[] pending = new Card?[2];
        private Card currentPrize;
        private int prizePot;
        private bool finished;

        public override string GameId => "gops";
        public override string Name => "GOPS";

        public GopsGame(int players, DeterministicRandom rng)
        {
            Players = 2;
            hands = new List<List<Card>>
            {
                Enumerable.Range(1,13).Select(rank=>new Card(Suit.Spades,rank)).ToList(),
                Enumerable.Range(1,13).Select(rank=>new Card(Suit.Clubs,rank)).ToList()
            };
            prizes = Enumerable.Range(1,13).Select(rank=>new Card(Suit.Diamonds,rank)).ToList();
            rng.Shuffle(prizes);
            RevealPrize();
        }

        private static int Value(Card card) => card.Rank;
        private void RevealPrize()
        {
            currentPrize = prizes[prizes.Count-1]; prizes.RemoveAt(prizes.Count-1);
            prizePot += Value(currentPrize); CurrentPlayer = 0;
            pending[0] = null; pending[1] = null;
        }

        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player);
            return hands[actual].Select(card=>new Action("bid",card)).ToArray();
        }

        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));
            Card card=action.Card!.Value;hands[player].Remove(card);pending[player]=card;TurnCount++;
            if(player==0){CurrentPlayer=1;return;}
            int left=Value(pending[0]!.Value),right=Value(pending[1]!.Value);
            if(left>right){scores[0]+=prizePot;prizePot=0;}
            else if(right>left){scores[1]+=prizePot;prizePot=0;}
            if(hands[0].Count==0){finished=true;return;}
            RevealPrize();
        }

        public override Action ChooseCpuAction(int player,DeterministicRandom rng,int difficulty=1)
        {
            int desired=Math.Min(13,Math.Max(1,prizePot));
            return LegalActions(player).OrderBy(action=>Math.Abs(Value(action.Card!.Value)-desired)).First();
        }

        public override bool IsTerminal=>finished;
        public override GameResult Result()
        {
            if(!finished)throw new InvalidOperationException("Game is not over.");int high=scores.Max();
            return new GameResult(Enumerable.Range(0,2).Where(i=>scores[i]==high),scores.Select(v=>(double)v),
                "diamond prize points",TurnCount,new Dictionary<string,object>{{"unclaimed",prizePot}});
        }
        public override string View(int? player=null)
        {
            int viewer=player??CurrentPlayer;
            string own=pending[viewer].HasValue?pending[viewer]!.Value.ToString():"-";
            return $"prize={currentPrize} pot={prizePot} scores=[{string.Join(",",scores)}] bids_left={hands[viewer].Count} your_pending={own}\n"+
                $"your bids: {string.Join(" ",hands[viewer])}";
        }
        public static void Register(GameRegistry registry)=>registry.Register(
            new GameInfo("gops","GOPS",2,2,"simultaneous-auction",
                "ダイヤを賞点札、スペードとクラブを同一構成の入札札として、伏せ入札で競う。","Pagat GOPS"),
            (p,r,o)=>new GopsGame(p,r));
    }

    public sealed class SevensGame : GameBase
    {
        private readonly List<List<Card>> hands;
        private readonly int[] passes;
        private readonly int[] finishOrder;
        private readonly bool[,] placed = new bool[4,14];
        private readonly List<int> eliminated = new List<int>();
        private readonly int[] low = Enumerable.Repeat(7,4).ToArray();
        private readonly int[] high = Enumerable.Repeat(7,4).ToArray();
        private int nextPlace=1;
        private bool finished;

        public override string GameId=>"sevens";
        public override string Name=>"七並べ";

        public SevensGame(int players,DeterministicRandom rng)
        {
            Players=players;List<Card> deck=Cards.Shuffled(Cards.StandardDeck(),rng);
            hands=Enumerable.Range(0,players).Select(_=>new List<Card>()).ToList();
            for(int index=0;index<deck.Count;index++)hands[index%players].Add(deck[index]);
            passes=new int[players];finishOrder=new int[players];
            for(int suit=0;suit<4;suit++)placed[suit,7]=true;
            int diamondSevenOwner=0;
            for(int player=0;player<players;player++)
            {
                if(hands[player].Contains(new Card(Suit.Diamonds,7)))diamondSevenOwner=player;
                hands[player].RemoveAll(card=>card.Rank==7);
            }
            CurrentPlayer=diamondSevenOwner;
            CompletePlayersWithNoCards();
        }

        private bool Playable(Card card)
        {
            int suit=(int)card.Suit;
            return card.Rank==low[suit]-1||card.Rank==high[suit]+1;
        }

        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player);var result=hands[actual].Where(Playable)
                .Select(card=>new Action("play",card)).ToList();
            if(passes[actual]<3)result.Add(new Action("pass"));
            else if(result.Count==0)result.Add(new Action("bankrupt"));
            return result;
        }

        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));TurnCount++;
            if(action.Kind=="play")
            {
                Card card=action.Card!.Value;hands[player].Remove(card);int suit=(int)card.Suit;
                placed[suit,card.Rank]=true;ExtendConnectedRow(suit);
                if(hands[player].Count==0)finishOrder[player]=nextPlace++;
            }
            else if(action.Kind=="pass")passes[player]++;
            else
            {
                foreach(Card card in hands[player])placed[(int)card.Suit,card.Rank]=true;
                hands[player].Clear();finishOrder[player]=-(eliminated.Count+1);eliminated.Add(player);
                for(int suit=0;suit<4;suit++)ExtendConnectedRow(suit);
            }
            CompletePlayersWithNoCards();
            if(finishOrder.All(place=>place!=0)){FinalizeEliminatedPlaces();finished=true;return;}
            CurrentPlayer=NextActive(player);
        }

        private void ExtendConnectedRow(int suit)
        {
            while(low[suit]>1&&placed[suit,low[suit]-1])low[suit]--;
            while(high[suit]<13&&placed[suit,high[suit]+1])high[suit]++;
        }

        private void FinalizeEliminatedPlaces()
        {
            for(int index=0;index<eliminated.Count;index++)finishOrder[eliminated[index]]=Players-index;
        }

        private void CompletePlayersWithNoCards()
        {
            foreach(int player in Enumerable.Range(0,Players).Where(i=>hands[i].Count==0&&finishOrder[i]==0).ToArray())
                finishOrder[player]=nextPlace++;
            if(finishOrder.All(place=>place!=0)){FinalizeEliminatedPlaces();finished=true;}
        }
        private int NextActive(int player)
        {for(int offset=1;offset<=Players;offset++){int next=(player+offset)%Players;if(finishOrder[next]==0)return next;}return player;}

        public override Action ChooseCpuAction(int player,DeterministicRandom rng,int difficulty=1)
        {
            Action[] plays=LegalActions(player).Where(action=>action.Kind=="play").ToArray();
            if(plays.Length>0)return plays.OrderBy(action=>Math.Abs(action.Card!.Value.Rank-7)).First();
            return LegalActions(player)[0];
        }
        public override bool IsTerminal=>finished;
        public override GameResult Result()
        {
            if(!finished)throw new InvalidOperationException("Game is not over.");
            return new GameResult(Enumerable.Range(0,Players).Where(i=>finishOrder[i]==1),
                finishOrder.Select(place=>(double)(Players+1-place)),"finish order",TurnCount,
                new Dictionary<string,object>{{"places",finishOrder.ToArray()}});
        }
        public override string View(int? player=null)
        {
            int viewer=player??CurrentPlayer;
            string rows=string.Join(" ",Enum.GetValues(typeof(Suit)).Cast<Suit>().Select(suit=>
                $"{Card.SuitCode(suit)}:{low[(int)suit]}-{high[(int)suit]}"));
            string allPlaced=string.Join(" ",Enum.GetValues(typeof(Suit)).Cast<Suit>().Select(suit=>
                $"{Card.SuitCode(suit)}:[{string.Join(",",Enumerable.Range(1,13).Where(rank=>placed[(int)suit,rank]))}]"));
            return $"layout={rows} placed={allPlaced} passes=[{string.Join(",",passes)}] places=[{string.Join(",",finishOrder)}]\n"+
                $"your hand: {string.Join(" ",hands[viewer])}";
        }
        public static void Register(GameRegistry registry)=>registry.Register(
            new GameInfo("sevens","七並べ",3,8,"layout-shedding",
                "ジョーカーなし・A/K非接続。4枚の7から各スートを上下へ伸ばし、3回まで任意にパスする。4回目は失格し、孤立札を含む全手札を所定位置へ公開する。","Trump Stadium Shichi Narabe"),
            (p,r,o)=>new SevensGame(p,r));
    }

    public sealed class ConcentrationGame : GameBase
    {
        private readonly List<Card> layout;
        private readonly bool[] taken;
        private readonly int[] pairs;
        private readonly int?[] knownRanks;
        private int? first;
        private int? second;
        private string phase="flip";
        private bool finished;

        public override string GameId=>"concentration";
        public override string Name=>"神経衰弱";
        public ConcentrationGame(int players,DeterministicRandom rng)
        {
            Players=players;layout=Cards.Shuffled(Cards.StandardDeck(),rng);
            taken=new bool[layout.Count];pairs=new int[players];knownRanks=new int?[layout.Count];
        }
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            ValidateTurn(player);
            if(phase=="resolve")return new[]{new Action("continue")};
            return Enumerable.Range(0,layout.Count).Where(index=>!taken[index]&&index!=first)
                .Select(index=>new Action("flip",value:index.ToString(CultureInfo.InvariantCulture))).ToArray();
        }
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));TurnCount++;
            if(phase=="resolve")
            {
                bool match=layout[first!.Value].Rank==layout[second!.Value].Rank;
                if(match){taken[first.Value]=true;taken[second.Value]=true;pairs[player]++;}
                else CurrentPlayer=(player+1)%Players;
                first=null;second=null;phase="flip";
                if(taken.All(value=>value))finished=true;
                return;
            }
            int index=int.Parse(action.Value!,CultureInfo.InvariantCulture);knownRanks[index]=layout[index].Rank;
            if(!first.HasValue)first=index;
            else{second=index;phase="resolve";}
        }
        public override Action ChooseCpuAction(int player,DeterministicRandom rng,int difficulty=1)
        {
            if(phase=="resolve")return new Action("continue");
            IReadOnlyList<Action> actions=LegalActions(player);
            if(first.HasValue)
            {
                Action[] known=actions.Where(action=>knownRanks[int.Parse(action.Value!,CultureInfo.InvariantCulture)]==layout[first.Value].Rank).ToArray();
                if(known.Length>0)return known[0];
            }
            else
            {
                var groups=Enumerable.Range(0,layout.Count).Where(index=>!taken[index]&&knownRanks[index].HasValue)
                    .GroupBy(index=>knownRanks[index]!.Value).FirstOrDefault(group=>group.Count()>=2);
                if(groups!=null)return new Action("flip",value:groups.First().ToString(CultureInfo.InvariantCulture));
            }
            return rng.Choice(actions);
        }
        public override bool IsTerminal=>finished;
        public override GameResult Result()
        {
            if(!finished)throw new InvalidOperationException("Game is not over.");int high=pairs.Max();
            return new GameResult(Enumerable.Range(0,Players).Where(i=>pairs[i]==high),pairs.Select(v=>(double)v),"most pairs",TurnCount);
        }
        public override string View(int? player=null)
        {
            string cells=string.Join(" ",Enumerable.Range(0,layout.Count).Select(index=>
                taken[index]?"XX":index==first||index==second?layout[index].ToString():index.ToString("00",CultureInfo.InvariantCulture)));
            return $"phase={phase} pairs=[{string.Join(",",pairs)}]\nlayout: {cells}";
        }
        public static void Register(GameRegistry registry)=>registry.Register(
            new GameInfo("concentration","神経衰弱",2,6,"memory",
                "伏せた52枚から2枚を公開し、同ランクなら組を獲得して続ける。","Bicycle Concentration"),
            (p,r,o)=>new ConcentrationGame(p,r));
    }

    public sealed class HeartsGame : GameBase
    {
        private readonly DeterministicRandom rng;
        private readonly int targetScore;
        private readonly int[] scores;
        private int roundNo;
        private List<List<Card>> hands=new List<List<Card>>();
        private List<List<Card>?> pending=new List<List<Card>?>();
        private List<List<Card>> captured=new List<List<Card>>();
        private readonly List<Tuple<int,Card>> trick=new List<Tuple<int,Card>>();
        private List<Card> kitty=new List<Card>();
        private bool heartsBroken;
        private bool firstTrick;
        private string phase="pass";
        private int passDirection;
        private bool finished;

        public override string GameId=>"hearts";
        public override string Name=>"ハーツ";
        public HeartsGame(int players,DeterministicRandom rng,IReadOnlyDictionary<string,string> options)
        {
            Players=players;this.rng=rng;targetScore=options.Integer("target_score",100);scores=new int[players];StartRound();
        }
        private static int Strength(Card card)=>card.Rank==1?14:card.Rank;
        private static int Penalty(Card card)=>card.Suit==Suit.Hearts?1:card.Suit==Suit.Spades&&card.Rank==12?13:0;
        private void StartRound()
        {
            roundNo++;List<Card> deck=Cards.Shuffled(Cards.StandardDeck(),rng);int size=52/Players;
            hands=Enumerable.Range(0,Players).Select(_=>new List<Card>()).ToList();
            for(int round=0;round<size;round++)for(int player=0;player<Players;player++)hands[player].Add(Pop(deck));
            kitty=deck;captured=Enumerable.Range(0,Players).Select(_=>new List<Card>()).ToList();trick.Clear();
            Card clubTwo=new Card(Suit.Clubs,2);
            if(Players==6&&kitty.Contains(clubTwo))
            {Card replacement=hands[0][hands[0].Count-1];hands[0][hands[0].Count-1]=clubTwo;kitty.Remove(clubTwo);kitty.Add(replacement);}
            heartsBroken=false;firstTrick=true;passDirection=PassDirection();
            phase=passDirection==0?"play":"pass";pending=Enumerable.Range(0,Players).Select(_=>(List<Card>?)null).ToList();
            CurrentPlayer=phase=="pass"?0:OpeningPlayer();
        }
        private int PassDirection()
        {
            if(Players==4){int value=(roundNo-1)%4;return value==0?1:value==1?-1:value==2?2:0;}
            int cycle=(roundNo-1)%3;return cycle==0?1:cycle==1?-1:0;
        }
        private int OpeningPlayer()
        {
            Card clubTwo=new Card(Suit.Clubs,2);int owner=hands.FindIndex(hand=>hand.Contains(clubTwo));
            if(owner>=0)return owner;
            return hands.Select((hand,index)=>Tuple.Create(index,hand.Where(c=>c.Suit==Suit.Clubs).Select(c=>c.Rank).DefaultIfEmpty(99).Min()))
                .OrderBy(item=>item.Item2).First().Item1;
        }
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player);
            if(phase=="pass")
            {
                var actions=new List<Action>();
                for(int a=0;a<hands[actual].Count-2;a++)for(int b=a+1;b<hands[actual].Count-1;b++)for(int c=b+1;c<hands[actual].Count;c++)
                    actions.Add(new Action("pass_three", value: $"{a},{b},{c}"));
                return actions;
            }
            IEnumerable<Card> cards=hands[actual];
            if(trick.Count==0)
            {
                if(firstTrick)
                {
                    Card required=cards.Where(card=>card.Suit==Suit.Clubs).OrderBy(card=>Strength(card)).First();
                    cards=new[]{required};
                }
                else if(!heartsBroken)
                {
                    Card[] nonHearts=cards.Where(card=>card.Suit!=Suit.Hearts).ToArray();if(nonHearts.Length>0)cards=nonHearts;
                }
            }
            else
            {
                Suit led=trick[0].Item2.Suit;Card[] follow=cards.Where(card=>card.Suit==led).ToArray();
                if(follow.Length>0)cards=follow;
                else if(firstTrick)
                {Card[] safe=cards.Where(card=>Penalty(card)==0).ToArray();if(safe.Length>0)cards=safe;}
            }
            return cards.Select(card=>new Action("play",card)).ToArray();
        }
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));TurnCount++;
            if(phase=="pass")
            {
                int[] indices=action.Value!.Split(',').Select(int.Parse).OrderByDescending(index=>index).ToArray();
                var cards=new List<Card>();foreach(int index in indices){cards.Add(hands[player][index]);hands[player].RemoveAt(index);}pending[player]=cards;
                if(player+1<Players){CurrentPlayer=player+1;return;}
                for(int sender=0;sender<Players;sender++)
                {int receiver=(sender+passDirection+Players)%Players;hands[receiver].AddRange(pending[sender]!);}
                phase="play";CurrentPlayer=OpeningPlayer();return;
            }
            Card card=action.Card!.Value;hands[player].Remove(card);trick.Add(Tuple.Create(player,card));
            if(card.Suit==Suit.Hearts||(card.Suit==Suit.Spades&&card.Rank==12))heartsBroken=true;
            if(trick.Count<Players){CurrentPlayer=(player+1)%Players;return;}
            Suit led=trick[0].Item2.Suit;Tuple<int,Card> winning=trick.Where(item=>item.Item2.Suit==led)
                .OrderByDescending(item=>Strength(item.Item2)).First();
            bool firstPenaltyTrick=captured.Sum(pile=>pile.Count(card=>Penalty(card)>0))==0&&trick.Any(item=>Penalty(item.Item2)>0);
            captured[winning.Item1].AddRange(trick.Select(item=>item.Item2));
            if(kitty.Count>0&&firstPenaltyTrick)
            {captured[winning.Item1].AddRange(kitty);kitty.Clear();}
            trick.Clear();CurrentPlayer=winning.Item1;firstTrick=false;
            if(hands.All(hand=>hand.Count==0))ScoreRound();
        }
        private void ScoreRound()
        {
            int[] penalties=captured.Select(pile=>pile.Sum(Penalty)).ToArray();int moon=Array.FindIndex(penalties,value=>value==26);
            if(moon>=0)for(int index=0;index<Players;index++)if(index!=moon)scores[index]+=26;
            else for(int other=0;other<Players;other++)scores[other]+=penalties[other];
            if(scores.Max()>=targetScore)finished=true;else StartRound();
        }
        public override Action ChooseCpuAction(int player,DeterministicRandom random,int difficulty=1)
        {
            IReadOnlyList<Action> actions=LegalActions(player);
            if(phase=="pass")return actions.OrderByDescending(action=>action.Value!.Split(',').Select(int.Parse)
                .Sum(index=>Penalty(hands[player][index])*20+Strength(hands[player][index]))).First();
            if(trick.Count==0)return actions.OrderBy(action=>Strength(action.Card!.Value)).First();
            return actions.OrderByDescending(action=>Penalty(action.Card!.Value)).ThenByDescending(action=>Strength(action.Card!.Value)).First();
        }
        public override bool IsTerminal=>finished;
        public override GameResult Result()
        {
            if(!finished)throw new InvalidOperationException("Game is not over.");int low=scores.Min();
            return new GameResult(Enumerable.Range(0,Players).Where(i=>scores[i]==low),scores.Select(value=>(double)-value),
                "lowest penalty score",TurnCount,new Dictionary<string,object>{{"penalties",scores.ToArray()}});
        }
        public override string View(int? player=null)
        {
            int viewer=player??CurrentPlayer;
            return $"round={roundNo} phase={phase} broken={heartsBroken} penalties=[{string.Join(",",scores)}] hands=[{string.Join(",",hands.Select(hand=>hand.Count))}]\n"+
                $"trick: {(trick.Count==0?"-":string.Join(" ",trick.Select(item=>item.Item2)))}\nyour hand: {string.Join(" ",hands[viewer])}";
        }
        private static Card Pop(List<Card> cards){Card card=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return card;}
        public static void Register(GameRegistry registry)=>registry.Register(
            new GameInfo("hearts","ハーツ",3,6,"trick-avoidance",
                "マストフォローでハートとスペードQを避け、100失点到達時の最少失点を競う。","Bicycle Hearts",
                new Dictionary<string,string>{{"target_score","終了する失点（既定100）"}}),(p,r,o)=>new HeartsGame(p,r,o));
    }

    public sealed class TwentyFourGame : GameBase
    {
        private readonly List<Card> stock;
        private readonly int[] scores;
        private readonly bool[] responded;
        private readonly int targetScore;
        private List<Card> puzzle=new List<Card>();
        private bool solvable;
        private int? noSolutionClaimant;
        private int starter=-1;
        private bool finished;

        public override string GameId=>"twenty_four";
        public override string Name=>"24";
        public TwentyFourGame(int players,DeterministicRandom rng,IReadOnlyDictionary<string,string> options)
        {
            Players=players;stock=Cards.Shuffled(Cards.StandardDeck(Enumerable.Range(1,10)),rng);
            scores=new int[players];responded=new bool[players];targetScore=options.Integer("target_score",5);NextPuzzle();
        }
        private void NextPuzzle()
        {
            if(stock.Count<4||scores.Max()>=targetScore){finished=true;return;}
            puzzle=new List<Card>();for(int index=0;index<4;index++)puzzle.Add(Pop(stock));
            solvable=CanMake24(puzzle.Select(card=>(double)card.Rank).ToArray());
            Array.Clear(responded,0,responded.Length);noSolutionClaimant=null;starter=(starter+1)%Players;CurrentPlayer=starter;
        }
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {ValidateTurn(player);return new[]{new Action("claim_24"),new Action("no_solution")};}
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));TurnCount++;responded[player]=true;
            if(action.Kind=="claim_24")
            {
                if(solvable){scores[player]++;NextPuzzle();return;}
                scores[player]--;
            }
            else
            {
                if(!noSolutionClaimant.HasValue)noSolutionClaimant=player;
                if(responded.All(value=>value))
                {if(!solvable)scores[noSolutionClaimant.Value]++;NextPuzzle();return;}
            }
            int next=NextUnresponded(player);if(next<0)NextPuzzle();else CurrentPlayer=next;
        }
        private int NextUnresponded(int player)
        {for(int offset=1;offset<=Players;offset++){int next=(player+offset)%Players;if(!responded[next])return next;}return -1;}
        public override Action ChooseCpuAction(int player,DeterministicRandom rng,int difficulty=1)=>
            new Action(solvable?"claim_24":"no_solution");
        public override bool IsTerminal=>finished;
        public override GameResult Result()
        {
            if(!finished)throw new InvalidOperationException("Game is not over.");int high=scores.Max();
            return new GameResult(Enumerable.Range(0,Players).Where(i=>scores[i]==high),scores.Select(v=>(double)v),"24 claims",TurnCount);
        }
        public override string View(int? player=null)=>$"numbers=[{string.Join(",",puzzle.Select(card=>card.Rank))}] scores=[{string.Join(",",scores)}] cards_left={stock.Count}";
        private static Card Pop(List<Card> cards){Card card=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return card;}
        public static bool CanMake24(double[] values)
        {
            if(values.Length==1)return Math.Abs(values[0]-24)<0.000001;
            for(int i=0;i<values.Length;i++)for(int j=i+1;j<values.Length;j++)
            {
                var rest=new List<double>();for(int k=0;k<values.Length;k++)if(k!=i&&k!=j)rest.Add(values[k]);
                double a=values[i],b=values[j];var candidates=new List<double>{a+b,a-b,b-a,a*b};
                if(Math.Abs(b)>0.000001)candidates.Add(a/b);if(Math.Abs(a)>0.000001)candidates.Add(b/a);
                foreach(double value in candidates){rest.Add(value);if(CanMake24(rest.ToArray()))return true;rest.RemoveAt(rest.Count-1);}
            }
            return false;
        }
        public static void Register(GameRegistry registry)=>registry.Register(
            new GameInfo("twenty_four","24",2,8,"arithmetic",
                "公開された4数を各1回、四則演算と括弧で24にできるか競う。","Pagat Twenty-Four",
                new Dictionary<string,string>{{"target_score","勝利点（既定5）"}}),(p,r,o)=>new TwentyFourGame(p,r,o));
    }
}
