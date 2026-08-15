using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    public sealed class OldMaidGame:GameBase
    {
        private readonly DeterministicRandom rng;private readonly List<List<Card>> hands;private int? loser;
        public override string GameId=>"old_maid";public override string Name=>"ババ抜き";
        public OldMaidGame(int players,DeterministicRandom rng,IReadOnlyDictionary<string,string> options)
        {
            Players=players;this.rng=rng;Suit omitted=Card.ParseSuit(options.Text("omitted_queen_suit","C"));
            List<Card> deck=Cards.Shuffled(Cards.StandardDeck().Where(c=>!(c.Rank==12&&c.Suit==omitted)),rng);
            hands=Enumerable.Range(0,players).Select(_=>new List<Card>()).ToList();
            for(int i=0;i<deck.Count;i++)hands[i%players].Add(deck[i]);
            for(int i=0;i<players;i++){DiscardPairs(i);rng.Shuffle(hands[i]);}
            CurrentPlayer=NextWithCards(-1);
        }
        private void DiscardPairs(int player)
        {
            hands[player]=hands[player].GroupBy(c=>c.Rank).Where(g=>g.Count()%2==1).Select(g=>g.Last()).ToList();
        }
        private int NextWithCards(int player)
        {for(int offset=1;offset<=Players;offset++){int c=(player+offset)%Players;if(hands[c].Count>0)return c;}return player;}
        private int Target(int player)=>NextWithCards(player);
        public override IReadOnlyList<Action> LegalActions(int? player=null)
        {
            int actual=ValidateTurn(player),target=Target(actual);if(target==actual)return Array.Empty<Action>();
            return Enumerable.Range(0,hands[target].Count).Select(i=>new Action("draw",target:target,value:i.ToString())).ToArray();
        }
        public override void Apply(Action action)
        {
            int player=ValidateTurn(null);Guard.Legal(action,LegalActions(player));int target=action.Target!.Value,index=int.Parse(action.Value!);
            Card card=hands[target][index];hands[target].RemoveAt(index);hands[player].Add(card);DiscardPairs(player);rng.Shuffle(hands[player]);TurnCount++;
            int[] alive=Enumerable.Range(0,Players).Where(i=>hands[i].Count>0).ToArray();
            if(alive.Length==1&&hands.Sum(h=>h.Count)==1)loser=alive[0];else CurrentPlayer=NextWithCards(player);
        }
        public override bool IsTerminal=>loser.HasValue;
        public override GameResult Result()
        {if(!loser.HasValue)throw new InvalidOperationException("Game is not over.");
            return new GameResult(Enumerable.Range(0,Players).Where(i=>i!=loser.Value),
                Enumerable.Range(0,Players).Select(i=>i==loser.Value?-1.0:1.0),
                $"player {loser.Value} holds old maid",TurnCount);}
        public override string View(int? player=null){int viewer=player??CurrentPlayer;
            return $"hands=[{string.Join(",",hands.Select(h=>h.Count))}] next target=P{Target(CurrentPlayer)}\n"+
                $"your hand: {string.Join(" ",hands[viewer])}";}
        public static void Register(GameRegistry registry)=>registry.Register(
            new GameInfo("old_maid","ババ抜き",2,6,"matching","同ランクのペアを捨て、最後の1枚を持つプレイヤーが負ける。","traditional",
                new Dictionary<string,string>{{"omitted_queen_suit","除外するQのスート C/D/H/S（既定C）"}}),
            (players,rng,options)=>new OldMaidGame(players,rng,options));
    }
}
