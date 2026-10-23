using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using BetterMalodyV.Shared;

namespace BetterMalodyV.Manager;

internal static class UpdateService
{
    private const string Repository = "MCPsDE/BetterMalodyV";
    private static readonly Uri LatestReleaseUri = new(
        $"https://api.github.com/repos/{Repository}/releases/latest");

    public static async Task<UpdateResult> CheckAndStartAsync(
        string gameRoot,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default,
        HttpClient? testClient = null,
        Func<System.Diagnostics.ProcessStartInfo, System.Diagnostics.Process?>? testLauncher = null,
        int? gameProcessId = null)
    {
        if (string.Equals(
                Environment.GetEnvironmentVariable("BETTER_MALODYV_DISABLE_UPDATES"),
                "1",
                StringComparison.Ordinal))
        {
            return UpdateResult.None;
        }

        try
        {
            using var ownedClient = testClient is null ? new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10)
            } : null;
            var client = testClient ?? ownedClient!;
            client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("BetterMalodyV", CurrentVersion()));
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var metadataTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            metadataTimeout.CancelAfter(TimeSpan.FromSeconds(8));
            using var response = await client.GetAsync(
                LatestReleaseUri,
                HttpCompletionOption.ResponseHeadersRead,
                metadataTimeout.Token);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return UpdateResult.None;
            }
            if (response.StatusCode is HttpStatusCode.Forbidden or (HttpStatusCode)429)
            {
                // GitHub's anonymous API limit is external state; do not turn it
                // into a scary error in the manager or block theme work.
                return UpdateResult.None;
            }
            response.EnsureSuccessStatusCode();
            await using var releaseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var releaseDocument = await JsonDocument.ParseAsync(
                releaseStream,
                cancellationToken: cancellationToken);
            var root = releaseDocument.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagProperty)
                ? tagProperty.GetString()
                : null;
            if (!TryParseVersion(tag, out var latestVersion)
                || latestVersion <= CurrentVersionInfo())
            {
                return UpdateResult.None;
            }

            var assets = root.TryGetProperty("assets", out var assetsProperty)
                ? assetsProperty.EnumerateArray().ToList()
                : new List<JsonElement>();
            var installerName = $"BetterMalodyV-Setup-{latestVersion}.exe";
            var installerAsset = assets.FirstOrDefault(asset =>
                string.Equals(
                    asset.GetProperty("name").GetString(),
                    installerName,
                    StringComparison.OrdinalIgnoreCase));
            if (installerAsset.ValueKind == JsonValueKind.Undefined)
            {
                return UpdateResult.Failed($"GitHub Release 缺少 {installerName}。");
            }

            var checksumAsset = assets.FirstOrDefault(asset =>
                string.Equals(
                    asset.GetProperty("name").GetString(),
                    "SHA256SUMS.txt",
                    StringComparison.OrdinalIgnoreCase));
            if (checksumAsset.ValueKind == JsonValueKind.Undefined)
            {
                return UpdateResult.Failed("GitHub Release 缺少 SHA256SUMS.txt，已停止更新。");
            }

            var temporaryRoot = Path.Combine(
                Path.GetTempPath(), "BetterMalodyV-update-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
            var installerPath = Path.Combine(temporaryRoot, installerName);
            var checksumPath = Path.Combine(temporaryRoot, "SHA256SUMS.txt");
            try
            {
                progress?.Report($"发现 BetterMalodyV {latestVersion}，正在下载并校验…");
                await DownloadAsync(
                    client,
                    installerAsset.GetProperty("browser_download_url").GetString()
                        ?? throw new InvalidDataException("安装器下载地址为空。"),
                    installerPath,
                    cancellationToken);
                await DownloadAsync(
                    client,
                    checksumAsset.GetProperty("browser_download_url").GetString()
                        ?? throw new InvalidDataException("校验文件下载地址为空。"),
                    checksumPath,
                    cancellationToken);

                var expected = FindExpectedHash(checksumPath, installerName);
                var actual = ComputeSha256(installerPath);
                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("更新安装器 SHA-256 校验失败，已停止更新。");
                }

                progress?.Report("校验通过。管理器将关闭并启动更新安装器…");
                var start = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = installerPath,
                    WorkingDirectory = temporaryRoot,
                    UseShellExecute = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal
                };
                start.ArgumentList.Add(gameProcessId.HasValue?"--update-game":"--update");
                start.ArgumentList.Add(Path.GetFullPath(gameRoot));
                start.ArgumentList.Add(Environment.ProcessId.ToString());
                if(gameProcessId.HasValue)start.ArgumentList.Add(gameProcessId.Value.ToString());
                _ = (testLauncher is null ? System.Diagnostics.Process.Start(start) : testLauncher(start))
                    ?? throw new InvalidOperationException("无法启动更新安装器。");
                return UpdateResult.Started(latestVersion);
            }
            catch
            {
                TryDeleteDirectory(temporaryRoot);
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            return cancellationToken.IsCancellationRequested ? UpdateResult.None : UpdateResult.Failed("更新检查超时，请稍后重试。");
        }
        catch (Exception exception) when (
            exception is HttpRequestException
                or IOException
                or InvalidDataException
                or JsonException
                or TaskCanceledException
                or InvalidOperationException
                or System.ComponentModel.Win32Exception)
        {
            return UpdateResult.Failed($"自动更新检查失败：{exception.Message}");
        }
    }

    private static async Task DownloadAsync(
        HttpClient client,
        string url,
        string destination,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destinationStream = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            128 * 1024,
            useAsync: true);
        await source.CopyToAsync(destinationStream, cancellationToken);
    }

    private static string FindExpectedHash(string path, string fileName)
    {
        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2
                && string.Equals(
                    Path.GetFileName(parts[^1].TrimStart('*')),
                    fileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return parts[0];
            }
        }

        throw new InvalidDataException($"SHA256SUMS.txt 中没有 {fileName} 的条目。");
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static Version CurrentVersionInfo() =>
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

    private static string CurrentVersion() => CurrentVersionInfo().ToString(3);

    private static bool TryParseVersion(string? value, out Version version)
    {
        version = new Version();
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = value.Trim().TrimStart('v', 'V');
        var dash = normalized.IndexOf('-');
        if (dash >= 0) normalized = normalized[..dash];
        if (!Version.TryParse(normalized, out var parsed) || parsed is null)
            return false;
        version = parsed;
        return true;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        catch { }
    }
}

internal sealed record UpdateResult(
    UpdateState State,
    string Message,
    Version? Version = null)
{
    public static UpdateResult None => new(UpdateState.None, "");
    public static UpdateResult Started(Version version) =>
        new(UpdateState.Started, $"已启动 BetterMalodyV {version} 更新。", version);
    public static UpdateResult Failed(string message) =>
        new(UpdateState.Failed, message);
}

internal enum UpdateState
{
    None,
    Started,
    Failed
}
