using System.Collections.Immutable;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Resolves saved last-known contributions against the committed catalog without touching progression.</summary>
internal static class GuardianForceAbilityProfileCoordinator
{
    public static ImmutableArray<GuardianForceAbilityPatchLayer> ResolveLayers(GuardianForceAbilityMods mods, ModCatalogSnapshot catalog, GuardianForceAbilityProfile? profile)
    {
        List<GuardianForceAbilityPatchLayer> layers = mods.Layers.ToList();
        foreach (GuardianForceAbilityPatchLayer saved in profile?.AppliedLayers ?? [])
        {
            if (IsMissing(saved, catalog) && layers.All(layer => layer.Document.DocumentId != saved.Document.DocumentId))
            {
                int savedPosition = profile!.AppliedLayers.IndexOf(saved);
                layers.Insert(Math.Min(savedPosition, layers.Count), saved);
            }
        }
        return layers.ToImmutableArray();
    }

    public static ImmutableArray<ModDiagnostic> MissingLayerDiagnostics(ModCatalogSnapshot catalog, GuardianForceAbilityProfile? profile) => (profile?.AppliedLayers ?? []).Where(layer => IsMissing(layer, catalog)).Select(layer => new ModDiagnostic("Memoria.MissingGuardianForcePayload", "The previously applied GF package or payload is missing; its last-known contribution is retained until it is restored or deliberately disabled.", layer.PackageId, GuardianForceAbilityMods.PayloadPath)).ToImmutableArray();

    public static GuardianForceAbilityComposition Compose(GuardianForceAbilityProfile profile, ImmutableArray<byte> abilityPoints) => GuardianForceAbilityComposer.Compose(profile.Source, profile.AppliedLayers.Where(layer => !profile.ExcludedDocuments.Contains(layer.Document.DocumentId)).Concat(profile.LocalDocuments.Select(document => new GuardianForceAbilityPatchLayer("Playthrough", document))), abilityPoints);

    public static GuardianForceAbilityPreparedApply? PrepareIndependentLocalExport(GameSession session, string localDocumentId, string exportedDocumentId, string sourceRevision, long generation)
    {
        GuardianForceAbilityProfile? profile = session.CharacterVitals?.GuardianForceProfile;
        if (localDocumentId == exportedDocumentId || profile is null || !profile.LocalDocuments.Any(document => document.DocumentId == localDocumentId))
        {
            return null;
        }
        profile = profile with
        {
            LocalOwnershipAliases = profile.LocalOwnershipAliases.SetItem(exportedDocumentId, localDocumentId),
            ExcludedDocuments = profile.ExcludedDocuments.Contains(exportedDocumentId) ? profile.ExcludedDocuments : profile.ExcludedDocuments.Add(exportedDocumentId)
        };
        GuardianForceAbilityApplyPreview preview = session.PreviewGuardianForceAbilities(session.CharacterVitals!.ResolveGuardianForceTemplates(), profile, sourceRevision: sourceRevision, modGeneration: generation);
        return session.PrepareGuardianForceAbilities(preview, sourceRevision, generation);
    }

    private static bool IsMissing(GuardianForceAbilityPatchLayer saved, ModCatalogSnapshot catalog)
    {
        ModPackage? package = catalog.Packages.FirstOrDefault(package => package.Id.Equals(saved.PackageId, StringComparison.OrdinalIgnoreCase));
        return package is null || package.RequestedEnabled && package.Status != ModEffectiveStatus.IncompatibleEdition && !catalog.Files.Any(file => file.PackageId.Equals(saved.PackageId, StringComparison.OrdinalIgnoreCase) && file.RelativePath.Equals(GuardianForceAbilityMods.PayloadPath, StringComparison.OrdinalIgnoreCase));
    }
}
