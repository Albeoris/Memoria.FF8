using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMegaSpark279StackTests
{
    [Fact]
    public void First_equal_HP_row_records_actual_register_push_with_unknown_low_DI()
    {
        var stack = NativeBattleMegaSpark279Stack.ObserveVitals(Unknown(), Party(Vital(100, 100)), new[] { false, false, false });
        Assert.Equal((ushort)0, stack.ReadUInt16(-0xA6));
        Assert.Equal((ushort)0, stack.ReadUInt16(-0xAA));
        Assert.Throws<NotSupportedException>(() => stack.ReadUInt32(-0xA8));
        var push = stack.Observations[0];
        Assert.Equal(NativeBattleStackOperation.RegisterStore, push.Operation);
        Assert.Equal(0x4B0833u, push.Instruction);
        Assert.Equal(-0xA8, push.Offset);
        Assert.Equal(4, push.Length);
        Assert.Equal(new[] { false, false, true, true }, stack.Known.Skip(4).ToArray());
    }

    [Fact]
    public void Increasing_HP_publishes_the_current_vitals_record_pointer()
    {
        var stack = NativeBattleMegaSpark279Stack.ObserveVitals(Unknown(), Party(Vital(99, 100)), new[] { false, false, false });
        Assert.Equal(0x1D7694Eu, stack.ReadUInt32(-0xA8));
        Assert.Equal((ushort)0x1D7, stack.ReadUInt16(-0xA6));
        Assert.Equal(0x4B884Au, stack.Observations[^1].Instruction);
    }

    [Fact]
    public void Decreasing_HP_writes_literal_three_but_keeps_phase_one_pointer_unknown()
    {
        var stack = NativeBattleMegaSpark279Stack.ObserveVitals(Unknown(), Party(Vital(100, 50)), new[] { false, false, false });
        Assert.Equal(3u, stack.ReadUInt32(-0xA8));
        Assert.Equal((ushort)0, stack.ReadUInt16(-0xA6));
        Assert.Throws<NotSupportedException>(() => stack.ReadUInt16(-0xAA));
        Assert.Equal(0x4B07EAu, stack.Observations[^1].Instruction);
        Assert.Equal(NativeBattleStackOperation.RegisterStore, stack.Observations[^1].Operation);
    }

    [Fact]
    public void Phase_one_decreasing_HP_uses_only_the_explicit_current_execution_frame()
    {
        var execution = new NativeBattleExecutionStack(0x100000, 0x569C3D, NativeFieldRuntimeProfile.FF8EnExecutableHash, new string('A', 64), 1, 1);
        var stack = NativeBattleMegaSpark279Stack.ObserveVitals(Unknown(), Party(Vital(100, 50)), new[] { false, false, false }, execution);
        Assert.Equal(0xFFF71u, stack.ReadUInt32(-0xAC));
        Assert.Equal((ushort)0xF, stack.ReadUInt16(-0xAA));
    }

    [Fact]
    public void Later_active_party_row_saves_actual_previous_record_EDI()
    {
        var party = new NativeBattlePartyPresentation(ImmutableArray.Create<NativeBattlePartyPresentedVital?>(Vital(100, 100), Vital(100, 100), null));
        var stack = NativeBattleMegaSpark279Stack.ObserveVitals(Unknown(), party, new[] { false, false, false });
        Assert.Equal(0x1D76962u, stack.ReadUInt32(-0xA8));
        Assert.Equal((ushort)0x1D7, stack.ReadUInt16(-0xA6));
    }

    [Fact]
    public void Active_GF_track_follows_HP_and_uses_its_own_record_pointer()
    {
        var row = new NativeBattlePartyPresentedVital(100, 1000, 50, 100) { DisplayedHitPointsQ12 = 100 << 12, DisplayedGuardianForceHitPointsQ12 = 49 << 12 };
        var stack = NativeBattleMegaSpark279Stack.ObserveVitals(Unknown(), Party(row), new[] { true, false, false });
        Assert.Equal(0x1D76962u, stack.ReadUInt32(-0xA8));
        Assert.Equal(0x4B884Au, stack.Observations[^1].Instruction);
    }

    [Fact]
    public void Current_frame_invalidation_does_not_carry_the_previous_UI_value()
    {
        var stack = NativeBattleMegaSpark279Stack.ObserveVitals(Unknown(), Party(Vital(100, 100)), new[] { false, false, false });
        stack = stack.Invalidate(-0xAC, 8, 0x485460, "new actual action");
        Assert.Throws<NotSupportedException>(() => stack.ReadUInt16(-0xA6));
        Assert.Throws<NotSupportedException>(() => Unknown().ReadUInt16(-0xA6));
    }

    private static NativeBattleStackBytes Unknown() => NativeBattleStackBytes.Unknown("current-host-frame", -0xAC, 8);
    private static NativeBattlePartyPresentedVital Vital(int displayed, ushort target) => new(target, 1000, null, null) { DisplayedHitPointsQ12 = displayed << 12 };
    private static NativeBattlePartyPresentation Party(NativeBattlePartyPresentedVital row) => new(ImmutableArray.Create<NativeBattlePartyPresentedVital?>(row, null, null));
}
