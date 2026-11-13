using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleArenaSurfaceQueriesTests
{
    private static readonly NativeBattleForeignSurface Surface = new(new TestMaterial(), [Vector2.Zero, Vector2.UnitX, Vector2.UnitY], [Vector4.One, Vector4.One, Vector4.One]);

    [Fact]
    public void Select_uses_connected_geometry_across_parts_and_preserves_original_materials()
    {
        NativeBattleArenaFace wall = Face(0, new(0, 0, 0), new(0, 100, 0), new(100, 0, 0));
        NativeBattleArenaFace first = Face(7, new(0, 17, 0), new(100, 17, 0), new(0, 17, 100));
        NativeBattleArenaFace next = Face(3, new(100, 17, 0), new(100, 17, 100), new(0, 17, 100));
        NativeBattleArenaFace disconnected = Face(5, new(200, 17, 0), new(300, 17, 0), new(200, 17, 100));
        NativeBattleArenaGeometry geometry = new([wall, first, next, disconnected]);

        NativeBattleArenaGeometry selected = NativeBattleArenaSurfaceQueries.Select(geometry, new(25, 0, 25));

        Assert.Equal([first, next], selected.Faces);
        Assert.Same(Surface, selected.Faces[0].Surface);
        Assert.Equal([wall, first, next, disconnected], geometry.Faces);
    }

    [Fact]
    public void Select_distinguishes_disconnected_raised_ledges_from_the_base()
    {
        NativeBattleArenaFace bottom = Face(0, new(0, 0, 0), new(100, 0, 0), new(0, 0, 100));
        NativeBattleArenaFace top = Face(1, new(0, -100, 0), new(100, -100, 0), new(0, -100, 100));
        NativeBattleArenaGeometry geometry = new([bottom, top]);

        Assert.Equal([bottom], NativeBattleArenaSurfaceQueries.Select(geometry, new(25, 0, 25)).Faces);
        Assert.Equal([top], NativeBattleArenaSurfaceQueries.Select(geometry, new(25, -80, 25)).Faces);
        Assert.Throws<NotSupportedException>(() => NativeBattleArenaSurfaceQueries.Select(geometry, new(25, -50, 25)));
    }

    [Fact]
    public void Select_connects_partial_collinear_edges_but_does_not_cross_a_single_vertex()
    {
        NativeBattleArenaFace first = Face(0, new(0, 0, 0), new(100, 0, 0), new(0, 0, 100));
        NativeBattleArenaFace seam = Face(1, new(20, 0, 80), new(80, 0, 20), new(100, 0, 100));
        NativeBattleArenaFace pointOnly = Face(2, new(0, 0, 0), new(-100, 0, 0), new(0, 0, -100));

        NativeBattleArenaGeometry selected = NativeBattleArenaSurfaceQueries.Select(new([first, seam, pointOnly]), new(10, 0, 10));

        Assert.Equal([first, seam], selected.Faces);
    }

    [Fact]
    public void Select_rejects_disconnected_overlapping_sheets_at_the_same_height()
    {
        NativeBattleArenaGeometry geometry = new([
            Face(0, new(0, 0, 0), new(100, 0, 0), new(0, 0, 100)),
            Face(1, new(-10, 0, -10), new(90, 0, -10), new(-10, 0, 90))]);

        Assert.Throws<NotSupportedException>(() => NativeBattleArenaSurfaceQueries.Select(geometry, new(25, 0, 25)));
    }

    [Fact]
    public void Select_rejects_sloped_nearest_surface_instead_of_guessing_a_floor()
    {
        NativeBattleArenaGeometry geometry = new([
            Face(0, new(0, 0, 0), new(100, -100, 0), new(0, 0, 100)),
            Face(1, new(0, 100, 0), new(100, 100, 0), new(0, 100, 100))]);

        Assert.Throws<NotSupportedException>(() => NativeBattleArenaSurfaceQueries.Select(geometry, new(25, -25, 25)));
        Assert.Throws<NotSupportedException>(() => NativeBattleArenaSurfaceQueries.Select(geometry, new(200, 0, 200)));
    }

    [Fact]
    public void Select_preserves_large_source_coordinates_and_rejects_duplicate_primitive_identity()
    {
        NativeBattleArenaFace face = Face(0, new(-100000, 0, -100000), new(100000, 0, -100000), new(0, 0, 100000));
        Assert.Equal([face], NativeBattleArenaSurfaceQueries.Select(new([face]), new(0, 0, 0)).Faces);
        Assert.Throws<InvalidDataException>(() => NativeBattleArenaSurfaceQueries.Select(new([face, face]), new(0, 0, 0)));
    }

    private static NativeBattleArenaFace Face(int part, Vector3 a, Vector3 b, Vector3 c) => new(new(part, 0, 0), a, b, c, Surface);

    private sealed record TestMaterial : INativeBattleForeignMaterial;
}
