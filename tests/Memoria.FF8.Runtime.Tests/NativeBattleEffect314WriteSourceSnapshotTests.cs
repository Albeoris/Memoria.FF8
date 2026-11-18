using System.Collections.Immutable;
using System.Reflection;
using static Memoria.FF8.Runtime.NativeBattleEffect314Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect314Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect314WriteSourceSnapshotTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Ordered_writes_match_previous_selected_arena_for_every_storage_domain(int domain)
    {
        NativeBattleEffect314Frame expected = Frame();
        NativeBattleEffect314Frame actual = Frame();
        uint address = domain switch
        {
            0 => CommonAddress + 0x80000,
            1 => RootBank + 12,
            2 => BaseAddress,
            3 => NativeBattleTemporaryMemory.Address,
            _ => CommonAddress + MainOffset + FragmentOffset
        };
        int length = domain == 1 ? 8 : domain == 2 ? 12 : 32;
        NativeBattleEffectArena source = Source(domain is 1 or 4);
        expected = PreviousWrite(expected, address, source, length);
        actual = WriteBytes(actual, address, source, length);
        AssertFrames(expected, actual);
        source.WriteByte(0, 255);
        AssertFrames(expected, actual);
        NativeBattleEffectArena expectedRead = ReadBytes(expected, address, length);
        NativeBattleEffectArena actualRead = ReadBytes(actual, address, length);
        Assert.Empty(actualRead.EnumerateExecutionReferences());
        Assert.Equal(expectedRead.Version, actualRead.Version);
        actualRead.WriteByte(0, 254);
        AssertFrames(expected, actual);
    }

    [Fact]
    public void Source_range_projection_is_detached_contains_only_complete_resource_descriptors_and_strips_execution_references()
    {
        NativeBattleEffectArena source = Source(false);
        foreach ((int offset, int length) in new (int, int)[] { (0, 32), (3, 24), (9, 15), (0, 0) })
        {
            NativeBattleEffectArena previous = NativeBattleEffect314TemporaryReads.Copy(source, offset, length);
            NativeBattleEffect314WriteSourceSnapshot captured = NativeBattleEffect314WriteSourceSnapshot.Capture(source, offset, length);
            NativeBattleEffectArena destination = NativeBattleEffectArena.Clear();
            CopyBytes(previous, 0, destination, 0, length);
            NativeBattleEffectArena actual = captured.CopyTo(NativeBattleEffectArena.Clear(), 0);
            Assert.Equal(NativeBattleTestSnapshots.Json(destination), NativeBattleTestSnapshots.Json(actual));
            Assert.Equal(destination.Version, actual.Version);
            captured.Data.AsSpan().Fill(99);
            Assert.NotEqual(99, source.ReadByte(0));
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Canonical_pointer_encoding_errors_precede_missing_destination_authority(bool numeric, bool unknown)
    {
        NativeBattleResourcePointer? pointer = numeric ? new(null, 0x12345678, 4) : null;
        ImmutableDictionary<int, NativeBattleResourcePointer?> pointers = ImmutableDictionary<int, NativeBattleResourcePointer?>.Empty.Add(0, pointer);
        NativeBattleEffectArena source = new([new(0, ImmutableArray.Create<byte>(1, 2, 3, 4))])
        {
            ResourcePointers = pointers,
            UnknownRegions = unknown ? [new(0, 1)] : []
        };
        NativeBattleEffect314Frame frame = Frame() with { Staging = null };
        InvalidDataException expected = Assert.Throws<InvalidDataException>(() => NativeBattleEffect314TemporaryReads.Copy(source, 0, 4));
        InvalidDataException actual = Assert.Throws<InvalidDataException>(() => WriteBytes(frame, NativeBattleTemporaryMemory.Address, source, 4));
        Assert.Equal(expected.Message, actual.Message);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectArena.CreateOwnedRead([1, 2, 3, 4], unknown ? [false, true, true, true] : [true, true, true, true], pointers, 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Task_header_and_null_pointer_failures_keep_the_previous_atomicity_and_error(bool nullPointer)
    {
        NativeBattleEffect314Frame expected = Frame();
        NativeBattleEffect314Frame actual = Frame();
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, new byte[12]);
        uint address = CommonAddress + MainOffset;
        if (nullPointer)
        {
            source.WriteResourcePointer(0, null);
            address += 12;
        }
        else
        {
            source.WriteByte(0, 1);
        }
        string before = NativeBattleTestSnapshots.Json(actual);
        long version = actual.Memory.Arena!.Version;
        Exception? previous = Record.Exception(() => PreviousWrite(expected, address, source, 12));
        Exception? current = Record.Exception(() => WriteBytes(actual, address, source, 12));
        Assert.NotNull(previous);
        Assert.NotNull(current);
        Assert.Equal(previous.GetType(), current.GetType());
        Assert.Equal(previous.Message, current.Message);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(actual));
        Assert.Equal(version, actual.Memory.Arena.Version);
    }

    [Fact]
    public void Aliased_cross_pool_write_captures_each_piece_after_the_previous_native_store()
    {
        NativeBattleEffect314Frame expected = AliasFrame(out NativeBattleEffectArena expectedSource);
        NativeBattleEffect314Frame actual = AliasFrame(out NativeBattleEffectArena actualSource);
        uint address = CommonAddress + MainOffset - 4;
        expected = PreviousWrite(expected, address, expectedSource, 16);
        actual = WriteBytes(actual, address, actualSource, 16);
        AssertFrames(expected, actual);
        Assert.Equal((ushort)2, ReadBytes(actual, CommonAddress + MainOffset, 2).ReadUInt16(0));
    }

    [Fact]
    public void Public_task_copy_keeps_execution_descriptors_while_effect314_selected_copy_strips_them()
    {
        NativeBattleEffectTaskPool expectedPool = Frame().Effect.Pools[1];
        NativeBattleEffect314Frame actual = Frame();
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, new byte[8]);
        BattleExecutionReference reference = new BattleExecutionReference.Targets(BattleModelTargets.From([Memoria.FF8.Tests.NativeBattleTypedFixture.OriginalOwner(0)]));
        source.WriteExecutionReference(0, 2, reference, [0x12, 0x34]);
        NativeBattleEffectTaskMemory.WriteBankSpan(expectedPool, 12, source, 8);
        actual = WriteBytes(actual, actual.Effect.Pools[1].BankAddress + 12, source, 8);
        Assert.Single(NativeBattleEffectTaskMemory.CaptureBank(expectedPool).EnumerateExecutionReferences());
        Assert.Empty(NativeBattleEffectTaskMemory.CaptureBank(actual.Effect.Pools[1]).EnumerateExecutionReferences());
    }

    [Fact]
    public void Captured_sources_allocate_less_than_selected_arena_plus_snapshot_without_changing_source_facts()
    {
        NativeBattleEffectArena source = Source(false);
        string beforeFacts = NativeBattleTestSnapshots.Json(source);
        for (int index = 0; index < 16; index++)
        {
            _ = NativeBattleEffect314TemporaryReads.Copy(source, 0, 32).ReadSnapshot(0, 32);
            _ = NativeBattleEffect314WriteSourceSnapshot.Capture(source, 0, 32);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 64; index++)
        {
            _ = NativeBattleEffect314TemporaryReads.Copy(source, 0, 32).ReadSnapshot(0, 32);
        }
        long previous = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 64; index++)
        {
            _ = NativeBattleEffect314WriteSourceSnapshot.Capture(source, 0, 32);
        }
        long current = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(current < previous, $"Captured sources allocated {current} bytes; selected arenas plus snapshots allocated {previous} bytes.");
        Assert.Equal(beforeFacts, NativeBattleTestSnapshots.Json(source));
    }

    private static NativeBattleEffect314Frame Frame() => NativeBattleEffect314ControllerTests.Frame() with { Staging = NativeBattleFileStaging.Unknown() };

    private static NativeBattleEffect314Frame AliasFrame(out NativeBattleEffectArena source)
    {
        NativeBattleEffect314Frame frame = Frame();
        frame.Memory.Arena!.Write(MainOffset - 6, new byte[20]);
        frame.Memory.Arena.WriteByte(MainOffset - 4, 2);
        source = NativeBattleEffectArena.BorrowWorking(frame.Memory.Arena.Working, MainOffset - 6, 20);
        return frame;
    }

    private static NativeBattleEffectArena Source(bool task)
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, Enumerable.Range(0, 32).Select(index => (byte)index).ToArray());
        source.MarkUnknown(2, 2);
        source.WriteResourcePointer(4, new(null, NativeBattleTemporaryMemory.Address, 4));
        if (!task)
        {
            source.WriteResourcePointer(12, null);
        }
        source.WriteResourcePointer(20, new(new(0, 70000) { ResourceOnly = true }, null, 0));
        source.WriteExecutionReference(28, 2, new BattleExecutionReference.Targets(BattleModelTargets.From([Memoria.FF8.Tests.NativeBattleTypedFixture.OriginalOwner(0)])), [0x34, 0x12]);
        return source;
    }

    private static void AssertFrames(NativeBattleEffect314Frame expected, NativeBattleEffect314Frame actual)
    {
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
        Assert.Equal(expected.Memory.Arena!.Version, actual.Memory.Arena!.Version);
        Assert.Equal(expected.State.Storage.Version, actual.State.Storage.Version);
        Assert.Equal(expected.Staging!.StorageVersion, actual.Staging!.StorageVersion);
        foreach ((NativeBattleEffectTaskPool left, NativeBattleEffectTaskPool right) in expected.Effect.Pools.Zip(actual.Effect.Pools))
        {
            Assert.Equal(NativeBattleEffectTaskMemory.CaptureBank(left).Version, NativeBattleEffectTaskMemory.CaptureBank(right).Version);
        }
    }

    private static NativeBattleEffect314Frame PreviousWrite(NativeBattleEffect314Frame frame, uint address, NativeBattleEffectArena source, int length)
    {
        int copied = 0;
        while (copied < length)
        {
            uint current = checked(address + (uint)copied);
            object?[] arguments = [frame, current, length - copied, -1, 0];
            int count = (int)typeof(NativeBattleEffect314Controller).GetMethod("RegionLength", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, arguments)!;
            int poolIndex = (int)arguments[3]!;
            int stateKind = (int)arguments[4]!;
            NativeBattleEffectArena selected = NativeBattleEffect314TemporaryReads.Copy(source, copied, count);
            if (poolIndex >= 0)
            {
                NativeBattleEffectTaskPool pool = frame.Effect.Pools[poolIndex];
                pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, checked((int)(current - pool.BankAddress)), selected, count);
                frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(poolIndex, pool) } };
            }
            else if (stateKind == 1)
            {
                CopyBytes(selected, 0, frame.Memory.Arena!, checked((int)(current - CommonAddress)), count);
            }
            else if (stateKind == 2)
            {
                CopyBytes(selected, 0, frame.State.ReadBank(RootBank), checked((int)(current - RootBank)), count);
            }
            else if (stateKind == 3)
            {
                frame = frame with { State = frame.State.WriteView(current, selected, count) };
            }
            else
            {
                frame = frame with { Staging = NativeBattleOffsetBodyContextMemory.Publish(RequireStaging(frame), current, selected, count) };
            }
            copied += count;
        }
        return frame;
    }
}
