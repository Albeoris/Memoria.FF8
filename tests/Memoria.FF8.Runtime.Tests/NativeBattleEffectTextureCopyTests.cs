using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectTextureCopyTests
{
    [Fact]
    public void OverlappingCopyUsesForwardDwordChunksRatherThanMemmove()
    {
        byte[] bytes = new byte[12];
        for (int index = 0; index < 6; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2), (ushort)(index + 1));
        }
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(0, bytes.ToImmutableArray())] };
        state = state.EnqueueCopy505EB0(0, 0, 4, 1, 1, 0);
        NativeBattleEffectTextureState restored = JsonSerializer.Deserialize<NativeBattleEffectTextureState>(JsonSerializer.Serialize(state))!;
        NativeBattleEffectTextureDrain step = state.Drain505D20();
        Assert.Equal(step.State.ReadVram(), restored.Drain505D20().State.ReadVram());
        byte[] result = step.State.ReadVram();
        Assert.Equal(new ushort[] { 1, 1, 2, 2, 4, 6 }, Enumerable.Range(0, 6).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(result.AsSpan(index * 2))));
        Assert.True(Assert.Single(step.Uploads).Copied);
        Assert.Equal((byte)3, step.State.Slots[0]!.Kind);
        Assert.Equal(0, step.State.Count);
    }

    [Fact]
    public void VerticalOverlapObservesEarlierRowsAndLaterQueuedCopiesObserveEarlierCommands()
    {
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(0, new byte[] { 1, 0, 2, 0 }.ToImmutableArray()), new(2048, new byte[] { 3, 0, 4, 0 }.ToImmutableArray())] };
        state = state.EnqueueCopy505EB0(0, 0, 2, 2, 0, 1).EnqueueCopy505EB0(0, 2, 2, 1, 4, 3);
        NativeBattleEffectTextureDrain step = state.Drain505D20();
        byte[] result = step.State.ReadVram();
        Assert.Equal(new byte[] { 1, 0, 2, 0 }, result.AsSpan(4096, 4).ToArray());
        Assert.Equal(new byte[] { 1, 0, 2, 0 }, result.AsSpan(2 * (3 * 1024 + 4), 4).ToArray());
        Assert.Equal(2, step.Uploads.Length);
    }

    [Fact]
    public void CopySharesThePhysicalQueueLimitAndRejectsReachedInvalidCoordinates()
    {
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess();
        for (int index = 0; index < 31; index++)
        {
            state = state.EnqueueCopy505EB0(0, 0, 1, 1, 1, 1);
        }
        Assert.Throws<NotSupportedException>(() => state.EnqueueCopy505EB0(0, 0, 1, 1, 1, 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectTextureState.FromColdProcess().EnqueueCopy505EB0(1023, 0, 2, 1, 0, 0).Drain505D20());
    }
}
