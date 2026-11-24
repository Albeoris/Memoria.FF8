using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8;

internal sealed partial class GameResourceHost
{
    private IReadOnlyList<ModDiagnostic> ValidateSpringBoneResources(FieldSpringBoneMods springMods, ModelMods modelMods, IReadOnlyList<EncodedResource> encodedResources)
    {
        if (springMods.Definitions.Count == 0)
        {
            return [];
        }
        ResourceService candidate = new(source, resourceLanguage);
        candidate.Prepare(null, encodedResources: encodedResources).Commit();
        using GameAssets assets = new(gameDirectory, resources: candidate);
        List<ModDiagnostic> diagnostics = [];
        foreach (FieldSpringBoneMod definition in springMods.Definitions)
        {
            try
            {
                if (definition.MainCharacter is { } name)
                {
                    definition.ValidateRig(assets.ReadFieldMainCharacterParents(name));
                }
                else if (definition.Entry is int entry)
                {
                    ModelCatalogEntry target = assets.GetFieldModelSource(definition.Field, entry);
                    FieldModel original = assets.LoadCatalogFieldModel(target);
                    FieldModel selected = modelMods.Apply(target.Id, original);
                    definition.ValidateRig(selected.GetSpringBoneParents());
                }
            }
            catch (Exception exception) when (IsSpringTargetError(exception))
            {
                diagnostics.Add(new("Memoria.InvalidFieldSpringBoneTarget", exception.Message, definition.PackageId, definition.RelativePath));
            }
        }

        Dictionary<string, FieldSpringBoneMod> shared = springMods.Definitions.Where(definition => definition.MainCharacter is not null).ToDictionary(definition => $"field/main_chr/{definition.MainCharacter}.mch", StringComparer.OrdinalIgnoreCase);
        if (shared.Count == 0 || diagnostics.Count != 0)
        {
            return diagnostics;
        }
        foreach (string modelId in modelMods.Variants.Where(variant => variant.Bank == "embedded" && variant.ClipSlot is null && variant.ModelId.StartsWith("field/", StringComparison.OrdinalIgnoreCase)).Select(variant => variant.ModelId).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string[] parts = modelId.Split('/');
            ModelCatalogEntry target;
            try
            {
                target = assets.GetFieldModelSource(parts[1], int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (Exception exception) when (IsSpringTargetError(exception))
            {
                continue;
            }
            if (!target.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase) || target.ModelLibraryResource is not { } libraryResource || !shared.TryGetValue(libraryResource, out FieldSpringBoneMod? definition))
            {
                continue;
            }
            try
            {
                FieldModel original = assets.LoadCatalogFieldModel(target);
                if (ReferenceEquals(springMods.Resolve(modelId, original), definition))
                {
                    definition.ValidateRig(modelMods.Apply(modelId, original).GetSpringBoneParents());
                }
            }
            catch (Exception exception) when (IsSpringTargetError(exception))
            {
                diagnostics.Add(new("Memoria.InvalidFieldSpringBoneTarget", $"{modelId}: {exception.Message}", definition.PackageId, definition.RelativePath));
            }
        }
        return diagnostics;
    }

    private static bool IsSpringTargetError(Exception exception) => exception is InvalidDataException or IOException or ArgumentException or NotSupportedException or OverflowException or UnauthorizedAccessException;
}
