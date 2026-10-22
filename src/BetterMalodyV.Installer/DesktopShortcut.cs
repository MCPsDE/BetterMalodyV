using System.Runtime.InteropServices;

namespace BetterMalodyV.Installer;

internal static class DesktopShortcut
{
    public static string GetPath()
    {
        var overrideDirectory = Environment.GetEnvironmentVariable(
            "BETTER_MALODYV_DESKTOP");
        var desktop = string.IsNullOrWhiteSpace(overrideDirectory)
            ? Environment.GetFolderPath(
                Environment.SpecialFolder.DesktopDirectory)
            : Path.GetFullPath(overrideDirectory);
        return Path.Combine(
            desktop,
            "BetterMalodyV 管理面板.lnk");
    }

    public static void Create(string path, string managerExecutable)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
                        ?? throw new InvalidOperationException(
                            "Windows Script Host is unavailable.");
        object? shell = null;
        object? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(shellType)
                    ?? throw new InvalidOperationException(
                        "Could not create Windows Script Host.");
            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                shell,
                new object[] { path });
            var shortcutType = shortcut?.GetType()
                               ?? throw new InvalidOperationException(
                                   "Could not create desktop shortcut.");
            shortcutType.InvokeMember(
                "TargetPath",
                System.Reflection.BindingFlags.SetProperty,
                null,
                shortcut,
                new object[] { managerExecutable });
            shortcutType.InvokeMember(
                "WorkingDirectory",
                System.Reflection.BindingFlags.SetProperty,
                null,
                shortcut,
                new object[] { Path.GetDirectoryName(managerExecutable)! });
            shortcutType.InvokeMember(
                "Description",
                System.Reflection.BindingFlags.SetProperty,
                null,
                shortcut,
                new object[] { "BetterMalodyV 优化与修改说明" });
            shortcutType.InvokeMember(
                "IconLocation",
                System.Reflection.BindingFlags.SetProperty,
                null,
                shortcut,
                new object[] { managerExecutable + ",0" });
            shortcutType.InvokeMember(
                "Save",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                shortcut,
                null);
        }
        finally
        {
            if (shortcut != null && Marshal.IsComObject(shortcut))
            {
                Marshal.FinalReleaseComObject(shortcut);
            }
            if (shell != null && Marshal.IsComObject(shell))
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}
