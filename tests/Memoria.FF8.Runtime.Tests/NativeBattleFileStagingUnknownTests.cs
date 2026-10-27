using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFileStagingUnknownTests
{
    [Fact]
    public void Symbolic_pointer_replacement_removes_loader_zero_raw_bytes_without_erasing_neighbors()
    {
        const uint address = 0x255FEC2;
        NativeBattleFileStaging cold = NativeBattleStorageSnapshots.Capture(NativeBattleFileStaging.FromColdProcess().Write(address, [1, 2, 3, 4]));
        NativeBattleResourcePointer pointer = new(null, null) { CpuRow = 3 };
        NativeBattleFileStaging symbolic = NativeBattleStorageSnapshots.Restore(cold).WriteResourcePointer(address, pointer);
        symbolic.Validate();
        NativeBattleFileStaging contradictory = NativeBattleStorageSnapshots.Capture(symbolic) with { LoaderZeroRanges = cold.LoaderZeroRanges };
        Assert.Throws<InvalidDataException>(() => contradictory.Validate());
        Assert.Equal(pointer, symbolic.ReadResourcePointer(address));
        Assert.All(symbolic.ReadSnapshot(address, 4).Known, value => Assert.False(value));
        Assert.Throws<NotSupportedException>(() => symbolic.Read(address, 4));
        Assert.Equal(new byte[1], symbolic.Read(address - 1, 1));
        Assert.Equal(new byte[1], symbolic.Read(address + 4, 1));
        NativeBattleFileStaging absent = symbolic.WriteResourcePointer(address, null);
        Assert.Null(absent.ReadResourcePointer(address));
        Assert.Throws<NotSupportedException>(() => absent.Read(address, 4));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, cold.Read(address, 4));
    }

    [Fact]
    public void Unknown_copy_padding_removes_only_its_bytes_and_overlapping_pointer_metadata()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x24F9950, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }).WriteResourcePointer(0x24F9954, new(null, 0xE2B518));
        staging = staging.MarkUnknown(0x24F9956, 2);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, staging.Read(0x24F9950, 4));
        Assert.Throws<NotSupportedException>(() => staging.Read(0x24F9954, 4));
        Assert.Throws<NotSupportedException>(() => staging.ReadResourcePointer(0x24F9954));
        staging = staging.Write(0x24F9956, new byte[] { 9, 10 });
        Assert.Equal(new byte[] { 0x18, 0xB5, 9, 10 }, staging.Read(0x24F9954, 4));
    }

    [Fact]
    public void Independent_restore_keeps_split_known_ranges_and_future_writes()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x24F9950, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }).MarkUnknown(0x24F9952, 4);
        NativeBattleFileStaging restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleFileStaging>(SnapshotJson(staging))!);
        Assert.Equal(new byte[] { 1, 2 }, restored.Read(0x24F9950, 2));
        Assert.Equal(new byte[] { 7, 8 }, restored.Read(0x24F9956, 2));
        Assert.Throws<NotSupportedException>(() => restored.Read(0x24F9952, 4));
        Assert.Equal(SnapshotJson(staging.Write(0x24F9952, new byte[] { 9, 10, 11, 12 })), SnapshotJson(restored.Write(0x24F9952, new byte[] { 9, 10, 11, 12 })));
    }
    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
