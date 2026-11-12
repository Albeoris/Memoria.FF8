using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class Ff7Game
{
    private SteamPlatformSession? platformSession;
    private bool platformDisposed;

    private void InitializePlatform()
    {
        if (platformDisposed || platformSession is not null)
        {
            return;
        }
        GamePlatformContext context = GamePlatformContextResolver.ResolveFf7(installation.RootDirectory, launchOptions?.AchievementsLocalOnly == true || launchOptions?.IsAchievementToolSession == true);
        if (context.CanInitializeExternalBackend && context.Provider == GameDistributionProvider.Steam)
        {
            platformSession = new SteamPlatformSession(Path.Combine(Memoria.FF8.Platform.ApplicationInstallation.DataDirectory, "Steam", "steam_api64.dll"), context.ProductId!);
        }
        Console.WriteLine($"platform provider={context.Provider} product={context.ProductId ?? "unknown"} diagnostic={platformSession?.Diagnostic ?? context.Diagnostic}");
    }

    private void PumpPlatform()
    {
        platformSession?.Pump();
    }

    private void DisposePlatform()
    {
        if (platformDisposed)
        {
            return;
        }
        platformDisposed = true;
        platformSession?.Dispose();
        platformSession = null;
    }
}
