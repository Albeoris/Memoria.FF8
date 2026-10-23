using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleBreath157Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBreath157ResourcesTests
{
    [Fact]
    public void Original_five_meshes_keep_distinct_sources_groups_and_sprite_fronts()
    {
        using GameAssets game = Open();
        NativeBattleBreath157Resources resources = game.ReadNativeBreath157Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeBreath157Resources());
        Assert.Equal(0x1320, resources.MeshA.Bytes.Length);
        Assert.Equal(0xE78, resources.MeshB.Bytes.Length);
        Assert.Equal(0xC88, resources.MeshC.Bytes.Length);
        Assert.Equal(0x6E8, resources.MeshD.Bytes.Length);
        Assert.Equal(0x9E8, resources.MeshE.Bytes.Length);
        Assert.Equal(new NativeBattleVector(0, 0, -4096), resources.ReferenceDirection);
        Assert.Equal(145, BinaryPrimitives.ReadInt32LittleEndian(resources.MeshA.Bytes.AsSpan().Slice(4)));
        Assert.Equal(110, BinaryPrimitives.ReadInt32LittleEndian(resources.MeshB.Bytes.AsSpan().Slice(4)));
        Assert.Equal(108, BinaryPrimitives.ReadInt32LittleEndian(resources.MeshC.Bytes.AsSpan().Slice(4)));
        Assert.Equal(72, BinaryPrimitives.ReadInt32LittleEndian(resources.MeshD.Bytes.AsSpan().Slice(4)));
        Assert.Equal(0x61ABAu, resources.SoundA);
        Assert.Equal(0x61ABDu, resources.SoundB);
        Assert.Equal(4, BinaryPrimitives.ReadUInt16LittleEndian(resources.SpriteB.Bytes.AsSpan().Slice(8)));
    }

    [Fact]
    public void Immutable_resources_roundtrip_with_exact_addressed_mesh_and_sprite_selection()
    {
        using GameAssets game = Open();
        NativeBattleBreath157Resources resources = game.ReadNativeBreath157Resources();
        NativeBattleBreath157Resources restored = JsonSerializer.Deserialize<NativeBattleBreath157Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Same(restored.MeshA, restored.Mesh(MeshAAddress));
        Assert.Same(restored.MeshB, restored.Mesh(MeshBAddress));
        Assert.Same(restored.MeshC, restored.Mesh(MeshCAddress));
        Assert.Same(restored.MeshD, restored.Mesh(MeshDAddress));
        Assert.Same(restored.MeshE, restored.Mesh(MeshEAddress));
        Assert.Same(restored.SpriteC, restored.Sprite(SpriteCAddress));
        Assert.Throws<NotSupportedException>(() => restored.Mesh(SpriteAAddress));
        Assert.Throws<NotSupportedException>(() => restored.Sprite(MeshAAddress));
    }

    [Fact]
    public void Strict_original_extents_and_primitive_groups_reject_changed_resources()
    {
        using GameAssets game = Open();
        NativeBattleBreath157Resources resources = game.ReadNativeBreath157Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { MeshA = resources.MeshA with { Bytes = resources.MeshA.Bytes.Take(0x131F).ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SpriteB = resources.SpriteB with { Bytes = resources.SpriteB.Bytes.Take(0x73).ToImmutableArray() } }).Validate());
        byte[] mesh = resources.MeshB.Bytes.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(0x380), 23);
        Assert.Throws<InvalidDataException>(() => (resources with { MeshB = resources.MeshB with { Bytes = mesh.ToImmutableArray() } }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Breath resources.");
        return new(directory!);
    }
}
