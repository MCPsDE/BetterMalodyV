using System.Reflection;

namespace BetterMalodyV.Installer;

internal static class PayloadProvider
{
    private const string ResourceName = "BetterMalodyV.Payload.zip";

    public static Stream Open()
    {
        var overridePath = Environment.GetEnvironmentVariable(
            "BETTER_MALODYV_PAYLOAD");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return File.OpenRead(overridePath);
        }

        return Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
               ?? throw new InvalidOperationException(
                   "The installer does not contain an embedded payload.");
    }
}
