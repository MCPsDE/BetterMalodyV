using System.Reflection;

namespace BetterMalodyV.Installer;

internal static class ProductInfo
{
    public const string Name = "BetterMalodyV";
    public static string Version =>
        typeof(ProductInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0]
        ?? "1.0.0";
    public const string ManifestRelativePath = "BetterMalodyV/install-manifest.json";
    public const string BackupRootRelativePath = "BetterMalodyV/.backup";
    public const string ManagerRelativePath =
        "BetterMalodyV/BetterMalodyV.Manager.exe";
}
