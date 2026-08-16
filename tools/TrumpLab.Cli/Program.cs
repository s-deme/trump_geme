using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using TrumpLab;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if(args.Length==0)throw new ArgumentException("Usage: trump-lab <list|catalogue|simulate|compare|play>");
            switch(args[0])
            {
                case "list":ListGames();return 0;
                case "catalogue":Catalogue(args.Skip(1).Contains("--pending"));return 0;
                case "simulate":return Simulate(args.Skip(1).ToArray());
                case "compare":return Compare(args.Skip(1).ToArray());
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
        int difficulty=IntOption(args,null,"--difficulty",Simulator.SupportedDifficulty);
        SimulationReport report=Simulator.Simulate(game,games,players,seed,options,difficulty);
        Console.WriteLine($"game: {report.GameId}\ncompleted: {report.Completed}/{report.Games}\naverage turns: {report.AverageTurns:F2}");
        Console.WriteLine("winner counts: {"+string.Join(", ",report.WinnerCounts.Select(x=>x.Key+": "+x.Value))+"}");
        Console.WriteLine("draws: "+report.Draws);foreach(string failure in report.Failures)Console.WriteLine("FAIL "+failure);
        return report.Failures.Count>0?1:0;
    }
    private static int Compare(string[] args)
    {
        int games=IntOption(args,"-n","--games",100),seed=IntOption(args,null,"--seed",1);
        int difficulty=IntOption(args,null,"--difficulty",Simulator.SupportedDifficulty);
        string[] requested=RepeatedStringOption(args,"--game");
        bool all=args.Contains("--all"),pending=args.Contains("--pending");
        if(new[]{requested.Length>0,all,pending}.Count(selected=>selected)>1)
            throw new ArgumentException("Use only one of --game, --all, or --pending.");
        IEnumerable<Candidate> candidates=GameCatalogue.Candidates();
        if(requested.Length>0)
        {
            var known=new HashSet<string>(candidates.Where(c=>c.ImplementationId!=null)
                .Select(c=>c.ImplementationId!),StringComparer.Ordinal);
            string? unknown=requested.FirstOrDefault(id=>!known.Contains(id));
            if(unknown!=null)throw new ArgumentException("Unknown game id: "+unknown);
        }
        string[] gameIds=requested.Length>0?requested:
            all?candidates.Select(c=>c.ImplementationId!).ToArray():
            pending?candidates.Where(c=>c.Status!=CandidateStatus.Verified)
                .Select(c=>c.ImplementationId!).ToArray():
            candidates.Where(c=>c.Status==CandidateStatus.Verified)
                .Select(c=>c.ImplementationId!).ToArray();
        IReadOnlyList<ComparisonRow> rows=Simulator.Compare(gameIds,games,seed,difficulty);
        string format=StringOption(args,"--format","table").ToLowerInvariant();
        string output=format=="table"?ComparisonTable(rows):
            format=="csv"?ComparisonCsv(rows):
            format=="json"?ComparisonJson(rows):
            throw new ArgumentException("format must be table, csv, or json");
        string? outputPath=NullableStringOption(args,"--output");
        if(outputPath==null)Console.WriteLine(output);
        else File.WriteAllText(outputPath,output+Environment.NewLine,new UTF8Encoding(false));
        return rows.Any(row=>row.Simulation.Failures.Count>0)?1:0;
    }
    private static string ComparisonTable(IReadOnlyList<ComparisonRow> rows)
    {
        var output=new StringBuilder();
        output.AppendLine("game                 done      avg turns  draws  ms/100   perf  seat wins");
        foreach(ComparisonRow row in rows)
        {
            SimulationReport report=row.Simulation;
            string winners=string.Join(",",report.WinnerCounts.OrderBy(item=>item.Key)
                .Select(item=>"p"+item.Key+"="+item.Value));
            output.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-20} {1,4}/{2,-4} {3,10:F2} {4,6} {5,8:F1} {6,6}  {7}",
                report.GameId,report.Completed,report.Games,report.AverageTurns,report.Draws,
                row.MillisecondsPerHundredGames,row.MeetsPerformanceTarget?"pass":"fail",winners));
        }
        return output.ToString().TrimEnd();
    }
    private static string ComparisonCsv(IReadOnlyList<ComparisonRow> rows)
    {
        var output=new StringBuilder("game_id,games,completed,average_turns,draws,elapsed_ms,ms_per_100,performance_target,winner_counts,failures\n");
        foreach(ComparisonRow row in rows)
        {
            SimulationReport report=row.Simulation;
            string winners=string.Join(";",report.WinnerCounts.OrderBy(item=>item.Key)
                .Select(item=>item.Key+":"+item.Value));
            output.AppendLine(string.Join(",",new[]{Csv(report.GameId),report.Games.ToString(CultureInfo.InvariantCulture),
                report.Completed.ToString(CultureInfo.InvariantCulture),report.AverageTurns.ToString("F2",CultureInfo.InvariantCulture),
                report.Draws.ToString(CultureInfo.InvariantCulture),row.ElapsedMilliseconds.ToString("F2",CultureInfo.InvariantCulture),
                row.MillisecondsPerHundredGames.ToString("F2",CultureInfo.InvariantCulture),
                row.MeetsPerformanceTarget?"pass":"fail",Csv(winners),Csv(string.Join(";",report.Failures))}));
        }
        return output.ToString().TrimEnd();
    }
    private static string ComparisonJson(IReadOnlyList<ComparisonRow> rows) =>
        JsonSerializer.Serialize(rows.Select(row=>new
        {
            game_id=row.Simulation.GameId,
            games=row.Simulation.Games,
            completed=row.Simulation.Completed,
            average_turns=row.Simulation.AverageTurns,
            draws=row.Simulation.Draws,
            elapsed_ms=row.ElapsedMilliseconds,
            ms_per_100=row.MillisecondsPerHundredGames,
            performance_target=row.MeetsPerformanceTarget?"pass":"fail",
            winner_counts=row.Simulation.WinnerCounts,
            failures=row.Simulation.Failures
        }),new JsonSerializerOptions{WriteIndented=true});
    private static string Csv(string value) => "\""+value.Replace("\"","\"\"")+"\"";
    private static int Play(string[] args)
    {
        if(args.Length==0)throw new ArgumentException("play requires a game id");int seed=IntOption(args,null,"--seed",1);
        IGame game=BuiltInGames.Registry.Create(args[0],NullableIntOption(args,"-p","--players"),seed,Options(args));
        var rng=new DeterministicRandom(seed+99991);int difficulty=IntOption(args,null,"--difficulty",Simulator.SupportedDifficulty);
        Simulator.ValidateDifficulty(game.GameId,difficulty);
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
    private static string StringOption(string[] args,string longName,string fallback) =>
        NullableStringOption(args,longName)??fallback;
    private static string? NullableStringOption(string[] args,string longName)
    {for(int i=0;i<args.Length;i++)if(args[i]==longName)
        {if(i+1>=args.Length)throw new ArgumentException(longName+" requires a value");return args[i+1];}return null;}
    private static string[] RepeatedStringOption(string[] args,string longName)
    {var values=new List<string>();for(int i=0;i<args.Length;i++)if(args[i]==longName)
        {if(i+1>=args.Length)throw new ArgumentException(longName+" requires a value");values.Add(args[++i]);}return values.ToArray();}
}
