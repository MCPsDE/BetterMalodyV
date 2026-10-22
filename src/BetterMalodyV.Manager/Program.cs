using BetterMalodyV.Windows;

namespace BetterMalodyV.Manager;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--ui-test")) { UiVerification.Run(args); return; }
        Application.Run(new ManagerForm(args.Contains("--skip-update")));
    }
}
