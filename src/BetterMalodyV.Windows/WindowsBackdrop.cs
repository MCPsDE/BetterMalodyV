using System.Runtime.InteropServices;

namespace BetterMalodyV.Windows;

/// <summary>
/// Uses the Windows 11 Mica system backdrop when available. Older Windows
/// versions keep the same Fluent neutral surface through the normal fallback.
/// This avoids simulating glass with gradients or blur inside the application.
/// </summary>
public static class WindowsBackdrop
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmsbtMica = 2;

    public static bool TryApplyMica(Form form)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            return false;

        try
        {
            form.BackColor = AppVisuals.Canvas;
            var dark = AppVisuals.DarkMode ? 1 : 0;
            var backdrop = DwmsbtMica;
            _ = DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode,
                ref dark, sizeof(int));
            return DwmSetWindowAttribute(form.Handle, DwmwaSystemBackdropType,
                ref backdrop, sizeof(int)) == 0;
        }
        catch (DllNotFoundException) { return false; }
        catch (EntryPointNotFoundException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int value,
        int valueSize);
}
