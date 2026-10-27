using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRecoveryColdIngressTests
{
    [Theory]
    [InlineData((ushort)53, 0x261814Cu, 0x26193D0u)]
    [InlineData((ushort)58, 0x260B30Cu, 0x260D5F0u)]
    [InlineData((ushort)59, 0x2608044u, 0x260A328u)]
    public void Authenticated_loader_maps_the_complete_physical_track_bank_as_zero_without_entry_reset(ushort effect, uint address, uint end)
    {
        byte[] executable = File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "FF8_EN.exe"));
        NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.ForEffect(effect);
        Assert.Equal(address, profile.ChannelAddress);
        Assert.Equal(checked((int)(end - address)), profile.ChannelLength);
        using PEReader image = new(new MemoryStream(executable));
        long imageBase = checked((long)image.PEHeaders.PEHeader!.ImageBase);
        SectionHeader section = Assert.Single(image.PEHeaders.SectionHeaders, section => imageBase + section.VirtualAddress <= address && end <= imageBase + section.VirtualAddress + section.VirtualSize);
        Assert.Equal(".data", section.Name);
        Assert.Equal(0x188C000L, imageBase + section.VirtualAddress + section.SizeOfRawData);
        Assert.Equal(0x279F000L, imageBase + section.VirtualAddress + section.VirtualSize);
        Assert.True(address >= imageBase + section.VirtualAddress + section.SizeOfRawData);

        NativeBattleRecoveryResources resources = NativeBattleRecoveryResourcesReader.Read(executable, effect);
        NativeBattleRecoveryExecutableState cold = NativeBattleRecoveryResourcesReader.ReadColdProcessState(executable, effect);
        ImmutableArray<byte> channels = Assert.IsType<ImmutableArray<byte>>(cold.Channels);
        Assert.Equal(profile.ChannelLength, channels.Length);
        Assert.All(channels, value => Assert.Equal((byte)0, value));
        Assert.Equal(1u, cold.InitializationFlag);
        Assert.Same(cold, cold.Initialize(resources.Executable));
        Assert.Equal(resources.Executable.Data.ToArray(), NativeBattleStorageSnapshots.Capture(cold.Data!).Data.ToArray());
        Assert.Throws<InvalidDataException>(() => cold.Read(address - 1, 1));
        Assert.Throws<InvalidDataException>(() => cold.Read(end, 1));
        Assert.Throws<InvalidDataException>(() => cold.Read(end - 1, 2));
    }

    [Theory]
    [InlineData((ushort)53)]
    [InlineData((ushort)58)]
    [InlineData((ushort)59)]
    public void Current_channels_survive_JSON_and_flag1_initialization_while_old_missing_channels_stay_unknown(ushort effect)
    {
        byte[] executable = File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "FF8_EN.exe"));
        NativeBattleRecoveryResources resources = NativeBattleRecoveryResourcesReader.Read(executable, effect);
        NativeBattleRecoveryExecutableState cold = NativeBattleRecoveryResourcesReader.ReadColdProcessState(executable, effect);
        NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.ForEffect(effect);
        ImmutableArray<byte> channels = Assert.IsType<ImmutableArray<byte>>(cold.Channels);
        NativeBattleRecoveryExecutableState current = cold with { Channels = channels.SetItem(0, 0xA5).SetItem(channels.Length - 1, 0x5A) };
        NativeBattleRecoveryExecutableState restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleRecoveryExecutableState>(SnapshotJson(current))!);
        Assert.Same(restored, restored.Initialize(resources.Executable));
        Assert.Equal(new byte[] { 0xA5 }, restored.Read(profile.ChannelAddress, 1));
        Assert.Equal(new byte[] { 0x5A }, restored.Read(profile.ChannelAddress + (uint)profile.ChannelLength - 1, 1));
        var oldJson = JsonSerializer.SerializeToNode(NativeBattleStorageSnapshots.Capture(current))!.AsObject();
        Assert.True(oldJson.Remove(nameof(NativeBattleRecoveryExecutableState.Channels)));
        NativeBattleRecoveryExecutableState old = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleRecoveryExecutableState>(oldJson)!);
        Assert.Null(old.Channels);
        Assert.Same(old, old.Initialize(resources.Executable));
        Assert.Throws<NotSupportedException>(() => old.Read(profile.ChannelAddress, 1));
    }

    [Theory]
    [InlineData((ushort)53, 0x26191C2u)]
    [InlineData((ushort)58, 0x260D5AAu)]
    [InlineData((ushort)59, 0x260A2E2u)]
    public void Proved_original_maximum_read_end_remains_inside_the_declared_physical_bank(ushort effect, uint readEnd)
    {
        byte[] executable = File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "FF8_EN.exe"));
        NativeBattleRecoveryExecutableState cold = NativeBattleRecoveryResourcesReader.ReadColdProcessState(executable, effect);
        NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.ForEffect(effect);
        Assert.True(readEnd <= profile.ChannelAddress + (uint)profile.ChannelLength);
        Assert.Equal(new byte[] { 0 }, cold.Read(readEnd - 1, 1));
        // This is the bounded read envelope from original source/lifetime operands,
        // not permission to infer any previously loaded save's channel history.
        Assert.Equal(1u, cold.InitializationFlag);
    }

    [Fact]
    public void MegaPotion_repeating_sprite_reads_cross_adjacent_zero_tracks_without_splitting_physical_memory()
    {
        byte[] executable = File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "FF8_EN.exe"));
        NativeBattleRecoveryExecutableState cold = NativeBattleRecoveryResourcesReader.ReadColdProcessState(executable, 53);
        NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.MegaPotion;
        uint source = BinaryPrimitives.ReadUInt32LittleEndian(cold.Read(profile.DirectoryAddress + 4 + 4 * 4, 4));
        byte[] record = cold.Read(source, 0x158);
        Assert.Equal((byte)2, record[0x16]);
        Assert.Equal(new byte[] { 1, 2, 3, 3 }, record.AsSpan(0x17, 4).ToArray());
        uint x = BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(0xD8));
        uint y = BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(0xDC));
        uint z = BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(0xE0));
        Assert.Equal(24u, y - x);
        Assert.Equal(24u, z - y);
        // Phase starts0; frames8 and three1..2 repeats allow13 motion samples.
        // The thirteenth WORD is physically the first WORD of the next track.
        Assert.Equal(new byte[26], cold.Read(x, 26));
        Assert.Equal(new byte[26], cold.Read(y, 26));
        Assert.Equal(new byte[26], cold.Read(z, 26));
        ImmutableArray<byte> channels = Assert.IsType<ImmutableArray<byte>>(cold.Channels);
        NativeBattleRecoveryExecutableState changed = cold with { Channels = channels.SetItem(checked((int)(y - profile.ChannelAddress)), 0xA5) };
        Assert.Equal((byte)0xA5, changed.Read(x, 26)[24]);
    }

    [Fact]
    public void Cold_channel_ingress_rejects_an_unauthenticated_image()
    {
        byte[] executable = File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "FF8_EN.exe"));
        executable[^1] ^= 1;
        Assert.Throws<NotSupportedException>(() => NativeBattleRecoveryResourcesReader.ReadColdProcessState(executable, 53));
    }

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
