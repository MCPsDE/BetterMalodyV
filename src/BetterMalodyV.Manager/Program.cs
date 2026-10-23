using BetterMalodyV.Windows;

namespace BetterMalodyV.Manager;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if(args.Length==3 && args[0]=="--game-update")
        {
            GameUpdateWorker.RunAsync(args[1],int.Parse(args[2])).GetAwaiter().GetResult();return;
        }
        ApplicationConfiguration.Initialize();
        if (args.Contains("--ui-test")) { UiVerification.Run(args); return; }
        Application.Run(new ManagerForm(args.Contains("--skip-update")));
    }
}
