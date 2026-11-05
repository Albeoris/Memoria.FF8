using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class GuardianForceNativeReplacementTests
{
    [Fact]
    public void InstalledNativeReplacementSurvivesSemanticIntentRestoreAndReapply()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals();
        initial.GuardianForces[0] = initial.GuardianForces[0] with { Flags = (byte)(initial.GuardianForces[0].Flags | 1) };
        var baseline = initial.ResolveGuardianForceTemplates();
        GuardianForceAbilityProfile selectedProfile = new(baseline, "selected-installed-profile", [], [], []);
        NativeCharacterVitals selected = initial.ApplyGuardianForceTemplates(baseline) with { GuardianForceProfile = selectedProfile };
        int original = selected.GuardianForceAbilities(0).First(entry => !entry.Learned && entry.RequiredPoints > 7).Ability;
        int replacement = Enumerable.Range(1, initial.MenuDefinitions.AbilityPoints.Length - 1).First(ability => baseline[0].All(entry => entry.Ability != ability) && initial.MenuDefinitions.AbilityPoints[ability] > 7);
        NativeCharacterVitals earned = selected.SelectGuardianForceLearning(0, original).AwardGuardianForceAbilityPoints(0, 7);
        GuardianForceAbilityPatchDocument document = new()
        {
            DocumentId = "local/native-replacement.json",
            Operations = [new() { Id = "replace", GuardianForce = 0, Kind = GuardianForceAbilityOperationKind.ReplaceAbility, Ability = original, Replacement = replacement }]
        };
        GuardianForceAbilityComposition composition = GuardianForceAbilityComposer.Compose(baseline, [new("Playthrough", document)], initial.MenuDefinitions.AbilityPoints);
        Assert.Equal("Applied", Assert.Single(composition.Outcomes).Status);
        NativeCharacterVitals authored = earned.ApplyGuardianForceTemplates(composition.Templates) with { GuardianForceProfile = selectedProfile with { LocalDocuments = [document] } };
        authored = authored.SelectGuardianForceLearning(0, replacement).AwardGuardianForceAbilityPoints(0, 3);
        int forgotten = authored.GuardianForceAbilities(0).First(entry => entry.Learned).Ability;
        authored = authored.ForgetGuardianForceAbility(0, forgotten);
        string before = JsonSerializer.Serialize(authored);

        GameplaySaveCharacters saved = JsonSerializer.Deserialize<GameplaySaveCharacters>(JsonSerializer.Serialize(GameplaySaveCharacters.Capture(authored)))!;
        NativeCharacterVitals restored = saved.Restore(selected);

        restored.Validate();
        Assert.Equal(before, JsonSerializer.Serialize(authored));
        Assert.Equal(initial.MenuDefinitions.Learning, restored.MenuDefinitions.Learning);
        Assert.Equal(authored.MenuDefinitions.ResolveLearning(0), restored.MenuDefinitions.ResolveLearning(0));
        Assert.Equal(authored.GuardianForces[0].Progression!.Abilities, restored.GuardianForces[0].Progression!.Abilities);
        Assert.Equal(JsonSerializer.Serialize(authored.GuardianForces.Select(GameplaySaveGuardianForce.Capture)), JsonSerializer.Serialize(restored.GuardianForces.Select(GameplaySaveGuardianForce.Capture)));
        Assert.Equal(JsonSerializer.Serialize(authored.GuardianForces[0].LastProjection), JsonSerializer.Serialize(restored.GuardianForces[0].LastProjection));
        Assert.Equal(3, Entry(restored, replacement).EarnedPoints);
        Assert.Equal(7, Entry(restored, original).EarnedPoints);
        Assert.False(Entry(restored, original).Active);
        Assert.True(Entry(restored, forgotten).Forgotten);
        Assert.Same(restored, restored.NormalizeGuardianForceCompatibility());
        Assert.Equal(authored.MenuDefinitions.ResolveLearning(0).Select(rule => rule.Ability), saved.GuardianForces[0].AbilityBindings);

        NativeCharacterVitals reapplied = restored.ApplyGuardianForceTemplates(composition.Templates);
        Assert.Equal(JsonSerializer.Serialize(restored.GuardianForces), JsonSerializer.Serialize(reapplied.GuardianForces));
        NativeCharacterVitals snapshot = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(restored))!;
        snapshot.Validate();
        Assert.Same(snapshot, snapshot.NormalizeGuardianForceCompatibility());
        NativeCharacterVitals reverted = restored.ApplyGuardianForceTemplates(baseline) with { GuardianForceProfile = selectedProfile };
        Assert.Equal(7, Entry(reverted, original).EarnedPoints);
        Assert.Equal(3, Entry(reverted, replacement).EarnedPoints);
        Assert.False(Entry(reverted, replacement).Active);
        Assert.Equal(authored.MenuDefinitions.Learning[0].Select(rule => (rule.Requirement, rule.Exclusion)), restored.MenuDefinitions.ResolveLearning(0).Select(rule => (rule.Requirement, rule.Exclusion)));
    }

    [Fact]
    public void InstalledNativeReplacementIntoExistingAbilityRemainsAnExplicitConflict()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals();
        var baseline = initial.ResolveGuardianForceTemplates();
        GuardianForceAbilityPatchDocument document = new()
        {
            DocumentId = "local/duplicate-replacement.json",
            Operations = [new() { Id = "replace", GuardianForce = 0, Kind = GuardianForceAbilityOperationKind.ReplaceAbility, Ability = baseline[0][0].Ability, Replacement = baseline[0][1].Ability }]
        };

        GuardianForceAbilityComposition composition = GuardianForceAbilityComposer.Compose(baseline, [new("Playthrough", document)], initial.MenuDefinitions.AbilityPoints);

        Assert.Equal("ConflictingValue", Assert.Single(composition.Outcomes).Status);
        Assert.Equal(baseline.SelectMany(row => row), composition.Templates.SelectMany(row => row));
        (initial.MenuDefinitions with { Templates = composition.Templates }).Validate();
    }

    private static GuardianForceAbilityProgress Entry(NativeCharacterVitals source, int ability)
    {
        return source.GuardianForces[0].Progression!.Abilities.Single(entry => entry.Ability == ability);
    }
}
