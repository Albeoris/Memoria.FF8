using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleCannonBlow151Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCannonBlow151ResourcesTests
{
    [Fact]
    public void Original_five_meshes_sprites_camera_and_sound_are_complete_and_cached()
    {
        using GameAssets game = Open();
        NativeBattleCannonBlow151Resources resources = game.ReadNativeCannonBlow151Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeCannonBlow151Resources());
        Assert.Equal(0x688, resources.MeshA.Bytes.Length);
        Assert.Equal(0x510, resources.MeshB.Bytes.Length);
        Assert.Equal(0xC30, resources.MeshC.Bytes.Length);
        Assert.Equal(0x928, resources.MeshD.Bytes.Length);
        Assert.Equal(0x810, resources.ScrollingMesh.Bytes.Length);
        Assert.False(resources.MeshB.Bytes.AsSpan().SequenceEqual(resources.MeshC.Bytes.AsSpan()));
        Assert.Equal(60, BinaryPrimitives.ReadInt32LittleEndian(resources.MeshA.Bytes.AsSpan().Slice(4)));
        Assert.Equal(0x1AC, resources.SpriteA.Bytes.Length);
        Assert.Equal(0x1AC, resources.SpriteB.Bytes.Length);
        Assert.Equal(0x1AC, resources.SpriteC.Bytes.Length);
        Assert.Equal(0x74, resources.SpriteD.Bytes.Length);
        Assert.Equal(0x1AC, resources.SpriteE.Bytes.Length);
        Assert.Equal(0x110, resources.Camera.Data.Length);
        Assert.Equal(0x61AD9u, resources.Sound);
        Assert.Same(resources.MeshC, resources.Mesh(MeshCAddress));
        Assert.Same(resources.SpriteB, resources.Sprite(SpriteBAddress));
        Assert.Throws<NotSupportedException>(() => resources.Mesh(SpriteAAddress));
        Assert.Throws<NotSupportedException>(() => resources.Sprite(MeshAAddress));
    }

    [Fact]
    public void Resource_JSON_roundtrip_preserves_each_original_addressed_source()
    {
        using GameAssets game = Open();
        NativeBattleCannonBlow151Resources resources = game.ReadNativeCannonBlow151Resources();
        NativeBattleCannonBlow151Resources restored = JsonSerializer.Deserialize<NativeBattleCannonBlow151Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Changed_mesh_face_sprite_directory_camera_extent_and_sound_fail_fast()
    {
        using GameAssets game = Open();
        NativeBattleCannonBlow151Resources resources = game.ReadNativeCannonBlow151Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { MeshA = resources.MeshA with { Bytes = resources.MeshA.Bytes.RemoveAt(0) } }).Validate());
        byte[] mesh = resources.ScrollingMesh.Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(8 + 61 * 8 + 12 + 4), 122);
        Assert.Throws<InvalidDataException>(() => (resources with { ScrollingMesh = resources.ScrollingMesh with { Bytes = mesh.ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SpriteB = resources.SpriteB with { Address = SpriteAAddress } }).Validate());
        byte[] sprite = resources.SpriteC.Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10), 0xFFFF);
        Assert.Throws<InvalidDataException>(() => (resources with { SpriteC = resources.SpriteC with { Bytes = sprite.ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sound = 0 }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Cannon Blow resources.");
        return new(directory!);
    }
}
