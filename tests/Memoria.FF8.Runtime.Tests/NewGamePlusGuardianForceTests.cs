using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NewGamePlusGuardianForceTests
{
    [Fact]
    public void EmbeddedDefinitionsRestoreBeforeLocalIntentAndPreserveCurrentEarnedProgress()
    {
        NativeCharacterVitals pristine = MagicSystemFixture.Create();
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> templates = pristine.ResolveGuardianForceTemplates().SetItem(0, [new(100, false, 20)]);
        NativeCharacterVitals current = NewGamePlusGuardianForceDefinitions.BindEmbedded(pristine, templates, ["source-document"]);
        current = current.ApplyGuardianForceTemplates(templates);
        GuardianForceAbilityPatchDocument local = new()
        {
            DocumentId = "local-edit",
            Operations = [new() { Id = "initial-state", GuardianForce = 0, Kind = GuardianForceAbilityOperationKind.SetInitialState, Ability = 100, InitialLearned = true }]
        };
        GuardianForceAbilityComposition composition = GuardianForceAbilityComposer.Compose(templates, [new("Playthrough", local)], pristine.MenuDefinitions.AbilityPoints);
        current = current.ApplyGuardianForceTemplates(composition.Templates) with { GuardianForceProfile = current.GuardianForceProfile! with { LocalDocuments = [local] } };
        current = current.AwardGuardianForceAbilityPoints(0, 7);
        GameplaySaveCharacters saved = JsonSerializer.Deserialize<GameplaySaveCharacters>(JsonSerializer.Serialize(GameplaySaveCharacters.Capture(current)))!;
        // A changed/missing authoring package has no role in reconstructing the saved baseline.
        NativeCharacterVitals changedInstallation = pristine.ApplyGuardianForceTemplates(pristine.ResolveGuardianForceTemplates().SetItem(0, [new(101, true, 1)]));
        NativeCharacterVitals bound = NewGamePlusGuardianForceDefinitions.BindEmbedded(changedInstallation, templates, ["source-document"]);
        NativeCharacterVitals restored = saved.Restore(bound);
        NativeGuardianForceAbility ability = restored.GuardianForceAbilities(0).Single(entry => entry.Ability == 100);
        Assert.False(ability.Learned);
        Assert.Equal(7, ability.Progress);
        Assert.True(restored.ResolveGuardianForceTemplates()[0].Single().InitialLearned);
        Assert.Equal(20, restored.ResolveGuardianForceTemplates()[0].Single().RequiredPoints);
        Assert.DoesNotContain(restored.GuardianForceAbilities(0), entry => entry.Ability == 101);
        Assert.Equal(NewGamePlusGuardianForceDefinitions.EmbeddedSourceFingerprint, restored.GuardianForceProfile!.SourceFingerprint);
        Assert.Empty(restored.GuardianForceProfile.AppliedLayers);
        Assert.Equal("local-edit", restored.GuardianForceProfile.LocalDocuments.Single().DocumentId);
        Assert.Contains("source-document", restored.GuardianForceProfile.ExcludedDocuments);
    }

    [Fact]
    public void EmbeddedDefinitionBindingDoesNotReplayItsStartingLearnedFlags()
    {
        NativeCharacterVitals pristine = MagicSystemFixture.Create();
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> templates = pristine.ResolveGuardianForceTemplates().SetItem(0, [new(100, false, 20)]);
        NativeCharacterVitals current = NewGamePlusGuardianForceDefinitions.BindEmbedded(pristine, templates, []).ApplyGuardianForceTemplates(templates).AwardGuardianForceAbilityPoints(0, 7);
        string before = JsonSerializer.Serialize(current.GuardianForces);
        NativeCharacterVitals rebound = NewGamePlusGuardianForceDefinitions.BindEmbedded(current, templates.SetItem(0, [new(100, true, 20)]), []);
        Assert.Equal(before, JsonSerializer.Serialize(rebound.GuardianForces));
        Assert.Equal(7, rebound.GuardianForceAbilities(0).Single(entry => entry.Ability == 100).Progress);
    }
}
