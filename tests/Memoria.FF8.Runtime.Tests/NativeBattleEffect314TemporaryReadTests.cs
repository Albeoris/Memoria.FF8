using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect314Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect314TemporaryReadTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 64)]
    [InlineData(3, 48)]
    [InlineData(17, 23)]
    public void Selected_write_source_matches_previous_bytes_knownness_descriptors_versions_and_later_mutations(int offset, int length)
    {
        NativeBattleEffectArena source = Source();
        NativeBattleEffectArena expected = PreviousCopy(source, offset, length);
        NativeBattleEffectArena actual = NativeBattleEffect314TemporaryReads.Copy(source, offset, length);
        AssertEquivalent(expected, actual, length);
        Assert.Equal(length != 0, actual.HasUnhydratedImport);
        source.WriteByte(offset, 99);
        AssertEquivalent(expected, actual, length);
        actual.WriteByte(length, 55);
        expected.WriteByte(length, 55);
        AssertEquivalent(expected, actual, length + 1);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
    }

    [Fact]
    public void Source_range_failures_do_not_modify_the_source_or_its_typed_metadata()
    {
        NativeBattleEffectArena source = Source();
        string before = NativeBattleTestSnapshots.Json(source);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect314TemporaryReads.Copy(source, -1, 16));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect314TemporaryReads.Copy(source, NativeBattleEffectArena.Length - 1, 2));
        Assert.Equal(before, NativeBattleTestSnapshots.Json(source));
    }

    [Fact]
    public void Geometry_byte_and_arena_readers_keep_current_and_pinned_visibility_through_tracked_untracked_and_reloaded_owners()
    {
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown();
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, cpu.Cpu.Banks[0].Write(0, Enumerable.Range(0, 16).Select(index => (byte)index).ToArray())) } };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleResourcePointer pointer = new(new(0, 0), null, 16);
        NativeBattleResourcePointer held = pointer.Pin(cpu);
        NativeBattleEffect314GeometryReadScope scope = new();
        byte[] ReadCurrent(int offset, int length) => scope.Read(cpu, staging, pointer, 16, offset, length);
        byte[] ReadPinned(int offset, int length) => scope.Read(cpu, staging, held, 16, offset, length);
        NativeBattleModelGeometrySource live = NativeBattleModelGeometrySource.OpenEditor(cpu, staging, pointer, ReadCurrent, (offset, length) => NativeBattleEffect314TemporaryReads.Known(ReadCurrent(offset, length)));
        NativeBattleModelGeometrySource pinned = NativeBattleModelGeometrySource.OpenEditor(cpu, staging, held, ReadPinned, (offset, length) => NativeBattleEffect314TemporaryReads.Known(ReadPinned(offset, length)));
        NativeBattleEffectArena earlier = live.ReadArena(0, 8);
        long identity = live.PresentationIdentity;
        NativeBattleModelCpuResources before = cpu;
        cpu = NativeBattleModelResourceView.WritePartySection(cpu, staging, pointer, 1, [77]);
        scope.ObserveWrite(before, cpu, pointer, 1, [77]);
        Check([0, 77, 2, 3]);
        cpu = NativeBattleModelResourceView.WritePartySection(cpu, staging, pointer, 2, [88]);
        Check([0, 77, 88, 3]);
        cpu = NativeBattleTestSnapshots.Independent(cpu);
        Check([0, 77, 88, 3]);
        Assert.Equal(new byte[] { 0, 1, 2, 3 }, earlier.Read(0, 4));
        Assert.Equal(identity, live.PresentationIdentity);
        Assert.Throws<InvalidDataException>(() => live.ReadArena(15, 2));
        Assert.Throws<InvalidDataException>(() => live.Read(15, 2));
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, NativeBattleModelCpuBank.Unknown().Write(0, [9])) } };
        Assert.Throws<NotSupportedException>(() => live.Read(0, 4));
        Assert.Throws<NotSupportedException>(() => live.ReadArena(0, 4));
        Assert.Equal(new byte[] { 0, 1, 2, 3 }, pinned.ReadArena(0, 4).Read(0, 4));

        void Check(byte[] bytes)
        {
            Assert.Equal(bytes, live.Read(0, 4));
            NativeBattleEffectArena snapshot = live.ReadArena(0, 4);
            Assert.Equal(bytes, snapshot.Read(0, 4));
            Assert.Equal(1, snapshot.Version);
            Assert.True(snapshot.HasUnhydratedImport);
            Assert.Equal(new byte[] { 0, 1, 2, 3 }, pinned.Read(0, 4));
            Assert.Equal(pinned.Read(0, 4), pinned.ReadArena(0, 4).Read(0, 4));
        }
    }

    [Fact]
    public void Physical_numeric_snapshot_preserves_unknown_raw_data_and_version_before_and_after_its_first_write()
    {
        byte[] data = [1, 2, 3, 4];
        bool[] known = [true, false, false, true];
        NativeBattleEffectArena expected = NativeBattleEffectArena.Clear().WriteWithKnownness(0, data, known);
        NativeBattleEffectArena actual = NativeBattleEffectArena.CreateOwnedRead(data.ToArray(), known, null, 1);
        AssertEquivalent(expected, actual, 4);
        Assert.True(actual.HasUnhydratedImport);
        actual.WriteByte(1, 99);
        expected.WriteByte(1, 99);
        AssertEquivalent(expected, actual, 4);
    }

    [Fact]
    public void Repeated_detached_source_slices_allocate_less_than_mutable_arena_copies()
    {
        NativeBattleEffectArena source = Source();
        for (int index = 0; index < 16; index++)
        {
            _ = PreviousCopy(source, 0, 64).ReadByte(0);
            _ = NativeBattleEffect314TemporaryReads.Copy(source, 0, 64).ReadByte(0);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 128; index++)
        {
            _ = PreviousCopy(source, 0, 64).ReadByte(0);
        }
        long previous = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 128; index++)
        {
            _ = NativeBattleEffect314TemporaryReads.Copy(source, 0, 64).ReadByte(0);
        }
        long current = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(current < previous, $"Detached slices allocated {current} bytes; mutable copies allocated {previous} bytes.");
    }

    private static NativeBattleEffectArena Source()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, Enumerable.Range(0, 64).Select(index => (byte)index).ToArray()).MarkUnknown(2, 2);
        source.WriteResourcePointer(8, new(new(0, 70000) { ResourceOnly = true }, null, 0));
        source.WriteResourcePointer(16, null);
        source.WriteResourcePointer(24, new(null, NativeBattleTemporaryMemory.Address, 4));
        source.WriteExecutionReference(40, 2, new BattleExecutionReference.Targets(BattleModelTargets.From([Memoria.FF8.Tests.NativeBattleTypedFixture.OriginalOwner(0)])), [0x34, 0x12]);
        return source;
    }

    private static NativeBattleEffectArena PreviousCopy(NativeBattleEffectArena source, int offset, int length)
    {
        return CopyBytes(source, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    private static void AssertEquivalent(NativeBattleEffectArena expected, NativeBattleEffectArena actual, int length)
    {
        Assert.Equal(expected.ReadSnapshot(0, length).Data, actual.ReadSnapshot(0, length).Data);
        Assert.Equal(expected.ReadSnapshot(0, length).Known, actual.ReadSnapshot(0, length).Known);
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.StoredLength, actual.StoredLength);
        Assert.Equal(expected.EnumerateResourcePointers().OrderBy(pair => pair.Key).ToArray(), actual.EnumerateResourcePointers().OrderBy(pair => pair.Key).ToArray());
        Assert.Equal(expected.EnumerateExecutionReferences().OrderBy(pair => pair.Key).ToArray(), actual.EnumerateExecutionReferences().OrderBy(pair => pair.Key).ToArray());
    }
}
