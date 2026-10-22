using System;
using System.Collections.Generic;

namespace BetterMalodyV.Shared;

public sealed class InstallManifest
{
    public int FormatVersion { get; set; } = 1;
    public string ProductVersion { get; set; } = "";
    public DateTimeOffset InstalledAt { get; set; }
    public string GameDirectory { get; set; } = "";
    public bool ReusedBepInEx { get; set; }
    public List<InstalledFile> Files { get; set; } = new();
    public List<InstalledExternalFile> ExternalFiles { get; set; } = new();
}

public sealed class InstalledFile
{
    public string RelativePath { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public bool ReplacedExistingFile { get; set; }
    public string? BackupRelativePath { get; set; }
}

public sealed class InstalledExternalFile
{
    public string FullPath { get; set; } = "";
    public string Sha256 { get; set; } = "";
}
