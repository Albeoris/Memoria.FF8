using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleWildCannonBlow156Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleWildCannonBlow156ResourcesTests
{
    [Fact]
    public void Original_four_meshes_keep_distinct_sources_groups_and_sprite_fronts()
    {
        using GameAssets game = Open();
        NativeBattleWildCannonBlow156Resources resources = game.ReadNativeWildCannonBlow156Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeWildCannonBlow156Resources());
        Assert.Equal(0x688, resources.MeshA.Bytes.Length);
        Assert.Equal(0x510, resources.MeshB.Bytes.Length);
        Assert.Equal(0xC30, resources.MeshC.Bytes.Length);
        Assert.Equal(0x928, resources.MeshD.Bytes.Length);
        Assert.Equal(60, BinaryPrimitives.ReadInt32LittleEndian(resources.MeshA.Bytes.AsSpan().Slice(4)));
        Assert.Equal(49, BinaryPrimitives.ReadInt32LittleEndian(resources.MeshB.Bytes.AsSpan().Slice(4)));
        Assert.Equal(73, BinaryPrimitives.ReadInt32LittleEndian(resources.MeshC.Bytes.AsSpan().Slice(4)));
        Assert.Equal(72, BinaryPrimitives.ReadInt32LittleEndian(resources.MeshD.Bytes.AsSpan().Slice(4)));
        Assert.Equal(0x61AD9u, resources.SoundA);
        Assert.Equal(0x61ADBu, resources.SoundB);
        Assert.Equal(4, BinaryPrimitives.ReadUInt16LittleEndian(resources.SpriteB.Bytes.AsSpan().Slice(8)));
    }

    [Fact]
    public void Immutable_resources_roundtrip_with_exact_addressed_mesh_and_sprite_selection()
    {
        using GameAssets game = Open();
        NativeBattleWildCannonBlow156Resources resources = game.ReadNativeWildCannonBlow156Resources();
        NativeBattleWildCannonBlow156Resources restored = JsonSerializer.Deserialize<NativeBattleWildCannonBlow156Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Same(restored.MeshA, restored.Mesh(MeshAAddress));
        Assert.Same(restored.MeshB, restored.Mesh(MeshBAddress));
        Assert.Same(restored.MeshC, restored.Mesh(MeshCAddress));
        Assert.Same(restored.MeshD, restored.Mesh(MeshDAddress));
        Assert.Same(restored.SpriteE, restored.Sprite(SpriteEAddress));
        Assert.Throws<NotSupportedException>(() => restored.Mesh(SpriteAAddress));
        Assert.Throws<NotSupportedException>(() => restored.Sprite(MeshAAddress));
    }

    [Fact]
    public void Strict_original_extents_and_primitive_groups_reject_changed_resources()
    {
        using GameAssets game = Open();
        NativeBattleWildCannonBlow156Resources resources = game.ReadNativeWildCannonBlow156Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { MeshA = resources.MeshA with { Bytes = resources.MeshA.Bytes.Take(0x687).ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SpriteB = resources.SpriteB with { Bytes = resources.SpriteB.Bytes.Take(0x73).ToImmutableArray() } }).Validate());
        byte[] mesh = resources.MeshB.Bytes.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(0x19C), 35);
        Assert.Throws<InvalidDataException>(() => (resources with { MeshB = resources.MeshB with { Bytes = mesh.ToImmutableArray() } }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Wild Cannon Blow resources.");
        return new(directory!);
    }
}
