using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleForeignPlaneDrawingTests
{
    private sealed class Material : INativeBattleForeignMaterial
    {
    }

    [Fact]
    public void Clipped_host_body_retains_material_and_interpolates_uv_and_color_at_the_native_intersections()
    {
        NativeBattleForeignModelGeometry geometry = Geometry();
        NativeBattleGfGeometryBatch result = Draw(geometry);
        NativeBattleGeometryPrimitive primitive = Assert.Single(result.Attempts).Primitive!;
        NativeBattlePilotVisualGeometry pilot = primitive.PilotGeometry!;
        Assert.Equal(4, pilot.CornerCount);
        Assert.Equal(40, result.AllocationBytes);
        Assert.Equal(new Vector3(-100, -100, 4000), pilot.A);
        Assert.Equal(new Vector3(100, -100, 4000), pilot.B);
        Assert.Equal(new Vector3(-50, 0, 4000), pilot.C);
        Assert.Equal(new Vector3(50, 0, 4000), pilot.D);
        NativeBattleForeignSurface surface = pilot.ForeignSurface!;
        Assert.Same(geometry.Faces[0].Surface.Material, surface.Material);
        Vector2[] uv = [new(0.1f, 0.2f), new(0.9f, 0.2f), new(0.3f, 0.5f), new(0.7f, 0.5f)];
        for (int corner = 0; corner < 4; corner++)
        {
            Assert.InRange(Vector2.Distance(uv[corner], surface.Uv[corner]), 0, 0.000001f);
        }
        Assert.Equal(new Vector4(0.5f, 0, 0.5f, 1), surface.Colors[2]);
        Assert.Equal(new Vector4(0, 0.5f, 0.5f, 1), surface.Colors[3]);
    }

    [Fact]
    public void Host_clipping_reuses_native_selected_topology_packet_depths_and_write_masks()
    {
        NativeBattleForeignModelGeometry geometry = Geometry();
        NativeBattleCameraProjection projection = Projection();
        NativeBattleShivaPlane plane = new(0, -4096, 0, 0);
        NativeBattleForeignModelFace face = geometry.Faces[0];
        ImmutableArray<NativeBattleShivaClipVertex> triangle = new[] { face.A, face.B, face.C }.Select(point => new NativeBattleShivaClipVertex(point, 0)).ToImmutableArray();
        ImmutableArray<NativeBattleShivaClipVertex> clipped = NativeBattleShivaPlaneClipping.Clip5C34B0(triangle, plane, Selectors());
        NativeBattleGfGeometryAttempt expected = NativeBattleShivaPlaneClipping.Emit5C3410(clipped, projection.View!, projection, 0x808080, 0, 0, 341, 256, 0, 0);
        NativeBattleGfGeometryAttempt actual = Assert.Single(Draw(geometry).Attempts);
        Assert.Equal(expected.Data, actual.Data);
        Assert.Equal(expected.WriteMask, actual.WriteMask);
        Assert.Equal(expected.Primitive!.Words, actual.Primitive!.Words);
        Assert.Equal(expected.Primitive.VertexPayload4, actual.Primitive.VertexPayload4);
        Assert.Equal(expected.Primitive.Bucket, actual.Primitive.Bucket);
    }

    [Fact]
    public void Host_clipping_observes_the_current_world_transform_and_native_backface_rejection()
    {
        NativeBattleForeignModelGeometry geometry = Geometry();
        NativeBattleGfGeometryBatch rejected = Draw(geometry with { World = geometry.World with { Y = 300 } });
        Assert.Empty(rejected.Attempts);
        NativeBattleForeignModelFace face = geometry.Faces[0];
        Assert.Empty(Draw(geometry with { Faces = [face with { B = face.C, C = face.B }] }).Attempts);
        NativeBattleGfGeometryBatch shifted = Draw(geometry with { World = geometry.World with { X = 25 } });
        NativeBattlePilotVisualGeometry pilot = Assert.Single(shifted.Attempts).Primitive!.PilotGeometry!;
        Assert.Equal(new Vector3(-75, -100, 4000), pilot.A);
        Assert.Equal(new Vector3(-25, 0, 4000), pilot.C);
    }

    private static NativeBattleGfGeometryBatch Draw(NativeBattleForeignModelGeometry geometry)
    {
        return NativeBattleForeignModelDrawing.ClipPlane(geometry, Projection(), new(0, -4096, 0, 0), Selectors(), 0x808080, 341, 256, 0, 0, new(165, 1, 2, 3, 4));
    }

    private static NativeBattleCameraProjection Projection() => new(NativeBattleForeignModelGeometry.Identity, 256, 160, 108);

    private static NativeBattleForeignModelGeometry Geometry()
    {
        NativeBattleForeignSurface surface = new(new Material(), [new(0.1f, 0.2f), new(0.9f, 0.2f), new(0.5f, 0.8f)], [new(1, 0, 0, 1), new(0, 1, 0, 1), new(0, 0, 1, 1)]);
        return new(NativeBattleForeignModelGeometry.Identity, new(default, default, 0, new(default, default)), [new(new(-100, -100, 4000), new(100, -100, 4000), new(0, 100, 4000), surface)]);
    }

    private static ImmutableArray<int> Selectors() => [0, 1, 2, 0, 0, 1, 2, 0, 0, 3, 2, 3, 3, 1, 2, 0, 0, 1, 4, 4, 0, 4, 2, 0, 3, 1, 4, 4, 0, 3, 2, 4, 5, 1, 2, 5, 0, 1, 5, 0, 0, 3, 5, 3, 5, 3, 2, 1, 5, 4, 2, 5, 0, 1, 5, 4];
}
