using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleMeltEye158ResourcesTests
{
    [Fact]
    public void Original_resources_preserve_exact_mesh_groups_sprite_frames_and_packed_vectors()
    {
        using GameAssets game = Open();
        NativeBattleMeltEye158Resources resources = game.ReadNativeMeltEye158Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeMeltEye158Resources());
        Assert.Equal("FF8_EN:DAFBF8", resources.Camera.Identity);
        Assert.Equal(0xB4, resources.Camera.Data.Length);
        Assert.Equal(0x61AC0u, resources.Sound);
        Assert.Equal(new NativeBattleModuleAnchor(0, 100, -360, 0), resources.Vectors[0]);
        Assert.Equal(new NativeBattleModuleAnchor(600, -400, 200, 0), resources.Vectors[10]);
        AssertMesh(resources.MeshA, 33, [0, 0, 0, 0, 16, 16, 0, 0]);
        AssertMesh(resources.MeshB, 25, [0, 0, 0, 16, 0, 0, 0, 0]);
        Assert.Equal((ushort)16, BinaryPrimitives.ReadUInt16LittleEndian(resources.SpriteA.Bytes.AsSpan().Slice(8)));
        Assert.Equal((ushort)16, BinaryPrimitives.ReadUInt16LittleEndian(resources.SpriteB.Bytes.AsSpan().Slice(8)));
        Assert.Equal((ushort)8, BinaryPrimitives.ReadUInt16LittleEndian(resources.SpriteC.Bytes.AsSpan().Slice(8)));
    }

    [Fact]
    public void JSON_and_strict_address_lookup_keep_complete_original_sources()
    {
        using GameAssets game = Open();
        NativeBattleMeltEye158Resources source = game.ReadNativeMeltEye158Resources();
        NativeBattleMeltEye158Resources restored = JsonSerializer.Deserialize<NativeBattleMeltEye158Resources>(JsonSerializer.Serialize(source))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(restored));
        Assert.Same(restored.SpriteA, restored.Sprite(NativeBattleMeltEye158Profile.SpriteAAddress));
        Assert.Same(restored.MeshB, restored.Mesh(NativeBattleMeltEye158Profile.MeshBAddress));
        Assert.Throws<NotSupportedException>(() => restored.Sprite(NativeBattleMeltEye158Profile.MeshAAddress));
        Assert.Throws<NotSupportedException>(() => restored.Mesh(NativeBattleMeltEye158Profile.CameraAddress));
        Assert.Throws<InvalidDataException>(() => (restored with { SpriteC = restored.SpriteC with { Bytes = restored.SpriteC.Bytes.Take(0xD8).ToImmutableArray() } }).Validate());
    }

    private static void AssertMesh(NativeBattleStaticMeshResource mesh, int vertices, int[] groups)
    {
        ReadOnlySpan<byte> bytes = mesh.Bytes.AsSpan();
        Assert.Equal(vertices, BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(4)));
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(cursor));
            Assert.Equal(groups[group], count);
            cursor += 4 + count * strides[group];
        }
        Assert.Equal(bytes.Length, cursor);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Melt-Eye resources.");
        return new(directory!);
    }
}
