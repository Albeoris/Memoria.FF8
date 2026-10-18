using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleSearchModelResourcesTests
{
    [Fact]
    public void Original_effect_owned_model_retains_all_four_sections_without_an_actor_or_texture_relocation()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleSearchModelResources resources = game.ReadNativeSearchModelResources();
        NativeBattleEffectOwnedModelResource model = resources.Model;
        Assert.Equal(0x153D59Cu, model.Address);
        Assert.Equal(0x5B38, model.Bytes.Length);
        Assert.Equal(new[] { 0x18, 0x688, 0x3344, 0x5B2C, 0x5B38 }, Enumerable.Range(0, 5).Select(model.Section).ToArray());
        Assert.Equal(0x670, model.PoseSource.Skeleton.Length);
        Assert.Equal(34, model.PoseSource.BoneCount);
        Assert.Equal(0x27E8, model.PoseSource.Animations.Length);
        Assert.Equal(new byte[] { 6, 73, 6, 32, 6 }, NativeBattlePoseDecoder.ReadFrameCounts(model.PoseSource.Animations.AsSpan()).ToArray());
        NativeBattleRawMesh mesh = Assert.Single(model.Geometry);
        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Polygons);
        Assert.All(mesh.Vertices, vertex => Assert.InRange(vertex.Bone, (ushort)0, (ushort)33));
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(model.Bytes.AsSpan().Slice(model.Section(1))));
        Assert.Equal(8, BinaryPrimitives.ReadInt32LittleEndian(model.Bytes.AsSpan().Slice(model.Section(1) + 4)));
        Assert.Equal(12, model.Section(4) - model.Section(3));
        Assert.Equal(0x32808080u, resources.DefaultColor44);
        Assert.Equal(0x8420, resources.ModelTim.Data.Length);
        Assert.Equal(0x10u, BinaryPrimitives.ReadUInt32LittleEndian(resources.ModelTim.Data.AsSpan()));
        Assert.Equal(9u, BinaryPrimitives.ReadUInt32LittleEndian(resources.ModelTim.Data.AsSpan().Slice(4)));
        var upload = NativeBattleEffectTextureState.FromColdProcess().EnqueueTim505E30(resources.ModelTim);
        Assert.Equal(resources.ModelTim.Data.Length, upload.NextOffset);
        Assert.NotEmpty(upload.State.Drain505D20().Uploads);
        Assert.Same(resources, game.ReadNativeSearchModelResources());
        string json = JsonSerializer.Serialize(resources);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleSearchModelResources>(json)!));
        Assert.Throws<NotSupportedException>(() => NativeBattleSearchModelResourcesReader.Read(new byte[128]));
    }
}
