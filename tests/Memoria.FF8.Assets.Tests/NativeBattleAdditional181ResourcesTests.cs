using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleAdditional181Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleAdditional181ResourcesTests
{
    [Fact]
    public void Original_two_meshes_three_sprites_camera_and_sound_are_complete_and_cached()
    {
        using GameAssets game = Open();
        NativeBattleAdditional181Resources resources = game.ReadNativeAdditional181Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeAdditional181Resources());
        Assert.Equal(new[] { 0x1D68, 0xDA8 }, new[] { resources.MeshA.Bytes.Length, resources.MeshB.Bytes.Length });
        Assert.Equal(new[] { 234, 144 }, new[] { BinaryPrimitives.ReadInt32LittleEndian(resources.MeshA.Bytes.AsSpan().Slice(4)), BinaryPrimitives.ReadInt32LittleEndian(resources.MeshB.Bytes.AsSpan().Slice(4)) });
        Assert.Equal(new[] { 0x1AC, 0x160, 0xC4 }, new[] { resources.SpriteA.Bytes.Length, resources.SpriteB.Bytes.Length, resources.SpriteC.Bytes.Length });
        Assert.Equal((ushort)0xB6, BinaryPrimitives.ReadUInt16LittleEndian(resources.SpriteA.ProjectedFrame571C80(0).Quads.AsSpan().Slice(14)));
        Assert.Equal((ushort)0x43A, BinaryPrimitives.ReadUInt16LittleEndian(resources.SpriteB.ProjectedFrame571C80(0).Quads.AsSpan().Slice(14)));
        Assert.Equal((ushort)0x3A, BinaryPrimitives.ReadUInt16LittleEndian(resources.SpriteC.ProjectedFrame571C80(0).Quads.AsSpan().Slice(14)));
        Assert.Equal(CameraLength, resources.Camera.Data.Length);
        Assert.Equal(0x4949Fu, resources.Sound);
        Assert.Same(resources.MeshB, resources.ReadMesh(MeshBAddress));
        Assert.Same(resources.SpriteC, resources.ReadSprite(SpriteCAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadMesh(SpriteAAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadSprite(MeshAAddress));
    }

    [Fact]
    public void Resource_JSON_preserves_each_original_addressed_front()
    {
        using GameAssets game = Open();
        NativeBattleAdditional181Resources resources = game.ReadNativeAdditional181Resources();
        NativeBattleAdditional181Resources restored = JsonSerializer.Deserialize<NativeBattleAdditional181Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Invalid_face_sprite_material_directory_camera_or_sound_fails_at_its_resource_boundary()
    {
        using GameAssets game = Open();
        NativeBattleAdditional181Resources resources = game.ReadNativeAdditional181Resources();
        byte[] mesh = resources.MeshA.Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(8 + 234 * 8 + 16 + 4), 468);
        Assert.Throws<InvalidDataException>(() => (resources with { MeshA = resources.MeshA with { Bytes = mesh.ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { MeshB = resources.MeshB with { Address = MeshAAddress } }).Validate());
        byte[] sprite = resources.SpriteB.Bytes.ToArray();
        int offset = BinaryPrimitives.ReadUInt16LittleEndian(sprite.AsSpan(10));
        BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(offset + 18), 0x4000);
        Assert.Throws<InvalidDataException>(() => (resources with { SpriteB = resources.SpriteB with { Bytes = sprite.ToImmutableArray() } }).Validate());
        BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10), 0xFFFF);
        Assert.Throws<InvalidDataException>(() => (resources with { SpriteB = resources.SpriteB with { Bytes = sprite.ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sound = 0 }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Additional181 resources.");
        return new(directory!);
    }
}
