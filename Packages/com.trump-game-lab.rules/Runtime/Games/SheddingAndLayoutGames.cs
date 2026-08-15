using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TrumpLab.Games
{
    internal static class SheddingAndLayoutGames
    {
        public static void RegisterGames(GameRegistry registry)
        {
            SpeedGame.Register(registry);
            CheatGame.Register(registry);
            PageOneGame.Register(registry);
            GolfGame.Register(registry);
            SpiteAndMaliceGame.Register(registry);
            CasinoGame.Register(registry);
        }
    }

    public sealed class SpiteAndMaliceGame : GameBase
    {
        private readonly DeterministicRandom rng;private readonly List<List<Card>> payoffs;private readonly List<List<Card>> hands;private readonly List<List<List<Card>>> sides;private readonly List<List<Card>> centers=new List<List<Card>>();private readonly List<Card> stock;private bool finished;private int winner=-1;
        public override string GameId=>"spite_and_malice";public override string Name=>"スパイト・アンド・マリス";
        public SpiteAndMaliceGame(int players,DeterministicRandom rng)
        {
            Players=2;this.rng=rng;List<Card> deck=Cards.Shuffled(Cards.StandardDeck(copies:2),rng);payoffs=new List<List<Card>>{new List<Card>(),new List<Card>()};hands=new List<List<Card>>{new List<Card>(),new List<Card>()};sides=new List<List<List<Card>>>{new List<List<Card>>(),new List<List<Card>>()};
            for(int round=0;round<20;round++)for(int player=0;player<2;player++)payoffs[player].Add(Pop(deck));for(int round=0;round<5;round++)for(int player=0;player<2;player++)hands[player].Add(Pop(deck));stock=deck;
            while(StartStrength(payoffs[0].Last())==StartStrength(payoffs[1].Last())){rng.Shuffle(payoffs[0]);rng.Shuffle(payoffs[1]);}
            CurrentPlayer=StartStrength(payoffs[0].Last())>StartStrength(payoffs[1].Last())?0:1;
        }
        private static int Strength(Card card)=>card.Rank==1?14:card.Rank;
        private static int StartStrength(Card card)=>card.Rank;
        private bool Fits(Card card,int center)=>card.Rank==13||(center==centers.Count?card.Rank==1:centers[center].Count+1==card.Rank);
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player);var result=new List<Action>();
            void AddSource(string source,Card card){for(int center=0;center<centers.Count;center++)if(Fits(card,center))result.Add(new Action("play_center",target:center,value:source));if(centers.Count<3&&Fits(card,centers.Count))result.Add(new Action("play_center",target:centers.Count,value:source));}
            if(payoffs[actual].Count>0)AddSource("p",payoffs[actual].Last());for(int index=0;index<hands[actual].Count;index++)AddSource("h:"+index,hands[actual][index]);for(int index=0;index<sides[actual].Count;index++)if(sides[actual][index].Count>0)AddSource("s:"+index,sides[actual][index].Last());
            for(int hand=0;hand<hands[actual].Count;hand++)for(int side=0;side<Math.Min(4,sides[actual].Count+1);side++)result.Add(new Action("discard_side",target:side,value:hand.ToString(CultureInfo.InvariantCulture)));return result;
        }
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));TurnCount++;
            if(action.Kind=="discard_side")
            {int index=int.Parse(action.Value!,CultureInfo.InvariantCulture);Card card=hands[player][index];hands[player].RemoveAt(index);while(sides[player].Count<=action.Target!.Value)sides[player].Add(new List<Card>());sides[player][action.Target.Value].Add(card);BeginTurn(1-player);return;}
            Card played=TakeSource(player,action.Value!);int target=action.Target!.Value;if(target==centers.Count)centers.Add(new List<Card>());centers[target].Add(played);
            if(action.Value=="p"&&payoffs[player].Count==0){winner=player;finished=true;return;}if(centers[target].Count==12){stock.AddRange(centers[target]);centers.RemoveAt(target);rng.Shuffle(stock);}
            if(hands[player].Count==0)FillHand(player);if(finished)return;
        }
        private Card TakeSource(int player,string source)
        {if(source=="p")return Pop(payoffs[player]);string[] parts=source.Split(':');int index=int.Parse(parts[1],CultureInfo.InvariantCulture);if(parts[0]=="h"){Card card=hands[player][index];hands[player].RemoveAt(index);return card;}return Pop(sides[player][index]);}
        private void BeginTurn(int player){CurrentPlayer=player;FillHand(player);}
        private void FillHand(int player){while(hands[player].Count<5&&stock.Count>0)hands[player].Add(Pop(stock));if(hands[player].Count==0&&stock.Count==0){winner=-1;finished=true;}}
        public override Action ChooseCpuAction(int player,DeterministicRandom random,int difficulty=1)
        {IReadOnlyList<Action> actions=LegalActions(player);Action[] payoff=actions.Where(action=>action.Kind=="play_center"&&action.Value=="p").ToArray();if(payoff.Length>0)return payoff[0];Action[] center=actions.Where(action=>action.Kind=="play_center").ToArray();if(center.Length>0)return center[0];return actions.Where(action=>action.Kind=="discard_side").OrderByDescending(action=>Strength(hands[player][int.Parse(action.Value!,CultureInfo.InvariantCulture)])).First();}
        public override bool IsTerminal=>finished;
        public override GameResult Result(){if(!finished)throw new InvalidOperationException("Game is not over.");IEnumerable<int> winners=winner<0?Enumerable.Range(0,2):new[]{winner};return new GameResult(winners,payoffs.Select(pile=>(double)-pile.Count),winner<0?"stock exhaustion draw":"payoff pile emptied",TurnCount);}
        public override string View(int? player=null){int viewer=player??CurrentPlayer;return $"payoffs=[{payoffs[0].Count}:{(payoffs[0].Count>0?payoffs[0].Last().ToString():"-")},{payoffs[1].Count}:{(payoffs[1].Count>0?payoffs[1].Last().ToString():"-")}] stock={stock.Count} centers=[{string.Join(" ",centers.Select(center=>center.Count+":"+center.Last()))}] side_tops=[{string.Join(" ",sides[viewer].Select(side=>side.Last()))}]\nyour hand: {string.Join(" ",hands[viewer])}";}
        private static Card Pop(List<Card> cards){Card card=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return card;}
        public static void Register(GameRegistry registry)=>registry.Register(new GameInfo("spite_and_malice","スパイト・アンド・マリス",2,2,"patience-race","2組を使い、20枚の支払い山を中央3列のAからQへ出す。手札補充と4つの脇山を備える。","Pagat Spite and Malice"),(p,r,o)=>new SpiteAndMaliceGame(p,r));
    }

    internal sealed class CasinoEntry
    {
        public List<Card> Cards { get; }=new List<Card>();public int? BuildValue { get; }public CasinoEntry(IEnumerable<Card> cards,int? build=null){Cards.AddRange(cards);BuildValue=build;}public override string ToString()=>BuildValue.HasValue?$"B{BuildValue}[{string.Join("+",Cards)}]":Cards[0].ToString();
    }

    public sealed class CasinoGame : GameBase
    {
        private readonly DeterministicRandom rng;private readonly int targetScore;private readonly int units;private readonly int[] scores;private List<Card> deck=new List<Card>();private List<List<Card>> hands=new List<List<Card>>();private readonly List<CasinoEntry> table=new List<CasinoEntry>();private List<List<Card>> captured=new List<List<Card>>();private int dealer=-1;private int lastCapturer=-1;private bool finished;
        public override string GameId=>"casino";public override string Name=>"カシノ";
        public CasinoGame(int players,DeterministicRandom rng,IReadOnlyDictionary<string,string> options)
        {Players=players;this.rng=rng;units=players==4?2:players;scores=new int[units];targetScore=options.Integer("target_score",21);StartRound();}
        private int Unit(int player)=>Players==4?player%2:player;private static int Value(Card card)=>card.Rank==1?1:card.Rank<=10?card.Rank:0;
        private void StartRound()
        {dealer=(dealer+1)%Players;deck=Cards.Shuffled(Cards.StandardDeck(),rng);hands=Enumerable.Range(0,Players).Select(_=>new List<Card>()).ToList();captured=Enumerable.Range(0,units).Select(_=>new List<Card>()).ToList();table.Clear();for(int index=0;index<4;index++)table.Add(new CasinoEntry(new[]{Pop(deck)}));DealHands();lastCapturer=-1;CurrentPlayer=(dealer+1)%Players;}
        private void DealHands(){for(int round=0;round<4&&deck.Count>0;round++)for(int offset=1;offset<=Players&&deck.Count>0;offset++)hands[(dealer+offset)%Players].Add(Pop(deck));}
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player);var result=new List<Action>();for(int handIndex=0;handIndex<hands[actual].Count;handIndex++)
            {
                Card played=hands[actual][handIndex];result.Add(new Action("trail",value:handIndex.ToString(CultureInfo.InvariantCulture)));
                if(Value(played)==0)
                {for(int index=0;index<table.Count;index++)if(!table[index].BuildValue.HasValue&&table[index].Cards[0].Rank==played.Rank)result.Add(CaptureAction(handIndex,new[]{index}));continue;}
                int rank=Value(played);int[] direct=Enumerable.Range(0,table.Count).Where(index=>table[index].BuildValue==rank||!table[index].BuildValue.HasValue&&Value(table[index].Cards[0])==rank).ToArray();if(direct.Length>0)result.Add(CaptureAction(handIndex,direct));
                int[] loose=Enumerable.Range(0,table.Count).Where(index=>!table[index].BuildValue.HasValue&&Value(table[index].Cards[0])>0&&Value(table[index].Cards[0])<rank).ToArray();foreach(int[] subset in SumSubsets(loose,rank))result.Add(CaptureAction(handIndex,direct.Concat(subset).Distinct().ToArray()));
                foreach(int[] subset in BuildSubsets(loose,rank))
                {int build=rank+subset.Sum(index=>Value(table[index].Cards[0]));if(build<=10&&hands[actual].Where((card,index)=>index!=handIndex).Any(card=>Value(card)==build))result.Add(new Action("build",target:build,value:handIndex+"|"+string.Join(",",subset)));}
            }
            return result.GroupBy(action=>action.ToString()).Select(group=>group.First()).ToArray();
        }
        private static Action CaptureAction(int handIndex,IEnumerable<int> entries)=>new Action("capture",value:handIndex+"|"+string.Join(",",entries.OrderBy(index=>index)));
        private IEnumerable<int[]> SumSubsets(int[] entries,int target){var result=new List<int[]>();Search(entries,target,0,new List<int>(),result);return result;}
        private IEnumerable<int[]> BuildSubsets(int[] entries,int played){var result=new List<int[]>();for(int target=1;target<=10-played;target++)Search(entries,target,0,new List<int>(),result);return result.Where(values=>values.Length>0);}
        private void Search(int[] entries,int remaining,int offset,List<int> chosen,List<int[]> result)
        {if(remaining==0){result.Add(chosen.ToArray());return;}for(int index=offset;index<entries.Length;index++){int value=Value(table[entries[index]].Cards[0]);if(value>remaining)continue;chosen.Add(entries[index]);Search(entries,remaining-value,index+1,chosen,result);chosen.RemoveAt(chosen.Count-1);}}
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));TurnCount++;string[] parts=action.Value!.Split('|');int handIndex=int.Parse(parts[0],CultureInfo.InvariantCulture);Card played=hands[player][handIndex];hands[player].RemoveAt(handIndex);
            if(action.Kind=="trail")table.Add(new CasinoEntry(new[]{played}));
            else
            {int[] entries=string.IsNullOrEmpty(parts[1])?Array.Empty<int>():parts[1].Split(',').Select(int.Parse).Distinct().OrderByDescending(index=>index).ToArray();var cards=new List<Card>{played};foreach(int index in entries){cards.AddRange(table[index].Cards);table.RemoveAt(index);}if(action.Kind=="build")table.Add(new CasinoEntry(cards,action.Target));else{captured[Unit(player)].AddRange(cards);lastCapturer=Unit(player);}}
            Advance(player);
        }
        private void Advance(int player)
        {
            if(hands.All(hand=>hand.Count==0))
            {if(deck.Count>0)DealHands();else{if(lastCapturer>=0){captured[lastCapturer].AddRange(table.SelectMany(entry=>entry.Cards));table.Clear();}ScoreRound();return;}}
            CurrentPlayer=(player+1)%Players;
        }
        private void ScoreRound()
        {
            int[] round=new int[units];int maxCards=captured.Max(cards=>cards.Count);if(captured.Count(cards=>cards.Count==maxCards)==1)round[Array.FindIndex(captured.ToArray(),cards=>cards.Count==maxCards)]+=3;int maxSpades=captured.Max(cards=>cards.Count(card=>card.Suit==Suit.Spades));if(captured.Count(cards=>cards.Count(card=>card.Suit==Suit.Spades)==maxSpades)==1)round[Array.FindIndex(captured.ToArray(),cards=>cards.Count(card=>card.Suit==Suit.Spades)==maxSpades)]++;
            for(int unit=0;unit<units;unit++){round[unit]+=captured[unit].Count(card=>card.Rank==1);if(captured[unit].Contains(new Card(Suit.Diamonds,10)))round[unit]+=2;if(captured[unit].Contains(new Card(Suit.Spades,2)))round[unit]++;scores[unit]+=round[unit];}
            int high=scores.Max();if(high>=targetScore&&scores.Count(value=>value==high)==1)finished=true;else StartRound();
        }
        public override Action ChooseCpuAction(int player,DeterministicRandom random,int difficulty=1)
        {IReadOnlyList<Action> actions=LegalActions(player);Action[] captures=actions.Where(action=>action.Kind=="capture").OrderByDescending(action=>action.Value!.Split('|')[1].Split(new[]{','},StringSplitOptions.RemoveEmptyEntries).Length).ToArray();if(captures.Length>0)return captures[0];Action[] builds=actions.Where(action=>action.Kind=="build").ToArray();return builds.Length>0?builds[0]:actions.First(action=>action.Kind=="trail");}
        public override bool IsTerminal=>finished;
        public override GameResult Result(){if(!finished)throw new InvalidOperationException("Game is not over.");int high=scores.Max();return new GameResult(Enumerable.Range(0,Players).Where(player=>scores[Unit(player)]==high),Enumerable.Range(0,Players).Select(player=>(double)scores[Unit(player)]),"casino score to 21",TurnCount,new Dictionary<string,object>{{"unit_scores",scores.ToArray()}});}
        public override string View(int? player=null){int viewer=player??CurrentPlayer;return $"dealer={dealer} scores=[{string.Join(",",scores)}] deck={deck.Count} table=[{string.Join(" ",table)}] captured=[{string.Join(",",captured.Select(cards=>cards.Count))}] hand_counts=[{string.Join(",",hands.Select(hand=>hand.Count))}]\nyour hand: {string.Join(" ",hands[viewer])}";}
        private static Card Pop(List<Card> cards){Card card=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return card;}
        public static void Register(GameRegistry registry)=>registry.Register(new GameInfo("casino","カシノ",2,4,"capture","一致・合計取りと単一ビルドを行い、最多札・最多スペード・A・10D・2Sの11点を21点まで累積する。","Pagat Casino",new Dictionary<string,string>{{"target_score","勝利点（既定21）"}}),(p,r,o)=>new CasinoGame(p,r,o));
    }

    public sealed class SpeedGame : GameBase
    {
        private readonly List<List<Card>> layouts;private readonly List<List<Card>> reserves;private readonly Card[] centers=new Card[2];private bool finished;private int winner=-1;
        public override string GameId=>"speed";public override string Name=>"スピード／スピット";
        public SpeedGame(int players,DeterministicRandom rng)
        {
            Players=2;layouts=new List<List<Card>>();reserves=new List<List<Card>>();
            for(int player=0;player<2;player++){List<Card> deck=Cards.Shuffled(Cards.StandardDeck(),rng);layouts.Add(deck.Take(4).ToList());deck.RemoveRange(0,4);reserves.Add(deck);}
            centers[0]=Pop(reserves[0]);centers[1]=Pop(reserves[1]);CurrentPlayer=0;
        }
        private static bool Adjacent(Card left,Card right){int a=left.Rank,b=right.Rank;return Math.Abs(a-b)==1||a==1&&b==13||a==13&&b==1;}
        private IEnumerable<Action> Plays(int player)=>Enumerable.Range(0,layouts[player].Count).SelectMany(index=>Enumerable.Range(0,2).Where(pile=>Adjacent(layouts[player][index],centers[pile])).Select(pile=>new Action("play",target:pile,value:index.ToString(CultureInfo.InvariantCulture))));
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player);Action[] plays=Plays(actual).ToArray();if(plays.Length>0)return plays;
            if(!Plays(1-actual).Any()&&(reserves[0].Count>0||reserves[1].Count>0))return new[]{new Action("spit")};return new[]{new Action("pass")};
        }
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));TurnCount++;
            if(action.Kind=="play")
            {int index=int.Parse(action.Value!,CultureInfo.InvariantCulture);centers[action.Target!.Value]=layouts[player][index];layouts[player].RemoveAt(index);if(reserves[player].Count>0)layouts[player].Add(Pop(reserves[player]));if(layouts[player].Count==0&&reserves[player].Count==0){winner=player;finished=true;return;}CurrentPlayer=1-player;return;}
            if(action.Kind=="spit")
            {for(int pile=0;pile<2;pile++){int owner=pile;if(reserves[owner].Count>0)centers[pile]=Pop(reserves[owner]);else if(reserves[1-owner].Count>0)centers[pile]=Pop(reserves[1-owner]);}CurrentPlayer=1-player;return;}
            if(reserves[0].Count==0&&reserves[1].Count==0&&!Plays(0).Any()&&!Plays(1).Any()){int left0=layouts[0].Count,left1=layouts[1].Count;winner=left0==left1?-1:left0<left1?0:1;finished=true;return;}CurrentPlayer=1-player;
        }
        public override Action ChooseCpuAction(int player,DeterministicRandom rng,int difficulty=1)=>LegalActions(player)[0];public override bool IsTerminal=>finished;
        public override GameResult Result(){if(!finished)throw new InvalidOperationException("Game is not over.");IEnumerable<int> winners=winner<0?Enumerable.Range(0,2):new[]{winner};return new GameResult(winners,new[]{-(double)(layouts[0].Count+reserves[0].Count),-(double)(layouts[1].Count+reserves[1].Count)},"first out in deterministic spit",TurnCount);}
        public override string View(int? player=null){int viewer=player??CurrentPlayer;return $"centers=[{centers[0]} {centers[1]}] reserves=[{reserves[0].Count},{reserves[1].Count}] layouts=[{layouts[0].Count},{layouts[1].Count}]\nyour layout: {string.Join(" ",layouts[viewer])}";}
        private static Card Pop(List<Card> cards){Card card=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return card;}
        public static void Register(GameRegistry registry)=>registry.Register(new GameInfo("speed","スピード／スピット",2,2,"real-time-shedding","各自デッキと4枚の場札を使い、中央札の上下1ランクへ出す同時進行を決定論的な交互入力に正規化する。","Bicycle Spit"),(p,r,o)=>new SpeedGame(p,r));
    }

    public sealed class CheatGame : GameBase
    {
        private readonly List<List<Card>> hands;private readonly List<Card> pile=new List<Card>();private readonly List<Card> pending=new List<Card>();private List<int> challengers=new List<int>();private int challengeIndex;private int requiredRank=1;private int claimant=-1;private int claimedCount;private string phase="play";private bool finished;private int winner=-1;
        public override string GameId=>"cheat";public override string Name=>"ダウト";
        public CheatGame(int players,DeterministicRandom rng)
        {Players=players;List<Card> deck=Cards.Shuffled(Cards.StandardDeck(),rng);hands=Enumerable.Range(0,players).Select(_=>new List<Card>()).ToList();for(int index=0;index<deck.Count;index++)hands[index%players].Add(deck[index]);}
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player);if(phase=="challenge")return new[]{new Action("pass"),new Action("challenge")};var result=new List<Action>();int maximum=Math.Min(4,hands[actual].Count);
            for(int size=1;size<=maximum;size++)AddClaims(hands[actual].Count,size,0,new List<int>(),result);return result;
        }
        private static void AddClaims(int count,int size,int offset,List<int> chosen,List<Action> result)
        {if(chosen.Count==size){result.Add(new Action("claim",value:string.Join(",",chosen)));return;}for(int index=offset;index<=count-(size-chosen.Count);index++){chosen.Add(index);AddClaims(count,size,index+1,chosen,result);chosen.RemoveAt(chosen.Count-1);}}
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));TurnCount++;
            if(phase=="play")
            {claimant=player;pending.Clear();pending.AddRange(RummyRules.RemoveIndexes(hands[player],RummyRules.ParseIndexes(action.Value!)));pile.AddRange(pending);claimedCount=pending.Count;challengers=Enumerable.Range(1,Players-1).Select(offset=>(player+offset)%Players).ToList();challengeIndex=0;phase="challenge";CurrentPlayer=challengers[0];return;}
            if(action.Kind=="challenge")
            {
                bool honest=pending.All(card=>card.Rank==requiredRank);int collector=honest?player:claimant;hands[collector].AddRange(pile);pile.Clear();
                if(honest&&hands[claimant].Count==0){winner=claimant;finished=true;return;}AdvanceRound();return;
            }
            challengeIndex++;if(challengeIndex<challengers.Count){CurrentPlayer=challengers[challengeIndex];return;}if(hands[claimant].Count==0){winner=claimant;finished=true;return;}AdvanceRound();
        }
        private void AdvanceRound(){requiredRank=requiredRank==13?1:requiredRank+1;phase="play";CurrentPlayer=(claimant+1)%Players;pending.Clear();}
        public override Action ChooseCpuAction(int player,DeterministicRandom rng,int difficulty=1)
        {
            IReadOnlyList<Action> actions=LegalActions(player);if(phase=="challenge")
            {int own=hands[player].Count(card=>card.Rank==requiredRank);return own+claimedCount>4?new Action("challenge"):new Action("pass");}
            int[] honest=Enumerable.Range(0,hands[player].Count).Where(index=>hands[player][index].Rank==requiredRank).Take(4).ToArray();if(honest.Length>0)return actions.First(action=>action.Value==string.Join(",",honest));return actions[0];
        }
        public override bool IsTerminal=>finished;
        public override GameResult Result(){if(!finished)throw new InvalidOperationException("Game is not over.");return new GameResult(new[]{winner},Enumerable.Range(0,Players).Select(i=>i==winner?1d:-hands[i].Count),"last claim survived challenges",TurnCount);}
        public override string View(int? player=null){int viewer=player??CurrentPlayer;return $"phase={phase} required={requiredRank} pile={pile.Count} last_claim=P{claimant}/{claimedCount} hand_counts=[{string.Join(",",hands.Select(hand=>hand.Count))}]\nyour hand: {string.Join(" ",hands[viewer])}";}
        public static void Register(GameRegistry registry)=>registry.Register(new GameInfo("cheat","ダウト",3,6,"bluffing-shedding","AからKへ順に1～4枚を伏せて宣言し、全員のチャレンジ機会を順次処理して、誤判定側が山を取る。","Pagat Cheat"),(p,r,o)=>new CheatGame(p,r));
    }

    internal readonly struct PageCard
    {
        public int Id { get; }public Card? Card { get; }public PageCard(int id,Card? card){Id=id;Card=card;}public bool Joker=>!Card.HasValue;
        public override string ToString()=>Joker?"JK":Card!.Value.ToString();
    }

    public sealed class PageOneGame : GameBase
    {
        private readonly DeterministicRandom rng;private readonly List<List<PageCard>> hands;private List<PageCard> stock;private readonly List<Tuple<int,PageCard>> trick=new List<Tuple<int,PageCard>>();private readonly List<PageCard> completed=new List<PageCard>();private Suit? led;private bool finished;private int winner=-1;
        public override string GameId=>"page_one";public override string Name=>"ページワン";
        public PageOneGame(int players,DeterministicRandom rng)
        {
            Players=players;this.rng=rng;int id=0;stock=Cards.StandardDeck().Select(card=>new PageCard(id++,card)).Append(new PageCard(id,null)).ToList();rng.Shuffle(stock);hands=Enumerable.Range(0,players).Select(_=>new List<PageCard>()).ToList();
            for(int round=0;round<4;round++)for(int player=0;player<players;player++)hands[player].Add(Pop(stock));
        }
        private static int Strength(PageCard card)=>card.Joker?100:card.Card!.Value.Rank==1?14:card.Card.Value.Rank;
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player);IEnumerable<PageCard> cards=hands[actual];if(trick.Count>0&&led.HasValue)
            {PageCard[] follow=cards.Where(card=>card.Joker||card.Card!.Value.Suit==led.Value).ToArray();if(follow.Length==0)return new[]{new Action(CanDraw()?"draw":"draw_stalemate")};cards=follow;}
            var result=new List<Action>();foreach(PageCard card in cards){result.Add(new Action("play",value:card.Id.ToString(CultureInfo.InvariantCulture)));if(hands[actual].Count==2)result.Add(new Action("play_page_one",value:card.Id.ToString(CultureInfo.InvariantCulture)));}return result;
        }
        private bool CanDraw()=>stock.Count>0||completed.Count>0;
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));TurnCount++;
            if(action.Kind=="draw_stalemate"){finished=true;winner=-1;return;}
            if(action.Kind=="draw")
            {Recycle();hands[player].Add(Pop(stock));return;}
            int id=int.Parse(action.Value!,CultureInfo.InvariantCulture);PageCard card=hands[player].First(value=>value.Id==id);hands[player].Remove(card);if(hands[player].Count==1&&action.Kind!="play_page_one")DrawPenalty(player,5);trick.Add(Tuple.Create(player,card));if(trick.Count==1&&!card.Joker)led=card.Card!.Value.Suit;else if(trick.Count==2&&!led.HasValue&&!card.Joker)led=card.Card!.Value.Suit;
            if(hands[player].Count==0){winner=player;finished=true;return;}if(trick.Count<Players){CurrentPlayer=(player+1)%Players;return;}
            int trickWinner=trick.Any(item=>item.Item2.Joker)?trick.First(item=>item.Item2.Joker).Item1:trick.Where(item=>item.Item2.Card!.Value.Suit==led).OrderByDescending(item=>Strength(item.Item2)).First().Item1;completed.AddRange(trick.Select(item=>item.Item2));trick.Clear();led=null;CurrentPlayer=trickWinner;
        }
        private void DrawPenalty(int player,int count){for(int index=0;index<count&&CanDraw();index++){Recycle();hands[player].Add(Pop(stock));}}
        private void Recycle(){if(stock.Count>0)return;stock=completed.ToList();completed.Clear();rng.Shuffle(stock);}
        public override Action ChooseCpuAction(int player,DeterministicRandom random,int difficulty=1)
        {IReadOnlyList<Action> actions=LegalActions(player);if(actions[0].Kind.StartsWith("draw",StringComparison.Ordinal))return actions[0];return actions.Where(action=>hands[player].Count==2?action.Kind=="play_page_one":action.Kind=="play").OrderBy(action=>Strength(hands[player].First(card=>card.Id==int.Parse(action.Value!,CultureInfo.InvariantCulture)))).First();}
        public override bool IsTerminal=>finished;
        public override GameResult Result(){if(!finished)throw new InvalidOperationException("Game is not over.");IEnumerable<int> winners=winner<0?Enumerable.Range(0,Players):new[]{winner};return new GameResult(winners,Enumerable.Range(0,Players).Select(i=>winner>=0&&i==winner?1d:0d),winner<0?"stock exhaustion draw":"first player out",TurnCount);}
        public override string View(int? player=null){int viewer=player??CurrentPlayer;return $"led={(led.HasValue?Card.SuitCode(led.Value):"-")} stock={stock.Count} completed={completed.Count} trick=[{string.Join(" ",trick.Select(item=>item.Item2))}] hand_counts=[{string.Join(",",hands.Select(hand=>hand.Count))}]\nyour hand: {string.Join(" ",hands[viewer])}";}
        private static PageCard Pop(List<PageCard> cards){PageCard card=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return card;}
        public static void Register(GameRegistry registry)=>registry.Register(new GameInfo("page_one","ページワン",2,6,"inflation-trick","4枚手札でマストフォローし、出せなければ同スートが出るまで引く。ジョーカーとPage One宣言罰を含む。","Pagat Page One"),(p,r,o)=>new PageOneGame(p,r));
    }

    public sealed class GolfGame : GameBase
    {
        private readonly DeterministicRandom rng;private readonly int[] scores;private List<List<Card>> layouts=new List<List<Card>>();private List<bool[]> faceUp=new List<bool[]>();private List<Card> stock=new List<Card>();private readonly List<Card> discard=new List<Card>();private Card? drawn;private bool drewDiscard;private int dealer;private int reveals;private int hole;private string phase="reveal";private bool finished;
        public override string GameId=>"golf";public override string Name=>"ゴルフ";
        public GolfGame(int players,DeterministicRandom rng){Players=players;this.rng=rng;scores=new int[players];dealer=players-1;StartHole();}
        private void StartHole()
        {hole++;reveals=0;stock=Cards.Shuffled(Cards.StandardDeck(copies:Players>=5?2:1),rng);layouts=Enumerable.Range(0,Players).Select(_=>new List<Card>()).ToList();faceUp=Enumerable.Range(0,Players).Select(_=>new bool[6]).ToList();for(int round=0;round<6;round++)for(int offset=1;offset<=Players;offset++)layouts[(dealer+offset)%Players].Add(Pop(stock));discard.Clear();discard.Add(Pop(stock));phase="reveal";CurrentPlayer=(dealer+1)%Players;}
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player);if(phase=="reveal"){var result=new List<Action>();for(int a=0;a<5;a++)for(int b=a+1;b<6;b++)result.Add(new Action("reveal_two",value:$"{a},{b}"));return result;}
            if(phase=="draw"){var draws=new List<Action>();if(stock.Count>0)draws.Add(new Action("draw_stock"));draws.Add(new Action("draw_discard"));return draws;}var actions=Enumerable.Range(0,6).Select(index=>new Action("swap",value:index.ToString(CultureInfo.InvariantCulture))).ToList();if(!drewDiscard)actions.Add(new Action("discard_drawn"));return actions;
        }
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));TurnCount++;
            if(phase=="reveal")
            {foreach(int index in RummyRules.ParseIndexes(action.Value!))faceUp[player][index]=true;reveals++;if(reveals<Players)CurrentPlayer=(player+1)%Players;else{phase="draw";CurrentPlayer=(dealer+1)%Players;}return;}
            if(phase=="draw")
            {drewDiscard=action.Kind=="draw_discard";drawn=drewDiscard?Pop(discard):Pop(stock);phase="replace";return;}
            if(action.Kind=="swap")
            {int index=int.Parse(action.Value!,CultureInfo.InvariantCulture);discard.Add(layouts[player][index]);layouts[player][index]=drawn!.Value;faceUp[player][index]=true;}
            else discard.Add(drawn!.Value);drawn=null;
            if(faceUp[player].All(value=>value)){ScoreHole();return;}phase="draw";CurrentPlayer=(player+1)%Players;
        }
        private static int Value(Card card)=>card.Rank==1?1:card.Rank==2?-2:card.Rank==13?0:Math.Min(card.Rank,10);
        private static int LayoutScore(IReadOnlyList<Card> cards){int total=0;for(int column=0;column<3;column++)if(cards[column].Rank!=cards[column+3].Rank)total+=Value(cards[column])+Value(cards[column+3]);return total;}
        private void ScoreHole(){for(int player=0;player<Players;player++)scores[player]+=LayoutScore(layouts[player]);if(hole>=9)finished=true;else{dealer=(dealer+1)%Players;StartHole();}}
        public override Action ChooseCpuAction(int player,DeterministicRandom random,int difficulty=1)
        {IReadOnlyList<Action> actions=LegalActions(player);if(phase=="reveal")return actions[0];if(phase=="draw")return actions.Count==1?actions[0]:Value(discard.Last())<=4?new Action("draw_discard"):new Action("draw_stock");int target=Enumerable.Range(0,6).OrderBy(index=>faceUp[player][index]?1:0).ThenByDescending(index=>Value(layouts[player][index])).First();if(drewDiscard)return new Action("swap",value:target.ToString(CultureInfo.InvariantCulture));return drawn.HasValue&&Value(drawn.Value)<(faceUp[player][target]?Value(layouts[player][target]):6)?new Action("swap",value:target.ToString(CultureInfo.InvariantCulture)):new Action("discard_drawn");}
        public override bool IsTerminal=>finished;
        public override GameResult Result(){if(!finished)throw new InvalidOperationException("Game is not over.");int low=scores.Min();return new GameResult(Enumerable.Range(0,Players).Where(i=>scores[i]==low),scores.Select(value=>(double)-value),"lowest score after nine holes",TurnCount,new Dictionary<string,object>{{"golf_scores",scores.ToArray()}});}
        public override string View(int? player=null){int viewer=player??CurrentPlayer;string Shown(int owner)=>string.Join(" ",Enumerable.Range(0,6).Select(index=>faceUp[owner][index]?layouts[owner][index].ToString():"??"));string pending=!drawn.HasValue?"-":drewDiscard||viewer==CurrentPlayer?drawn.Value.ToString():"??";string top=discard.Count==0?"-":discard.Last().ToString();return $"hole={hole}/9 dealer=P{dealer} phase={phase} scores=[{string.Join(",",scores)}] stock={stock.Count} discard={top} drawn={pending} face_up=[{string.Join(",",faceUp.Select(cards=>cards.Count(value=>value)))}] layouts=[{string.Join(" | ",Enumerable.Range(0,Players).Select(owner=>$"P{owner}:{Shown(owner)}"))}]\nyour layout: {Shown(viewer)}";}
        private static Card Pop(List<Card> cards){Card card=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return card;}
        public static void Register(GameRegistry registry)=>registry.Register(new GameInfo("golf","ゴルフ",2,6,"layout","Pagat Six-card版。6枚を2×3に伏せ、2枚公開から交換して、同列ペア0点の9ホール合計を最小化する。5～6人は2組を使う。","Pagat Six-card Golf"),(p,r,o)=>new GolfGame(p,r));
    }
}
