using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect345ResourceTests
{
    [Fact]
    public void Original_selected_resources_keep_model_node_and_morph_owners_distinct()
    {
        using GameAssets game = Open();
        NativeBattleEffect345Resources resources = game.ReadNativeEffect345Resources();
        resources.Validate();
        Assert.Equal("mag013-073-344.tim", resources.MainTim.Name);
        Assert.Equal(167456, resources.MainTim.Data.Length);
        Assert.Equal(4064, resources.Camera.Data.Length);
        Assert.Equal(0x255E618u, resources.ColdPointerSeeds[0x1524658]);
        Assert.Equal(0x255EA58u, resources.ColdPointerSeeds[0x15297A0]);
        Assert.Equal(28, resources.Model.PoseSource.BoneCount);
        Assert.Equal(302, Assert.Single(resources.Model.Geometry).Vertices.Length);
        Assert.Equal(526, Assert.Single(resources.Model.Geometry).Polygons.Length);
        Assert.Equal(new[] { 18932, 9708 }, resources.Nodes.Select(node => node.Data.Length).ToArray());
        ImmutableArray<byte> counts = NativeBattlePoseDecoder.ReadFrameCounts(resources.Model.PoseSource.Animations.AsSpan());
        Assert.Equal((byte)24, counts[5]);
        Assert.Equal((byte)18, counts[6]);
        Assert.Equal(28, resources.Model.PoseSource.Sample(5, 24, 0).Bones.Length);
        Assert.Equal(28, resources.Model.PoseSource.Sample(6, 18, 0).Bones.Length);
        NativeBattleEffect345NodeResourceBank bank = resources.Nodes[0];
        uint node = bank.Directory(0, 3);
        uint curve = Dword(bank.Data, checked((int)node + 0x138));
        foreach (int index in new[] { 0, 1 })
        {
            uint shape = bank.Directory(3, index);
            short selector = BinaryPrimitives.ReadInt16LittleEndian(bank.Data.AsSpan().Slice(checked((int)shape + 0x1CA), 2));
            Assert.Equal((short)0, selector);
            Assert.Equal((ushort)2048, BinaryPrimitives.ReadUInt16LittleEndian(bank.Data.AsSpan().Slice(checked((int)curve + selector * 2), 2)));
            uint destination = bank.Directory(2, 3);
            Assert.True(destination + 2872 <= shape || shape + 680 <= destination);
        }
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(bank.Address, 4));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0xB8B9A8, 4));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0x1524658, 4));
        NativeBattleEffect345State state = NativeBattleEffect345State.FromColdProcess().BindNodeSources(resources).WriteDword(bank.Address, 1);
        Assert.Same(state, state.BindNodeSources(resources));
        Assert.Equal(0u, Dword(bank.Data, 0));
        Assert.Equal(1u, state.ReadDword(bank.Address));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect345State.FromUnknown().BindNodeSources(resources));
        string stateJson = JsonSerializer.Serialize(state);
        Assert.Equal(stateJson, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleEffect345State>(stateJson)));
    }

    [Fact]
    public void Resource_roundtrip_rejects_changed_identity_truncation_and_unowned_static_spans()
    {
        using GameAssets game = Open();
        NativeBattleEffect345Resources resources = game.ReadNativeEffect345Resources();
        string json = JsonSerializer.Serialize(resources);
        NativeBattleEffect345Resources restored = JsonSerializer.Deserialize<NativeBattleEffect345Resources>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => (resources with { MainTim = resources.MainTim with { Name = "other.tim" } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Nodes = resources.Nodes.SetItem(0, resources.Nodes[0] with { Data = resources.Nodes[0].Data.RemoveAt(0) }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { StaticSpans = resources.StaticSpans.Add(0xB8B9A8, new byte[4].ToImmutableArray()) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { ColdPointerSeeds = resources.ColdPointerSeeds.SetItem(0x1524658, 0) }).Validate());
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Effect345 resources.");
        return new(directory!);
    }
}
