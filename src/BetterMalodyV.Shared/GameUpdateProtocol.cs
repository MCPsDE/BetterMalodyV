namespace BetterMalodyV.Shared;

public enum GameUpdateStage { Checking, Downloading, Verifying, Ready, Failed, None }

public sealed record GameUpdateProgress(GameUpdateStage Stage, string Version="", long DownloadedBytes=0, long? TotalBytes=null)
{
    public double? Fraction => TotalBytes is > 0 ? Math.Clamp((double)DownloadedBytes/TotalBytes.Value,0,1) : null;
    public bool Visible => Stage is GameUpdateStage.Downloading or GameUpdateStage.Verifying or GameUpdateStage.Ready || Stage==GameUpdateStage.Failed && Version.Length>0;
}

public static class GameUpdateProtocol
{
    public static string ProgressPath(string gameRoot,int gamePid)=>Path.Combine(gameRoot,"BetterMalodyV",".updates",$"progress-{gamePid}.json");
    public static void WriteProgress(string gameRoot,int gamePid,GameUpdateProgress progress)
    {
        var path=ProgressPath(gameRoot,gamePid);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary=path+".tmp";
        File.WriteAllText(temporary,System.Text.Json.JsonSerializer.Serialize(progress));File.Move(temporary,path,true);
    }
    public static GameUpdateProgress? ReadProgress(string gameRoot,int gamePid)
    {
        var path=ProgressPath(gameRoot,gamePid);if(!File.Exists(path))return null;
        try {return System.Text.Json.JsonSerializer.Deserialize<GameUpdateProgress>(File.ReadAllText(path));}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException){return null;}
    }
    public static string GateName(string gameRoot)
    {
        var path=System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(gameRoot).TrimEnd('\\','/').ToUpperInvariant());
        using var hash=System.Security.Cryptography.SHA256.Create();
        return "Local\\BetterMalodyV-update-"+Convert.ToHexString(hash.ComputeHash(path));
    }
    public static string RequestPath(string gameRoot,int gamePid)=>Path.Combine(gameRoot,"BetterMalodyV",".updates",$"quit-{gamePid}.request");
    public static bool IsExpectedGame(string gameRoot,string? executable)=>executable is not null &&
        string.Equals(Path.GetFullPath(executable),Path.Combine(Path.GetFullPath(gameRoot),"Malody V.exe"),StringComparison.OrdinalIgnoreCase);
}
