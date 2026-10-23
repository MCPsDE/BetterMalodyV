using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace BetterMalodyV.Shared;

/// <summary>Validate the host's exact chart before replacing any local file.
/// Keep rollback data until native reload and hash validation succeed.</summary>
public sealed class SharedChartInstall : IDisposable
{
    private readonly string staging;
    private readonly List<(string Target,string? Backup)> changes=new();
    private bool complete;
    public string SongPath { get; private set; }="";
    public string ChartPath { get; private set; }="";
    private SharedChartInstall(string staging) { this.staging=staging; }
    public static SharedChartInstall Prepare(string zipPath,string chartRoot,string expectedHash,string? existingSong=null,string? replacedChart=null)
    {
        if(expectedHash.Length!=32 || !expectedHash.All(Uri.IsHexDigit)) throw new InvalidDataException("Invalid shared chart hash.");
        var stage=Path.Combine(Path.GetTempPath(),"BetterMalodyV-share-"+Guid.NewGuid().ToString("N"));
        var operation=new SharedChartInstall(stage);
        try
        {
            var extracted=Path.Combine(stage,"files");Directory.CreateDirectory(extracted);
            using var archive=ZipFile.OpenRead(zipPath);
            if(archive.Entries.Count>20000) throw new InvalidDataException("Too many shared files.");
            long total=0;var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var entry in archive.Entries)
            {
                var relative=entry.FullName.Replace('\\','/');
                if(relative.Length==0 || relative.EndsWith('/')) continue;
                if(relative.Split('/').Any(p=>p is ".." or "." || p.Contains(':')) || !names.Add(relative)) throw new InvalidDataException("Invalid shared file path.");
                var target=PathRules.GetSafeCombinedPath(extracted,relative);
                total+=entry.Length;if(total>2L*1024*1024*1024) throw new InvalidDataException("Shared chart archive is too large.");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);entry.ExtractToFile(target);
            }
            var charts=Directory.EnumerateFiles(extracted,"*",SearchOption.AllDirectories).Where(IsChart).ToArray();
            var matches=charts.Where(p=>MatchesHash(p,expectedHash)).ToArray();
            if(matches.Length!=1) throw new InvalidDataException("The shared package does not contain the host's exact chart.");
            var selected=matches[0];var selectedRelative=Path.GetRelativePath(extracted,selected);
            var parts=selectedRelative.Split(Path.DirectorySeparatorChar);
            // ShareChart includes exactly one outer song folder.
            if(parts.Length<2) throw new InvalidDataException("Shared package is missing its song folder.");
            var outer=Path.Combine(extracted,parts[0]);
            if(Directory.EnumerateFileSystemEntries(extracted).Any(p=>!string.Equals(p,outer,StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Shared package contains multiple song folders.");
            operation.SongPath=existingSong is null ? PathRules.GetSafeCombinedPath(chartRoot,parts[0]) : Path.GetFullPath(existingSong);
            operation.ChartPath=PathRules.GetSafeCombinedPath(operation.SongPath,Path.GetRelativePath(outer,selected));
            foreach(var file in Directory.EnumerateFiles(outer,"*",SearchOption.AllDirectories))
            {
                var relative=Path.GetRelativePath(outer,file);
                var target=PathRules.GetSafeCombinedPath(operation.SongPath,relative);
                RejectLinks(operation.SongPath,target);
                string? backup=null;
                if(File.Exists(target))
                {
                    backup=PathRules.GetSafeCombinedPath(Path.Combine(stage,"backup"),relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);File.Copy(target,backup);
                }
                operation.changes.Add((target,backup));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var temporary=target+".bm-new";
                try { File.Copy(file,temporary,true);File.Move(temporary,target,true); }
                finally { if(File.Exists(temporary))File.Delete(temporary); }
            }
            // A server chart can change filenames between versions. Retire only
            // the uniquely identified old chart, never another difficulty.
            if(replacedChart is not null && File.Exists(replacedChart) &&
                !operation.changes.Any(c=>string.Equals(c.Target,Path.GetFullPath(replacedChart),StringComparison.OrdinalIgnoreCase)))
            {
                var relative=Path.GetRelativePath(operation.SongPath,replacedChart);
                var target=PathRules.GetSafeCombinedPath(operation.SongPath,relative);
                if(!IsChart(target))throw new InvalidDataException("Replacement target is not a chart.");
                RejectLinks(operation.SongPath,target);
                var backup=PathRules.GetSafeCombinedPath(Path.Combine(stage,"backup-old-chart"),relative);
                Directory.CreateDirectory(Path.GetDirectoryName(backup)!);File.Copy(target,backup);
                operation.changes.Add((target,backup));File.Delete(target);
            }
            return operation;
        }
        catch { operation.Dispose();throw; }
    }
    public void Commit() { complete=true; }
    public void Dispose()
    {
        if(!complete)
            foreach(var (target,backup) in changes.AsEnumerable().Reverse())
            {
                if(backup is null) { if(File.Exists(target)) File.Delete(target); }
                else File.Copy(backup,target,true);
            }
        if(Directory.Exists(staging))Directory.Delete(staging,true);
    }
    public static bool MatchesHash(string path,string expected)
    {
        using var input=File.OpenRead(path);using var md5=MD5.Create();
        var hash=Convert.ToHexString(md5.ComputeHash(input)).ToLowerInvariant();
        if(hash.Equals(expected,StringComparison.OrdinalIgnoreCase))return true;
        if(Path.GetExtension(path).ToLowerInvariant() is not (".sm" or ".bms" or ".bme" or ".bml" or ".pms"))return false;
        for(var i=0;i<256;i++)
        {
            var combined=Encoding.UTF8.GetBytes($"{i}{hash}{i}");
            if(Convert.ToHexString(md5.ComputeHash(combined)).Equals(expected,StringComparison.OrdinalIgnoreCase))return true;
        }
        return false;
    }
    private static bool IsChart(string path)=>Path.GetExtension(path).ToLowerInvariant() is ".mc" or ".osu" or ".sm" or ".bms" or ".bme" or ".bml" or ".pms" or ".tja" or ".qua" or ".vsc";
    private static void RejectLinks(string root,string path)
    {
        for(var current=Path.GetDirectoryName(path);current is not null;current=Path.GetDirectoryName(current))
        {
            if(Directory.Exists(current) && (File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) throw new InvalidDataException("Shared chart destination contains a link.");
            if(string.Equals(Path.GetFullPath(current).TrimEnd('\\'),Path.GetFullPath(root).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))break;
        }
    }
}
