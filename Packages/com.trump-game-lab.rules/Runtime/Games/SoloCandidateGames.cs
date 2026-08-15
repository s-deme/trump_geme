using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TrumpLab.Games
{
    internal static class SoloCandidateGames
    {
        public static void RegisterGames(GameRegistry registry)
        {
            CardCaptureGame.Register(registry);
            ScoundrelGame.Register(registry);
            GosankyoGame.Register(registry);
        }
    }

    internal readonly struct CaptureCard
    {
        public int Id { get; }public Card? Card { get; }public CaptureCard(int id,Card? card){Id=id;Card=card;}public bool Joker=>!Card.HasValue;
        public override string ToString()=>Joker?"JK"+Id:Card!.Value.ToString();
    }

    public sealed class CardCaptureGame : GameBase
    {
        private readonly DeterministicRandom rng;private List<CaptureCard> personalDeck;private readonly List<CaptureCard> personalDiscard=new List<CaptureCard>();private readonly List<CaptureCard> hand=new List<CaptureCard>();private readonly List<Card> enemies;private readonly List<Card> row=new List<Card>();private int nextId=52;private string phase="discard";private bool finished;private bool won;
        public override string GameId=>"card_capture";public override string Name=>"Card Capture";
        public CardCaptureGame(int players,DeterministicRandom rng)
        {
            Players=1;this.rng=rng;personalDeck=Cards.StandardDeck(new[]{2,3,4}).Select((card,id)=>new CaptureCard(id,card)).Concat(new[]{new CaptureCard(nextId++,null),new CaptureCard(nextId++,null)}).ToList();rng.Shuffle(personalDeck);
            enemies=Cards.StandardDeck().Where(card=>card.Rank!=2&&card.Rank!=3&&card.Rank!=4).ToList();rng.Shuffle(enemies);for(int slot=0;slot<4;slot++){Card card=Pop(enemies);if(card.Rank>=11||card.Rank==1)enemies.Insert(0,card);else row.Add(card);}StartRound();
        }
        private static int Strength(Card card)=>card.Rank==1?14:card.Rank;
        private void StartRound(){while(row.Count<4&&enemies.Count>0)row.Add(Pop(enemies));if(row.Count==0&&enemies.Count==0){won=true;finished=true;return;}phase="discard";}
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            ValidateTurn(player);if(phase=="discard")
            {var result=new List<Action>();for(int mask=0;mask<(1<<hand.Count);mask++){int[] ids=Enumerable.Range(0,hand.Count).Where(index=>(mask&(1<<index))!=0).Select(index=>hand[index].Id).ToArray();result.Add(new Action("discard_cards",value:string.Join(",",ids)));}return result;}
            var actions=new List<Action>();for(int enemy=0;enemy<row.Count;enemy++)foreach(int[] ids in CaptureSubsets(row[enemy]))actions.Add(new Action("capture",target:enemy,value:string.Join(",",ids)));
            if(actions.Count>0)return actions;int right=row.Count-1;if(right>=0&&row[right].Rank<=10)foreach(CaptureCard card in hand.Where(card=>!card.Joker&&card.Card!.Value.Rank<=10))actions.Add(new Action("enemy_capture",value:card.Id.ToString(CultureInfo.InvariantCulture)));
            if(right>=0&&row.Any(card=>card.Rank<=10)){CaptureCard[] low=hand.Where(card=>!card.Joker&&card.Card!.Value.Rank<=10).ToArray();for(int a=0;a<low.Length-1;a++)for(int b=a+1;b<low.Length;b++)foreach(int enemy in Enumerable.Range(0,row.Count).Where(index=>row[index].Rank<=10))actions.Add(new Action("sacrifice",target:enemy,value:low[a].Id+","+low[b].Id));}
            if(actions.Count==0)actions.Add(new Action("game_over"));return actions;
        }
        private IEnumerable<int[]> CaptureSubsets(Card enemy)
        {
            var eligible=hand.Where(card=>card.Joker||card.Card!.Value.Suit==enemy.Suit).ToArray();var result=new List<int[]>();
            for(int mask=1;mask<(1<<eligible.Length);mask++)
            {CaptureCard[] selected=Enumerable.Range(0,eligible.Length).Where(index=>(mask&(1<<index))!=0).Select(index=>eligible[index]).ToArray();Card[] sameSuit=hand.Where(card=>!card.Joker&&card.Card!.Value.Suit==enemy.Suit).Select(card=>card.Card!.Value).ToArray();if(selected.Any(card=>card.Joker)&&sameSuit.Length==0)continue;int copy=sameSuit.Select(Strength).DefaultIfEmpty(0).Max();int total=selected.Sum(card=>card.Joker?copy:Strength(card.Card!.Value));if(total>=Strength(enemy))result.Add(selected.Select(card=>card.Id).ToArray());}
            return result.OrderBy(ids=>ids.Length).ThenBy(ids=>ids.Sum());
        }
        public override void Apply(Action action)
        {
            ValidateTurn(null);Guard.Legal(action,LegalActions());TurnCount++;
            if(phase=="discard")
            {MoveHandToDiscard(ParseIds(action.Value!));DrawToFour();phase="capture";return;}
            if(action.Kind=="game_over"){finished=true;won=false;return;}
            if(action.Kind=="capture")
            {personalDiscard.Add(new CaptureCard(nextId++,row[action.Target!.Value]));row.RemoveAt(action.Target.Value);MoveHandToDiscard(ParseIds(action.Value!));}
            else if(action.Kind=="enemy_capture")
            {int id=int.Parse(action.Value!,CultureInfo.InvariantCulture);hand.RemoveAll(card=>card.Id==id);row.RemoveAt(row.Count-1);}
            else
            {MoveHandOut(ParseIds(action.Value!));Card enemy=row[action.Target!.Value];row.RemoveAt(action.Target.Value);enemies.Insert(0,enemy);}
            StartRound();
        }
        private void DrawToFour(){while(hand.Count<4&&(personalDeck.Count>0||personalDiscard.Count>0)){if(personalDeck.Count==0){personalDeck=personalDiscard.ToList();personalDiscard.Clear();rng.Shuffle(personalDeck);}hand.Add(Pop(personalDeck));}}
        private void MoveHandToDiscard(IEnumerable<int> ids){int[] values=ids.ToArray();foreach(CaptureCard card in hand.Where(card=>values.Contains(card.Id)).ToArray()){hand.Remove(card);personalDiscard.Add(card);}}
        private void MoveHandOut(IEnumerable<int> ids){int[] values=ids.ToArray();hand.RemoveAll(card=>values.Contains(card.Id));}
        private static int[] ParseIds(string value)=>string.IsNullOrEmpty(value)?Array.Empty<int>():value.Split(',').Select(int.Parse).ToArray();
        public override Action ChooseCpuAction(int player,DeterministicRandom random,int difficulty=1)
        {IReadOnlyList<Action> actions=LegalActions(player);if(phase=="discard")return actions[0];Action[] captures=actions.Where(action=>action.Kind=="capture").OrderBy(action=>ParseIds(action.Value!).Length).ToArray();if(captures.Length>0)return captures[0];return actions.First();}
        public override bool IsTerminal=>finished;
        public override GameResult Result(){if(!finished)throw new InvalidOperationException("Game is not over.");int remaining=enemies.Concat(row).Count(card=>card.Rank==1||card.Rank>=11);return new GameResult(won?new[]{0}:Array.Empty<int>(),new[]{won?1d:-remaining},won?"enemy deck cleared":"uncapturable court card",TurnCount,new Dictionary<string,object>{{"remaining_high_cards",remaining}});}
        public override string View(int? player=null)=>$"phase={phase} enemies={enemies.Count} row=[{string.Join(" ",row)}] personal_deck={personalDeck.Count} discard={personalDiscard.Count}\nhand: {string.Join(" ",hand)}";
        private static T Pop<T>(List<T> cards){T card=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return card;}
        public static void Register(GameRegistry registry)=>registry.Register(new GameInfo("card_capture","Card Capture",1,1,"deck-building-capture","2～4とジョーカーの個人デッキを循環させ、同スート合計で敵を捕獲し、全A・絵札を含む敵デッキの完走を目指す。","gokurakism/Card Capture"),(p,r,o)=>new CardCaptureGame(p,r));
    }

    public sealed class ScoundrelGame : GameBase
    {
        private readonly List<Card> dungeon;private readonly List<Card> room=new List<Card>();private int health=20;private Card? weapon;private int weaponLastMonster=int.MaxValue;private int selected;private bool potionUsed;private bool lastAvoided;private Card? lastDungeonCard;private bool finished;private int finalScore;
        public override string GameId=>"scoundrel";public override string Name=>"Scoundrel（悪党）";
        public ScoundrelGame(int players,DeterministicRandom rng){Players=1;dungeon=Cards.Shuffled(Cards.StandardDeck().Where(card=>!(card.Suit==Suit.Hearts||card.Suit==Suit.Diamonds)||card.Rank<=10&&card.Rank!=1),rng);FillRoom();}
        private static int Strength(Card card)=>card.Rank==1?14:card.Rank;
        private void FillRoom(){while(room.Count<4&&dungeon.Count>0){Card card=Pop(dungeon);room.Add(card);if(dungeon.Count==0)lastDungeonCard=card;}if(room.Count<4&&dungeon.Count==0)FinishDungeon();}
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            ValidateTurn(player);var result=new List<Action>();if(selected==0&&!lastAvoided&&room.Count==4)result.Add(new Action("avoid"));for(int index=0;index<room.Count;index++)
            {Card card=room[index];if(card.Suit==Suit.Diamonds)result.Add(new Action("equip",target:index));else if(card.Suit==Suit.Hearts)result.Add(new Action("potion",target:index));else{result.Add(new Action("fight_bare",target:index));if(weapon.HasValue&&Strength(card)<weaponLastMonster)result.Add(new Action("fight_weapon",target:index));}}return result;
        }
        public override void Apply(Action action)
        {
            ValidateTurn(null);Guard.Legal(action,LegalActions());TurnCount++;if(action.Kind=="avoid"){dungeon.InsertRange(0,room);room.Clear();lastAvoided=true;FillRoom();return;}
            Card card=room[action.Target!.Value];room.RemoveAt(action.Target.Value);selected++;
            if(action.Kind=="equip"){weapon=card;weaponLastMonster=int.MaxValue;}
            else if(action.Kind=="potion"){if(!potionUsed){health=Math.Min(20,health+Strength(card));potionUsed=true;}}
            else if(action.Kind=="fight_bare")health-=Strength(card);
            else{health-=Math.Max(0,Strength(card)-Strength(weapon!.Value));weaponLastMonster=Strength(card);}
            if(health<=0){finished=true;finalScore=health-dungeon.Concat(room).Where(monster=>monster.Suit==Suit.Clubs||monster.Suit==Suit.Spades).Sum(Strength);return;}
            if(selected>=3){selected=0;potionUsed=false;lastAvoided=false;FillRoom();}
        }
        private void FinishDungeon(){if(finished)return;finalScore=health;
            if(health==20&&lastDungeonCard.HasValue&&lastDungeonCard.Value.Suit==Suit.Hearts)finalScore+=Strength(lastDungeonCard.Value);finished=true;}
        public override Action ChooseCpuAction(int player,DeterministicRandom random,int difficulty=1)
        {IReadOnlyList<Action> actions=LegalActions(player);Action[] potions=actions.Where(action=>action.Kind=="potion"&&!potionUsed&&health<15).ToArray();if(potions.Length>0)return potions[0];Action[] equips=actions.Where(action=>action.Kind=="equip").OrderByDescending(action=>Strength(room[action.Target!.Value])).ToArray();if(equips.Length>0)return equips[0];Action[] weapons=actions.Where(action=>action.Kind=="fight_weapon").OrderBy(action=>Strength(room[action.Target!.Value])).ToArray();if(weapons.Length>0)return weapons[0];return actions.First(action=>action.Kind!="avoid");}
        public override bool IsTerminal=>finished;
        public override GameResult Result(){if(!finished)throw new InvalidOperationException("Game is not over.");return new GameResult(finalScore>0?new[]{0}:Array.Empty<int>(),new[]{(double)finalScore},finalScore>0?"dungeon cleared":"health depleted",TurnCount);}
        public override string View(int? player=null)=>$"health={health} weapon={(weapon.HasValue?weapon.ToString():"-")} last_monster={(weaponLastMonster==int.MaxValue?"-":weaponLastMonster.ToString(CultureInfo.InvariantCulture))} dungeon={dungeon.Count} selected={selected} can_avoid={!lastAvoided}\nroom: {string.Join(" ",room)}";
        private static Card Pop(List<Card> cards){Card card=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return card;}
        public static void Register(GameRegistry registry)=>registry.Register(new GameInfo("scoundrel","Scoundrel（悪党）",1,1,"solitaire-dungeon","黒札をモンスター、ダイヤを武器、ハートを1室1回の回復として、4枚の部屋から3枚ずつ処理する。","gokurakism/Scoundrel"),(p,r,o)=>new ScoundrelGame(p,r));
    }

    public sealed class GosankyoGame : GameBase
    {
        private const int Self=0,Left=1,Right=2;private readonly DeterministicRandom rng;private readonly HashSet<int> usedBids=new HashSet<int>();private List<List<Card>> hands=new List<List<Card>>();private readonly List<Tuple<int,Card>> trick=new List<Tuple<int,Card>>();private int round;private int bid;private int selfTricks;private int currentSeat;private string phase="bid";private bool finished;private bool success;
        public override string GameId=>"gosankyo";public override string Name=>"御三卿";
        public GosankyoGame(int players,DeterministicRandom rng){Players=1;this.rng=rng;StartRound();}
        private static int Strength(Card card)=>card.Rank==1?14:card.Rank;
        private void StartRound()
        {
            round++;List<Card> deck=Cards.Shuffled(Cards.StandardDeck(new[]{1,6,7,8,9,10,11,12,13}),rng);hands=Enumerable.Range(0,3).Select(_=>new List<Card>()).ToList();
            while(deck.Count>0){hands[Left].Add(Pop(deck));hands[Right].Add(Pop(deck));hands[Self].Add(Pop(deck));}trick.Clear();Card opening=hands[Right][hands[Right].Count-1];hands[Right].RemoveAt(hands[Right].Count-1);trick.Add(Tuple.Create(Right,opening));currentSeat=Self;selfTricks=0;phase="bid";
        }
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            ValidateTurn(player);if(phase=="bid")return Enumerable.Range(4,4).Where(value=>!usedBids.Contains(value)).Select(value=>new Action("bid",value:value.ToString(CultureInfo.InvariantCulture))).ToArray();
            if(currentSeat!=Self)return new[]{new Action(trick.Count==0?"reveal_lead":"reveal_follow")};IEnumerable<Card> cards=hands[Self];if(trick.Count>0){Suit led=trick[0].Item2.Suit;Card[] follow=cards.Where(card=>card.Suit==led).ToArray();if(follow.Length>0)cards=follow;}return cards.Select(card=>new Action("play",card)).ToArray();
        }
        public override void Apply(Action action)
        {
            ValidateTurn(null);Guard.Legal(action,LegalActions());TurnCount++;
            if(phase=="bid"){bid=int.Parse(action.Value!,CultureInfo.InvariantCulture);usedBids.Add(bid);phase="play";return;}
            Card card;if(currentSeat==Self){card=action.Card!.Value;hands[Self].Remove(card);}else
            {IEnumerable<Card> eligible=hands[currentSeat];if(trick.Count>0){Suit led=trick[0].Item2.Suit;Card[] follow=eligible.Where(value=>value.Suit==led).ToArray();if(follow.Length>0)eligible=follow;}else{Suit[] suits=eligible.Select(value=>value.Suit).Distinct().ToArray();Suit chosen=rng.Choice(suits);eligible=eligible.Where(value=>value.Suit==chosen);}Card[] choices=eligible.ToArray();card=rng.Choice(choices);hands[currentSeat].Remove(card);}trick.Add(Tuple.Create(currentSeat,card));
            if(trick.Count<3){currentSeat=(currentSeat+1)%3;return;}Suit lead=trick[0].Item2.Suit;int winner=trick.Where(item=>item.Item2.Suit==lead).OrderByDescending(item=>Strength(item.Item2)).First().Item1;if(winner==Self)selfTricks++;trick.Clear();if(hands.All(hand=>hand.Count==0)){EndRound();return;}currentSeat=winner;
        }
        private void EndRound(){if(selfTricks!=bid){finished=true;success=false;return;}if(round>=4){finished=true;success=true;return;}StartRound();}
        public override Action ChooseCpuAction(int player,DeterministicRandom random,int difficulty=1)
        {IReadOnlyList<Action> actions=LegalActions(player);if(phase=="bid"){int estimate=Math.Max(4,Math.Min(7,hands[Self].Count(card=>Strength(card)>=11)));return actions.OrderBy(action=>Math.Abs(int.Parse(action.Value!,CultureInfo.InvariantCulture)-estimate)).First();}if(currentSeat!=Self)return actions[0];return selfTricks<bid?actions.OrderByDescending(action=>Strength(action.Card!.Value)).First():actions.OrderBy(action=>Strength(action.Card!.Value)).First();}
        public override bool IsTerminal=>finished;
        public override GameResult Result(){if(!finished)throw new InvalidOperationException("Game is not over.");return new GameResult(success?new[]{0}:Array.Empty<int>(),new[]{(double)(success?4:round-1)},success?"four exact bids":"exact bid failed",TurnCount,new Dictionary<string,object>{{"rounds_succeeded",success?4:round-1}});}
        public override string View(int? player=null){string Opponent(int seat)=>string.Join(" ",hands[seat].GroupBy(card=>card.Suit).Select(group=>Card.SuitCode(group.Key)+":"+group.Count()));return $"round={round}/4 phase={phase} bid={bid} tricks={selfTricks} seat={currentSeat} used=[{string.Join(",",usedBids)}] left=[{Opponent(Left)}] right=[{Opponent(Right)}]\ntrick: {string.Join(" ",trick.Select(item=>item.Item2))}\nyour hand: {string.Join(" ",hands[Self])}";}
        private static Card Pop(List<Card> cards){Card card=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return card;}
        public static void Register(GameRegistry registry)=>registry.Register(new GameInfo("gosankyo","御三卿",1,1,"solo-exact-trick","スートだけ見える仮想相手2人と36枚・12トリックを行い、4～7の各ビッドを1回ずつ連続成功させる。","gokurakism/御三卿"),(p,r,o)=>new GosankyoGame(p,r));
    }
}
