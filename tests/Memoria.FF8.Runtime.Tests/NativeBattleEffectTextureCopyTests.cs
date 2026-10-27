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
        NativeBattleEffectTextureState restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectTextureState>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MultirowTransfersPreserveScalarStateWithoutCopyingTheVramBankForEveryRow(bool rectangleUpload)
    {
        byte[] memory = new byte[NativeBattleEffectArena.Length];
        new Random(50520).NextBytes(memory);
        NativeBattleEffectArena source = new([new(0, memory.ToImmutableArray())]);
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = source.Capture().Regions };
        state = state.PublishVramSpan(0, []);
        const short width = 8;
        const short height = 64;
        const short destinationX = 16;
        const short destinationY = 128;
        byte[] transferred = new byte[2 * width * height];
        for (int row = 0; row < height; row++)
        {
            memory.AsSpan(2 * row * 1024, 2 * width).CopyTo(transferred.AsSpan(2 * row * width));
        }
        state = rectangleUpload ? state.EnqueueRectangle505DF0(new(destinationX, destinationY, width, height), new("texture-transfer", transferred.ToImmutableArray())) : state.EnqueueCopy505EB0(0, 0, width, height, destinationX, destinationY);
        long before = GC.GetAllocatedBytesForCurrentThread();
        NativeBattleEffectTextureDrain drained = state.Drain505D20();
        long transferBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        NativeBattleEffectArena scalar = NativeBattleStorageSnapshots.Restore<NativeBattleEffectArena>(source.Capture());
        for (int row = 0; row < height; row++)
        {
            scalar = scalar.Write(2 * ((destinationY + row) * 1024 + destinationX), transferred.AsSpan(2 * row * width, 2 * width));
        }
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state with { Count = 0, VramRegions = scalar.Capture().Regions })), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(drained.State)));
        Assert.Equal(transferred, Assert.Single(drained.Uploads).Bytes.ToArray());
        Assert.Equal(memory, state.ReadVram().AsSpan(0, memory.Length).ToArray());
        Assert.True(transferBytes < 1048576, $"A warm bounded transfer allocated {transferBytes} bytes.");
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
