using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFileStagingTransactionTests
{
    [Fact]
    public void Ordered_stores_are_immediately_visible_to_callbacks_and_freeze_preserves_prior_publications()
    {
        uint address = NativeBattleFileStaging.Address;
        NativeBattleFileStaging source = NativeBattleFileStaging.Unknown().Write(address, new byte[16000]);
        using NativeBattleFileStagingTransaction transaction = new(source);
        NativeBattleFileStaging current = transaction.View.Write(address + 4094, [1, 2, 3, 4]);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, current.Read(address + 4094, 4));
        Assert.Equal(new byte[4], source.Read(address + 4094, 4));
        current = current.MarkUnknown(address + 4096, 1);
        Assert.Throws<NotSupportedException>(() => current.Read(address + 4094, 4));
        (byte[] data, bool[] known) = current.ReadSnapshot(address + 4094, 4);
        Assert.Equal(new byte[] { 1, 2, 0, 4 }, data);
        Assert.Equal(new[] { true, true, false, true }, known);
        NativeBattleFileStaging first = transaction.Freeze(current);
        current = current.Write(address + 4096, [8]);
        NativeBattleFileStaging second = transaction.Freeze(current);
        Assert.Throws<NotSupportedException>(() => first.Read(address + 4096, 1));
        Assert.Equal(new byte[] { 1, 2, 8, 4 }, second.Read(address + 4094, 4));
        NativeBattleFileStaging restored = JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(second))!;
        Assert.Equal(second.Read(address, 16000), restored.Read(address, 16000));
        Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(current));
    }

    [Fact]
    public void Rebased_overlapping_copies_and_pointer_metadata_match_immutable_memory()
    {
        uint address = NativeBattleFileStaging.Address;
        NativeBattleFileStaging source = NativeBattleFileStaging.Unknown().Write(address, new byte[10000]);
        using NativeBattleFileStagingTransaction transaction = new(source);
        NativeBattleFileStaging expected = Apply(source);
        NativeBattleFileStaging actual = transaction.Freeze(Apply(transaction.View));
        Assert.Equal(expected.BaseAddress, actual.BaseAddress);
        Assert.Equal(expected.ResourcePointers, actual.ResourcePointers);
        (byte[] expectedData, bool[] expectedKnown) = expected.ReadSnapshot(address - 11, 10011);
        (byte[] actualData, bool[] actualKnown) = actual.ReadSnapshot(address - 11, 10011);
        Assert.Equal(expectedKnown, actualKnown);
        Assert.Equal(expectedData, actualData);

        NativeBattleFileStaging Apply(NativeBattleFileStaging memory)
        {
            memory = memory.Write(address - 11, [7, 8]).Write(address + 4094, [1, 2, 3, 4, 5, 6]);
            memory = memory.MarkUnknown(address + 4096, 2).CopyMemory(address + 4094, address + 4095, 6);
            NativeBattleResourcePointer pointer = new(null, null) { CpuRow = 1 };
            memory = memory.WriteResourcePointer(address + 8190, pointer);
            Assert.Equal(pointer, memory.ReadResourcePointer(address + 8190));
            memory = memory.Write(address + 8191, [1, 2]);
            Assert.False(memory.ResourcePointers.ContainsKey(address + 8190));
            return memory.WriteResourcePointer(address + 32, new(null, address + 4094));
        }
    }

    [Fact]
    public void Thousands_of_ordered_scratch_stores_publish_once_without_resource_image_copies()
    {
        uint address = NativeBattleFileStaging.Address;
        NativeBattleFileStaging source = new([new(0, ImmutableArray.CreateRange(new byte[1024 * 1024]))]);
        using NativeBattleFileStagingTransaction transaction = new(source);
        NativeBattleFileStaging current = transaction.View;
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 4096; index++)
        {
            current = current.Write(address + 128, BitConverter.GetBytes(index));
        }
        current = transaction.Freeze(current);
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.True(allocated < 4 * 1024 * 1024, $"Private scratch stores allocated {allocated} bytes.");
        Assert.Equal(4095, BitConverter.ToInt32(current.Read(address + 128, 4)));
        Assert.Equal(new byte[4], source.Read(address + 128, 4));
    }

    [Fact]
    public void Failed_private_pass_does_not_publish_and_disposed_views_refuse_access()
    {
        NativeBattleFileStaging source = NativeBattleFileStaging.Unknown().Write(NativeBattleFileStaging.Address, [1]);
        NativeBattleFileStaging? escaped = null;
        Assert.Throws<InvalidDataException>((Action)(() =>
        {
            using NativeBattleFileStagingTransaction transaction = new(source);
            escaped = transaction.View.Write(NativeBattleFileStaging.Address, [9]);
            throw new InvalidDataException("Private pass failed before publication.");
        }));
        Assert.Equal(new byte[] { 1 }, source.Read(NativeBattleFileStaging.Address, 1));
        Assert.Throws<ObjectDisposedException>(() => escaped!.Read(NativeBattleFileStaging.Address, 1));
        Assert.Throws<ObjectDisposedException>(() => escaped!.Write(NativeBattleFileStaging.Address, [2]));
    }
}
