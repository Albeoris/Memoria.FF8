using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectTextureTests
{
    [Fact]
    public void Multirow_rectangle_keeps_scalar_region_layout_with_bounded_page_publication()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, new byte[NativeBattleEffectArena.Length]);
        byte[] data = new byte[128 * 128 * 2];
        new Random(505).NextBytes(data);
        NativeBattleEffectTextureState queued = (NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = source.Capture().Regions }).PublishVramSpan(0, []).EnqueueRectangle505DF0(new(32, 64, 128, 128), new("rectangle", data.ToImmutableArray()));
        NativeBattleEffectArena scalar = NativeBattleStorageSnapshots.Restore<NativeBattleEffectArena>(source.Capture());
        for (int row = 0; row < 128; row++)
        {
            scalar = scalar.Write(2 * ((64 + row) * 1024 + 32), data.AsSpan(row * 256, 256));
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        NativeBattleEffectTextureDrain drained = queued.Drain505D20();
        long batchedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(JsonSerializer.Serialize(scalar.Capture().Regions), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(drained.State).VramRegions));
        Assert.Equal(data, drained.Uploads.Single().Bytes);
        Assert.True(batchedBytes < 2097152, $"A warm rectangle publication allocated {batchedBytes} bytes.");
        Assert.All(source.Read(0, NativeBattleEffectArena.Length), value => Assert.Equal(0, value));
    }

    [Fact]
    public void QueueDrainUploadsBothBlocksInOrderAndRetainsPhysicalSlotsAcrossInitialization()
    {
        NativeBattleEffectHeapAllocation source = Tim();
        NativeBattleEffectTextureState cold = NativeBattleEffectTextureState.FromColdProcess();
        var queued = cold.EnqueueTim505E30(source);
        Assert.Equal(source.Data.Length, queued.NextOffset);
        Assert.Equal(1, queued.State.Count);
        Assert.Equal(0, cold.Count);
        Assert.All(cold.ReadVram(), value => Assert.Equal(0, value));
        NativeBattleEffectTextureDrain drained = queued.State.Drain505D20();
        Assert.Equal(2, drained.Uploads.Length);
        Assert.Equal((short)0, drained.Uploads[0].X);
        Assert.Equal((short)64, drained.Uploads[1].X);
        Assert.Equal((short)256, drained.Uploads[1].Y);
        Assert.Equal(0x3210, BinaryPrimitives.ReadUInt16LittleEndian(drained.State.ReadVram().AsSpan(2 * (256 * 1024 + 64))));
        Assert.Equal(0, drained.State.Count);
        Assert.Equal(source, drained.State.Slots[0]!.Source);
        Assert.Equal(drained.State.ReadVram(), drained.State.Initialize500400().ReadVram());
        Assert.Equal(queued.State.Slots, queued.State.Initialize500400().Slots);
        Assert.Empty(drained.State.Drain505D20().Uploads);
        NativeBattleEffectTextureState restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectTextureState>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(drained.State)))!);
        restored.Validate();
        Assert.Equal(drained.State.ReadVram(), restored.ReadVram());
    }

    [Fact]
    public void QueueHonorsTheNativeThirtyOneSuccessfulUploadsAndRejectsMalformedSource()
    {
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess();
        for (int index = 0; index < 31; index++)
        {
            state = state.EnqueueTim505E30(Tim()).State;
        }
        Assert.Equal(31, state.Count);
        Assert.Throws<NotSupportedException>(() => state.EnqueueTim505E30(Tim()));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectTextureState.FromColdProcess().EnqueueTim505E30(new("bad", [0, 0, 0])));
    }

    [Fact]
    public void AnimationRegistrationUsesUnsignedRelativeWordOffsetsAndSurvivesQueueInitialization()
    {
        byte[] bytes = new byte[0x8010];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 0x8000);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 0x8002);
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess().Register5099A0(new("original-descriptor", bytes.ToImmutableArray()));
        Assert.Equal(0x8000, state.Animation!.StreamOffset);
        Assert.Equal(0x8002, state.Animation.ModelTableOffset);
        Assert.Equal(state.Animation, state.Initialize500400().Animation);
    }

    [Fact]
    public void MutableArenaTimResolvesItsOriginalPointerAtDrainAndSlotReuseClearsThatPointer()
    {
        NativeBattleEffectHeapAllocation tim = Tim();
        NativeBattleEffectArena arena = new NativeBattleEffectArena([]).Write(128, tim.Data.AsSpan());
        NativeBattleEffectTextureState queued = NativeBattleEffectTextureState.FromColdProcess().EnqueueArenaTim505E30(arena, 128).State;
        Assert.Throws<NotSupportedException>(() => queued.Drain505D20());
        arena = arena.Write(128 + 64, [0xCD, 0xAB]);
        NativeBattleEffectTextureDrain drained = queued.Drain505D20(arena);
        Assert.Equal(0xABCD, BinaryPrimitives.ReadUInt16LittleEndian(drained.State.ReadVram().AsSpan(2 * (256 * 1024 + 64))));
        NativeBattleEffectTextureState baseline = NativeBattleStorageSnapshots.Capture(drained.State);
        NativeBattleEffectTextureState copy = NativeBattleStorageSnapshots.Restore(baseline).EnqueueCopy505EB0(64, 256, 1, 1, 65, 256);
        Assert.Null(copy.Slots[0]!.ArenaOffset);
        Assert.Equal(0xABCD, BinaryPrimitives.ReadUInt16LittleEndian(copy.Drain505D20().State.ReadVram().AsSpan(2 * (256 * 1024 + 65))));
        NativeBattleEffectTextureState heap = NativeBattleStorageSnapshots.Restore(baseline).EnqueueTim505E30(tim).State;
        Assert.Null(heap.Slots[0]!.ArenaOffset);
        Assert.Equal(0x3210, BinaryPrimitives.ReadUInt16LittleEndian(heap.Drain505D20().State.ReadVram().AsSpan(2 * (256 * 1024 + 64))));
        NativeBattleEffectTextureState rectangle = NativeBattleStorageSnapshots.Restore(baseline).EnqueueRectangle505DF0(new(64, 256, 1, 1), new("replacement-rectangle", [0xEF, 0xBE]));
        Assert.Null(rectangle.Slots[0]!.ArenaOffset);
        Assert.Equal(0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(rectangle.Drain505D20().State.ReadVram().AsSpan(2 * (256 * 1024 + 64))));
    }

    private static NativeBattleEffectHeapAllocation Tim()
    {
        byte[] data = new byte[66];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 16);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), 44);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(18), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(22), 31);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(52), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(56), 64);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(58), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(60), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(62), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(64), 0x3210);
        return new("tim", data.ToImmutableArray());
    }
}
