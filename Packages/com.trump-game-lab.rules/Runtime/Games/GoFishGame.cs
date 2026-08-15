using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    public sealed class GoFishGame : GameBase
    {
        private readonly List<List<Card>> hands;
        private readonly List<Card> stock;
        private readonly int[] books;
        public override string GameId=>"go_fish";
        public override string Name=>"ゴーフィッシュ";
        public GoFishGame(int players,DeterministicRandom rng)
        {
            Players=players;List<Card> deck=Cards.Shuffled(Cards.StandardDeck(),rng);
            hands=Enumerable.Range(0,players).Select(_=>new List<Card>()).ToList();
            int size=players==2?7:5;
            for(int round=0;round<size;round++)foreach(List<Card> hand in hands)hand.Add(Pop(deck));
            stock=deck;books=new int[players];
            for(int i=0;i<players;i++)RemoveBooks(i);
        }
        private void RemoveBooks(int player)
        {
            foreach(int rank in hands[player].GroupBy(c=>c.Rank).Where(g=>g.Count()==4).Select(g=>g.Key).ToArray())
            {hands[player].RemoveAll(c=>c.Rank==rank);books[player]++;}
        }
        private int NextPlayer(int player)
        {
            for(int offset=1;offset<=Players;offset++){int candidate=(player+offset)%Players;
                if(hands[candidate].Count>0||stock.Count>0)return candidate;}
            return player;
        }
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player);
            if(hands[actual].Count==0)return stock.Count>0?new[]{new Action("draw")}:Array.Empty<Action>();
            int[] ranks=hands[actual].Select(c=>c.Rank).Distinct().OrderBy(v=>v).ToArray();
            var actions=new List<Action>();
            for(int target=0;target<Players;target++)if(target!=actual&&hands[target].Count>0)
                foreach(int rank in ranks)actions.Add(new Action("ask",target:target,value:rank.ToString()));
            if(actions.Count==0&&stock.Count>0)actions.Add(new Action("draw"));
            return actions;
        }
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));bool again=false;
            if(action.Kind=="draw")hands[player].Add(Pop(stock));
            else
            {
                int target=action.Target!.Value,rank=int.Parse(action.Value!);
                List<Card> taken=hands[target].Where(c=>c.Rank==rank).ToList();
                if(taken.Count>0){hands[target].RemoveAll(c=>c.Rank==rank);hands[player].AddRange(taken);again=true;}
                else if(stock.Count>0){Card card=Pop(stock);hands[player].Add(card);again=card.Rank==rank;}
            }
            RemoveBooks(player);if(hands[player].Count==0)again=false;TurnCount++;
            if(!again&&!IsTerminal)CurrentPlayer=NextPlayer(player);
        }
        public override Action ChooseCpuAction(int player,DeterministicRandom rng,int difficulty=1)
        {
            IReadOnlyList<Action> actions=LegalActions(player);if(actions.Count==1)return actions[0];
            var counts=hands[player].GroupBy(c=>c.Rank).ToDictionary(g=>g.Key,g=>g.Count());
            int best=counts.Values.Max();Action[] candidates=actions.Where(a=>counts[int.Parse(a.Value!)]==best).ToArray();
            return rng.Choice(candidates);
        }
        public override bool IsTerminal=>books.Sum()==13||(stock.Count==0&&hands.All(h=>h.Count==0));
        public override GameResult Result()
        {
            if(!IsTerminal)throw new InvalidOperationException("Game is not over.");int high=books.Max();
            return new GameResult(Enumerable.Range(0,Players).Where(i=>books[i]==high),
                books.Select(v=>(double)v),"most books",TurnCount);
        }
        public override string View(int? player=null){int viewer=player??CurrentPlayer;
            return $"stock={stock.Count} books=[{string.Join(",",books)}] hands=[{string.Join(",",hands.Select(h=>h.Count))}]\n"+
                $"your hand: {string.Join(" ",hands[viewer])}";}
        private static Card Pop(List<Card> cards){Card c=cards[cards.Count-1];cards.RemoveAt(cards.Count-1);return c;}
        public static void Register(GameRegistry registry)=>registry.Register(
            new GameInfo("go_fish","ゴーフィッシュ",2,5,"collection","相手に同じランクを要求し、4枚組を集める。","traditional"),
            (players,rng,options)=>new GoFishGame(players,rng));
    }
}
