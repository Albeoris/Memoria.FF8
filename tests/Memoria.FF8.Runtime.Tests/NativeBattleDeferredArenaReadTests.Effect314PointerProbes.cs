using static Memoria.FF8.Runtime.NativeBattleEffect314Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattleDeferredArenaReadTests
{
    [Theory]
    [InlineData("none")]
    [InlineData("numeric")]
    [InlineData("opaque")]
    [InlineData("null")]
    public void Tiny_staging_reads_match_the_enumerating_oracle_at_every_unaligned_pointer_boundary(string kind)
    {
        uint address = NativeBattleTemporaryMemory.Address;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(address, Enumerable.Range(0, 32).Select(value => (byte)(value + 1)).ToArray());
        if (kind != "none")
        {
            staging = staging.WriteResourcePointer(address + 3, ProbePointer(kind));
        }
        NativeBattleEffect314Frame frame = ProbeFrame(staging);
        long version = staging.StorageVersion;
        for (int length = 0; length <= 20; length++)
        {
            NativeBattleEffectArena expected = PreviousReadBytes(frame, address, length);
            NativeBattleEffectArena actual = ReadBytes(frame, address, length);
            AssertFacts(expected, actual, length);
            byte[] data = new byte[length];
            bool[] known = new bool[length];
            ReadWithKnownness(frame, address, data, known);
            Assert.Equal(expected.ReadSnapshot(0, length).Data, data);
            Assert.Equal(expected.ReadSnapshot(0, length).Known, known);
        }
        Assert.Equal(version, staging.StorageVersion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Multiple_overlapping_fields_keep_the_original_enumeration_and_overlay_order(bool reverse)
    {
        uint address = NativeBattleTemporaryMemory.Address;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(address, Enumerable.Range(0, 24).Select(value => (byte)value).ToArray());
        uint[] offsets = reverse ? [6, 3, 1] : [1, 3, 6];
        foreach (uint offset in offsets)
        {
            staging.SetResourcePointerMetadata(address + offset, null);
        }
        NativeBattleEffect314Frame frame = ProbeFrame(staging);
        foreach (int length in new[] { 4, 8, 16, 20 })
        {
            AssertFacts(PreviousReadBytes(frame, address, length), ReadBytes(frame, address, length), length);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Multiple_invalid_fields_retain_the_enumerating_oracle_exception_before_any_pointer_is_published(bool reverse)
    {
        const uint address = 0x1DCB0F0;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(address, new byte[16]);
        NativeFieldResourceOwner exceeded = NativeFieldResourceOwner.Create(96);
        NativeFieldResourceOwner leased = NativeFieldResourceOwner.Create(96);
        using NativeBattleFileStagingTransaction resourceTransaction = new(leased.Storage);
        NativeFieldResourceOwner expired = new(resourceTransaction.View, 96);
        NativeBattleResourcePointer exceedsBounds = new(null, 0x12345678, 4) { FieldStorage = new(0) { Resource = exceeded } };
        NativeBattleResourcePointer disposed = new(null, 0x23456789, 4) { FieldStorage = new(2) { Resource = expired } };
        foreach (uint offset in reverse ? new uint[] { 8, 0 } : [0u, 8u])
        {
            staging = staging.WriteResourcePointer(address + offset, offset == 0 ? exceedsBounds : disposed);
        }
        exceeded.Storage.WriteByte(96, 1);
        resourceTransaction.Dispose();
        NativeBattleEffect314Frame frame = ProbeFrame(staging);
        long version = staging.StorageVersion;
        Exception? expected = Record.Exception(() => PreviousReadBytes(frame, address, 16));
        Exception? actual = Record.Exception(() => ReadBytes(frame, address, 16));
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(reverse ? typeof(ObjectDisposedException) : typeof(InvalidDataException), expected.GetType());
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Message, actual.Message);
        Assert.Equal(version, staging.StorageVersion);
    }

    [Fact]
    public void Virtual_sparse_addresses_outside_lookup_offset_domain_keep_the_canonical_enumeration_path()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(NativeBattleTemporaryMemory.Address, [1]);
        NativeBattleEffect314Frame frame = ProbeFrame(staging);
        foreach (uint address in new[] { staging.BaseAddress - 8, checked(staging.BaseAddress + (uint)int.MaxValue + 8), uint.MaxValue - 7 })
        {
            NativeBattleEffectArena expected = PreviousReadBytes(frame, address, 8);
            NativeBattleEffectArena actual = ReadBytes(frame, address, 8);
            AssertFacts(expected, actual, 8);
        }
    }

    [Fact]
    public void Frozen_snapshots_preserve_detached_pointer_facts_and_never_hydrate_an_owner_for_lookup()
    {
        uint address = NativeBattleTemporaryMemory.Address;
        NativeBattleFileStaging source = NativeBattleFileStaging.Unknown().Write(address, new byte[16]).WriteResourcePointer(address + 5, null);
        NativeBattleFileStaging frozen = NativeBattleStorageSnapshots.Capture(source);
        Assert.True(frozen.IsSnapshot);
        string before = NativeBattleTestSnapshots.Json(frozen);
        NativeBattleEffect314Frame frame = ProbeFrame(frozen);
        AssertFacts(PreviousReadBytes(frame, address, 16), ReadBytes(frame, address, 16), 16);
        source = source.WriteResourcePointer(address + 5, ProbePointer("numeric"));
        Assert.Equal(before, NativeBattleTestSnapshots.Json(frozen));
    }

    [Fact]
    public void Transaction_child_queries_observe_late_parent_additions_null_overrides_and_checkpoint_rollback()
    {
        uint address = NativeBattleTemporaryMemory.Address;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(address, new byte[24]);
        using NativeBattleFileStagingTransaction parent = new(staging);
        using NativeBattleFileStagingTransaction child = NativeBattleFileStagingTransaction.CreatePrivateChild(parent.View);
        NativeBattleEffect314Frame frame = ProbeFrame(child.View);
        Compare();
        parent.View.WriteResourcePointer(address + 3, ProbePointer("numeric"));
        Compare();
        child.BeginCheckpoint();
        child.View.WriteResourcePointer(address + 3, null);
        Compare();
        child.RollbackCheckpoint();
        Compare();
        child.View.WriteResourcePointer(address + 11, ProbePointer("opaque"));
        Compare();
        child.Dispose();
        Exception? expected = Record.Exception(() => PreviousReadBytes(frame, address, 16));
        Exception? actual = Record.Exception(() => ReadBytes(frame, address, 16));
        Assert.NotNull(expected);
        Assert.Equal(expected.GetType(), actual?.GetType());

        void Compare()
        {
            AssertFacts(PreviousReadBytes(frame, address, 16), ReadBytes(frame, address, 16), 16);
        }
    }

    [Fact]
    public void Zero_and_single_descriptor_tiny_reads_allocate_less_than_global_enumeration_without_changing_facts()
    {
        uint address = NativeBattleTemporaryMemory.Address;
        Span<byte> data = stackalloc byte[17];
        Span<bool> known = stackalloc bool[17];
        foreach (bool present in new[] { false, true })
        {
            NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(address, new byte[17]);
            if (present)
            {
                staging = staging.WriteResourcePointer(address + 3, null);
            }
            NativeBattleEffect314Frame frame = ProbeFrame(staging);
            for (int index = 0; index < 16; index++)
            {
                ReadWithKnownness(frame, address, data, known);
                ReadWithKnownness(frame, address, data[..16], known[..16]);
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 128; index++)
            {
                ReadWithKnownness(frame, address, data, known);
            }
            long previous = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 128; index++)
            {
                ReadWithKnownness(frame, address, data[..16], known[..16]);
            }
            long current = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(current < previous, $"Tiny span read allocated {current} bytes; same-kernel enumerating 17-byte span allocated {previous} bytes; pointer present={present}.");
            AssertFacts(PreviousReadBytes(frame, address, 16), ReadBytes(frame, address, 16), 16);
        }
    }

    private static NativeBattleEffect314Frame ProbeFrame(NativeBattleFileStaging staging) => NativeBattleEffect314ControllerTests.Frame() with { Staging = staging };

    private static NativeBattleResourcePointer? ProbePointer(string kind) => kind switch
    {
        "numeric" => new(null, 0x12345678, 4),
        "opaque" => new(new(0, 70000) { ResourceOnly = true }, null, 0),
        _ => null
    };
}
