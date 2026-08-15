using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TrumpLab;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if(args.Length==0)throw new ArgumentException("Usage: trump-lab <list|catalogue|simulate|play>");
            switch(args[0])
            {
                case "list":ListGames();return 0;
                case "catalogue":Catalogue(args.Skip(1).Contains("--pending"));return 0;
                case "simulate":return Simulate(args.Skip(1).ToArray());
                case "play":return Play(args.Skip(1).ToArray());
                default:throw new ArgumentException("Unknown command: "+args[0]);
            }
        }
        catch(Exception exception){Console.Error.WriteLine(exception.Message);return 2;}
    }
    private static void ListGames()
    {
        foreach(GameInfo info in BuiltInGames.Registry.All())
        {
            Console.WriteLine($"{info.GameId,-16} {info.Name,-16} {info.MinPlayers}-{info.MaxPlayers}人  {info.Category}");
            Console.WriteLine("  "+info.Description);
            if(info.Options.Count>0)Console.WriteLine("  options: "+string.Join(", ",info.Options.Keys));
        }
    }
    private static void Catalogue(bool pending)
    {
        Candidate[] rows=GameCatalogue.Candidates().Where(c=>!pending||c.Status!=CandidateStatus.Verified).ToArray();
        foreach(Candidate row in rows)Console.WriteLine(
            $"{(row.ImplementationId==null?"pending":"implemented:"+row.ImplementationId),-28} {row.Status,-13} {row.Players,-10} {row.Name}");
        Console.WriteLine($"\n合計 {rows.Length} 件");
    }
    private static int Simulate(string[] args)
    {
        if(args.Length==0)throw new ArgumentException("simulate requires a game id");
        string game=args[0];int games=IntOption(args,"-n","--games",100),seed=IntOption(args,null,"--seed",1);
        int? players=NullableIntOption(args,"-p","--players");Dictionary<string,string> options=Options(args);
        SimulationReport report=Simulator.Simulate(game,games,players,seed,options);
        Console.WriteLine($"game: {report.GameId}\ncompleted: {report.Completed}/{report.Games}\naverage turns: {report.AverageTurns:F2}");
        Console.WriteLine("winner counts: {"+string.Join(", ",report.WinnerCounts.Select(x=>x.Key+": "+x.Value))+"}");
        Console.WriteLine("draws: "+report.Draws);foreach(string failure in report.Failures)Console.WriteLine("FAIL "+failure);
        return report.Failures.Count>0?1:0;
    }
    private static int Play(string[] args)
    {
        if(args.Length==0)throw new ArgumentException("play requires a game id");int seed=IntOption(args,null,"--seed",1);
        IGame game=BuiltInGames.Registry.Create(args[0],NullableIntOption(args,"-p","--players"),seed,Options(args));
        var rng=new DeterministicRandom(seed+99991);int difficulty=IntOption(args,null,"--difficulty",1);
        while(!game.IsTerminal)
        {
            Console.WriteLine("\n"+game.View(0));IReadOnlyList<TrumpLab.Action> actions=game.LegalActions();TrumpLab.Action action;
            if(game.CurrentPlayer==0){for(int i=0;i<actions.Count;i++)Console.WriteLine($"  {i}: {actions[i]}");
                while(true){Console.Write("> ");if(int.TryParse(Console.ReadLine(),out int selected)&&selected>=0&&selected<actions.Count){action=actions[selected];break;}
                    Console.WriteLine("番号を選択してください。");}}
            else{action=game.ChooseCpuAction(game.CurrentPlayer,rng,difficulty);Console.WriteLine($"CPU{game.CurrentPlayer}: {action}");}
            game.Apply(action);
        }
        Console.WriteLine("\n"+game.View(0));GameResult result=game.Result();
        Console.WriteLine($"winners=[{string.Join(",",result.Winners)}] scores=[{string.Join(",",result.Scores)}] reason={result.Reason} turns={result.Turns}");return 0;
    }
    private static int IntOption(string[] args,string? shortName,string longName,int fallback)
    {int? value=NullableIntOption(args,shortName,longName);return value??fallback;}
    private static int? NullableIntOption(string[] args,string? shortName,string longName)
    {for(int i=0;i<args.Length-1;i++)if(args[i]==longName||(shortName!=null&&args[i]==shortName))return int.Parse(args[i+1],CultureInfo.InvariantCulture);return null;}
    private static Dictionary<string,string> Options(string[] args)
    {var values=new Dictionary<string,string>();for(int i=0;i<args.Length-1;i++)if(args[i]=="-o"||args[i]=="--option")
        {string[] pair=args[++i].Split(new[]{'='},2);if(pair.Length!=2)throw new ArgumentException("option must be key=value");values[pair[0]]=pair[1];}return values;}
}
