using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleBlaster186Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBlaster186ResourcesTests
{
    [Fact]
    public void Original_meshes_sprites_vertex_only_shapes_tables_camera_and_sound_are_complete_and_cached()
    {
        using GameAssets game = Open();
        NativeBattleBlaster186Resources resources = game.ReadNativeBlaster186Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeBlaster186Resources());
        Assert.Equal(new[] { 0x808, 0xAB0, 0x5F0 }, new[] { resources.MeshA.Bytes.Length, resources.MeshB.Bytes.Length, resources.MeshC.Bytes.Length });
        Assert.Equal(new[] { 0x74, 0x1AC, 0x1AC, 0x144 }, new[] { resources.SpriteA.Bytes.Length, resources.SpriteB.Bytes.Length, resources.SpriteC.Bytes.Length, resources.SpriteD.Bytes.Length });
        Assert.Equal(4, resources.SourceShapes.Length);
        Assert.All(resources.SourceShapes, shape => Assert.Equal(VertexFrameBytes, shape.Bytes.Length));
        Assert.Equal(SourceAAddress - 8, resources.SourceShapes[0].HeaderAddress);
        Assert.Equal((uint)0x338, resources.SourceShapes[0].PolygonOffset);
        Assert.Equal((ushort)103, BinaryPrimitives.ReadUInt16LittleEndian(resources.SourceShapes[0].Bytes.AsSpan().Slice(6)));
        Assert.Equal(20, resources.VertexPointers.Length);
        Assert.Equal(MutableFrameAddresses[14], resources.VertexPointers[18]);
        Assert.Equal(8, resources.Colors.Length);
        Assert.Equal(-1, resources.Timings[^1]);
        Assert.Equal(new[] { 0, 0, -4096 }, resources.Forward.ToArray());
        Assert.Equal(0x2FC, resources.Camera.Data.Length);
        Assert.Equal(0x61B44u, resources.Sound);
        Assert.Same(resources.MeshA, resources.ReadMesh(MeshAAddress));
        Assert.Same(resources.SpriteD, resources.ReadSprite(SpriteDAddress));
        Assert.Equal(resources.SourceShapes[0].Bytes.ToArray(), resources.ReadSourceVertices(SourceAAddress).Read(0, VertexFrameBytes));
        Assert.Throws<NotSupportedException>(() => resources.ReadSourceVertices(SourceAAddress).Read(VertexFrameBytes, 1));
        Assert.Throws<NotSupportedException>(() => resources.ReadSourceVertices(MutableFrameAddresses[0]));
        Assert.Throws<NotSupportedException>(() => resources.ReadMesh(SourceAAddress - 8));
    }

    [Fact]
    public void Original_resource_JSON_preserves_source_headers_and_all_selected_addresses()
    {
        using GameAssets game = Open();
        NativeBattleBlaster186Resources resources = game.ReadNativeBlaster186Resources();
        NativeBattleBlaster186Resources restored = JsonSerializer.Deserialize<NativeBattleBlaster186Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Changed_face_shape_header_source_extent_matrix_gap_pointer_tables_and_sound_fail_fast()
    {
        using GameAssets game = Open();
        NativeBattleBlaster186Resources resources = game.ReadNativeBlaster186Resources();
        byte[] mesh = resources.MeshA.Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(8 + VertexCount * 8 + 16 + 4), 204);
        Assert.Throws<InvalidDataException>(() => (resources with { MeshA = resources.MeshA with { Bytes = mesh.ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SourceShapes = resources.SourceShapes.SetItem(0, resources.SourceShapes[0] with { PolygonOffset = 0 }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SourceShapes = resources.SourceShapes.SetItem(0, resources.SourceShapes[0] with { Bytes = resources.SourceShapes[0].Bytes.RemoveAt(0) }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { VertexPointers = resources.VertexPointers.SetItem(18, MatrixAddress) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Colors = resources.Colors.SetItem(0, 1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Timings = resources.Timings.RemoveAt(6) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SpriteA = resources.SpriteA with { Address = SpriteBAddress } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sound = 0 }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Blaster resources.");
        return new(directory!);
    }
}
