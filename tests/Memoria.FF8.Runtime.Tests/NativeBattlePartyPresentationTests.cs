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

    private static NativeBattleContactHalf Half(byte hitFlags, ushort amount) => new(0, 4, 0, hitFlags, 0, amount, 0);
}
