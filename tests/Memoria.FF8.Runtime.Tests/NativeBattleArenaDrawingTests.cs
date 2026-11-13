using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleArenaDrawingTests
{
    private sealed class Material : INativeBattleForeignMaterial
    {
    }

    [Fact]
    public void Capture_retains_distant_original_geometry_without_narrowing_its_coordinates()
    {
        NativeBattleArenaGeometry geometry = Geometry(new(100000, 0, -80000));
        NativeBattleArenaHost host = new(geometry);
        NativeBattlePilotVisualIdentity owner = new(1, 2, 3, 4, 5);
        NativeBattleArenaCaptureMaterial capture = host.Capture(owner, geometry, Projection(), 256, 256);
        Assert.Same(geometry, capture.Geometry);
        Assert.Same(capture, host.ReadCapture(owner));
        Assert.Equal(100000, capture.Geometry.Faces[0].A.X);
    }

    [Fact]
    public void Replacement_is_owned_by_task_generation_and_restores_original_visibility_on_release()
    {
        NativeBattleArenaGeometry geometry = Geometry(Vector3.Zero);
        NativeBattleArenaHost host = new(geometry);
        NativeBattlePilotVisualIdentity first = new(1, 2, 3, 4, 5);
        NativeBattlePilotVisualIdentity later = first with { Generation = 6 };
        host.Replace(first, geometry);
        Assert.Equal(first, Assert.Single(host.Replacements).Owner);
        Assert.Throws<NotSupportedException>(() => host.Replace(later, geometry));
        host.Release(later);
        Assert.Single(host.Replacements);
        host.Release(first);
        Assert.Empty(host.Replacements);
        host.Replace(later, geometry);
        Assert.Equal(later, Assert.Single(host.Replacements).Owner);
        Assert.Throws<InvalidDataException>(() => host.Replace(first, geometry with { Faces = [geometry.Faces[0] with { Id = new(9, 9, 9) }] }));
    }

    [Fact]
    public void Capture_sampling_uses_native_texture_coordinates_and_retained_world_geometry()
    {
        NativeBattleArenaCaptureMaterial material = new(Geometry(Vector3.Zero), Projection(), 256, 256);
        NativeBattlePilotVisualGeometry geometry = new(new(1, 2, 3, 4, 5), new(1, 2, 3), new(4, 5, 6), new(7, 8, 9), default, 3);
        NativeBattleGeometryPrimitive primitive = new(17, [0, 0x34808080, 0, 0xABCD2010, 0x808080, 0, 0x01004030, 0x808080, 0, 0x6050], [1, 2, 3, 0]) { PilotGeometry = geometry };
        NativeBattleGfGeometryBatch batch = new([new(0, new byte[40].ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 40).ToImmutableArray(), primitive)], 40);
        NativeBattleGeometryPrimitive bound = Assert.Single(NativeBattleArenaDrawing.BindCapture(batch, material).Attempts).Primitive!;
        Assert.Equal(primitive.Words, bound.Words);
        Assert.Equal(geometry.A, bound.PilotGeometry!.A);
        NativeBattleForeignSurface surface = bound.PilotGeometry.ForeignSurface!;
        Assert.Same(material, surface.Material);
        Assert.Equal(new Vector2(16 / 256f, 32 / 256f), surface.Uv[0]);
        Assert.Equal(new Vector2(48 / 256f, 64 / 256f), surface.Uv[1]);
        Assert.Equal(new Vector2(80 / 256f, 96 / 256f), surface.Uv[2]);
    }

    [Theory]
    [InlineData(0x11A, 64, 128)]
    [InlineData(0x9A, 128, 192)]
    public void Capture_sampling_maps_texture_page_origin_depth_and_subrectangle(int page, int firstU, int secondU)
    {
        NativeBattleArenaCaptureMaterial material = new(Geometry(Vector3.Zero), Projection(), 128, 128) { NativeX = 704, NativeY = 256 };
        NativeBattlePilotVisualGeometry geometry = new(new(1, 2, 3, 4, 5), default, default, default, default, 3);
        NativeBattleGeometryPrimitive primitive = new(17, [0, 0x24808080, 0, (uint)firstU, 0, (uint)(page << 16 | 64 << 8 | secondU), 0, (uint)firstU], [1, 2, 3, 0]) { PilotGeometry = geometry };
        NativeBattleGfGeometryBatch batch = new([new(0, new byte[32].ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 32).ToImmutableArray(), primitive)], 32);
        NativeBattleForeignSurface surface = Assert.Single(NativeBattleArenaDrawing.BindCapture(batch, material).Attempts).Primitive!.PilotGeometry!.ForeignSurface!;
        Assert.Equal(Vector2.Zero, surface.Uv[0]);
        Assert.Equal(new Vector2(page == 0x11A ? 0.5f : 0.25f, 0.5f), surface.Uv[1]);
    }

    [Fact]
    public void Deformation_keeps_actual_material_and_native_output_coordinates()
    {
        NativeBattleArenaGeometry geometry = Geometry(Vector3.Zero);
        NativeBattlePilotVisualIdentity identity = new(1, 2, 3, 4, 5);
        NativeBattleGfGeometryBatch drawn = NativeBattleArenaDrawing.Deform(geometry, point => point with { Y = 123 }, identity, (polygons, vertices, owner) =>
        {
            Assert.Equal(20, Assert.Single(polygons).Bytes.Length);
            Assert.All(vertices, point => Assert.Equal((short)123, point.Y));
            NativeBattleGeometryPrimitive primitive = new(17, [0, 0x24808080], []) { PilotGeometry = new(owner, new(vertices[0].X, vertices[0].Y, vertices[0].Z), default, default, default, 3) };
            return new([new(0, new byte[32].ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 32).ToImmutableArray(), primitive)], 32);
        });
        NativeBattlePilotVisualGeometry result = Assert.Single(drawn.Attempts).Primitive!.PilotGeometry!;
        Assert.Equal(123, result.A.Y);
        Assert.Same(geometry.Faces[0].Surface, result.ForeignSurface);
        Assert.Throws<NotSupportedException>(() => NativeBattleArenaDrawing.Deform(Geometry(new(100000, 0, 0)), point => point, identity, (_, _, _) => throw new InvalidOperationException()));
    }

    private static NativeBattleCameraProjection Projection() => new(NativeBattleForeignModelGeometry.Identity, 320, 160, 108);

    private static NativeBattleArenaGeometry Geometry(Vector3 origin)
    {
        NativeBattleForeignSurface surface = new(new Material(), [Vector2.Zero, Vector2.UnitX, Vector2.UnitY], [Vector4.One, Vector4.One, Vector4.One]);
        return new([new(new(1, 2, 3), origin, origin + Vector3.UnitX, origin + Vector3.UnitZ, surface)]);
    }
}
