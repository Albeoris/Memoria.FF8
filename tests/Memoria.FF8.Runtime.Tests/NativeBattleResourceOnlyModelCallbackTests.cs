namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleResourceOnlyModelCallbackTests
{
    [Theory]
    [InlineData(3, 10, 15, 74)]
    [InlineData(1027, 266, 1039, 330)]
    public void Resource_only_callback_republishes_native_readiness_after_a_ring_sequence_clears_it(int modelFlags, int resourceFlags, int expectedModelFlags, int expectedResourceFlags)
    {
        NativeBattleSequenceState sequence = new() { Offset46 = 2, Pending3 = 0, Default2 = 1 };
        NativeBattleGraphicsModel original = new(6, (ushort)modelFlags) { ModelId4 = 143, Kind5 = 0, Flags8 = 0, ResourceFlags44 = (ushort)resourceFlags, Sequence = 1, SequenceFrame = 100, SequenceState = sequence };
        Assert.True(NativeBattleActionGraphics.IsModelBusy(original, 0x40));
        NativeBattleGraphicsModel ready = NativeBattleGraphicsModels.PublishResourceOnlyReadiness502AB0(original);
        Assert.Equal((ushort)expectedModelFlags, ready.Flags0);
        Assert.Equal((ushort)expectedResourceFlags, ready.ResourceFlags44);
        Assert.False(NativeBattleActionGraphics.IsModelBusy(ready, 0x40));
        Assert.Same(sequence, ready.SequenceState);
        Assert.Equal(original.SequenceFrame, ready.SequenceFrame);
        Assert.Equal((ushort)resourceFlags, original.ResourceFlags44);
        Assert.Same(ready, NativeBattleGraphicsModels.PublishResourceOnlyReadiness502AB0(ready));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(142)]
    [InlineData(144)]
    public void Ordinary_models_retain_their_existing_completion_and_resource_owners(byte modelId)
    {
        NativeBattleGraphicsModel original = new(3, 3) { ModelId4 = modelId, ResourceFlags44 = 10 };
        Assert.Same(original, NativeBattleGraphicsModels.PublishResourceOnlyReadiness502AB0(original));
    }
}
