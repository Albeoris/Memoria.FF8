using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using static Memoria.FF8.Runtime.NativeBattleEffect314Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect314Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDeferredArenaReadTests
{
    [Fact]
    public void Factory_reads_share_one_validated_version_marker_and_hydration_removes_only_that_marker()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.CreateOwnedRead([1, 2, 3, 4], new[] { true, true, true, true }, null, 2);
        ConditionalWeakTable<NativeBattleEffectArena, object> validations = (ConditionalWeakTable<NativeBattleEffectArena, object>)typeof(NativeBattleEffectArena).GetField("ValidatedSnapshots", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        Assert.True(HasDeferredMarker(arena));
        Assert.False(validations.TryGetValue(arena, out _));
        for (int index = 0; index < 32; index++)
        {
            arena.Validate();
            Assert.Equal((ushort)0x0201, arena.ReadUInt16(0));
            Assert.Equal(2, arena.Version);
        }
        Assert.False(validations.TryGetValue(arena, out _));
        Assert.Throws<InvalidDataException>(() => arena.Write(-1, new byte[] { 99 }));
        Assert.True(HasDeferredMarker(arena));
        NativeBattleEffectArena clone = arena with { };
        Assert.False(HasDeferredMarker(arena));
        Assert.False(HasDeferredMarker(clone));
        Assert.Same(arena.Working, clone.Working);
        Assert.Equal(2, clone.Version);
        clone.WriteByte(0, 99);
        Assert.Equal((byte)99, arena.ReadByte(0));
        Assert.Equal(3, arena.Version);
        NativeBattleEffectArena ordinary = new([new(0, [5])]);
        ordinary.Validate();
        Assert.True(validations.TryGetValue(ordinary, out _));
        Assert.False(HasDeferredMarker(ordinary));
    }

    [Fact]
    public void Factory_validation_rejects_contradictory_descriptors_and_clones_cannot_replace_validated_mutable_facts()
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectArena.CreateOwnedRead([1, 2, 3, 4], new[] { true, true, true, true }, ImmutableDictionary<int, NativeBattleResourcePointer?>.Empty.Add(0, null), 2));
        NativeBattleEffectArena invalid = new(default(ImmutableArray<NativeBattleEffectMemoryRegion>));
        Assert.Throws<InvalidDataException>(() => invalid.ReadByte(0));
        NativeBattleEffectArena invalidRegion = new([null!]);
        Assert.Throws<InvalidDataException>(() => invalidRegion.Validate());
        NativeBattleEffectArena arena = NativeBattleEffectArena.CreateOwnedRead([1, 2, 3, 4], new[] { true, true, true, true }, null, 2);
        arena.Validate();
        Assert.Throws<InvalidOperationException>(() => arena with { UnknownRegions = [new(0, 1)] });
        Assert.False(HasDeferredMarker(arena));
        Assert.Equal((byte)1, arena.ReadByte(0));
        Assert.Equal(2, arena.Version);
    }

    [Fact]
    public void Failed_first_hydration_retains_the_validated_version_marker_and_can_retry_without_partial_changes()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.CreateOwnedRead([1, 2, 3, 4], new[] { true, true, true, true }, null, 2);
        using (NativeBattleTelemetry.BeginScope(new FailingHydrationSink()))
        {
            Assert.Throws<InvalidOperationException>(() => arena.WriteByte(0, 99));
        }
        Assert.True(arena.HasUnhydratedImport);
        Assert.True(HasDeferredMarker(arena));
        Assert.Equal(2, arena.Version);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, arena.Read(0, 4));
        arena.WriteByte(0, 99);
        Assert.False(HasDeferredMarker(arena));
        Assert.Equal(3, arena.Version);
        Assert.Equal(new byte[] { 99, 2, 3, 4 }, arena.Read(0, 4));
    }

    [Fact]
    public void Owned_import_reads_keep_raw_unknown_bytes_without_materializing_and_first_write_preserves_history()
    {
        NativeBattleEffectArena arena = new([new(0, [10, 20, 30, 40])]) { UnknownRegions = [new(1, 2)] };
        Assert.True(arena.HasUnhydratedImport);
        Assert.Equal((byte)10, arena.ReadByte(0));
        var observed = arena.ReadSnapshot(0, 4);
        Assert.Equal(new byte[] { 10, 20, 30, 40 }, observed.Data);
        Assert.Equal(new[] { true, false, false, true }, observed.Known);
        Assert.Throws<NotSupportedException>(() => arena.ReadUInt16(0));
        Assert.Equal(4, arena.StoredLength);
        Assert.Equal(0, arena.Version);
        arena.Validate();
        Assert.True(arena.HasUnhydratedImport);
        Assert.Throws<InvalidDataException>(() => arena.Write(-1, new byte[] { 99 }));
        Assert.True(arena.HasUnhydratedImport);
        NativeBattleEffectArenaSnapshot before = arena.Capture();
        arena.WriteByte(1, 99);
        Assert.False(arena.HasUnhydratedImport);
        Assert.Equal((byte)99, arena.ReadByte(1));
        Assert.Equal(new byte[] { 10, 20, 30, 40 }, before.ReadSnapshot(0, 4).Data);
        Assert.Throws<NotSupportedException>(() => before.ReadByte(1));
        Assert.Throws<InvalidOperationException>(() => before.WriteByte(0, 0));
        using NativeBattleEffectArenaTransaction transaction = new(arena);
        transaction.BeginCheckpoint();
        transaction.View.WriteByte(0, 77);
        Assert.Equal((byte)77, transaction.View.ReadByte(0));
        transaction.RollbackCheckpoint();
        Assert.Equal((byte)10, transaction.View.ReadByte(0));
    }

    [Fact]
    public void Import_record_clone_retains_the_existing_shared_mutable_owner_contract()
    {
        NativeBattleEffectArena original = new([new(0, [1, 2, 3])]);
        NativeBattleEffectArena clone = original with { };
        clone.WriteByte(1, 99);
        Assert.Equal((byte)99, original.ReadByte(1));
        Assert.Same(original.Working, clone.Working);
    }

    [Fact]
    public void Deferred_versions_typed_writes_copy_and_capture_keep_the_original_mutation_contract()
    {
        byte[] data = Enumerable.Range(0, 16).Select(index => (byte)index).ToArray();
        bool[] known = Enumerable.Repeat(true, data.Length).ToArray();
        Array.Fill(known, false, 4, 4);
        NativeBattleEffectArena expected = NativeBattleEffectArena.Clear().MarkUnknown(0, data.Length).WriteWithKnownness(0, data, known).WriteResourcePointer(4, null);
        NativeBattleEffectArena actual = NativeBattleEffectArena.CreateOwnedRead(data.ToArray(), known, ImmutableDictionary<int, NativeBattleResourcePointer?>.Empty.Add(4, null), expected.Version);
        AssertFacts(expected, actual, data.Length);
        Assert.True(actual.HasUnhydratedImport);
        Assert.Equal(3, actual.Version);
        BattleExecutionReference.Targets targets = new(BattleModelTargets.From([Memoria.FF8.Tests.NativeBattleTypedFixture.OriginalOwner(0)]));
        expected.WriteExecutionReference(8, 2, targets, [0x34, 0x12]);
        actual.WriteExecutionReference(8, 2, targets, [0x34, 0x12]);
        AssertFacts(expected, actual, data.Length);
        Assert.Equal(targets, actual.ReadExecutionReference<BattleExecutionReference.Targets>(8, 2));
        NativeBattleEffectArena clone = actual with { };
        clone.WriteByte(0, 99);
        expected.WriteByte(0, 99);
        AssertFacts(expected, actual, data.Length);
        NativeBattleEffectArenaSnapshot snapshot = actual.Capture();
        Assert.Equal(0, snapshot.Version);
        NativeBattleEffectArena restored = NativeBattleTestSnapshots.Independent(actual);
        Assert.Equal(0, restored.Version);
        Assert.Equal(targets, restored.ReadExecutionReference<BattleExecutionReference.Targets>(8, 2));
        actual.WriteByte(8, 0);
        Assert.False(actual.TryReadExecutionReference<BattleExecutionReference.Targets>(8, 2, out _));
        Assert.Equal(targets, snapshot.ReadExecutionReference<BattleExecutionReference.Targets>(8, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleWorkingMemory.RestoreAtVersion(expected.Working.Capture(), -1));
    }

    [Fact]
    public void Effect314_detached_read_observes_the_explicit_transaction_then_keeps_its_own_bytes_after_rollback()
    {
        NativeBattleEffect314Frame frame = NativeBattleEffect314ControllerTests.Frame();
        const int offset = 0x80000;
        frame.Memory.Arena!.WriteUInt32(offset, 0x12345678);
        using NativeBattleEffectArenaTransaction transaction = new(frame.Memory.Arena);
        frame = frame with { Memory = frame.Memory with { Arena = transaction.View } };
        transaction.BeginCheckpoint();
        transaction.View.MarkUnknown(offset + 1, 1);
        NativeBattleEffectArena expected = PreviousReadBytes(frame, CommonAddress + offset, 4);
        NativeBattleEffectArena actual = ReadBytes(frame, CommonAddress + offset, 4);
        AssertFacts(expected, actual, 4);
        transaction.RollbackCheckpoint();
        Assert.Equal(0x12345678u, transaction.View.ReadUInt32(offset));
        AssertFacts(expected, actual, 4);
        Assert.Throws<NotSupportedException>(() => actual.ReadUInt32(0));
    }

    [Fact]
    public void Effect314_detached_read_matches_previous_writable_temporary_across_domains_and_metadata()
    {
        NativeBattleEffect314Frame frame = NativeBattleEffect314ControllerTests.Frame() with { Staging = NativeBattleFileStaging.Unknown() };
        uint owned = CommonAddress + 0x80000;
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, Enumerable.Range(0, 32).Select(index => (byte)index).ToArray()).MarkUnknown(2, 2);
        source = source.WriteResourcePointer(8, new(new(0, 70000) { ResourceOnly = true }, null, 0)).WriteResourcePointer(16, null).WriteResourcePointer(24, new(null, NativeBattleTemporaryMemory.Address, 4));
        frame = WriteBytes(frame, owned, source, 32);
        frame = WriteBytes(frame, NativeBattleTemporaryMemory.Address, source, 32);
        (uint Address, int Length)[] ranges = [(owned, 32), (CommonAddress + MainOffset + FragmentOffset, FragmentStride), (RootBank + 10, 12), (NativeBattleTemporaryMemory.Address, 32), (owned, 0)];
        foreach ((uint address, int length) in ranges)
        {
            NativeBattleEffectArena expected = PreviousReadBytes(frame, address, length);
            NativeBattleEffectArena actual = ReadBytes(frame, address, length);
            Assert.Equal(length != 0, actual.HasUnhydratedImport);
            AssertFacts(expected, actual, length);
            Assert.Equal(length != 0, actual.HasUnhydratedImport);
            frame = WriteBytes(frame, owned + 24, NativeBattleEffectArena.Clear().Write(0, new byte[] { 99 }), 1);
            AssertFacts(expected, actual, length);
            actual.WriteByte(length, 44);
            expected.WriteByte(length, 44);
            AssertFacts(expected, actual, length + 1);
            Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
        }
    }

    private static void AssertFacts(NativeBattleEffectArena expected, NativeBattleEffectArena actual, int length)
    {
        Assert.Equal(expected.ReadSnapshot(0, length).Data, actual.ReadSnapshot(0, length).Data);
        Assert.Equal(expected.ReadSnapshot(0, length).Known, actual.ReadSnapshot(0, length).Known);
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.StoredLength, actual.StoredLength);
        Assert.Equal(expected.EnumerateResourcePointers().OrderBy(pair => pair.Key).ToArray(), actual.EnumerateResourcePointers().OrderBy(pair => pair.Key).ToArray());
        Assert.Equal(expected.EnumerateExecutionReferences().OrderBy(pair => pair.Key).ToArray(), actual.EnumerateExecutionReferences().OrderBy(pair => pair.Key).ToArray());
    }

    private static bool HasDeferredMarker(NativeBattleEffectArena arena)
    {
        object table = typeof(NativeBattleEffectArena).GetField("DeferredImports", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        object?[] arguments = [arena, null];
        return (bool)table.GetType().GetMethod("TryGetValue")!.Invoke(table, arguments)!;
    }

    private sealed class FailingHydrationSink : INativeBattleTelemetrySink
    {
        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
        {
            if (siteId == 1013 && kind == NativeBattleTrafficKind.ZeroFill)
            {
                throw new InvalidOperationException("Hydration allocation telemetry failed.");
            }
        }
    }

    private static NativeBattleEffectArena PreviousReadBytes(NativeBattleEffect314Frame frame, uint address, int length)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, length);
        int copied = 0;
        while (copied < length)
        {
            uint current = checked(address + (uint)copied);
            object?[] arguments = [frame, current, length - copied, -1, 0];
            int count = (int)typeof(NativeBattleEffect314Controller).GetMethod("RegionLength", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, arguments)!;
            int poolIndex = (int)arguments[3]!;
            int stateKind = (int)arguments[4]!;
            NativeBattleEffectArena source;
            int offset;
            if (poolIndex >= 0)
            {
                NativeBattleEffectTaskPool pool = frame.Effect.Pools[poolIndex];
                source = NativeBattleEffectTaskMemory.CaptureBank(pool);
                offset = checked((int)(current - pool.BankAddress));
            }
            else if (stateKind == 1)
            {
                source = frame.Memory.Arena!;
                offset = checked((int)(current - CommonAddress));
            }
            else if (stateKind == 2)
            {
                source = frame.State.ReadBank(RootBank);
                offset = checked((int)(current - RootBank));
            }
            else if (stateKind == 3)
            {
                source = frame.State.ReadView(current, count);
                offset = 0;
            }
            else
            {
                source = NativeBattleOffsetBodyContextMemory.Read(RequireStaging(frame), current, count);
                offset = 0;
            }
            result = CopyBytes(source, offset, result, copied, count);
            copied += count;
        }
        return result;
    }
}
