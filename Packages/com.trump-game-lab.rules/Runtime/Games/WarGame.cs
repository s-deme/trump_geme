using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Games
{
    public sealed class WarGame:GameBase
    {
        private readonly List<Queue<Card>> piles;private readonly int downCards,maxTurns;private readonly DeterministicRandom rng;private int? winner;private string reason="";
        public override string GameId=>"war";public override string Name=>"戦争";
        public WarGame(int players,DeterministicRandom rng,IReadOnlyDictionary<string,string> options)
        {
            Players=players;this.rng=rng;downCards=options.Integer("war_down_cards",1);maxTurns=options.Integer("max_turns",0);
            List<Card> deck=Cards.Shuffled(Cards.StandardDeck(),rng);
            piles=Enumerable.Range(0,players).Select(p=>new Queue<Card>(deck.Where((c,i)=>i%players==p))).ToList();
        }
        private static int Strength(Card card)=>card.Rank==1?14:card.Rank;
        public override IReadOnlyList<Action> LegalActions(int? player=null){ValidateTurn(player);return new[]{new Action("battle")};}
        public override void Apply(Action action)
        {
            ValidateTurn(null);if(action.Kind!="battle")throw new ArgumentException("War accepts only battle.");
            List<int> participants=Enumerable.Range(0,Players).Where(i=>piles[i].Count>0).ToList();var pot=new List<Card>();
            while(participants.Count>1)
            {
                var face=new List<Tuple<int,Card>>();
                foreach(int p in participants)if(piles[p].Count>0){Card c=piles[p].Dequeue();pot.Add(c);face.Add(Tuple.Create(p,c));}
                if(face.Count==0)break;int high=face.Max(x=>Strength(x.Item2));int[] tied=face.Where(x=>Strength(x.Item2)==high).Select(x=>x.Item1).ToArray();
                if(tied.Length==1){Award(tied[0],pot);break;}
                var nextParticipants=new List<int>();
                foreach(int p in participants)
                {
                    if(piles[p].Count==0)continue;int down=Math.Min(downCards,Math.Max(0,piles[p].Count-1));
                    for(int i=0;i<down;i++)pot.Add(piles[p].Dequeue());if(piles[p].Count>0)nextParticipants.Add(p);
                }
                participants=nextParticipants;
                if(participants.Count==1){Award(participants[0],pot);break;}
            }
            TurnCount++;int[] alive=Enumerable.Range(0,Players).Where(i=>piles[i].Count>0).ToArray();
            if(alive.Length==1){winner=alive[0];reason="all cards captured";}
            else if(maxTurns>0&&TurnCount>=maxTurns){winner=Enumerable.Range(0,Players).OrderByDescending(i=>piles[i].Count).First();reason="local turn limit; most cards";}
        }
        private void Award(int player,List<Card> pot){rng.Shuffle(pot);foreach(Card card in pot)piles[player].Enqueue(card);}
        public override bool IsTerminal=>winner.HasValue;
        public override GameResult Result(){if(!winner.HasValue)throw new InvalidOperationException("Game is not over.");
            return new GameResult(new[]{winner.Value},piles.Select(p=>(double)p.Count),reason,TurnCount);}
        public override string View(int? player=null)=>$"戦争 turn={TurnCount} cards=[{string.Join(",",piles.Select(p=>p.Count))}]";
        public static void Register(GameRegistry registry)=>registry.Register(
            new GameInfo("war","戦争",2,4,"comparison","同時にカードを公開し、最も強いカードが場札を獲得する。","traditional",
                new Dictionary<string,string>{{"war_down_cards","同値時に伏せる枚数（既定1）"},{"max_turns","明示時だけ使うローカル打切り（既定なし）"}}),
            (players,rng,options)=>new WarGame(players,rng,options));
    }
}
