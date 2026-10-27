using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuake38StateTests
{
    [Theory]
    [InlineData(0x278C8E0u, 0xD8)]
    [InlineData(0x279299Cu, 0x34)]
    [InlineData(0x2792B40u, 0x10)]
    [InlineData(0x2792D68u, 0x10)]
    [InlineData(0x2792E04u, 0x5C)]
    public void Scalar_access_uses_the_original_bounded_banks_and_native_signed_little_endian_values(uint address, int length)
    {
        NativeBattleQuake38State state = NativeBattleQuake38State.FromColdProcess();
        object identity = state.StorageIdentity;
        Assert.Equal(0u, state.Dword(address));
        Assert.Same(state, state.SetDword(address, 0xFEDCBA98));
        Assert.Equal((byte)0x98, state.Byte(address));
        Assert.Equal(unchecked((short)0xBA98), state.Word(address));
        Assert.Equal(0xFEDCBA98u, state.Dword(address));
        Assert.Same(state, state.SetWord(address + 1, -2));
        Assert.Equal(0xFEFFFE98u, state.Dword(address));
        state.SetByte(address + (uint)length - 1, 0xA5);
        Assert.Equal(0xA5, state.Byte(address + (uint)length - 1));
        Assert.Throws<InvalidDataException>(() => state.Word(address + (uint)length - 1));
        Assert.Throws<InvalidDataException>(() => state.SetDword(address + (uint)length - 3, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(address, -1));
        Assert.Empty(state.Read(address + (uint)length, 0));
        Assert.Same(identity, state.StorageIdentity);
        byte[] detached = state.Read(address, 4).ToArray();
        detached[0] = 0;
        Assert.Equal((byte)0x98, state.Byte(address));
    }

    [Fact]
    public void Capture_and_restore_preserve_old_JSON_shape_and_isolate_two_continuations_and_saved_actors()
    {
        NativeBattleQuake38State live = NativeBattleQuake38State.FromColdProcess();
        live.SetDword(0x278C8E0, 0x12345678);
        live.SetWord(0x2792E5E, -1234);
        live.SetSavedActor(0, live.SavedActors[0] with { Velocity = 33, RotationPadding18 = null });
        NativeBattleQuake38State first = NativeBattleStorageSnapshots.Capture(live);
        string firstJson = JsonSerializer.Serialize(first);
        string legacyShape = JsonSerializer.Serialize(new { first.Header, first.Middle, first.Pointers, first.Tail, first.End, first.SavedActors });
        Assert.Equal(legacyShape, firstJson);
        Assert.True(((INativeBattleStorageNode)first).IsSnapshot);
        Assert.Throws<InvalidOperationException>(() => first.SetByte(0x278C8E0, 0));
        Assert.Throws<InvalidOperationException>(() => first.SetSavedActor(0, NativeBattleQuake38SavedActor.FromColdProcess()));
        Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(live));
        Assert.Throws<InvalidOperationException>(() => live with { SavedActors = first.SavedActors });

        NativeBattleQuake38State deserialized = JsonSerializer.Deserialize<NativeBattleQuake38State>(firstJson)!;
        NativeBattleQuake38State a = NativeBattleStorageSnapshots.Restore(deserialized);
        NativeBattleQuake38State b = NativeBattleStorageSnapshots.Restore(deserialized);
        live.SetDword(0x278C8E0, 9);
        NativeBattleQuake38State second = NativeBattleStorageSnapshots.Capture(live);
        a.SetDword(0x278C8E0, 10);
        a.SetSavedActor(0, a.SavedActors[0] with { Velocity = 44 });
        Assert.Equal(0x12345678u, b.Dword(0x278C8E0));
        Assert.Equal((short)33, b.SavedActors[0].Velocity);
        Assert.Null(b.SavedActors[0].RotationPadding18);
        Assert.Equal((short)-1234, b.Word(0x2792E5E));
        Assert.Equal(9u, second.Dword(0x278C8E0));
        Assert.Equal(firstJson, JsonSerializer.Serialize(first));
        Assert.NotSame(a.StorageIdentity, b.StorageIdentity);
        Assert.Equal(firstJson, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(b)));
    }

    [Fact]
    public void A_scalar_checkpoint_rolls_back_all_banks_and_explicit_saved_actor_updates_without_old_reference_aliases()
    {
        NativeBattleQuake38State state = NativeBattleQuake38State.FromColdProcess();
        state.SetWord(0x278C916, 7);
        state.SetSavedActor(1, state.SavedActors[1] with { BouncePhase = 9 });
        string original = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state));
        state.BeginCheckpoint();
        Assert.Throws<InvalidOperationException>(state.BeginCheckpoint);
        state.SetWord(0x278C916, 8);
        state.SetDword(0x2792B40, 0x12345678);
        state.SetSavedActors(Enumerable.Range(0, 7).Select(_ => NativeBattleQuake38SavedActor.FromColdProcess() with { Velocity = 12 }).ToImmutableArray());
        state.SetSavedActor(1, state.SavedActors[1] with { Velocity = 13 });
        Assert.Throws<InvalidOperationException>(() => NativeBattleStorageSnapshots.Capture(state));
        state.RollbackCheckpoint();
        Assert.False(state.HasCheckpoint);
        Assert.Equal(original, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)));
        state.BeginCheckpoint();
        state.SetWord(0x278C916, 10);
        state.SetSavedActor(1, state.SavedActors[1] with { Velocity = 14 });
        state.CommitCheckpoint();
        NativeBattleQuake38State accepted = NativeBattleStorageSnapshots.Capture(state);
        Assert.Equal((short)10, accepted.Word(0x278C916));
        Assert.Equal((short)14, accepted.SavedActors[1].Velocity);
        Assert.Throws<InvalidOperationException>(state.RollbackCheckpoint);
    }

    [Fact]
    public void Aliased_working_facades_capture_one_authoritative_owner_and_warmed_scalar_access_does_not_allocate()
    {
        NativeBattleQuake38State state = NativeBattleQuake38State.FromColdProcess();
        NativeBattleQuake38State facade = state with { };
        state.SetWord(0x278C916, 1);
        Assert.Equal((short)1, facade.Word(0x278C916));
        NativeBattleStorageCaptureContext capture = new();
        NativeBattleQuake38State a = capture.Map(state);
        NativeBattleQuake38State b = capture.Map(facade);
        Assert.Same(a, b);
        for (int index = 0; index < 1000; index++)
        {
            state.SetWord(0x278C916, unchecked((short)index));
            state.Word(0x278C916);
        }
        object identity = state.StorageIdentity;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1000; index++)
        {
            state.SetWord(0x278C916, unchecked((short)index));
            state.Word(0x278C916);
            state.Byte(0x278C916);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Same(identity, state.StorageIdentity);
        Assert.Equal((short)1, a.Word(0x278C916));
    }
}
