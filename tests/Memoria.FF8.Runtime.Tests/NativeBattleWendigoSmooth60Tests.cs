using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWendigoSmooth60Tests
{
    [Theory]
    [InlineData(41)]
    [InlineData(42)]
    public void Ball_receipt_captures_post_morph_consumed_vertices_and_preserves_native_packets(ushort effect)
    {
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleActionMatrix camera = identity with { Z = 2000 };
        NativeBattleCameraProjection projection = new(identity, 216, 160, 108);
        NativeBattleRawVertex[] raw = [new(new(-100, -100, 0), 0), new(new(100, -100, 0), 0), new(new(-100, 100, 0), 0)];
        NativeBattleVector[] morphed = raw.Select(vertex => NativeBattleWendigoBallGeometry.Morph(vertex, identity, identity, new(0, 0, -100), 2048, new(500, 500, 500))).ToArray();
        Vector3[] world = morphed.Select(vertex => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(camera, vertex), projection)).ToArray();
        ImmutableArray<NativeBattleModuleProjectedVertex> projected = morphed.Select(vertex => NativeBattleModuleProjection.Project45FE10(camera, vertex, projection)).ToImmutableArray();
        ImmutableArray<NativeBattleRawPolygon> polygons = [Polygon(0, 2, 1), Polygon(0x1002, 0x1000, 0x1001)];
        NativeBattleGfGeometryBatch native = NativeBattleWendigoBallGeometry.Packets(polygons, projected, 0x808080);
        NativeBattlePilotVisualIdentity owner = new(effect, 10, 1, 2, 3);
        NativeBattleGfGeometryBatch captured = NativeBattleWendigoBallGeometry.Packets(polygons, projected, 0x808080, world, owner);
        Assert.Equal(native.AllocationBytes, captured.AllocationBytes);
        NativeBattleGfGeometryAttempt actual = Assert.Single(captured.Attempts);
        NativeBattleGfGeometryAttempt expected = Assert.Single(native.Attempts);
        Assert.Equal(expected.ByteOffsetRelative, actual.ByteOffsetRelative);
        Assert.Equal(expected.Data, actual.Data);
        Assert.Equal(expected.WriteMask, actual.WriteMask);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(actual.Primitive);
        Assert.Equal(expected.Primitive!.Words, primitive.Words);
        Assert.Equal(expected.Primitive.VertexPayload4, primitive.VertexPayload4);
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        Assert.Equal(owner with { Primitive = unchecked(owner.Primitive * 65537 + 1) }, geometry.Identity);
        Assert.Equal(world[2], geometry.A);
        Assert.Equal(world[0], geometry.B);
        Assert.Equal(world[1], geometry.C);
        Vector3 retained = geometry.A;
        world[2] = default;
        Assert.Equal(retained, geometry.A);
        NativeBattleGfGeometryBatch linked = NativeBattleWendigoBallGeometry.Packets(polygons, projected, 0x808080, world, owner with { Primitive = 0x10003 });
        Assert.NotEqual(geometry.Identity, Assert.Single(linked.Attempts).Primitive!.PilotGeometry!.Identity);
    }

    private static NativeBattleRawPolygon Polygon(ushort a, ushort b, ushort c)
    {
        byte[] bytes = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, a);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), b);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), c);
        return new(false, bytes.ToImmutableArray());
    }
}
