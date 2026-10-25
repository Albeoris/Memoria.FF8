using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Explicitly selected native integration check; it never creates progress or submits writes.</summary>
internal static class SteamAchievementProbe
{
    public static int Run(string[] args)
    {
        LaunchOptions options = LaunchOptions.Parse(args);
        GamePlatformContext context = GamePlatformContextResolver.Resolve(options.GameDirectory, options.AchievementsLocalOnly);
        if (!context.CanInitializeExternalBackend || context.Provider != GameDistributionProvider.Steam)
        {
            Console.Error.WriteLine("Steam achievement probe unavailable: " + context.Diagnostic);
            return 2;
        }
        Directory.SetCurrentDirectory(context.InstallationDirectory);
        using SteamAchievementBackend backend = new(Path.Combine(AppContext.BaseDirectory, "Steam", "steam_api64.dll"), context.ProductId!, "read-only-probe");
        AchievementBinding[] bindings = SteamAchievementMappings.ForProduct(context.ProductId);
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        do
        {
            backend.Pump();
            if (backend.State is AchievementBackendState.Ready or AchievementBackendState.OfflineReady)
            {
                AchievementRemoteState? state = backend.Read(bindings);
                if (state is not null)
                {
                    Console.WriteLine($"Steam achievements: product={context.ProductId} architecture=x64 identity-verified={backend.Identity is not null} state={backend.State} achievement-read=true stat-read={(bindings.All(binding => binding.NativeCounter is null || state.Counters.ContainsKey(binding.Id.Value)))} writes=0");
                    return 0;
                }
            }
            if (backend.State == AchievementBackendState.Unsupported)
            {
                break;
            }
            Thread.Sleep(10);
        }
        while (DateTime.UtcNow < deadline);
        Console.Error.WriteLine($"Steam achievements: product={context.ProductId} state={backend.State} writes=0 diagnostic={backend.Diagnostic}");
        return 1;
    }
}
