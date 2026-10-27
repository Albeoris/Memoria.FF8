using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaExecutableStateTests
{
    [Fact]
    public void First_use_relocates_nonzero_directories_and_all_node_channels_including_zero_values_once()
    {
        byte[] bytes = new byte[NativeBattleCuragaExecutableState.Length];
        Write(bytes, 4, 0x1000);
        Write(bytes, 0x44, 0x2000);
        Write(bytes, 0x1000 + 0xC8, 24);
        bytes[0x1000 + 0x20] = 0xA5;
        NativeBattleCuragaExecutableSource source = new(bytes.ToImmutableArray());
        NativeBattleCuragaExecutableState memory = NativeBattleCuragaExecutableState.FromColdProcess().Initialize87EA20(source);
        Assert.Equal(1u, memory.InitializationFlag);
        Assert.Equal(NativeBattleCuragaExecutableState.Address + 0x1000u, Read(memory, 4));
        Assert.Equal(NativeBattleCuragaExecutableState.Address + 0x2000u, Read(memory, 0x44));
        Assert.Equal(0u, Read(memory, 8));
        Assert.Equal(NativeBattleCuragaExecutableState.Address + 24, Read(memory, 0x1000 + 0xC8));
        Assert.Equal(NativeBattleCuragaExecutableState.Address, Read(memory, 0x1000 + 0xCC));
        Assert.Equal(new byte[] { 0xA5 }, memory.Read(NativeBattleCuragaExecutableState.Address + 0x1020, 1));
        Assert.Same(memory, memory.Initialize87EA20(source));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan()));
        NativeBattleCuragaExecutableState restored = RoundTrip(memory);
        Assert.Equal(memory.Read(NativeBattleCuragaExecutableState.Address, NativeBattleCuragaExecutableState.Length), restored.Initialize87EA20(source).Read(NativeBattleCuragaExecutableState.Address, NativeBattleCuragaExecutableState.Length));
    }

    [Fact]
    public void Unknown_prior_fixed_bytes_are_not_reloaded_from_the_original_executable()
    {
        NativeBattleCuragaExecutableSource source = new(new byte[NativeBattleCuragaExecutableState.Length].ToImmutableArray());
        Assert.Throws<NotSupportedException>(() => new NativeBattleCuragaExecutableState(1, null).Initialize87EA20(source));
        Assert.Throws<NotSupportedException>(() => NativeBattleCuragaExecutableState.FromColdProcess().Read(NativeBattleCuragaExecutableState.Address, 4));
        Assert.Throws<InvalidDataException>(() => new NativeBattleCuragaExecutableState(1, source.Data).Validate());
    }

    [Fact]
    public void First_use_rejects_a_directory_node_whose_channel_fields_leave_the_original_bank()
    {
        byte[] bytes = new byte[NativeBattleCuragaExecutableState.Length];
        Write(bytes, 4, NativeBattleCuragaExecutableState.Length - 4u);
        Assert.Throws<InvalidDataException>(() => NativeBattleCuragaExecutableState.FromColdProcess().Initialize87EA20(new(bytes.ToImmutableArray())));
    }

    private static void Write(byte[] data, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
    private static uint Read(NativeBattleCuragaExecutableState memory, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(NativeBattleCuragaExecutableState.Address + (uint)offset, 4));
    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
    private static T RoundTrip<T>(T value) => NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<T>(SnapshotJson(value))!);

}
