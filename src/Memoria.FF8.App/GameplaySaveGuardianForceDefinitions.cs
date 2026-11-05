using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Composes selected installed GF layers before saved local intent and progression are overlaid.</summary>
internal static class GameplaySaveGuardianForceDefinitions
{
    internal static NativeCharacterVitals Select(NativeCharacterVitals initialized, GameResourceHost selected)
    {
        return Select(initialized, GuardianForceAbilityProfileCoordinator.ResolveLayers(selected.GuardianForceMods, selected.Mods.Catalog, null));
    }

    internal static NativeCharacterVitals Select(NativeCharacterVitals initialized, ImmutableArray<GuardianForceAbilityPatchLayer> layers)
    {
        ArgumentNullException.ThrowIfNull(initialized);
        if (layers.IsDefault)
        {
            throw new ArgumentException("Selected GF layers must have an explicit value.", nameof(layers));
        }
        if (layers.IsEmpty)
        {
            return initialized;
        }
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> source = initialized.GuardianForceProfile?.Source ?? (initialized.MenuDefinitions.SourceTemplates.IsDefault ? initialized.ResolveGuardianForceTemplates() : initialized.MenuDefinitions.SourceTemplates);
        string revision = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(initialized.MenuDefinitions)));
        GuardianForceAbilityProfile profile = new(source, revision, layers, [], []);
        profile.Validate();
        GuardianForceAbilityComposition composition = GuardianForceAbilityProfileCoordinator.Compose(profile, initialized.MenuDefinitions.AbilityPoints);
        NativeCharacterVitals result = initialized.ApplyGuardianForceTemplates(composition.Templates) with { GuardianForceProfile = profile };
        result.Validate();
        return result;
    }
}
