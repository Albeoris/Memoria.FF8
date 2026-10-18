using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePartyPresentationTests
{
    [Fact]
    public void ContactUpdatesItsOwnCachedHpAndKoForcesZeroAfterHealing()
    {
        NativeBattlePartyPresentedVital row = new(500, 1000, null, null);
        row = row.Apply(Half(0, 37));
        Assert.Equal(463, row.HitPoints);
        row = row.Apply(Half(1, 900));
        Assert.Equal(1000, row.HitPoints);
        row = row.Apply(Half(1, 10) with { SavedStatuses4 = 1 });
        Assert.Equal(0, row.HitPoints);
        Assert.Equal(1000, row.MaximumHitPoints);
    }

    [Fact]
    public void WordWrapPrecedesTheOriginalSignedComparison()
    {
        NativeBattlePartyPresentedVital row = new(30000, 32767, null, null);
        Assert.Equal(40000, row.Apply(Half(1, 10000)).HitPoints);
        Assert.Equal(0, row.Apply(Half(0, 31000)).HitPoints);
        Assert.Equal(30001, row.Apply(Half(0, ushort.MaxValue)).HitPoints);
    }

    [Fact]
    public void SummonedGuardianContactChangesOnlyTheObservedGfCache()
    {
        NativeBattlePartyPresentedVital unknown = new(500, 1000, null, null);
        Assert.Throws<NotSupportedException>(() => unknown.Apply(Half(0x20, 37)));
        NativeBattlePartyPresentedVital row = unknown with { GuardianForceHitPoints = 100, GuardianForceMaximumHitPoints = 200 };
        row = row.Apply(Half(0x20, 37));
        Assert.Equal(500, row.HitPoints);
        Assert.Equal((ushort)63, row.GuardianForceHitPoints);
        Assert.Equal((ushort)0, row.Apply(Half(0x20, 100)).GuardianForceHitPoints);
    }

    [Fact]
    public void PhysicalCacheAndUnknownGfAllocationSurviveJson()
    {
        NativeBattlePartyPresentation state = new([new(500, 1000, null, null), null, null]);
        state.Validate();
        NativeBattlePartyPresentation restored = JsonSerializer.Deserialize<NativeBattlePartyPresentation>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(state.Rows[0], restored.Rows[0]);
        Assert.Null(restored.Rows[1]);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattlePartyPresentation>("{}"));
        Assert.Throws<InvalidDataException>(() => new NativeBattlePartyPresentation([new(500, 1000, 20, null), null, null]).Validate());
    }

    [Fact]
    public void HudDamageUsesMaximumBasedQ12StepAndClampsAtContactTarget()
    {
        NativeBattlePartyPresentedVital row = new(500, 1000, null, null) { DisplayedHitPointsQ12 = 500 << 12 };
        row = row.Apply(Half(0, 37));
        Assert.Equal(500 << 12, row.DisplayedHitPointsQ12);
        int[] expected = [489, 479, 468, 463];
        foreach (int hp in expected)
        {
            row = row.AdvanceHud4B0500(false);
            Assert.Equal(hp, row.DisplayedHitPointsQ12 / 4096);
        }
        Assert.Equal(463 << 12, row.DisplayedHitPointsQ12);
        Assert.Equal(row, row.AdvanceHud4B0500(false));
    }

    [Fact]
    public void HudHealingRetargetsWithoutDiscardingFractionalProgress()
    {
        NativeBattlePartyPresentedVital row = new(500, 1000, null, null) { DisplayedHitPointsQ12 = 500 << 12 };
        row = row.Apply(Half(1, 100)).AdvanceHud4B0500(false);
        Assert.Equal((500 << 12) + (1000 << 12) / 96, row.DisplayedHitPointsQ12);
        row = row.Apply(Half(0, 120)).AdvanceHud4B0500(false);
        Assert.Equal(500 << 12, row.DisplayedHitPointsQ12);
        Assert.Equal(480, row.HitPoints);
    }

    [Fact]
    public void GuardianHudHasIndependentStepAndStopsWhenItsCacheFlagIsClear()
    {
        NativeBattlePartyPresentedVital row = new(500, 1000, 100, 200) { DisplayedHitPointsQ12 = 500 << 12, DisplayedGuardianForceHitPointsQ12 = 100 << 12 };
        row = row.Apply(Half(0x20, 37));
        Assert.Equal(100 << 12, row.AdvanceHud4B0500(false).DisplayedGuardianForceHitPointsQ12);
        row = row.AdvanceHud4B0500(true);
        Assert.Equal((100 << 12) - (200 << 12) / 96, row.DisplayedGuardianForceHitPointsQ12);
        Assert.Equal(500 << 12, row.DisplayedHitPointsQ12);
    }

    [Fact]
    public void MidCountJsonKeepsFractionalNextCallbackAndRejectsMissingAccumulators()
    {
        NativeBattlePartyPresentedVital row = new(500, 1000, null, null) { DisplayedHitPointsQ12 = 500 << 12 };
        row = row.Apply(Half(0, 37)).AdvanceHud4B0500(false);
        NativeBattlePartyPresentedVital restored = JsonSerializer.Deserialize<NativeBattlePartyPresentedVital>(JsonSerializer.Serialize(row))!;
        Assert.Equal(row.AdvanceHud4B0500(false), restored.AdvanceHud4B0500(false));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattlePartyPresentedVital>("{\"HitPoints\":500,\"MaximumHitPoints\":1000,\"GuardianForceHitPoints\":null,\"GuardianForceMaximumHitPoints\":null}"));
        Assert.Throws<InvalidDataException>(() => new NativeBattlePartyPresentation([new(500, 1000, 100, 200), null, null]).Validate());
    }

    private static NativeBattleContactHalf Half(byte hitFlags, ushort amount) => new(0, 4, 0, hitFlags, 0, amount, 0);
}
