using System.Text.Json;

namespace BetterMalodyV.Windows;

internal static class AppearancePreferences
{
    private static string SettingsPath => Path.Combine(
        Environment.GetEnvironmentVariable("BETTER_MALODYV_PREFERENCES")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BetterMalodyV"),
        "appearance.json");

    public static bool LoadDarkMode()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return true;
            using var document = JsonDocument.Parse(File.ReadAllText(SettingsPath));
            return !document.RootElement.TryGetProperty("mode", out var mode) || mode.GetString() != "light";
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException) { return true; }
    }

    public static void SaveDarkMode(bool dark)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var temporary = SettingsPath + ".new-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { mode = dark ? "dark" : "light" }));
            File.Move(temporary, SettingsPath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
