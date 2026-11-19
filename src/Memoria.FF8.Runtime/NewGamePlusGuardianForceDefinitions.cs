using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Installs a self-contained resolved definition before saved local intent and current progression are restored.</summary>
public static class NewGamePlusGuardianForceDefinitions
{
    public const string EmbeddedSourceFingerprint = "new-game-plus-embedded-effective-definitions-v1";

    public static NativeCharacterVitals BindEmbedded(NativeCharacterVitals installed, ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> templates, ImmutableArray<string> excludedDocuments)
    {
        ArgumentNullException.ThrowIfNull(installed);
        GuardianForceAbilityProfile profile = new(templates, EmbeddedSourceFingerprint, [], [], excludedDocuments);
        profile.Validate();
        foreach (string identity in excludedDocuments)
        {
            GameplaySaveGuardianForceIntent.ValidateIdentity(identity);
        }
        GuardianForceAbilityComposition composition = GuardianForceAbilityComposer.Compose(templates, [], installed.MenuDefinitions.AbilityPoints);
        return installed with
        {
            MenuDefinitions = installed.MenuDefinitions with { Templates = composition.Templates, SourceTemplates = templates },
            GuardianForceProfile = profile,
            GuardianForceOpaqueBaseline = false,
            GuardianForceRecoveryProfile = null,
            GuardianForceRecoveryProgression = [],
            GuardianForceRecoveryTemplates = [],
            GuardianForceCompatibilityDiagnostics = []
        };
    }
}
