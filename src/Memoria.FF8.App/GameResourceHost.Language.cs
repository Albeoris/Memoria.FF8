using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Platform;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameResourceHost
{
    internal GameLanguageProfile LanguageProfile => new(Resources.Source.Edition.ToString(), GameResourceLanguages.Normalize(Resources.ResourceLanguage) ?? Resources.ResourceLanguage);

    internal GameResourceHost? OpenLanguageCandidateIfNeeded(LaunchOptions options, GameLanguageOffer offer, GameLanguageChoice? previous, BattleDifficulty difficulty, CancellationToken cancellationToken)
    {
        GameLanguageProfile profile = offer.Profile!;
        bool selectingJunction = offer.InstalledPackage?.Format == ModFormat.Junction || offer.Release?.SourceId == RemoteModCatalogAdapter.SourceId;
        bool leavingJunction = Mods.Catalog.Packages.Any(package => package.Format == ModFormat.Junction && package.Status == ModEffectiveStatus.Active
            && (previous is null ? LanguagePackages.IsLanguage(package) : package.Id.Equals(previous.PackageId, StringComparison.OrdinalIgnoreCase)));
        // Junction editions can replace native banks already acquired by the title and its menus.
        return profile != LanguageProfile || selectingJunction || leavingJunction
            ? OpenLanguageCandidate(options, profile, difficulty, cancellationToken)
            : null;
    }

    internal static GameResourceHost OpenLanguageCandidate(LaunchOptions options, GameLanguageProfile profile, BattleDifficulty difficulty, CancellationToken cancellationToken)
    {
        var candidate = new GameResourceHost(options.GameDirectory, cancellationToken, modsDirectory: options.ModsDirectory, resourceLanguage: profile.ResourceLanguage, modelSourceDirectory: options.ModelSourceDirectory, modLocations: options.ModLocations, connectEncounterMods: true, deferStartupPreparation: true);
        candidate.Mods.SetHardcoreMode(difficulty == BattleDifficulty.Hardcore);
        return candidate;
    }

    internal void FinishLanguageStartup(CancellationToken cancellationToken)
    {
        SealLooseData();
        ValidateModLibraryStartup(cancellationToken);
    }

    internal ModPackageInstaller LanguageInstaller(GameLanguageProfile profile)
    {
        return ModLibraryProfile.CreateInstaller(Mods.PrimaryModsDirectory, gameDirectory, profile.Edition, profile.ResourceLanguage, profile.Edition == "Classic" && profile.ResourceLanguage == "en", Mods.PreferencesDirectory);
    }

    internal GameSession CreateColdSession()
    {
        using GameAssets assets = new(gameDirectory, resources: Resources, modelOverrides: this);
        return GameWorld.CreateColdProcess(assets);
    }
}
