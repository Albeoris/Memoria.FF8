using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GuardianForcePassiveAbilityTests
{
    [Fact]
    public void LearnedPassiveAppliesFromAnUnjunctionedGfToTheWholeParty()
    {
        NativeCharacterVitals vitals = Learned(15);
        Assert.True(vitals.GuardianForcePassiveAbilityEnabled(15, 79));
        Assert.True(vitals.HasActivePassiveAbility(79));
        Assert.Equal(2, vitals.GetEncounterAbilityFlags());
        Assert.All(vitals.Records, row => Assert.Equal(0u, row.Junction.Abilities));
        Assert.DoesNotContain(vitals.Records, row => (row.Junction.GuardianForces & (1 << 15)) != 0);
    }

    [Fact]
    public void OwningGfMustBeObtainedAliveAndHaveAnActiveLearnedEntry()
    {
        NativeCharacterVitals vitals = Learned(15);
        vitals.GuardianForces[15] = vitals.GuardianForces[15] with { Flags = 0 };
        Assert.False(vitals.HasActivePassiveAbility(79));
        vitals.GuardianForces[15] = vitals.GuardianForces[15] with { Flags = 1, HitPoints = 0 };
        Assert.False(vitals.HasActivePassiveAbility(79));
        Assert.True(vitals.GuardianForcePassiveAbilityEnabled(15, 79));
        vitals.GuardianForces[15] = vitals.GuardianForces[15] with { HitPoints = 1, Progression = new(true, [new(79, true, Active: false)]) };
        Assert.False(vitals.HasActivePassiveAbility(79));
    }

    [Fact]
    public void DisabledPreferenceIsPerGfAndOldEquippedAbilityCannotBypassIt()
    {
        NativeCharacterVitals vitals = Learned(0);
        vitals.Records[0] = vitals.Records[0] with { Junction = vitals.Records[0].Junction with { Abilities = 79 } };
        NativeCharacterVitals disabled = vitals.SetGuardianForcePassiveAbilityEnabled(0, 79, false);
        Assert.True(vitals.HasActivePassiveAbility(79));
        Assert.False(disabled.HasActivePassiveAbility(79));
        Assert.Equal(0, disabled.GetEncounterAbilityFlags());
        disabled.GuardianForces[15] = disabled.GuardianForces[15] with { LearnedBank = new(0, 1UL << 15) };
        Assert.True(disabled.HasActivePassiveAbility(79));
        disabled = disabled.SetGuardianForcePassiveAbilityEnabled(15, 79, false);
        Assert.False(disabled.HasActivePassiveAbility(79));
        Assert.True(disabled.SetGuardianForcePassiveAbilityEnabled(0, 79, true).HasActivePassiveAbility(79));
        Assert.Throws<InvalidOperationException>(() => vitals.SetJunctionAbility(0, 0, 79, false));
        Assert.Equal(0u, vitals.SetJunctionAbility(0, 0, 0, false).Records[0].Junction.Abilities);
    }

    [Fact]
    public void OldJsonDefaultsToEnabledAndDisabledPreferenceRoundTrips()
    {
        NativeCharacterVitals original = Learned(15);
        string oldJson = JsonSerializer.Serialize(original);
        Assert.DoesNotContain("MoveFindDisabled", oldJson);
        NativeCharacterVitals restored = JsonSerializer.Deserialize<NativeCharacterVitals>(oldJson)!;
        Assert.True(restored.HasActivePassiveAbility(79));
        Assert.False(restored.GuardianForceRequiresMemoriaFormat);
        restored = restored.SetGuardianForcePassiveAbilityEnabled(15, 79, false);
        Assert.True(restored.GuardianForceRequiresMemoriaFormat);
        restored = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(restored))!;
        Assert.False(restored.HasActivePassiveAbility(79));
        Assert.True(restored.GuardianForces[15].MoveFindDisabled);
    }

    [Fact]
    public void ApLearningAutomaticallyEnablesAndSubsequentAwardsPreserveToggle()
    {
        NativeCharacterVitals vitals = Learned(0).SetGuardianForcePassiveAbilityEnabled(0, 79, false);
        vitals = vitals with { MenuDefinitions = vitals.MenuDefinitions with { Templates = vitals.ResolveGuardianForceTemplates().SetItem(0, [new(79, false, 3)]) } };
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { LearnedBank = new(0, 0), Learning = 79 };
        vitals = vitals.AwardGuardianForceAbilityPoints(0, 2);
        Assert.False(vitals.HasActivePassiveAbility(79));
        vitals = vitals.AwardGuardianForceAbilityPoints(0, 1);
        Assert.True(vitals.HasActivePassiveAbility(79));
        vitals = vitals.SetGuardianForcePassiveAbilityEnabled(0, 79, false).AwardGuardianForceAbilityPoints(0, 1);
        Assert.False(vitals.HasActivePassiveAbility(79));
    }

    [Fact]
    public void ItemRelearningEnablesButReteachingLearnedAbilityPreservesPreference()
    {
        NativeCharacterVitals vitals = Learned(0).SetGuardianForcePassiveAbilityEnabled(0, 79, false);
        vitals = vitals.TeachGuardianForceAbility(0, 79);
        Assert.False(vitals.HasActivePassiveAbility(79));
        vitals = vitals.ForgetGuardianForceAbility(0, 79);
        Assert.False(vitals.HasActivePassiveAbility(79));
        vitals = vitals.TeachGuardianForceAbility(0, 79);
        Assert.True(vitals.HasActivePassiveAbility(79));
    }

    [Fact]
    public void ExternalCompatibilityLearningEnablesThePassive()
    {
        NativeCharacterVitals vitals = Learned(0);
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { LearnedBank = new(0, 0), MoveFindDisabled = true };
        vitals = vitals.ApplyGuardianForceTemplates(vitals.ResolveGuardianForceTemplates());
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { LearnedBank = new(0, 1UL << 15) };
        vitals = vitals.NormalizeGuardianForceCompatibility();
        Assert.True(vitals.HasActivePassiveAbility(79));
    }

    [Fact]
    public void RestoringADormantLearnedEntryPreservesItsDisabledPreference()
    {
        NativeCharacterVitals vitals = Learned(0).SetGuardianForcePassiveAbilityEnabled(0, 79, false);
        var templates = vitals.ResolveGuardianForceTemplates();
        vitals = vitals.ApplyGuardianForceTemplates(templates.SetItem(0, []));
        Assert.False(vitals.HasActivePassiveAbility(79));
        vitals = vitals.ApplyGuardianForceTemplates(templates);
        Assert.True(vitals.GuardianForces[0].HasLearnedAbility(79));
        Assert.False(vitals.HasActivePassiveAbility(79));
    }

    [Fact]
    public void NewlyAddedInitiallyLearnedPassiveEnablesAutomatically()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { MoveFindDisabled = true };
        vitals = vitals.ApplyGuardianForceTemplates(vitals.ResolveGuardianForceTemplates().SetItem(0, [new(79, true, 3)]));
        Assert.True(vitals.HasActivePassiveAbility(79));
    }

    [Fact]
    public void UnsupportedOrUnlearnedTogglesAreRejected()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        Assert.Throws<InvalidOperationException>(() => vitals.SetGuardianForcePassiveAbilityEnabled(0, 79, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => vitals.SetGuardianForcePassiveAbilityEnabled(0, 78, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => vitals.GuardianForcePassiveAbilityEnabled(-1, 79));
    }

    private static NativeCharacterVitals Learned(int id)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals = vitals with { MenuDefinitions = vitals.MenuDefinitions with { PartyAbilityFlags = ImmutableArray.Create<byte>(1, 2, 4, 8, 16) } };
        vitals.GuardianForces[id] = vitals.GuardianForces[id] with { LearnedBank = vitals.GuardianForces[id].LearnedBank with { High = 1UL << 15 } };
        return vitals;
    }
}
