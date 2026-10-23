namespace BetterMalodyV.Shared;

public static class GameUpdateProtocol
{
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
