using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Platform;

namespace Memoria.FF8;

internal static class InstalledGuardianForms
{
    internal static GuardianFormMods Read(GameAssets game, IModelOverrideSource? modelOverrides = null)
    {
        if (modelOverrides is GameResourceHost host)
        {
            return host.GuardianForms;
        }
        var source = game.Resources.Source;
        GuardianFormMods forms = GuardianFormMods.Empty;
        using ModRuntime runtime = new(ApplicationInstallation.DataDirectory, format => format == ModFormat.Memoria && ModCompatibility.IsCompatible(source.Edition, format),
            prepare: catalog =>
            {
                if (!GuardianFormMods.TryPrepare(catalog, () => new BattleAssets(game.GameDirectory, game.Resources, modelOverrides), out GuardianFormMods candidate, out IReadOnlyList<ModDiagnostic> diagnostics))
                {
                    return ModPreparationResult.RejectPackagePayloads(diagnostics);
                }
                return new(true, candidate.PackageIds, Commit: () => forms = candidate);
            }, watch: false, edition: source.Edition.ToString(), language: source.Language);
        runtime.RequestRefresh();
        while (runtime.IsReloadPending)
        {
            if (runtime.PreparePending(force: true))
            {
                runtime.CommitPending();
                break;
            }
            if (runtime.View.Outcome == ModUpdateOutcome.Failed)
            {
                throw new InvalidDataException(string.Join(Environment.NewLine, runtime.View.Diagnostics.Select(diagnostic => diagnostic.Message)));
            }
        }
        return forms;
    }
}
