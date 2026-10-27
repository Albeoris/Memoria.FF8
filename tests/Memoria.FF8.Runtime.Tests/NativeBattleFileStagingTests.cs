using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFileStagingTests
{
    [Fact]
    public void Lower_field_arena_rebases_coordinates_without_changing_prior_absolute_bytes_or_unknown_gaps()
    {
        uint battle = NativeBattleFileStaging.Address;
        const uint field = 0x18DD018;
        NativeBattleFileStaging original = NativeBattleStorageSnapshots.Capture(NativeBattleFileStaging.Unknown().Write(battle + 8, new byte[] { 2, 3 }));
        NativeBattleFileStaging rebased = NativeBattleStorageSnapshots.Restore(original).Write(field, new byte[] { 9, 7 });
        Assert.Equal(field, rebased.BaseAddress);
        Assert.Equal(new byte[] { 9, 7 }, rebased.Read(field, 2));
        Assert.Equal(original.Read(battle + 8, 2), rebased.Read(battle + 8, 2));
        Assert.Throws<NotSupportedException>(() => rebased.Read(field + 2, 1));
        Assert.Throws<NotSupportedException>(() => rebased.Read(battle, 1));
        Assert.Equal(battle, original.BaseAddress);
        NativeBattleFileStaging restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleFileStaging>(SnapshotJson(rebased))!);
        Assert.Equal(rebased.Read(battle + 8, 2), restored.Read(battle + 8, 2));
        Assert.Equal(rebased.Read(field, 2), restored.Read(field, 2));
        string prior = "{\"KnownRanges\":[{\"Offset\":8,\"Data\":[2,3]}]}";
        NativeBattleFileStaging historical = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleFileStaging>(prior)!);
        Assert.Equal(battle, historical.BaseAddress);
        Assert.Equal(new byte[] { 2, 3 }, historical.Read(battle + 8, 2));
    }

    [Fact]
    public void Unknown_gaps_and_unwritten_tails_refuse_and_shorter_writes_preserve_the_previous_suffix()
    {
        uint address = NativeBattleFileStaging.Address;
        NativeBattleFileStaging memory = NativeBattleFileStaging.Unknown().Write(address, new byte[] { 1, 2, 3, 4, 5 }).Write(address + 1, new byte[] { 9, 8 });
        Assert.Equal(new byte[] { 1, 9, 8, 4, 5 }, memory.Read(address, 5));
        Assert.Throws<NotSupportedException>(() => memory.Read(address + 4, 2));
        memory = memory.Write(address + 8, new byte[] { 7 });
        Assert.Throws<NotSupportedException>(() => memory.Read(address + 4, 5));
        memory = memory.Write(address + 5, new byte[] { 6, 6, 6 });
        Assert.Equal(new byte[] { 1, 9, 8, 4, 5, 6, 6, 6, 7 }, memory.Read(address, 9));
        NativeBattleFileStaging restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleFileStaging>(SnapshotJson(memory))!);
        Assert.Equal(memory.Read(address, 9), restored.Read(address, 9));
        Assert.Throws<NotSupportedException>(() => restored.Read(address + 9, 1));
    }

    [Fact]
    public void Dat_extent_is_read_from_current_memory_and_unknown_large_extent_refuses_before_allocating_it()
    {
        uint address = NativeBattleFileStaging.Address;
        NativeBattleFileStaging memory = NativeBattleFileStaging.Unknown().Write(address, Directory());
        Assert.Equal(Directory(), memory.ReadDat(address).ToArray());
        byte[] length = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length, 100_000_000);
        memory = memory.Write(address + 12, length);
        Assert.Throws<NotSupportedException>(() => memory.ReadDat(address));
        Assert.Throws<NotSupportedException>(() => NativeBattleFileStaging.Unknown().Read(address - 1, 1));
    }

    [Fact]
    public void Pending_music_retains_pointers_and_observes_an_intervening_write_in_the_shared_source()
    {
        uint address = NativeBattleFileStaging.Address;
        NativeBattleFileStaging memory = NativeBattleFileStaging.Unknown().Write(address, Directory());
        NativeBattleMusicProcessState process = new(new(1, null, null, null, 0, null), 0, null);
        process = process.InitializeCommands501C60().StageLoad501A20(memory, address);
        Assert.Equal(new NativeBattleMusicLoadPointers(address + 16, address + 24, 8), process.LoadSource);
        string pending = SnapshotJson(process);
        memory = memory.Write(address + 28, new byte[] { 7 });
        NativeGlobalSoundProcess sound = NativeGlobalSoundProcess.FromBufferlessColdProcess();
        NativeBattleMusicLoadStep next = process.AdvanceLoad501A70(sound, memory, 1, 0x1234);
        Assert.Equal((byte)7, next.State.Buffers.ReadBuffer(0).EncodedAkaoId);
        Assert.Equal(pending, SnapshotJson(process));
        NativeBattleMusicLoadStep independent = JsonSerializer.Deserialize<NativeBattleMusicProcessState>(pending)!.AdvanceLoad501A70(JsonSerializer.Deserialize<NativeGlobalSoundProcess>(SnapshotJson(sound))!, NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleFileStaging>(SnapshotJson(memory))!), 1, 0x1234);
        Assert.Equal(SnapshotJson(next), SnapshotJson(independent));
        Assert.Throws<NotSupportedException>(() => process.AdvanceLoad501A70(sound, null, 1, 0x1234));
    }

    [Fact]
    public void Retained_memory_preserves_unknown_imports_and_explicit_cold_loader_provenance()
    {
        uint address = NativeBattleFileStaging.Address;
        NativeBattleFileStaging memory = NativeBattleFileStaging.Unknown().Write(address + 8, new byte[] { 2, 3 });
        NativeBattleRetainedMemory retained = NativeBattleRetainedMemory.FromColdProcess() with { FileStaging = memory };
        retained.Validate();
        retained = JsonSerializer.Deserialize<NativeBattleRetainedMemory>(SnapshotJson(retained))!;
        NativeBattleFileStaging restored = Assert.IsType<NativeBattleFileStaging>(retained.FileStaging);
        Assert.Equal(new byte[] { 2, 3 }, restored.Read(address + 8, 2));
        Assert.Throws<NotSupportedException>(() => restored.Read(address, 1));
        NativeBattleFileStaging cold = Assert.IsType<NativeBattleFileStaging>(NativeBattleRetainedMemory.FromColdProcess().FileStaging);
        Assert.Equal(new byte[4], cold.Read(0x1D999C0, 4));
        Assert.Equal(new byte[1], cold.Read(address, 1));
        Assert.Equal(new byte[1], cold.Read(0x1D999BF, 1));
        Assert.Equal(new byte[4], cold.Read(0x1D999C4, 4));
        Assert.Single(cold.LoaderZeroRanges);
        Assert.Empty(restored.LoaderZeroRanges);
    }

    private static byte[] Directory()
    {
        byte[] bytes = new byte[32];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 24);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), bytes.Length);
        "AKAO"u8.CopyTo(bytes.AsSpan(16));
        "AKAO"u8.CopyTo(bytes.AsSpan(24));
        bytes[28] = 2;
        return bytes;
    }
    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
