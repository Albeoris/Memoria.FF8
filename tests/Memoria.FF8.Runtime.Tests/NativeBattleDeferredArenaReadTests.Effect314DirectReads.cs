using static Memoria.FF8.Runtime.NativeBattleEffect314Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect314Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattleDeferredArenaReadTests
{
    [Theory]
    [InlineData(0, 32)]
    [InlineData(3, 24)]
    [InlineData(9, 15)]
    [InlineData(0, 0)]
    public void Direct_staging_piece_matches_canonical_overlay_and_remains_independently_writable(int offset, int length)
    {
        uint address = NativeBattleTemporaryMemory.Address;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(address, Enumerable.Range(0, 40).Select(index => (byte)(index + 1)).ToArray());
        staging = staging.SetResourcePointerMetadata(address + 4, new(new(0, 70000) { ResourceOnly = true }, null, 0));
        staging = staging.SetResourcePointerMetadata(address + 6, null);
        staging = staging.SetResourcePointerMetadata(address + 12, null);
        staging = staging.WriteResourcePointer(address + 20, new(null, address + 128, 4));
        NativeBattleEffect314Frame frame = NativeBattleEffect314ControllerTests.Frame() with { Staging = staging };
        NativeBattleEffectArena expected = PreviousReadBytes(frame, address + (uint)offset, length);
        NativeBattleEffectArena first = ReadBytes(frame, address + (uint)offset, length);
        NativeBattleEffectArena second = ReadBytes(frame, address + (uint)offset, length);
        AssertFacts(expected, first, length);
        AssertFacts(expected, second, length);
        string before = NativeBattleTestSnapshots.Json(first);
        frame = frame with { Staging = staging.WriteByte(address + (uint)offset, 200) };
        second.WriteByte(0, 201);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(first));
        first.WriteByte(length, 202);
        expected.WriteByte(length, 202);
        AssertFacts(expected, first, length + 1);
    }

    [Fact]
    public void Direct_state_piece_keeps_raw_unknown_bytes_and_drops_typed_metadata_like_the_canonical_copy()
    {
        NativeBattleEffect314Frame frame = NativeBattleEffect314ControllerTests.Frame();
        frame.State.Storage.Write(0, Enumerable.Range(0, 12).Select(index => (byte)(index + 7)).ToArray());
        frame.State.Storage.WriteResourcePointer(0, null);
        frame.State.Storage.WriteResourcePointer(4, new(null, NativeBattleTemporaryMemory.Address, 4));
        frame.State.Storage.MarkUnknown(9, 1);
        NativeBattleEffectArena expected = PreviousReadBytes(frame, BaseAddress, 12);
        NativeBattleEffectArena actual = ReadBytes(frame, BaseAddress, 12);
        AssertFacts(expected, actual, 12);
        Assert.Empty(actual.EnumerateResourcePointers());
        frame.State.Storage.WriteByte(8, 255);
        AssertFacts(expected, actual, 12);
    }

    [Fact]
    public void Direct_staging_reads_observe_the_current_transaction_and_detach_before_later_stores()
    {
        uint address = NativeBattleTemporaryMemory.Address;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(address, new byte[16]);
        using NativeBattleFileStagingTransaction transaction = new(staging);
        NativeBattleEffect314Frame frame = NativeBattleEffect314ControllerTests.Frame() with { Staging = transaction.View };
        transaction.View.WriteByte(address, 41);
        transaction.View.SetResourcePointerMetadata(address + 4, null);
        NativeBattleEffectArena expected = PreviousReadBytes(frame, address, 16);
        NativeBattleEffectArena first = ReadBytes(frame, address, 16);
        AssertFacts(expected, first, 16);
        transaction.View.WriteByte(address, 42);
        NativeBattleEffectArena second = ReadBytes(frame, address, 16);
        Assert.Equal(41, first.ReadByte(0));
        Assert.Equal(42, second.ReadByte(0));
        AssertFacts(expected, first, 16);
    }

    [Fact]
    public void Direct_reads_preserve_cross_domain_splits_and_validation_failures_without_modifying_sources()
    {
        NativeBattleEffect314Frame frame = NativeBattleEffect314ControllerTests.Frame() with { Staging = NativeBattleFileStaging.Unknown() };
        frame = frame with { Staging = frame.Staging.Write(BaseAddress + 12, Enumerable.Range(0, 64).Select(index => (byte)index).ToArray()) };
        NativeBattleEffectArena expected = PreviousReadBytes(frame, BaseAddress + 8, 12);
        NativeBattleEffectArena actual = ReadBytes(frame, BaseAddress + 8, 12);
        AssertFacts(expected, actual, 12);
        string before = NativeBattleTestSnapshots.Json(frame.State);
        Assert.Throws<InvalidDataException>(() => frame.State.ReadWithKnownness(BaseAddress + 10, new byte[4], new bool[4]));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleTemporaryMemory.ReadWithKnownness(frame.Staging, uint.MaxValue, new byte[2], new bool[2]));
        Assert.Throws<ArgumentException>(() => NativeBattleTemporaryMemory.ReadWithKnownness(frame.Staging, NativeBattleTemporaryMemory.Address, new byte[2], new bool[1]));
        Assert.Equal(before, NativeBattleTestSnapshots.Json(frame.State));
    }

    [Fact]
    public void Direct_staging_reads_reduce_temporary_graph_allocations_against_the_canonical_arena_copy()
    {
        uint address = NativeBattleTemporaryMemory.Address;
        NativeBattleEffect314Frame frame = NativeBattleEffect314ControllerTests.Frame() with { Staging = NativeBattleFileStaging.Unknown().Write(address, new byte[88]) };
        for (int index = 0; index < 16; index++)
        {
            _ = PreviousReadBytes(frame, address, 88).ReadByte(0);
            _ = ReadBytes(frame, address, 88).ReadByte(0);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 64; index++)
        {
            _ = PreviousReadBytes(frame, address, 88).ReadByte(0);
        }
        long previous = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 64; index++)
        {
            _ = ReadBytes(frame, address, 88).ReadByte(0);
        }
        long current = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(current < previous, $"Direct reads allocated {current} bytes; canonical arena copies allocated {previous} bytes.");
    }
}
