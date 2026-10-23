using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleSandStorm155Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleSandStorm155ResourcesTests
{
    [Fact]
    public void Original_mesh_keeps_304_vertices_and_exact_FT4_GT4_sources()
    {
        using GameAssets game = Open();
        NativeBattleSandStorm155Resources resources = game.ReadNativeSandStorm155Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeSandStorm155Resources());
        Assert.Equal(MeshLength, resources.Mesh.Bytes.Length);
        Assert.Equal(VertexCount, BinaryPrimitives.ReadInt32LittleEndian(resources.Mesh.Bytes.AsSpan().Slice(4)));
        Assert.Equal(0x988, BinaryPrimitives.ReadInt32LittleEndian(resources.Mesh.Bytes.AsSpan()));
        Assert.Equal(304 * 8, checked((int)(SortedRowsAddress - SelectedVerticesAddress)));
        Assert.Equal(0x61AE5u, resources.Sound);
        Assert.Equal(16, BinaryPrimitives.ReadUInt16LittleEndian(resources.SpriteA.Bytes.AsSpan().Slice(8)));
        Assert.Equal(10, BinaryPrimitives.ReadUInt16LittleEndian(resources.SpriteB.Bytes.AsSpan().Slice(8)));
    }

    [Fact]
    public void Immutable_resources_roundtrip_and_keep_exact_addressed_sprite_selection()
    {
        using GameAssets game = Open();
        NativeBattleSandStorm155Resources resources = game.ReadNativeSandStorm155Resources();
        NativeBattleSandStorm155Resources restored = JsonSerializer.Deserialize<NativeBattleSandStorm155Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Same(restored.SpriteA, restored.Sprite(SpriteAAddress));
        Assert.Same(restored.SpriteB, restored.Sprite(SpriteBAddress));
        Assert.Throws<NotSupportedException>(() => restored.Sprite(MeshAddress));
    }

    [Fact]
    public void Strict_extents_and_geometry_groups_reject_truncated_or_changed_sources()
    {
        using GameAssets game = Open();
        NativeBattleSandStorm155Resources resources = game.ReadNativeSandStorm155Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { Mesh = resources.Mesh with { Bytes = resources.Mesh.Bytes.Take(MeshLength - 1).ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SpriteB = resources.SpriteB with { Bytes = resources.SpriteB.Bytes.Take(0x10F).ToImmutableArray() } }).Validate());
        byte[] mesh = resources.Mesh.Bytes.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(0x994), 225);
        Assert.Throws<InvalidDataException>(() => (resources with { Mesh = resources.Mesh with { Bytes = mesh.ToImmutableArray() } }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Sand Storm resources.");
        return new(directory!);
    }
}
