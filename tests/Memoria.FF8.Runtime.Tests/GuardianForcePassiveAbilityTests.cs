using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GuardianForcePassiveAbilityTests
{
    [Theory]
    [InlineData(79)]
    [InlineData(80)]
    [InlineData(81)]
    public void LearnedPassiveAppliesFromAnUnjunctionedGfToTheWholeParty(int ability)
    {
        NativeCharacterVitals vitals = Learned(15, ability);
        Assert.True(vitals.GuardianForcePassiveAbilityEnabled(15, ability));
        Assert.True(vitals.HasActivePassiveAbility(ability));
        Assert.Equal(1 << (ability - 78), vitals.GetEncounterAbilityFlags());
        Assert.All(vitals.Records, row => Assert.Equal(0u, row.Junction.Abilities));
        Assert.DoesNotContain(vitals.Records, row => (row.Junction.GuardianForces & (1 << 15)) != 0);
    }

    [Theory]
    [InlineData(79)]
    [InlineData(80)]
    [InlineData(81)]
    public void OwningGfMustBeObtainedAliveAndHaveAnActiveLearnedEntry(int ability)
    {
        NativeCharacterVitals vitals = Learned(15, ability);
        vitals.GuardianForces[15] = vitals.GuardianForces[15] with { Flags = 0 };
        Assert.False(vitals.HasActivePassiveAbility(ability));
        vitals.GuardianForces[15] = vitals.GuardianForces[15] with { Flags = 1, HitPoints = 0 };
        Assert.False(vitals.HasActivePassiveAbility(ability));
        Assert.True(vitals.GuardianForcePassiveAbilityEnabled(15, ability));
        vitals.GuardianForces[15] = vitals.GuardianForces[15] with { HitPoints = 1, Progression = new(true, [new(ability, true, Active: false)]) };
        Assert.False(vitals.HasActivePassiveAbility(ability));
        vitals.GuardianForces[15] = vitals.GuardianForces[15] with { Progression = new(true, [new(ability, false)]) };
        Assert.False(vitals.HasActivePassiveAbility(ability));
        vitals.GuardianForces[15] = vitals.GuardianForces[15] with { Progression = new(true, [new(ability, false, Forgotten: true)]) };
        Assert.False(vitals.HasActivePassiveAbility(ability));
        vitals.GuardianForces[15] = vitals.GuardianForces[15] with { Progression = new(true, [new(ability, true)]) };
        Assert.True(vitals.HasActivePassiveAbility(ability));
    }

    [Theory]
    [InlineData(79)]
    [InlineData(80)]
    [InlineData(81)]
    public void DisabledPreferenceIsPerGfAndOldEquippedAbilityCannotBypassIt(int ability)
    {
        NativeCharacterVitals vitals = Learned(0, ability);
        vitals.Records[0] = vitals.Records[0] with { Junction = vitals.Records[0].Junction with { Abilities = (uint)ability } };
        NativeCharacterVitals disabled = vitals.SetGuardianForcePassiveAbilityEnabled(0, ability, false);
        Assert.True(vitals.HasActivePassiveAbility(ability));
        Assert.False(disabled.HasActivePassiveAbility(ability));
        Assert.Equal(0, disabled.GetEncounterAbilityFlags());
        disabled.GuardianForces[15] = disabled.GuardianForces[15] with { LearnedBank = new(0, 1UL << (ability - 64)) };
        Assert.True(disabled.HasActivePassiveAbility(ability));
        disabled = disabled.SetGuardianForcePassiveAbilityEnabled(15, ability, false);
        Assert.False(disabled.HasActivePassiveAbility(ability));
        Assert.True(disabled.SetGuardianForcePassiveAbilityEnabled(0, ability, true).HasActivePassiveAbility(ability));
        Assert.Throws<InvalidOperationException>(() => vitals.SetJunctionAbility(0, 0, ability, false));
        Assert.Equal(0u, vitals.SetJunctionAbility(0, 0, 0, false).Records[0].Junction.Abilities);
    }

    [Theory]
    [InlineData(79)]
    [InlineData(80)]
    [InlineData(81)]
    public void OldJsonDefaultsToEnabledAndDisabledPreferenceRoundTrips(int ability)
    {
        NativeCharacterVitals original = Learned(15, ability);
        string oldJson = JsonSerializer.Serialize(original);
        Assert.DoesNotContain("MoveFindDisabled", oldJson);
        Assert.DoesNotContain("EncHalfDisabled", oldJson);
        Assert.DoesNotContain("EncNoneDisabled", oldJson);
        NativeCharacterVitals restored = JsonSerializer.Deserialize<NativeCharacterVitals>(oldJson)!;
        Assert.True(restored.HasActivePassiveAbility(ability));
        Assert.False(restored.GuardianForceRequiresMemoriaFormat);
        restored = restored.SetGuardianForcePassiveAbilityEnabled(15, ability, false);
        Assert.True(restored.GuardianForceRequiresMemoriaFormat);
        restored = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(restored))!;
        Assert.False(restored.HasActivePassiveAbility(ability));
        Assert.True(Disabled(restored.GuardianForces[15], ability));
    }

    [Theory]
    [InlineData(79)]
    [InlineData(80)]
    [InlineData(81)]
    public void ApLearningAutomaticallyEnablesAndSubsequentAwardsPreserveToggle(int ability)
    {
        NativeCharacterVitals vitals = Learned(0, ability).SetGuardianForcePassiveAbilityEnabled(0, ability, false);
        vitals = vitals with { MenuDefinitions = vitals.MenuDefinitions with { Templates = vitals.ResolveGuardianForceTemplates().SetItem(0, [new(ability, false, 3)]) } };
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { LearnedBank = new(0, 0), Learning = (byte)ability };
        vitals = vitals.AwardGuardianForceAbilityPoints(0, 2);
        Assert.False(vitals.HasActivePassiveAbility(ability));
        vitals = vitals.AwardGuardianForceAbilityPoints(0, 1);
        Assert.True(vitals.HasActivePassiveAbility(ability));
        vitals = vitals.SetGuardianForcePassiveAbilityEnabled(0, ability, false).AwardGuardianForceAbilityPoints(0, 1);
        Assert.False(vitals.HasActivePassiveAbility(ability));
    }

    [Theory]
    [InlineData(79)]
    [InlineData(80)]
    [InlineData(81)]
    public void ItemRelearningEnablesButReteachingLearnedAbilityPreservesPreference(int ability)
    {
        NativeCharacterVitals vitals = Learned(0, ability).SetGuardianForcePassiveAbilityEnabled(0, ability, false);
        vitals = vitals.TeachGuardianForceAbility(0, ability);
        Assert.False(vitals.HasActivePassiveAbility(ability));
        vitals = vitals.ForgetGuardianForceAbility(0, ability);
        Assert.False(vitals.HasActivePassiveAbility(ability));
        vitals = vitals.TeachGuardianForceAbility(0, ability);
        Assert.True(vitals.HasActivePassiveAbility(ability));
    }

    [Theory]
    [InlineData(79)]
    [InlineData(80)]
    [InlineData(81)]
    public void ExternalCompatibilityLearningEnablesThePassive(int ability)
    {
        NativeCharacterVitals vitals = Learned(0, ability);
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { LearnedBank = new(0, 0), MoveFindDisabled = ability == 79, EncHalfDisabled = ability == 80, EncNoneDisabled = ability == 81 };
        vitals = vitals.ApplyGuardianForceTemplates(vitals.ResolveGuardianForceTemplates());
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { LearnedBank = new(0, 1UL << (ability - 64)) };
        vitals = vitals.NormalizeGuardianForceCompatibility();
        Assert.True(vitals.HasActivePassiveAbility(ability));
    }

    [Theory]
    [InlineData(79)]
    [InlineData(80)]
    [InlineData(81)]
    public void RestoringADormantLearnedEntryPreservesItsDisabledPreference(int ability)
    {
        NativeCharacterVitals vitals = Learned(0, ability).SetGuardianForcePassiveAbilityEnabled(0, ability, false);
        var templates = vitals.ResolveGuardianForceTemplates();
        vitals = vitals.ApplyGuardianForceTemplates(templates.SetItem(0, []));
        Assert.False(vitals.HasActivePassiveAbility(ability));
        vitals = vitals.ApplyGuardianForceTemplates(templates);
        Assert.True(vitals.GuardianForces[0].HasLearnedAbility(ability));
        Assert.False(vitals.HasActivePassiveAbility(ability));
    }

    [Theory]
    [InlineData(79)]
    [InlineData(80)]
    [InlineData(81)]
    public void NewlyAddedInitiallyLearnedPassiveEnablesAutomatically(int ability)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { MoveFindDisabled = ability == 79, EncHalfDisabled = ability == 80, EncNoneDisabled = ability == 81 };
        vitals = vitals.ApplyGuardianForceTemplates(vitals.ResolveGuardianForceTemplates().SetItem(0, [new(ability, true, 3)]));
        Assert.True(vitals.HasActivePassiveAbility(ability));
    }

    [Theory]
    [InlineData(79)]
    [InlineData(80)]
    [InlineData(81)]
    public void UnsupportedOrUnlearnedTogglesAreRejected(int ability)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        Assert.Throws<InvalidOperationException>(() => vitals.SetGuardianForcePassiveAbilityEnabled(0, ability, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => vitals.SetGuardianForcePassiveAbilityEnabled(0, 78, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => vitals.SetGuardianForcePassiveAbilityEnabled(0, 82, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => vitals.GuardianForcePassiveAbilityEnabled(-1, ability));
    }

    [Fact]
    public void PassiveTogglesAreIndependentWithinTheSameGuardianForce()
    {
        NativeCharacterVitals vitals = Learned(15);
        vitals.GuardianForces[15] = vitals.GuardianForces[15] with { LearnedBank = new(0, (1UL << 15) | (1UL << 16) | (1UL << 17)) };
        Assert.Equal(14, vitals.GetEncounterAbilityFlags());
        vitals = vitals.SetGuardianForcePassiveAbilityEnabled(15, 80, false);
        Assert.Equal(10, vitals.GetEncounterAbilityFlags());
        Assert.False(vitals.GuardianForces[15].MoveFindDisabled);
        Assert.True(vitals.GuardianForces[15].EncHalfDisabled);
        Assert.False(vitals.GuardianForces[15].EncNoneDisabled);
        vitals = vitals.SetGuardianForcePassiveAbilityEnabled(15, 81, false);
        Assert.Equal(2, vitals.GetEncounterAbilityFlags());
        vitals = vitals.SetGuardianForcePassiveAbilityEnabled(15, 79, false);
        Assert.Equal(0, vitals.GetEncounterAbilityFlags());
        vitals = vitals.SetGuardianForcePassiveAbilityEnabled(15, 80, true);
        Assert.Equal(4, vitals.GetEncounterAbilityFlags());
        Assert.True(vitals.GuardianForces[15].MoveFindDisabled);
        Assert.False(vitals.GuardianForces[15].EncHalfDisabled);
        Assert.True(vitals.GuardianForces[15].EncNoneDisabled);
    }

    [Theory]
    [InlineData(79)]
    [InlineData(80)]
    [InlineData(81)]
    public void GameplaySavePreservesDisabledPreferenceAndOlderSavesDefaultToEnabled(int ability)
    {
        NativeCharacterVitals installed = Learned(15, ability);
        NativeCharacterVitals source = installed.SetGuardianForcePassiveAbilityEnabled(15, ability, false);
        GameplaySaveCharacters saved = GameplaySaveCharacters.Capture(source);
        Assert.Equal(ability == 79, saved.GuardianForces[15].MoveFindDisabled);
        Assert.Equal(ability == 80, saved.GuardianForces[15].EncHalfDisabled);
        Assert.Equal(ability == 81, saved.GuardianForces[15].EncNoneDisabled);
        string json = JsonSerializer.Serialize(saved);
        GameplaySaveCharacters restoredSave = JsonSerializer.Deserialize<GameplaySaveCharacters>(json)!;
        NativeCharacterVitals restored = restoredSave.Restore(installed);
        Assert.True(Disabled(restored.GuardianForces[15], ability));
        Assert.False(restored.HasActivePassiveAbility(ability));
        JsonNode olderSave = JsonNode.Parse(json)!;
        foreach (JsonNode? guardianForce in olderSave["GuardianForces"]!.AsArray())
        {
            JsonObject row = guardianForce!.AsObject();
            row.Remove("MoveFindDisabled");
            row.Remove("EncHalfDisabled");
            row.Remove("EncNoneDisabled");
        }
        restored = JsonSerializer.Deserialize<GameplaySaveCharacters>(olderSave.ToJsonString())!.Restore(installed);
        Assert.False(Disabled(restored.GuardianForces[15], ability));
        Assert.True(restored.HasActivePassiveAbility(ability));
        Assert.Equal(1 << (ability - 78), restored.GetEncounterAbilityFlags());
    }

    private static bool Disabled(NativeGuardianForceVital guardianForce, int ability)
    {
        return ability switch
        {
            79 => guardianForce.MoveFindDisabled,
            80 => guardianForce.EncHalfDisabled,
            81 => guardianForce.EncNoneDisabled,
            _ => throw new ArgumentOutOfRangeException(nameof(ability))
        };
    }

    private static NativeCharacterVitals Learned(int id, int ability = 79)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals = vitals with { MenuDefinitions = vitals.MenuDefinitions with { PartyAbilityFlags = ImmutableArray.Create<byte>(1, 2, 4, 8, 16) } };
        vitals.GuardianForces[id] = vitals.GuardianForces[id] with { LearnedBank = vitals.GuardianForces[id].LearnedBank with { High = 1UL << (ability - 64) } };
        return vitals;
    }
}
