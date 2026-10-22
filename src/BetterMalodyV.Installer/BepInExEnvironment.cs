using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using BetterMalodyV.Shared;

namespace BetterMalodyV.Installer;

internal sealed record BepInExEnvironment(bool Present, bool Compatible, string Message)
{
    public static bool IsRuntimeFile(string path) =>
        !path.StartsWith("BetterMalodyV/", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWith("BepInEx/plugins/BetterMalodyV/", StringComparison.OrdinalIgnoreCase);

    public static BepInExEnvironment Inspect(string gameRoot, string? payloadRoot = null)
    {
        var present = File.Exists(Path.Combine(gameRoot, "winhttp.dll"))
            || File.Exists(Path.Combine(gameRoot, "doorstop_config.ini"))
            || Directory.Exists(Path.Combine(gameRoot, "BepInEx"));
        if (!present) return new(false, false, "将安装随附的 BepInEx 6 与BetterMalodyV。");
        try
        {
            RequireX64(Path.Combine(gameRoot, "winhttp.dll"));
            var proxyBytes = File.ReadAllBytes(Path.Combine(gameRoot, "winhttp.dll"));
            if (!System.Text.Encoding.ASCII.GetString(proxyBytes).Contains("doorstop", StringComparison.OrdinalIgnoreCase)
                && !System.Text.Encoding.Unicode.GetString(proxyBytes).Contains("doorstop", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("winhttp.dll 无法识别为 Unity Doorstop");
            var config = ReadConfig(Path.Combine(gameRoot, "doorstop_config.ini"));
            if (!string.Equals(config.GetValueOrDefault("General/enabled"), "true", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Doorstop 尚未启用");
            var target = config.GetValueOrDefault("General/target_assembly")?.Replace('\\', '/');
            if (!string.Equals(target, "BepInEx/core/BepInEx.Unity.IL2CPP.dll", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Doorstop 入口不是 BepInEx IL2CPP");
            RequireX64(Resolve(gameRoot, config, "Il2Cpp/coreclr_path"));
            var corlib = Resolve(gameRoot, config, "Il2Cpp/corlib_dir");
            foreach (var name in new[] { "System.Private.CoreLib", "System.Runtime", "System.Runtime.Loader", "netstandard" })
                ReadAssembly(Path.Combine(corlib, name + ".dll"));
            RequireX64(Path.Combine(Path.GetDirectoryName(Resolve(gameRoot, config, "Il2Cpp/coreclr_path"))!, "hostpolicy.dll"));
            var coreRoot = Path.Combine(gameRoot, "BepInEx", "core");
            var core = ReadAssembly(Path.Combine(coreRoot, "BepInEx.Core.dll"));
            if (core.Version.Major != 6) throw new InvalidDataException("需要 BepInEx 6 IL2CPP x64");
            foreach (var name in new[] { "BepInEx.Unity.IL2CPP", "BepInEx.Unity.Common", "BepInEx.Preloader.Core",
                         "Il2CppInterop.Runtime", "Il2CppInterop.Common", "Il2CppInterop.HarmonySupport", "0Harmony" })
                ReadAssembly(Path.Combine(coreRoot, name + ".dll"));
            RequireX64(Path.Combine(coreRoot, "dobby.dll"));
            var pending = new Queue<string>(new[] { "BepInEx.Unity.IL2CPP.dll", "Il2CppInterop.HarmonySupport.dll" });
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (pending.TryDequeue(out var file))
            {
                if (!visited.Add(file)) continue;
                var assembly = Path.Combine(coreRoot, file);
                using var dependencyStream = File.OpenRead(assembly);
                using var dependencyPe = new PEReader(dependencyStream);
                if (!dependencyPe.HasMetadata) continue;
                var dependencyMetadata = dependencyPe.GetMetadataReader();
                foreach (var handle in dependencyMetadata.AssemblyReferences)
                {
                    var dependency = dependencyMetadata.GetString(dependencyMetadata.GetAssemblyReference(handle).Name) + ".dll";
                    // Framework assemblies come from CoreCLR; all other BepInEx dependencies must exist.
                    if (dependency.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
                        || dependency.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
                        || dependency.StartsWith("Unity", StringComparison.OrdinalIgnoreCase)
                        || dependency.StartsWith("Il2CppSystem", StringComparison.OrdinalIgnoreCase)
                        || dependency.StartsWith("Il2Cppmscorlib", StringComparison.OrdinalIgnoreCase)
                        || dependency is "mscorlib.dll" or "netstandard.dll") continue;
                    if (File.Exists(Path.Combine(coreRoot, dependency))) pending.Enqueue(dependency);
                    else if (!File.Exists(Path.Combine(corlib, dependency))
                             && !File.Exists(Path.Combine(gameRoot,"BepInEx","interop",dependency)))
                        throw new InvalidDataException($"运行时缺少依赖 {dependency}");
                }
            }

            // Compare actual plugin assembly references and referenced public types without loading user code.
            if (payloadRoot is not null)
            {
                var plugin = Path.Combine(payloadRoot, "BepInEx", "plugins", "BetterMalodyV", "BetterMalodyV.Plugin.dll");
                using var stream = File.OpenRead(plugin);
                using var pe = new PEReader(stream);
                var metadata = pe.GetMetadataReader();
                var runtimeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "BepInEx.Core", "BepInEx.Unity.IL2CPP", "BepInEx.Unity.Common", "Il2CppInterop.Runtime", "0Harmony" };
                foreach (var handle in metadata.AssemblyReferences)
                {
                    var reference = metadata.GetAssemblyReference(handle);
                    var name = metadata.GetString(reference.Name);
                    if (!runtimeNames.Contains(name)) continue;
                    var actual = ReadAssembly(Path.Combine(coreRoot, name + ".dll"));
                    if (actual.Version < reference.Version)
                        throw new InvalidDataException($"{name} 版本过低（需要 {reference.Version}，现有 {actual.Version}）");
                    foreach (var typeHandle in metadata.TypeReferences)
                    {
                        var type = metadata.GetTypeReference(typeHandle);
                        if (type.ResolutionScope != handle) continue;
                        var fullName = metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name);
                        if (!actual.Types.Contains(fullName))
                            throw new InvalidDataException($"{name} 缺少 API {fullName}");
                    }
                }
            }
            return new(true, true, $"已检测到 BepInEx {core.Version} · 将直接安装BetterMalodyV，保留现有环境。");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or BadImageFormatException or InvalidOperationException or ArgumentException or UnauthorizedAccessException)
        {
            return new(true, false, "现有 BepInEx 无法复用：" + exception.Message + "。请先修复运行环境，再重试。");
        }
    }

    private static string Resolve(string root, Dictionary<string, string> config, string key) =>
        PathRules.GetSafeCombinedPath(root, config.GetValueOrDefault(key)
            ?? throw new InvalidDataException($"Doorstop 配置缺少 {key}"));

    private static Dictionary<string, string> ReadConfig(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var section = "";
        foreach (var line in File.ReadLines(path))
        {
            var value = line.Split('#', ';')[0].Trim();
            if (value.StartsWith('[') && value.EndsWith(']')) section = value[1..^1];
            else if (value.IndexOf('=') is var index && index > 0)
            {
                var key = section + "/" + value[..index].Trim();
                if (!result.TryAdd(key, value[(index + 1)..].Trim()))
                    throw new InvalidDataException($"Doorstop 配置重复：{key}");
            }
        }
        return result;
    }

    private static void RequireX64(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        if (pe.PEHeaders.CoffHeader.Machine != Machine.Amd64)
            throw new InvalidDataException($"{Path.GetFileName(path)} 不是 Windows x64 文件");
    }

    private static (Version Version, HashSet<string> Types) ReadAssembly(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var types = reader.TypeDefinitions.Select(handle => reader.GetTypeDefinition(handle))
            .Select(type => reader.GetString(type.Namespace) + "." + reader.GetString(type.Name))
            .ToHashSet(StringComparer.Ordinal);
        return (reader.GetAssemblyDefinition().Version, types);
    }
}
