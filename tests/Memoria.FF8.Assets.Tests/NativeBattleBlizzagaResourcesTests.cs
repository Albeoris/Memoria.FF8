using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBlizzagaResourcesTests
{
    [Fact]
    public void Original_Blizzaga_resources_retain_separate_geometry_vertex_banks_and_full_animation()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([104])[103];
        Assert.Equal(0x6D6310u, binding.Entry);
        Assert.Equal(0x6D62F0u, binding.Initializer);
        Assert.Equal("mag103.tim", binding.InitializerAsset!.Name);
        NativeBattleBlizzagaResources resources = Assert.IsType<NativeBattleBlizzagaResources>(binding.BlizzagaResources);
        Assert.Same(resources, game.ReadNativeBlizzagaResources());
        Assert.Null(game.ReadNativeEffectCatalog()[103].BlizzagaResources);
        Assert.Equal(0xC30, resources.Animation.Data.Length);
        NativeBattleEffectTextureState textures = NativeBattleEffectMemory.FromColdProcess().Textures!.Register5099A0(resources.Animation);
        Assert.Equal(resources.Animation, textures.Animation!.Source);
        Assert.Equal(0x1312690u, resources.Sprite10.Address);
        Assert.Equal(0x13127A0u, resources.Sprite14.Address);
        Assert.Equal(10, BinaryPrimitives.ReadUInt16LittleEndian(resources.Sprite10.Bytes.AsSpan().Slice(8)));
        Assert.Equal(14, BinaryPrimitives.ReadUInt16LittleEndian(resources.Sprite14.Bytes.AsSpan().Slice(8)));
        resources.Sprite10.Validate();
        resources.Sprite14.Validate();
        resources.PrimaryMesh.Validate(6, 28);
        resources.PartnerMesh.Validate(2, 20);
        resources.MorphSourceVertices.Validate();
        resources.ReferenceVertices.Validate();
        Assert.Equal(0x1312918u, resources.PrimaryMesh.Address);
        Assert.Equal(0x1315760u, resources.PartnerMesh.Address);
        Assert.Equal(0x1314C40u, resources.MorphSourceVertices.Address);
        Assert.Equal(0x13151D0u, resources.ReferenceVertices.Address);
        Assert.Equal(0x2328, resources.PrimaryMesh.Bytes.Length);
        Assert.Equal(0x1928, resources.PartnerMesh.Bytes.Length);
        Assert.Equal(0x1408, resources.Mesh1317088.Bytes.Length);
        Assert.Equal(0x6E8, resources.Mesh1318490.Bytes.Length);
        AssertGroups(resources.Mesh1317088, [0, 0, 0, 60, 0, 0, 12, 60]);
        AssertGroups(resources.Mesh1318490, [0, 0, 0, 0, 0, 48, 0, 0]);
        string json = JsonSerializer.Serialize(binding);
        NativeBattleEffectBinding restored = JsonSerializer.Deserialize<NativeBattleEffectBinding>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Throws<NotSupportedException>(() => NativeBattleBlizzagaResourcesReader.Read(new byte[128]));
    }

    [Fact]
    public void Separate_bank_contract_rejects_inline_layout_truncation_and_wrong_group_ownership()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleBlizzagaResources resources = game.ReadNativeBlizzagaResources();
        byte[] inline = resources.PrimaryMesh.Bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(inline, 0x590);
        Assert.Throws<InvalidDataException>(() => (resources.PrimaryMesh with { Bytes = inline.ToImmutableArray() }).Validate(6, 28));
        Assert.Throws<InvalidDataException>(() => resources.PrimaryMesh.Validate(2, 20));
        Assert.Throws<InvalidDataException>(() => (resources.PrimaryMesh with { Bytes = resources.PrimaryMesh.Bytes.RemoveAt(resources.PrimaryMesh.Bytes.Length - 1) }).Validate(6, 28));
        byte[] header = resources.ReferenceVertices.Bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(header, 8);
        Assert.Throws<InvalidDataException>(() => (resources.ReferenceVertices with { Bytes = header.ToImmutableArray() }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources.ReferenceVertices with { Bytes = resources.ReferenceVertices.Bytes.RemoveAt(resources.ReferenceVertices.Bytes.Length - 1) }).Validate());
    }

    private static void AssertGroups(NativeBattleStaticMeshResource mesh, int[] counts)
    {
        for (int group = 0; group < counts.Length; group++)
        {
            Assert.Equal(counts[group], NativeBattleIceMeshVertices.Group(mesh.Bytes.AsSpan(), group).Count);
        }
    }
}
