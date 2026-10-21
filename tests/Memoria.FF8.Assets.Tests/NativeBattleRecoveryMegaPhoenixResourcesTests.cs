using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRecoveryMegaPhoenixResourcesTests
{
    [Fact]
    public void Original_mega_phoenix_preserves_its_own_directories_sprites_and_mesh_groups()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        byte[] executable = File.ReadAllBytes(Path.Combine(game.GameDirectory, "FF8_EN.exe"));
        NativeBattleRecoveryResources resources = game.ReadNativeRecoveryResources(7);
        NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.MegaPhoenix;
        NativeBattleEffectBinding binding = NativeBattleEffectCatalogReader.Read(executable)[6];
        Assert.Equal(profile.Entry, binding.Entry);
        Assert.Equal(profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag006.tim", 0x275FB80), binding.InitializerResource);
        Assert.Equal(300185u, resources.Sound);
        Assert.Equal(0x35C8, resources.Executable.Data.Length);
        NativeBattleRecoveryExecutableState state = new(7, resources.Executable.Data);
        Assert.Equal(1u, state.InitializationFlag);
        int[] counts = new int[4];
        for (int table = 0; table < 4; table++)
        {
            for (int index = 0; index < 16; index++)
            {
                uint pointer = U32(profile.DirectoryAddress + 4u + (uint)(table * 64 + index * 4));
                if (pointer != 0)
                {
                    counts[table]++;
                    Assert.InRange(pointer, profile.ExecutableAddress, profile.ExecutableAddress + (uint)profile.ExecutableLength - 1);
                }
            }
        }
        Assert.Equal(new[] { 5, 2, 3, 2 }, counts);
        foreach (uint pointer in new uint[] { 0x163ACD4, 0x163B02C })
        {
            ImmutableArray<byte> sprite = NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(executable, pointer);
            Assert.Equal((byte)6, sprite[3]);
            Assert.Equal((ushort)16, BinaryPrimitives.ReadUInt16LittleEndian(sprite.AsSpan().Slice(8)));
            Assert.Equal(sprite.ToArray(), state.Read(pointer, sprite.Length));
        }
        foreach (uint pointer in new uint[] { 0x163B1D8, 0x163C6B4, 0x163C784 })
        {
            NativeBattleStaticMeshResource mesh = NativeBattleEffectStaticResourcesReader.ReadMesh(executable, pointer);
            Assert.Equal(mesh.Bytes.ToArray(), state.Read(pointer, mesh.Bytes.Length));
            int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()));
            int[] groups = new int[8];
            int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
            for (int group = 0; group < groups.Length; group++)
            {
                groups[group] = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor)));
                cursor += 4 + groups[group] * strides[group];
            }
            Assert.Equal(pointer == 0x163B1D8 ? new[] { 0, 0, 0, 35, 0, 0, 0, 85 } : new[] { 0, 0, 0, 4, 0, 0, 0, 0 }, groups);
        }

        uint U32(uint pointer) => BinaryPrimitives.ReadUInt32LittleEndian(state.Read(pointer, 4));
    }

    [Fact]
    public void Mega_phoenix_loader_tracks_remain_distinct_and_restore_preserves_mutation()
    {
        byte[] executable = File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "FF8_EN.exe"));
        NativeBattleRecoveryResources resources = NativeBattleRecoveryResourcesReader.Read(executable, 7);
        NativeBattleRecoveryExecutableState cold = NativeBattleRecoveryResourcesReader.ReadColdProcessState(executable, 7);
        ImmutableArray<byte> channels = Assert.IsType<ImmutableArray<byte>>(cold.Channels);
        Assert.Equal(0xD3C, channels.Length);
        Assert.All(channels, value => Assert.Equal((byte)0, value));
        NativeBattleRecoveryExecutableState changed = cold with { Channels = channels.SetItem(0, 0xA5).SetItem(channels.Length - 1, 0x5A) };
        NativeBattleRecoveryExecutableState restored = JsonSerializer.Deserialize<NativeBattleRecoveryExecutableState>(JsonSerializer.Serialize(changed))!;
        Assert.Same(restored, restored.Initialize(resources.Executable));
        Assert.Equal(new byte[] { 0xA5 }, restored.Read(0x2760B64, 1));
        Assert.Equal(new byte[] { 0x5A }, restored.Read(0x276189F, 1));
        Assert.Throws<InvalidDataException>(() => restored.Read(0x27618A0, 1));
        NativeBattleRecoveryExecutableState unknown = new(7, cold.Data);
        Assert.Throws<NotSupportedException>(() => unknown.Read(0x2760B64, 1));
    }
}
