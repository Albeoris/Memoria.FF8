using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFileStagingPerformanceTests
{
    [Fact]
    public void Scattered_hot_scratch_rows_do_not_recopy_or_expand_the_untouched_resource_image()
    {
        uint address = NativeBattleFileStaging.Address;
        NativeBattleFileStaging memory = new([new(0, ImmutableArray.CreateRange(new byte[16 * 1024 * 1024]))]);
        int[] offsets = [16, 5 * 1024 * 1024 + 16, 10 * 1024 * 1024 + 16, 15 * 1024 * 1024 + 16];
        foreach (int offset in offsets)
        {
            memory = memory.Write(address + (uint)offset, [1, 2, 3, 4]);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 128; index++)
        {
            foreach (int offset in offsets)
            {
                memory = memory.Write(address + (uint)offset, BitConverter.GetBytes(index));
            }
        }
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.True(allocated < 8 * 1024 * 1024, $"Hot scratch rows allocated {allocated} bytes beside untouched resources.");
        foreach (int offset in offsets)
        {
            Assert.Equal(127, BitConverter.ToInt32(memory.Read(address + (uint)offset, 4)));
        }
    }

    [Fact]
    public void Scalar_scratch_stores_do_not_copy_adjacent_resource_images_and_preserve_old_snapshots()
    {
        uint address = NativeBattleFileStaging.Address;
        byte[] image = new byte[1024 * 1024];
        Random random = new(873);
        random.NextBytes(image);
        NativeBattleFileStaging original = NativeBattleStorageSnapshots.Capture(new NativeBattleFileStaging([new(0, image.ToImmutableArray())]));
        NativeBattleFileStaging memory = NativeBattleStorageSnapshots.Restore(original).Write(address + 16, [1, 2, 3, 4]);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 256; index++)
        {
            memory = memory.Write(address + (uint)(32 + index * 4), BitConverter.GetBytes(index));
        }
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.True(allocated < 8 * 1024 * 1024, $"Scalar stores allocated {allocated} bytes beside a 1MB resource image.");
        Assert.Equal(image, original.Read(address, image.Length));
        image[16] = 1;
        image[17] = 2;
        image[18] = 3;
        image[19] = 4;
        for (int index = 0; index < 256; index++)
        {
            BitConverter.GetBytes(index).CopyTo(image, 32 + index * 4);
        }
        Assert.Equal(image, memory.Read(address, image.Length));
        NativeBattleFileStaging restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleFileStaging>(SnapshotJson(memory))!);
        Assert.Equal(memory.Read(address, image.Length), restored.Read(address, image.Length));
        Assert.Equal(memory.Write(address + 4094, [3, 4, 5, 6]).Read(address, image.Length), restored.Write(address + 4094, [3, 4, 5, 6]).Read(address, image.Length));
    }

    [Fact]
    public void Overlapping_writes_unknown_gaps_rebases_and_symbolic_pointers_keep_their_byte_owners()
    {
        uint address = NativeBattleFileStaging.Address;
        byte[] expected = new byte[24000];
        bool[] known = new bool[expected.Length];
        NativeBattleFileStaging memory = NativeBattleFileStaging.Unknown();
        Random random = new(722);
        for (int step = 0; step < 200; step++)
        {
            int first = random.Next(expected.Length - 5000);
            int count = random.Next(1, 5000);
            if (step % 4 == 0)
            {
                memory = memory.MarkUnknown(address + (uint)first, count);
                Array.Fill(known, false, first, count);
            }
            else
            {
                byte[] bytes = new byte[count];
                random.NextBytes(bytes);
                memory = memory.Write(address + (uint)first, bytes);
                bytes.CopyTo(expected, first);
                Array.Fill(known, true, first, count);
            }
        }
        memory = memory.Write(address - 13, [9]);
        memory.Validate();
        NativeBattleFileStaging restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleFileStaging>(SnapshotJson(memory))!);
        (byte[] actual, bool[] observed) = restored.ReadSnapshot(address, expected.Length);
        Assert.Equal(known, observed);
        Assert.Equal(expected, actual);
        NativeBattleResourcePointer pointer = new(null, null) { CpuRow = 0 };
        restored = restored.WriteResourcePointer(address + 4094, pointer);
        Assert.Equal(pointer, restored.ReadResourcePointer(address + 4094));
        Assert.Throws<NotSupportedException>(() => restored.Read(address + 4094, 4));
        restored = restored.Write(address + 4094, [1, 2, 3, 4]);
        Assert.Equal(0x04030201u, restored.ReadResourcePointer(address + 4094)!.StagingAddress);
    }
    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
