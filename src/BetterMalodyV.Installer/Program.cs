using BetterMalodyV.Windows;

namespace BetterMalodyV.Installer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--ui-test")
        {
            ApplicationConfiguration.Initialize(); UiVerification.Run(args[1]); return 0;
        }
        if (args.Length > 0)
        {
            return InstallerCommand.Run(args);
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new InstallerForm());
        return 0;
    }
}
