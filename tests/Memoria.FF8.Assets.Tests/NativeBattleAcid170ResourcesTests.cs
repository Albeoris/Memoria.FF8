using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleAcid170Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleAcid170ResourcesTests
{
    [Fact]
    public void Original_meshes_sprites_endpoints_thresholds_camera_and_sound_are_complete_and_cached()
    {
        using GameAssets game = Open();
        NativeBattleAcid170Resources resources = game.ReadNativeAcid170Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeAcid170Resources());
        Assert.Equal(new[] { 0xA20, 0x6E8 }, new[] { resources.MeshA.Bytes.Length, resources.MeshB.Bytes.Length });
        Assert.Equal(new[] { 73, 72 }, new[] { BinaryPrimitives.ReadInt32LittleEndian(resources.MeshA.Bytes.AsSpan().Slice(4)), BinaryPrimitives.ReadInt32LittleEndian(resources.MeshB.Bytes.AsSpan().Slice(4)) });
        Assert.Equal(new[] { 0xA8, 0x1AC }, new[] { resources.SpriteA.Bytes.Length, resources.SpriteB.Bytes.Length });
        Assert.Equal((ushort)0x4BA, BinaryPrimitives.ReadUInt16LittleEndian(resources.SpriteA.ProjectedFrame571C80(0).Quads.AsSpan().Slice(14)));
        Assert.Equal((ushort)0x4B8, BinaryPrimitives.ReadUInt16LittleEndian(resources.SpriteB.ProjectedFrame571C80(0).Quads.AsSpan().Slice(14)));
        Assert.Equal(SelectedVerticesLength, resources.EndpointVertices.Length);
        Assert.Equal(new int[] { 5, 4, 4, 3, 3, 0, 2, 1, 0, 1, 1, 0, 1, 0, 0, 0 }, resources.DripCounts.ToArray());
        Assert.Equal(CameraLength, resources.Camera.Data.Length);
        Assert.Equal(0x61AFBu, resources.Sound);
        Assert.Same(resources.MeshA, resources.ReadMesh(MeshAAddress));
        Assert.Same(resources.SpriteA, resources.ReadSprite(SpriteAAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadMesh(SpriteAAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadSprite(MeshAAddress));
    }

    [Fact]
    public void Resource_JSON_preserves_each_original_addressed_front()
    {
        using GameAssets game = Open();
        NativeBattleAcid170Resources resources = game.ReadNativeAcid170Resources();
        NativeBattleAcid170Resources restored = JsonSerializer.Deserialize<NativeBattleAcid170Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Invalid_face_sprite_material_directory_camera_or_sound_fails_at_its_resource_boundary()
    {
        using GameAssets game = Open();
        NativeBattleAcid170Resources resources = game.ReadNativeAcid170Resources();
        byte[] mesh = resources.MeshA.Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(8 + 73 * 8 + 16), 146);
        Assert.Throws<InvalidDataException>(() => (resources with { MeshA = resources.MeshA with { Bytes = mesh.ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { EndpointVertices = resources.EndpointVertices.RemoveAt(0) }).Validate());
        byte[] sprite = resources.SpriteA.Bytes.ToArray();
        int offset = BinaryPrimitives.ReadUInt16LittleEndian(sprite.AsSpan(10));
        BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(offset + 18), 0x4000);
        Assert.Throws<InvalidDataException>(() => (resources with { SpriteA = resources.SpriteA with { Bytes = sprite.ToImmutableArray() } }).Validate());
        BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10), 0xFFFF);
        Assert.Throws<InvalidDataException>(() => (resources with { SpriteA = resources.SpriteA with { Bytes = sprite.ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sound = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { DripCounts = resources.DripCounts.RemoveAt(0) }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Acid170 resources.");
        return new(directory!);
    }
}
