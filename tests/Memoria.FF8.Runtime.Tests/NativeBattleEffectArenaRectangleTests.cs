using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectArenaRectangleTests
{
    [Fact]
    public void RectanglePointerResolvesCurrentArenaAfterMutationAndJsonRestore()
    {
        NativeBattleEffectArena arena = new NativeBattleEffectArena([]).Write(128, [1, 2, 3, 4]);
        NativeBattleEffectTextureState queued = NativeBattleEffectTextureState.FromColdProcess().EnqueueArenaRectangle505DF0(new(4, 7, 1, 2), arena, 128);
        arena = arena.Write(128, [5, 6, 7, 8]);
        NativeBattleEffectTextureState restored = RoundTrip(queued);
        NativeBattleEffectArena restoredArena = RoundTrip(arena);
        Assert.Null(restored.Slots[0]!.Source);
        Assert.Equal(128, restored.Slots[0]!.ArenaOffset);
        NativeBattleEffectTextureDrain continuous = queued.Drain505D20(arena);
        NativeBattleEffectTextureDrain resumed = restored.Drain505D20(restoredArena);
        Assert.Equal(new byte[] { 5, 6, 7, 8 }, resumed.Uploads.Single().Bytes);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(continuous)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(resumed)));
        Assert.Equal(new byte[] { 5, 6 }, resumed.State.ReadVram().AsSpan(2 * (7 * 1024 + 4), 2).ToArray());
        Assert.Equal(new byte[] { 7, 8 }, resumed.State.ReadVram().AsSpan(2 * (8 * 1024 + 4), 2).ToArray());
    }

    [Fact]
    public void EnqueueDoesNotConsumeUnknownPixelsButDrainRequiresTheirCurrentOwner()
    {
        NativeBattleEffectArena arena = new NativeBattleEffectArena([]).MarkUnknown(128, 4);
        NativeBattleEffectTextureState queued = NativeBattleEffectTextureState.FromColdProcess().EnqueueArenaRectangle505DF0(new(0, 0, 2, 1), arena, 128);
        Assert.Throws<NotSupportedException>(() => queued.Drain505D20());
        Assert.Throws<NotSupportedException>(() => queued.Drain505D20(arena));
        arena = arena.Write(128, [9, 8, 7, 6]);
        Assert.Equal(new byte[] { 9, 8, 7, 6 }, queued.Drain505D20(arena).Uploads.Single().Bytes);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectTextureState.FromColdProcess().EnqueueArenaRectangle505DF0(new(0, 0, 2, 1), arena, NativeBattleEffectArena.Length - 2));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectTextureState.FromColdProcess().EnqueueArenaRectangle505DF0(new(1024, 0, 1, 1), arena, 128));
    }

    [Fact]
    public void PointerUnionRejectsConflictingHeapOffsetAndStagingOwners()
    {
        NativeBattleEffectArena arena = new([]);
        NativeBattleEffectTextureState queued = NativeBattleEffectTextureState.FromColdProcess().EnqueueArenaRectangle505DF0(new(0, 0, 1, 1), arena, 128);
        NativeBattleEffectTimUpload pending = queued.Slots[0]!;
        AssertInvalid(pending with { Source = new("conflict", [1, 2]) });
        AssertInvalid(pending with { Offset = 1 });
        AssertInvalid(pending with { StagingAddress = NativeBattleFileStaging.Address });
        AssertInvalid(pending with { ArenaOffset = NativeBattleEffectArena.Length });

        void AssertInvalid(NativeBattleEffectTimUpload slot)
        {
            Assert.Throws<InvalidDataException>(() => (queued with { Slots = queued.Slots.SetItem(0, slot) }).Validate());
        }
    }

    [Fact]
    public void ReusedSlotsSwitchBetweenStagingArenaRectangleHeapAndCopyWithoutStalePointers()
    {
        NativeBattleEffectArena arena = new NativeBattleEffectArena([]).Write(128, [3, 4]);
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess();
        NativeBattleEffectTimUpload staging = new(null, 0) { StagingAddress = NativeBattleFileStaging.Address };
        state = state with { Slots = state.Slots.SetItem(0, staging) };
        state = state.EnqueueArenaRectangle505DF0(new(0, 0, 1, 1), arena, 128);
        Assert.Null(state.Slots[0]!.StagingAddress);
        state = state.Drain505D20(arena).State;
        state = state.EnqueueRectangle505DF0(new(0, 0, 1, 1), new("heap", [5, 6]));
        Assert.Null(state.Slots[0]!.ArenaOffset);
        state = state.Drain505D20().State.EnqueueArenaRectangle505DF0(new(0, 0, 1, 1), arena, 128).Drain505D20(arena).State;
        state = state.EnqueueCopy505EB0(0, 0, 1, 1, 1, 0);
        Assert.Null(state.Slots[0]!.ArenaOffset);
        Assert.Null(state.Slots[0]!.Source);
        Assert.Null(state.Slots[0]!.StagingAddress);
        Assert.Equal(new byte[] { 3, 4 }, state.Drain505D20().Uploads.Single().Bytes);
    }

    private static T RoundTrip<T>(T value) => NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value)))!);
}
