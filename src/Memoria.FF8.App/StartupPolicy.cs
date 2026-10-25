using Memoria.FF8.Mods;

namespace Memoria.FF8;

internal sealed record StartupPolicy(bool SkipPublisherMovie = false, bool SkipSquareLogo = false, bool SkipCreditsPause = false, bool SkipCreditsImages = false)
{
    public static StartupPolicy Default { get; } = new();

    public static StartupPolicy FromCatalog(ModCatalogSnapshot catalog)
    {
        HashSet<JunctionBehavior> behaviors = catalog.Packages
            .Where(package => package.Format == ModFormat.Junction && package.Status == ModEffectiveStatus.Active)
            .SelectMany(package => package.JunctionMetadata?.Behaviors ?? [])
            .ToHashSet();
        return new(behaviors.Contains(JunctionBehavior.SkipPublisherMovie), behaviors.Contains(JunctionBehavior.SkipSquareLogo), behaviors.Contains(JunctionBehavior.SkipCreditsPause), behaviors.Contains(JunctionBehavior.SkipCreditsImages));
    }
}
