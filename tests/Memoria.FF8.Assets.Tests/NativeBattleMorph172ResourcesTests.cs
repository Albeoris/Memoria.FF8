using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleMorph172Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleMorph172ResourcesTests
{
    [Fact]
    public void Original_mesh_sprite_color_cycle_camera_and_sound_are_complete_and_cached()
    {
        using GameAssets game = Open();
        NativeBattleMorph172Resources resources = game.ReadNativeMorph172Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeMorph172Resources());
        Assert.Equal(0x2C40, resources.MeshA.Bytes.Length);
        Assert.Equal(261, BinaryPrimitives.ReadInt32LittleEndian(resources.MeshA.Bytes.AsSpan().Slice(4)));
        Assert.Equal(0x160, resources.SpriteA.Bytes.Length);
        Assert.Equal((ushort)0xB8, BinaryPrimitives.ReadUInt16LittleEndian(resources.SpriteA.ProjectedFrame571C80(0).Quads.AsSpan().Slice(14)));
        Assert.Equal(new uint[] { 0x20C0C0,0x20C0A0,0x20C080,0x20C060,0x20C040,0x20C020,0x40A020,0x608020,0x806020,0xA04020,0xC02020,0xA04020,0x806020,0x608020,0x40A020,0x20C020,0x20C040,0x20C060,0x20C080,0x20C0A0 }, resources.Colors.ToArray());
        Assert.Equal(CameraLength, resources.Camera.Data.Length);
        Assert.Equal(0x61AD3u, resources.Sound);
        Assert.Same(resources.MeshA, resources.ReadMesh(MeshAAddress));
        Assert.Same(resources.SpriteA, resources.ReadSprite(SpriteAAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadMesh(SpriteAAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadSprite(MeshAAddress));
    }

    [Fact]
    public void Resource_JSON_preserves_each_original_addressed_front()
    {
        using GameAssets game = Open();
        NativeBattleMorph172Resources resources = game.ReadNativeMorph172Resources();
        NativeBattleMorph172Resources restored = JsonSerializer.Deserialize<NativeBattleMorph172Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Invalid_face_sprite_material_directory_camera_or_sound_fails_at_its_resource_boundary()
    {
        using GameAssets game = Open();
        NativeBattleMorph172Resources resources = game.ReadNativeMorph172Resources();
        byte[] mesh = resources.MeshA.Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(8 + 261 * 8 + 32), 522);
        Assert.Throws<InvalidDataException>(() => (resources with { MeshA = resources.MeshA with { Bytes = mesh.ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Colors = resources.Colors.RemoveAt(0) }).Validate());
        byte[] sprite = resources.SpriteA.Bytes.ToArray();
        int offset = BinaryPrimitives.ReadUInt16LittleEndian(sprite.AsSpan(10));
        BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(offset + 18), 0x4000);
        Assert.Throws<InvalidDataException>(() => (resources with { SpriteA = resources.SpriteA with { Bytes = sprite.ToImmutableArray() } }).Validate());
        BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10), 0xFFFF);
        Assert.Throws<InvalidDataException>(() => (resources with { SpriteA = resources.SpriteA with { Bytes = sprite.ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sound = 0 }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Morph172 resources.");
        return new(directory!);
    }
}
