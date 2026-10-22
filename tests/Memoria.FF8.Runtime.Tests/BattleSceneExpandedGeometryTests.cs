using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class BattleSceneExpandedGeometryTests
{
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static readonly NativeBattleCameraProjection Projection = new(null, 1000, 160, 108);
    private static readonly NativeBattleGeometryViewport ExpandedViewport = new(short.MinValue / 8, short.MinValue / 8, ushort.MaxValue / 8, ushort.MaxValue / 8);

    [Theory]
    [InlineData(-200, 0)]
    [InlineData(200, 0)]
    [InlineData(0, -160)]
    [InlineData(0, 160)]
    public void Expanded_scene_submits_each_native_offscreen_edge_without_mutating_native_scratch_or_geometry(int x, int y)
    {
        NativeBattleRawMesh mesh = Mesh(x, y);
        NativeBattleRawVertex[] originalVertices = mesh.Vertices.ToArray();
        byte[] originalPolygon = mesh.Polygons[0].Bytes.ToArray();
        NativeBattleProjectedVertex[]? nativeScratch = null;
        NativeBattleProjectedVertex[]? expandedScratch = null;
        Assert.Empty(Emit(mesh, publish: vertices => nativeScratch = vertices.ToArray()));
        NativeBattleGeometryPrimitive primitive = Assert.Single(Emit(mesh, ExpandedViewport, vertices => expandedScratch = vertices.ToArray()));
        Assert.Equal(nativeScratch, expandedScratch);
        Assert.All(expandedScratch!, vertex => Assert.NotEqual(0u, vertex.DepthAndClip & 0x3F00000));
        Assert.Equal(expandedScratch![0].Screen, primitive.Words[2]);
        Assert.Equal(expandedScratch[1].Screen, primitive.Words[4]);
        Assert.Equal(expandedScratch[2].Screen, primitive.Words[6]);
        Assert.Equal(originalVertices, mesh.Vertices.ToArray());
        Assert.Equal(originalPolygon, mesh.Polygons[0].Bytes.ToArray());
        Assert.Empty(Emit(mesh));
    }

    [Fact]
    public void Expanded_scene_preserves_native_near_and_backface_rejection()
    {
        Assert.Empty(Emit(Mesh(200, 0, depth: 400), ExpandedViewport));
        Assert.Empty(Emit(Mesh(200, 0, reverse: true), ExpandedViewport));
        Assert.Single(Emit(Mesh(200, 0), ExpandedViewport));
    }

    [Fact]
    public void Expanded_scene_retains_representable_coordinate_bounds()
    {
        Assert.Empty(Emit(Mesh(5000, 0), ExpandedViewport));
        Assert.Empty(Emit(Mesh(-5000, 0), ExpandedViewport));
        Assert.Empty(Emit(Mesh(0, 5000), ExpandedViewport));
        Assert.Empty(Emit(Mesh(0, -5000), ExpandedViewport));
    }

    [Fact]
    public void Expanded_scene_preserves_existing_visible_packet_words_depth_and_bucket()
    {
        NativeBattleRawMesh mesh = Mesh(0, 0);
        NativeBattleGeometryPrimitive native = Assert.Single(Emit(mesh));
        NativeBattleGeometryPrimitive expanded = Assert.Single(Emit(mesh, ExpandedViewport));
        Assert.Equal(native.Words, expanded.Words);
        Assert.Equal(native.VertexPayload4, expanded.VertexPayload4);
        Assert.Equal(native.Bucket, expanded.Bucket);
    }

    private static ImmutableArray<NativeBattleGeometryPrimitive> Emit(NativeBattleRawMesh mesh, NativeBattleGeometryViewport? viewport = null, Action<IReadOnlyList<NativeBattleProjectedVertex>>? publish = null)
    {
        return NativeBattleRawGeometryPackets.Emit5099D0([mesh], [Identity], Projection, 0x808080, 1, null, 17, 4, null, publishProjectedScratch: publish, viewport: viewport);
    }

    private static NativeBattleRawMesh Mesh(int x, int y, short depth = 1000, bool reverse = false)
    {
        byte[] polygon = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(polygon, 0x8000);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(2), reverse ? (ushort)2 : (ushort)1);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(4), reverse ? (ushort)1 : (ushort)2);
        return new([new(new((short)(x - 10), (short)(y - 10), depth), 0), new(new((short)(x + 10), (short)(y - 10), depth), 0), new(new((short)x, (short)(y + 10), depth), 0)], [new(false, polygon.ToImmutableArray())]);
    }
}
