using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using BetterMalodyV.Shared;

namespace BetterMalodyV.Installer;

internal sealed class InstallService
{
    public OperationResult InstallOrRepair(
        string gameDirectory,
        IProgress<string>? progress = null)
    {
        if (!GameLocator.IsGameDirectory(gameDirectory, out var validationError))
        {
            return OperationResult.Fail(validationError);
        }

        gameDirectory = Path.GetFullPath(gameDirectory);
        var manifestPath = PathRules.GetSafeCombinedPath(
            gameDirectory, ProductInfo.ManifestRelativePath);
        var previousManifest = ReadManifest(manifestPath);
        var previousOwned = previousManifest?.Files
            .ToDictionary(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, InstalledFile>(StringComparer.OrdinalIgnoreCase);
        var previousExternal = previousManifest?.ExternalFiles
            .ToDictionary(file => file.FullPath, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, InstalledExternalFile>(
                StringComparer.OrdinalIgnoreCase);

        var staging = Path.Combine(
            Path.GetTempPath(), "BetterMalodyV-" + Guid.NewGuid().ToString("N"));
        var changedFiles = new List<RollbackEntry>();

        try
        {
            Directory.CreateDirectory(staging);
            progress?.Report("Extracting installer payload...");
            using (var payload = PayloadProvider.Open())
            using (var archive = new ZipArchive(payload, ZipArchiveMode.Read))
            {
                archive.ExtractToDirectory(staging);
            }

            var payloadFiles = Directory.EnumerateFiles(
                    staging, "*", SearchOption.AllDirectories)
                .Select(path => new PayloadFile(
                    path,
                    PathRules.NormalizeLogicalPath(Path.GetRelativePath(staging, path))))
                .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                .ToList();

            var environment = BepInExEnvironment.Inspect(gameDirectory, staging);
            var ownsRuntime = previousOwned.ContainsKey("winhttp.dll");
            if (environment.Present && !environment.Compatible && !ownsRuntime)
                throw new InvalidOperationException(environment.Message);
            var reuseRuntime = environment.Compatible;
            progress?.Report(environment.Message);
            var retainedRuntime = reuseRuntime
                ? previousOwned.Values.Where(file => BepInExEnvironment.IsRuntimeFile(file.RelativePath)
                    && File.Exists(PathRules.GetSafeCombinedPath(gameDirectory, file.RelativePath))).ToList()
                : new List<InstalledFile>();
            if (reuseRuntime)
                payloadFiles.RemoveAll(file => BepInExEnvironment.IsRuntimeFile(file.RelativePath)
                    && (!ownsRuntime || File.Exists(PathRules.GetSafeCombinedPath(gameDirectory, file.RelativePath))));

            ValidateOwnership(gameDirectory, payloadFiles, previousOwned);
            ValidateExternalOwnership(previousExternal);

            var installedFiles = new List<InstalledFile>(retainedRuntime);
            foreach (var payloadFile in payloadFiles)
            {
                progress?.Report($"Installing {payloadFile.RelativePath}");
                var target = PathRules.GetSafeCombinedPath(
                    gameDirectory, payloadFile.RelativePath);
                previousOwned.TryGetValue(
                    payloadFile.RelativePath, out var previousFile);

                var editableDocumentation = payloadFile.RelativePath.Equals("BetterMalodyV/docs/FEATURES.md", StringComparison.OrdinalIgnoreCase);
                var preserveDocumentation = editableDocumentation && File.Exists(target) && (previousFile is null || !string.Equals(ComputeSha256(target), previousFile.Sha256, StringComparison.OrdinalIgnoreCase));
                if (IsMutableFile(payloadFile.RelativePath) && File.Exists(target) && (!editableDocumentation || preserveDocumentation))
                {
                    installedFiles.Add(new InstalledFile
                    {
                        RelativePath = payloadFile.RelativePath,
                        Sha256 = previousFile?.Sha256 ?? ComputeSha256(target),
                        BackupRelativePath = previousFile?.BackupRelativePath,
                        ReplacedExistingFile =
                            previousFile?.ReplacedExistingFile ?? false
                    });
                    continue;
                }

                string? rollbackBackup = null;
                if (File.Exists(target))
                {
                    rollbackBackup = PathRules.GetSafeCombinedPath(
                        Path.Combine(staging, "rollback"), payloadFile.RelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(rollbackBackup)!);
                    File.Copy(target, rollbackBackup, true);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var temporaryTarget = target + ".mvtl-new-" + Guid.NewGuid().ToString("N");
                File.Copy(payloadFile.SourcePath, temporaryTarget, true);
                changedFiles.Add(new RollbackEntry(target, temporaryTarget, rollbackBackup));
                File.Move(temporaryTarget, target, true);

                installedFiles.Add(new InstalledFile
                {
                    RelativePath = payloadFile.RelativePath,
                    Sha256 = ComputeSha256(target),
                    BackupRelativePath = previousFile?.BackupRelativePath,
                    ReplacedExistingFile =
                        previousFile?.ReplacedExistingFile ?? false
                });
            }

            var currentOwnedPaths = payloadFiles
                .Select(file => file.RelativePath)
                .Concat(retainedRuntime.Select(file => file.RelativePath))
                .ToList();
            // Keep existing ownership for our own runtime; never acquire ownership of external BepInEx.
            RemoveObsoleteOwnedFiles(
                gameDirectory,
                staging,
                previousOwned,
                currentOwnedPaths,
                changedFiles,
                progress);

            progress?.Report("Creating BetterMalodyV desktop shortcut...");
            var externalFiles = InstallDesktopShortcut(
                gameDirectory,
                staging,
                changedFiles);
            RemoveObsoleteExternalFiles(previousExternal, externalFiles, progress);

            var manifest = new InstallManifest
            {
                ProductVersion = ProductInfo.Version,
                InstalledAt = DateTimeOffset.Now,
                GameDirectory = gameDirectory,
                ReusedBepInEx = reuseRuntime,
                Files = installedFiles,
                ExternalFiles = externalFiles
            };
            WriteManifestAtomic(manifestPath, manifest);

            progress?.Report("Installation complete.");
            return OperationResult.Ok(
                $"Installed {installedFiles.Count} files to {gameDirectory}.");
        }
        catch (Exception exception)
        {
            RollBack(gameDirectory, changedFiles, progress);
            return OperationResult.Fail(exception.Message);
        }
        finally
        {
            TryDeleteDirectory(staging);
        }
    }

    public OperationResult Uninstall(
        string gameDirectory,
        IProgress<string>? progress = null)
    {
        if (!Directory.Exists(gameDirectory))
        {
            return OperationResult.Fail("The selected directory does not exist.");
        }

        gameDirectory = Path.GetFullPath(gameDirectory);
        var manifestPath = PathRules.GetSafeCombinedPath(
            gameDirectory, ProductInfo.ManifestRelativePath);
        var manifest = ReadManifest(manifestPath);
        if (manifest == null)
        {
            return OperationResult.Fail("BetterMalodyV install manifest was not found.");
        }

        var preserved = new List<string>();
        var pluginsRoot = Path.Combine(gameDirectory, "BepInEx", "plugins");
        var ownPluginRoot = Path.Combine(pluginsRoot, "BetterMalodyV") + Path.DirectorySeparatorChar;
        var otherPlugins = Directory.Exists(pluginsRoot) && Directory.EnumerateFiles(pluginsRoot, "*.dll", SearchOption.AllDirectories)
            .Any(path => !Path.GetFullPath(path).StartsWith(ownPluginRoot, StringComparison.OrdinalIgnoreCase));
        foreach (var file in manifest.Files
                     .OrderByDescending(entry => entry.RelativePath.Length))
        {
            // Another plugin may have been installed after us and now share our runtime.
            if (otherPlugins && BepInExEnvironment.IsRuntimeFile(file.RelativePath)) continue;
            var target = PathRules.GetSafeCombinedPath(gameDirectory, file.RelativePath);
            if (!File.Exists(target))
            {
                RestoreBackupIfPresent(gameDirectory, file, target);
                continue;
            }

            if (!string.Equals(
                    ComputeSha256(target), file.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                preserved.Add(file.RelativePath);
                continue;
            }

            progress?.Report($"Removing {file.RelativePath}");
            File.Delete(target);
            RestoreBackupIfPresent(gameDirectory, file, target);
        }

        foreach (var external in manifest.ExternalFiles)
        {
            if (!File.Exists(external.FullPath))
            {
                continue;
            }
            if (!string.Equals(
                    ComputeSha256(external.FullPath),
                    external.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                preserved.Add(external.FullPath);
                continue;
            }

            progress?.Report($"Removing {external.FullPath}");
            File.Delete(external.FullPath);
        }

        File.Delete(manifestPath);
        RemoveEmptyOwnedDirectories(gameDirectory);

        var message = preserved.Count == 0
            ? "BetterMalodyV was uninstalled."
            : $"BetterMalodyV was uninstalled. Preserved {preserved.Count} modified files.";
        return OperationResult.Ok(message);
    }

    public OperationResult Verify(string gameDirectory)
    {
        if (!GameLocator.IsGameDirectory(gameDirectory, out var validationError))
        {
            return OperationResult.Fail(validationError);
        }

        var manifestPath = PathRules.GetSafeCombinedPath(
            Path.GetFullPath(gameDirectory), ProductInfo.ManifestRelativePath);
        var manifest = ReadManifest(manifestPath);
        if (manifest == null)
        {
            return OperationResult.Fail("BetterMalodyV is not installed.");
        }

        var invalid = manifest.Files.Count(file =>
        {
            var path = PathRules.GetSafeCombinedPath(gameDirectory, file.RelativePath);
            return !File.Exists(path)
                   || (!IsMutableFile(file.RelativePath)
                       && !string.Equals(
                           ComputeSha256(path), file.Sha256,
                           StringComparison.OrdinalIgnoreCase));
        });
        invalid += manifest.ExternalFiles.Count(file =>
            !File.Exists(file.FullPath)
            || !string.Equals(
                ComputeSha256(file.FullPath),
                file.Sha256,
                StringComparison.OrdinalIgnoreCase));
        if (manifest.ReusedBepInEx && !BepInExEnvironment.Inspect(gameDirectory).Compatible)
            return OperationResult.Fail("BetterMalodyV文件存在，但复用的 BepInEx 环境需要修复。");

        return invalid == 0
            ? OperationResult.Ok("All installed files match the manifest.")
            : OperationResult.Fail($"{invalid} installed files are missing or modified.");
    }

    private static void ValidateOwnership(
        string gameDirectory,
        IEnumerable<PayloadFile> payloadFiles,
        IReadOnlyDictionary<string, InstalledFile> previousOwned)
    {
        foreach (var payloadFile in payloadFiles)
        {
            var target = PathRules.GetSafeCombinedPath(
                gameDirectory, payloadFile.RelativePath);
            if (!File.Exists(target)
                || previousOwned.ContainsKey(payloadFile.RelativePath)
                || IsMutableFile(payloadFile.RelativePath))
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Refusing to overwrite an unowned file: {payloadFile.RelativePath}");
        }
    }

    private static bool IsMutableFile(string relativePath)
    {
        var normalized = PathRules.NormalizeLogicalPath(relativePath);
        return normalized.Equals("BetterMalodyV/settings.json", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("BetterMalodyV/docs/FEATURES.md", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("BepInEx/config/", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateExternalOwnership(
        IReadOnlyDictionary<string, InstalledExternalFile> previousExternal)
    {
        var shortcut = DesktopShortcut.GetPath();
        if (File.Exists(shortcut) && !previousExternal.ContainsKey(shortcut))
        {
            throw new InvalidOperationException(
                $"Refusing to overwrite an unowned desktop shortcut: {shortcut}");
        }
    }

    private static List<InstalledExternalFile> InstallDesktopShortcut(
        string gameDirectory,
        string staging,
        ICollection<RollbackEntry> changedFiles)
    {
        var shortcut = DesktopShortcut.GetPath();
        var manager = PathRules.GetSafeCombinedPath(
            gameDirectory, ProductInfo.ManagerRelativePath);
        if (!File.Exists(manager))
        {
            throw new FileNotFoundException(
                "The installer payload does not contain the management panel.",
                manager);
        }

        TrackExistingForRollback(
            shortcut,
            Path.Combine(staging, "rollback-shortcut"),
            changedFiles);
        Directory.CreateDirectory(Path.GetDirectoryName(shortcut)!);
        var temporary = Path.Combine(
            Path.GetDirectoryName(shortcut)!,
            ".mvtl-shortcut-" + Guid.NewGuid().ToString("N") + ".lnk");
        DesktopShortcut.Create(temporary, manager);
        changedFiles.Add(new RollbackEntry(shortcut, temporary, null));
        File.Move(temporary, shortcut, true);

        return new List<InstalledExternalFile>
        {
            new()
            {
                FullPath = shortcut,
                Sha256 = ComputeSha256(shortcut)
            }
        };
    }

    private static void RemoveObsoleteExternalFiles(
        IReadOnlyDictionary<string, InstalledExternalFile> previousExternal,
        IEnumerable<InstalledExternalFile> currentExternal,
        IProgress<string>? progress)
    {
        var current = currentExternal
            .Select(file => file.FullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var previous in previousExternal.Values)
        {
            if (current.Contains(previous.FullPath) || !File.Exists(previous.FullPath))
            {
                continue;
            }
            if (!string.Equals(
                    ComputeSha256(previous.FullPath),
                    previous.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            progress?.Report($"Removing obsolete shortcut {previous.FullPath}");
            File.Delete(previous.FullPath);
        }
    }

    private static void TrackExistingForRollback(
        string target,
        string backupRoot,
        ICollection<RollbackEntry> changedFiles)
    {
        if (!File.Exists(target))
        {
            changedFiles.Add(new RollbackEntry(
                target,
                target + ".mvtl-placeholder",
                null));
            return;
        }

        Directory.CreateDirectory(backupRoot);
        var backup = Path.Combine(
            backupRoot, Guid.NewGuid().ToString("N") + Path.GetExtension(target));
        File.Copy(target, backup, true);
        changedFiles.Add(new RollbackEntry(
            target,
            target + ".mvtl-placeholder",
            backup));
    }

    private static void RemoveObsoleteOwnedFiles(
        string gameDirectory,
        string staging,
        IReadOnlyDictionary<string, InstalledFile> previousOwned,
        IEnumerable<string> currentPayloadPaths,
        ICollection<RollbackEntry> changedFiles,
        IProgress<string>? progress)
    {
        var current = currentPayloadPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var previous in previousOwned.Values)
        {
            if (current.Contains(previous.RelativePath))
            {
                continue;
            }

            var target = PathRules.GetSafeCombinedPath(
                gameDirectory, previous.RelativePath);
            if (!File.Exists(target)
                || !string.Equals(
                    ComputeSha256(target),
                    previous.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var backup = PathRules.GetSafeCombinedPath(
                Path.Combine(staging, "rollback-obsolete"), previous.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
            File.Copy(target, backup, true);
            changedFiles.Add(new RollbackEntry(
                target,
                target + ".mvtl-obsolete-placeholder",
                backup));

            progress?.Report($"Removing obsolete {previous.RelativePath}");
            File.Delete(target);
        }
    }

    private static void RollBack(
        string gameDirectory,
        IEnumerable<RollbackEntry> changedFiles,
        IProgress<string>? progress)
    {
        progress?.Report("Installation failed; rolling back...");
        foreach (var entry in changedFiles.Reverse())
        {
            TryDeleteFile(entry.TemporaryPath);
            TryDeleteFile(entry.TargetPath);

            if (entry.RollbackBackupPath == null)
            {
                continue;
            }

            if (File.Exists(entry.RollbackBackupPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(entry.TargetPath)!);
                File.Copy(entry.RollbackBackupPath, entry.TargetPath, true);
            }
        }
    }

    private static void RestoreBackupIfPresent(
        string gameDirectory,
        InstalledFile file,
        string target)
    {
        if (string.IsNullOrWhiteSpace(file.BackupRelativePath))
        {
            return;
        }

        var backup = PathRules.GetSafeCombinedPath(
            gameDirectory, file.BackupRelativePath);
        if (!File.Exists(backup))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Move(backup, target, true);
    }

    private static InstallManifest? ReadManifest(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<InstallManifest>(
            File.ReadAllText(path), JsonDefaults.Options);
    }

    private static void WriteManifestAtomic(string path, InstallManifest manifest)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".new";
        File.WriteAllText(
            temporary, JsonSerializer.Serialize(manifest, JsonDefaults.Options));
        File.Move(temporary, path, true);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void RemoveEmptyOwnedDirectories(string gameDirectory)
    {
        foreach (var relative in new[]
                 {
                     "BepInEx/plugins/BetterMalodyV",
                     "BepInEx",
                     "dotnet",
                     "BetterMalodyV/.backup",
                     "BetterMalodyV"
                 })
        {
            var directory = PathRules.GetSafeCombinedPath(gameDirectory, relative);
            RemoveEmptyTree(directory);
        }
    }

    private static void RemoveEmptyTree(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var child in Directory.GetDirectories(directory))
        {
            RemoveEmptyTree(child);
        }

        if (!Directory.EnumerateFileSystemEntries(directory).Any())
        {
            try
            {
                Directory.Delete(directory);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            var fullTemp = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(fullTemp, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, true);
            }
        }
        catch
        {
        }
    }
}

internal sealed record PayloadFile(string SourcePath, string RelativePath);

internal sealed record RollbackEntry(
    string TargetPath,
    string TemporaryPath,
    string? RollbackBackupPath);

internal sealed record OperationResult(bool Success, string Message)
{
    public static OperationResult Ok(string message) => new(true, message);
    public static OperationResult Fail(string message) => new(false, message);
}
