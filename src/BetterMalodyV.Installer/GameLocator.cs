using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace BetterMalodyV.Installer;

internal static class GameLocator
{
    private static readonly string[] RequiredFiles =
    {
        "Malody V.exe",
        "UnityPlayer.dll",
        "GameAssembly.dll"
    };

    public static string? FindGameDirectory()
    {
        foreach (var candidate in FindCandidates())
        {
            if (IsGameDirectory(candidate, out _))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    public static bool IsGameDirectory(string path, out string error)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            error = "目录不存在，请选择包含 Malody V.exe 的游戏安装文件夹。";
            return false;
        }

        foreach (var file in RequiredFiles)
        {
            if (!File.Exists(Path.Combine(path, file)))
            {
                error = $"未找到 {file}，请选择完整的 Malody V 游戏目录。";
                return false;
            }
        }

        var dataPath = Path.Combine(path, "Malody V_Data");
        if (!Directory.Exists(dataPath)
            || !Directory.Exists(Path.Combine(dataPath, "il2cpp_data")))
        {
            error = "未找到 Malody V_Data 或 IL2CPP 数据，请确认游戏文件完整。";
            return false;
        }

        error = "";
        return true;
    }

    private static IEnumerable<string> FindCandidates()
    {
        yield return AppContext.BaseDirectory;

        foreach (var steamRoot in FindSteamRoots())
        {
            yield return Path.Combine(
                steamRoot, "steamapps", "common", "MalodyV");
        }
    }

    private static IEnumerable<string> FindSteamRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var registryPath in new[]
                 {
                     @"HKEY_CURRENT_USER\Software\Valve\Steam",
                     @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam"
                 })
        {
            var value = Registry.GetValue(registryPath, "SteamPath", null) as string
                        ?? Registry.GetValue(registryPath, "InstallPath", null) as string;
            if (!string.IsNullOrWhiteSpace(value))
            {
                roots.Add(value);
            }
        }

        roots.Add(@"C:\Program Files (x86)\Steam");
        roots.Add(@"C:\Program Files\Steam");

        foreach (var root in roots.ToArray())
        {
            var libraries = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraries))
            {
                continue;
            }

            foreach (Match match in Regex.Matches(
                         File.ReadAllText(libraries),
                         "\"path\"\\s+\"(?<path>[^\"]+)\"",
                         RegexOptions.IgnoreCase))
            {
                roots.Add(match.Groups["path"].Value.Replace(@"\\", @"\"));
            }
        }

        return roots;
    }
}
