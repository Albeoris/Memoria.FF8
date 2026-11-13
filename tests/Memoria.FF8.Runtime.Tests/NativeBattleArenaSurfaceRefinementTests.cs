using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleArenaSurfaceRefinementTests
{
    [Fact]
    public void Refine_overlays_actual_control_triangles_on_coarse_floor_and_preserves_outside_area_and_materials()
    {
        NativeBattleForeignSurface material = new(new TestMaterial(), [new(0, 0), new(1, 0), new(0, 1)], [new(0, 0, 0, 1), new(1, 0, 0, 1), new(0, 0, 1, 1)]);
        NativeBattleArenaFace original = new(new(4, 3, 2), new(-1000, 37, -1000), new(1000, 37, -1000), new(-1000, 37, 1000), material);
        NativeBattleArenaGeometry geometry = new([original]);
        Vector3 a = new(-100, 37, -100);
        Vector3 b = new(100, 37, -100);
        Vector3 c = new(100, 37, 100);
        Vector3 d = new(-100, 37, 100);
        Vector3 center = new(0, 37, 0);

        NativeBattleArenaGeometry refined = NativeBattleArenaSurfaceQueries.Refine(geometry, [(a, b, center), (b, c, center), (c, d, center), (d, a, center)]);

        Assert.True(refined.Faces.Length > geometry.Faces.Length);
        Assert.True(Math.Abs(Area(original) - refined.Faces.Sum(Area)) < 0.1);
        Assert.Contains(refined.Faces.SelectMany(face => new[] { face.A, face.B, face.C }), point => point == center);
        Assert.Contains(refined.Faces.SelectMany(face => new[] { face.A, face.B, face.C }), point => point == original.A);
        Assert.Contains(refined.Faces.SelectMany(face => new[] { face.A, face.B, face.C }), point => point == original.B);
        Assert.Contains(refined.Faces.SelectMany(face => new[] { face.A, face.B, face.C }), point => point == original.C);
        foreach (NativeBattleArenaFace face in refined.Faces)
        {
            Assert.Equal(original.Id, face.Id);
            Assert.Same(material.Material, face.Surface.Material);
            Vector3[] positions = [face.A, face.B, face.C];
            for (int corner = 0; corner < positions.Length; corner++)
            {
                Vector3 point = positions[corner];
                Assert.True(float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z));
                Assert.Equal(37, point.Y);
                Vector2 expectedUv = new((point.X + 1000) / 2000, (point.Z + 1000) / 2000);
                Assert.True(Vector2.Distance(expectedUv, face.Surface.Uv[corner]) < 0.00001f);
                Assert.True(Vector4.Distance(new(expectedUv.X, 0, expectedUv.Y, 1), face.Surface.Colors[corner]) < 0.00001f);
            }
        }
        Assert.Equal([original], geometry.Faces);
    }

    [Fact]
    public void Refine_keeps_original_winding_and_clips_control_mesh_at_material_seams()
    {
        NativeBattleForeignSurface firstMaterial = new(new TestMaterial(), [Vector2.Zero, Vector2.UnitX, Vector2.UnitY], [Vector4.One, Vector4.One, Vector4.One]);
        NativeBattleForeignSurface secondMaterial = firstMaterial with { Material = new TestMaterial() };
        NativeBattleArenaFace first = new(new(1, 0, 0), new(-100, 0, -100), new(-100, 0, 100), new(100, 0, -100), firstMaterial);
        NativeBattleArenaFace second = new(new(2, 0, 0), new(100, 0, -100), new(-100, 0, 100), new(100, 0, 100), secondMaterial);
        NativeBattleArenaGeometry refined = NativeBattleArenaSurfaceQueries.Refine(new([first, second]), [(new(-20, 0, -20), new(20, 0, -20), new(0, 0, 20))]);

        Assert.True(Math.Abs(40000 - refined.Faces.Sum(Area)) < 0.01);
        Assert.All(refined.Faces, face =>
        {
            Assert.True(Cross(face) < 0);
            Assert.Same(face.Id == first.Id ? firstMaterial.Material : secondMaterial.Material, face.Surface.Material);
        });
    }

    [Fact]
    public void Refine_rejects_missing_or_degenerate_control_contracts()
    {
        NativeBattleForeignSurface material = new(new TestMaterial(), [Vector2.Zero, Vector2.UnitX, Vector2.UnitY], [Vector4.One, Vector4.One, Vector4.One]);
        NativeBattleArenaGeometry geometry = new([new(new(0, 0, 0), new(0, 0, 0), new(100, 0, 0), new(0, 0, 100), material)]);

        Assert.Throws<NotSupportedException>(() => NativeBattleArenaSurfaceQueries.Refine(geometry, []));
        Assert.Throws<InvalidDataException>(() => NativeBattleArenaSurfaceQueries.Refine(geometry, [(Vector3.Zero, Vector3.One, Vector3.One)]));
    }

    private static double Cross(NativeBattleArenaFace face) => ((double)face.B.X - face.A.X) * ((double)face.C.Z - face.A.Z) - ((double)face.B.Z - face.A.Z) * ((double)face.C.X - face.A.X);
    private static double Area(NativeBattleArenaFace face) => Math.Abs(Cross(face)) / 2;

    private sealed record TestMaterial : INativeBattleForeignMaterial;
}
