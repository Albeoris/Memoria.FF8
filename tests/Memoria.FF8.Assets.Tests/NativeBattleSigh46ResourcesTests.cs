using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleSigh46Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleSigh46ResourcesTests
{
    [Fact]
    public void Original_flag_one_sources_sprite_channels_and_custom_color_tables_keep_exact_fronts()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeSigh46Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeSigh46Resources());
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(resources.Executable.Data.AsSpan()));
        Assert.Equal(6, resources.Sprites.Length);
        Assert.Equal(34, resources.ColorStreams.Length);
        Assert.Equal(0x61AB4u, resources.Sound);
        Assert.Equal(CameraLength, resources.Camera.Data.Length);
        for (int node = 0; node < 7; node++)
        {
            uint address = BinaryPrimitives.ReadUInt32LittleEndian(resources.Executable.Data.AsSpan().Slice(4 + node * 4));
            byte[] source = resources.ReadStatic(address, 344);
            Assert.NotEqual((byte)4, source[0x11]);
            Assert.InRange(source[0x12], (byte)1, (byte)40);
        }
        foreach (uint table in new[] { ColorTableAAddress, ColorTableBAddress })
        {
            for (int selector = 0; selector < 17; selector++)
            {
                ImmutableArray<byte> stream = resources.ReadColorStream(table, selector);
                Assert.Equal(960, stream.Length);
                for (int face = 0; face < 60; face++)
                {
                    Assert.Equal((byte)0x3E, stream[face * 16 + 3]);
                }
            }
        }
        Assert.Equal(new short[] { 2, 3, 4, 5, 4, 3, 2, 1, 0 }, resources.Delays.ToArray());
        Assert.Equal((short)4096, resources.Fade[15]);
        Assert.Throws<InvalidDataException>(() => resources.ReadColorStream(ColorTableAAddress, -1));
        Assert.Throws<InvalidDataException>(() => resources.ReadColorStream(ColorTableBAddress, 17));
        Assert.Throws<InvalidDataException>(() => resources.ReadStatic(ChannelBase, 2));
        Assert.Throws<InvalidDataException>(() => resources.ReadStatic(SourceAddress, SourceLength));
        Assert.Throws<InvalidDataException>(() => resources.ReadStatic(MeshAAddress, MeshLength));
    }

    [Fact]
    public void Current_material_colors_and_unknown_W_survive_JSON_without_mutating_original_seeds()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeSigh46Resources();
        var state = NativeBattleSigh46State.FromColdProcess().EnsureSources(resources);
        uint faceAddress = MeshAAddress + 656;
        state = state.WriteDword(faceAddress, 0x3E123456).WriteView(MeshAAddress + 14, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2);
        var restored = JsonSerializer.Deserialize<NativeBattleSigh46State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Same(restored, restored.EnsureSources(resources));
        Assert.Equal(0x3E123456u, restored.ReadDword(faceAddress));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(MeshAAddress + 14));
        Assert.Equal(MeshLength, restored.ReadMesh(resources, MeshAAddress).Bytes.Length);
        Assert.NotEqual(0x3E123456u, BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadMesh(MeshAAddress).Bytes.AsSpan().Slice(656)));
        var resourceCopy = JsonSerializer.Deserialize<NativeBattleSigh46Resources>(JsonSerializer.Serialize(resources))!;
        resourceCopy.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(resourceCopy));
    }

    [Fact]
    public void Truncated_or_changed_sources_tables_meshes_camera_and_delay_fronts_fail()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeSigh46Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { Executable = resources.Executable with { Data = resources.Executable.Data.SetItem(0, 0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { MeshSeeds = resources.MeshSeeds.SetItem(0, resources.MeshSeeds[0] with { Bytes = resources.MeshSeeds[0].Bytes.RemoveAt(0) }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { ColorTables = resources.ColorTables.SetItem(0, resources.ColorTables[0] with { Bytes = resources.ColorTables[0].Bytes.SetItem(0, 0) }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { ColorStreams = resources.ColorStreams.SetItem(0, resources.ColorStreams[0] with { Bytes = resources.ColorStreams[0].Bytes.RemoveAt(0) }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Delays = resources.Delays.SetItem(8, 1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Fade = resources.Fade.SetItem(15, 4095) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sound = 0 }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Sigh resources.");
        return new(directory!);
    }
}
