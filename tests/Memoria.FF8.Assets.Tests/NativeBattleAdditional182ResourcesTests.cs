using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleAdditional182Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleAdditional182ResourcesTests
{
    [Fact]
    public void Original_fourteen_meshes_two_sprites_and_both_camera_sound_branches_are_complete_and_cached()
    {
        using GameAssets game = Open();
        NativeBattleAdditional182Resources resources = game.ReadNativeAdditional182Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeAdditional182Resources());
        Assert.Equal(0x560, resources.HistoryMesh.Bytes.Length);
        Assert.Equal(0x270, resources.PrimaryMesh.Bytes.Length);
        Assert.Equal(MeshVariantAddresses.ToArray(), resources.MeshVariants.Select(mesh => mesh.Address).ToArray());
        Assert.All(resources.MeshVariants, mesh => Assert.Equal(0x270, mesh.Bytes.Length));
        Assert.Equal(new[] { 0x10C, 0x1AC }, new[] { resources.SpriteA.Bytes.Length, resources.SpriteB.Bytes.Length });
        Assert.Equal(CameraClearLength, resources.Camera(false).Data.Length);
        Assert.Equal(CameraSetLength, resources.Camera(true).Data.Length);
        Assert.Equal(0x494ABu, resources.Sound(false));
        Assert.Equal(0x494C1u, resources.Sound(true));
        Assert.Equal(8, resources.Colors.Length);
        Assert.Same(resources.HistoryMesh, resources.ReadMesh(HistoryMeshAddress));
        Assert.Same(resources.MeshVariants[11], resources.ReadMesh(MeshVariantAddresses[11]));
        Assert.Same(resources.SpriteB, resources.ReadSprite(SpriteBAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadMesh(MeshPointersAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadSprite(CameraSetAddress));
    }

    [Fact]
    public void Resource_JSON_preserves_all_selected_meshes_and_distinct_branch_resources()
    {
        using GameAssets game = Open();
        NativeBattleAdditional182Resources resources = game.ReadNativeAdditional182Resources();
        NativeBattleAdditional182Resources restored = JsonSerializer.Deserialize<NativeBattleAdditional182Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Altered_F4_face_variant_identity_group_camera_sound_or_palette_fails_fast()
    {
        using GameAssets game = Open();
        NativeBattleAdditional182Resources resources = game.ReadNativeAdditional182Resources();
        byte[] mesh = resources.HistoryMesh.Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(8 + 35 * 8 + 8 + 4), 70);
        Assert.Throws<InvalidDataException>(() => (resources with { HistoryMesh = resources.HistoryMesh with { Bytes = mesh.ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { MeshVariants = resources.MeshVariants.SetItem(0, resources.MeshVariants[0] with { Address = PrimaryMeshAddress }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { MeshVariants = resources.MeshVariants.RemoveAt(11) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { CameraClear = resources.CameraSet }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SpriteA = resources.SpriteA with { Address = SpriteBAddress } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SoundSet = resources.SoundClear }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Colors = resources.Colors.SetItem(0, 0) }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Additional182 resources.");
        return new(directory!);
    }
}
