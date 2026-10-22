using System;
using System.IO;

namespace BetterMalodyV.Shared;

public static class PathRules
{
    public static string NormalizeLogicalPath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    public static string GetSafeCombinedPath(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var combined = Path.GetFullPath(Path.Combine(fullRoot, relativePath));

        if (!combined.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Path escapes root: {relativePath}");
        }

        return combined;
    }
}
