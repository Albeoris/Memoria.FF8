using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleReadySequenceTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(0x40, true)]
    [InlineData(0x100, true)]
    [InlineData(0x140, true)]
    public void Ready_selection_accepts_either_native_resource_ready_bit(ushort flags, bool expected)
    {
        NativeBattleGraphicsModel model = new(3, 2) { ResourceFlags44 = flags };
        Assert.Equal(expected, NativeBattleSequenceSelections.CanSelectReady505CE0(model));
    }

    [Fact]
    public void Raw_ready_selection_does_not_inherit_the_BF_party_status_gate()
    {
        NativeBattleGraphicsModel model = new(0, 2) { ResourceFlags44 = 0x40, Flags8 = 0x300000 };
        Assert.True(NativeBattleSequenceSelections.CanSelectReady505CE0(model));
        Assert.False(NativeBattleSequenceSelections.CanSelectTargetBF(model));
        Assert.True(NativeBattleSequenceSelections.CanSelectReady505CE0(model with { Flags8 = null }));
    }

    [Fact]
    public void Unknown_resource_flags_cannot_claim_ready_selection()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceSelections.CanSelectReady505CE0(new(3, 2)));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    public void Eligible_selection_of_an_absent_sequence_completes_without_replacing_live_sequence_or_pending_control(int selected)
    {
        NativeBattleGraphicsResource resource = new(ImmutableArray.Create<byte>(5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        NativeBattleGraphicsModel model = new(3, 2)
        {
            ModelId4 = 80,
            ResourceFlags44 = 0x100,
            Sequence = 2,
            SequenceFrame = 17,
            SequenceState = new() { Pending3 = 7, Offset46 = 29, Callback42 = 9 }
        };
        Assert.True(NativeBattleSequenceSelections.CanSelectReady505CE0(model));
        Assert.False(resource.ContainsNativeIndex505850(selected));
        NativeBattleGraphicsModel completed = NativeBattleSequenceSelections.SelectResource505BC0(model, resource, selected);
        Assert.Equal(model with { ResourceFlags44 = 0x140 }, completed);
        Assert.Same(model.SequenceState, completed.SequenceState);
    }
}
