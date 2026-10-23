using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect83Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect83ResourceTests
{
    [Fact]
    public void Original_selected_textures_camera_and_owned_model_keep_complete_native_fronts()
    {
        using GameAssets game = Open();
        NativeBattleEffect83Resources resources = game.ReadNativeEffect83Resources();
        resources.Validate();
        Assert.Equal("mag082.tim", resources.MainTim.Name);
        Assert.Equal(164896, resources.MainTim.Data.Length);
        Assert.Equal(33824, resources.EmbeddedTim.Data.Length);
        Assert.Equal(656, resources.Camera.Data.Length);
        Assert.Equal(0x66901u, resources.Sound);
        Assert.Equal(TargetAddress, resources.TargetRecordAddress);
        Assert.Equal(27, resources.Model.PoseSource.BoneCount);
        Assert.Equal(141, Assert.Single(resources.Model.Geometry).Vertices.Length);
        Assert.Equal(239, Assert.Single(resources.Model.Geometry).Polygons.Length);
        Assert.Equal(new[] { 20, 35, 21 }, NativeBattlePoseDecoder.DecodeFrames(resources.Model.PoseSource.Animations.AsSpan(), 27).Select(animation => animation.Length).ToArray());
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(NodeSourceAddress, 4));
    }

    [Fact]
    public void Current_node_bank_binds_once_retains_null_mesh_and_signed_shape_alias_factor()
    {
        using GameAssets game = Open();
        NativeBattleEffect83Resources resources = game.ReadNativeEffect83Resources();
        NativeBattleEffect83State cold = NativeBattleEffect83State.FromColdProcess();
        NativeBattleEffect83State state = cold.BindNodeSource(resources);
        Assert.Null(cold.NodeData);
        Assert.Equal(13, Enumerable.Range(0, 16).Count(index => resources.Nodes.Directory(0, index) != 0));
        Assert.Equal(0u, resources.Nodes.Directory(2, 6));
        int node0 = checked((int)resources.Nodes.Directory(0, 0));
        uint curve = NodeSourceAddress + Dword(resources.Nodes.Data, node0 + 0x138);
        foreach (int index in new[] { 0, 1 })
        {
            uint shape = NodeSourceAddress + resources.Nodes.Directory(3, index);
            short selector = unchecked((short)state.ReadWord(shape + 0x1CA));
            Assert.Equal((short)-451, selector);
            uint selected = unchecked(curve + (uint)(2 * selector));
            Assert.Equal(0x172FDBEu, selected);
            Assert.Equal((ushort)0, state.ReadWord(selected));
        }
        state = state.WriteDword(NodeSourceAddress, 1).WriteWord(NodeSourceAddress + 0xB278 + 0x10, 19);
        Assert.Same(state, state.BindNodeSource(resources));
        Assert.Equal(0u, Dword(resources.Nodes.Data, 0));
        Assert.Equal(1u, state.ReadDword(NodeSourceAddress));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect83State.FromUnknown().BindNodeSource(resources));
        string json = JsonSerializer.Serialize(state);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleEffect83State>(json)));
    }

    [Fact]
    public void Resource_roundtrip_rejects_truncated_banks_wrong_pointer_and_texture_identity()
    {
        using GameAssets game = Open();
        NativeBattleEffect83Resources resources = game.ReadNativeEffect83Resources();
        string json = JsonSerializer.Serialize(resources);
        NativeBattleEffect83Resources restored = JsonSerializer.Deserialize<NativeBattleEffect83Resources>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => (resources with { TargetRecordAddress = TargetAddress + 4 }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Nodes = new(resources.Nodes.Data.RemoveAt(0)) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { MainTim = resources.MainTim with { Name = "other.tim" } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { EmbeddedTim = resources.EmbeddedTim with { Data = resources.EmbeddedTim.Data.RemoveAt(0) } }).Validate());
    }

    private static uint Dword(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(offset, 4));

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Effect83 resources.");
        return new(directory!);
    }
}
