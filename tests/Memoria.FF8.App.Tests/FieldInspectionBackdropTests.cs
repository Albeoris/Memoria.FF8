using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class FieldInspectionBackdropTests
{
    [Fact]
    public void Inspection_backdrop_reprojects_the_clipped_tile_and_retains_both_alpha_passes()
    {
        FieldCamera native = new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new(20, -30, 25), new(10, -6), 100);
        BackgroundTile image = new(-168, -8, 100, 255, 0, 0, MapBlendMode.Add, true, true, new PixelImage(16, 16));
        FieldAssets field = new(0, "inspection-backdrop", [], [], [image], native, new FieldWalkmesh([]), []);
        RecordingRenderer commands = new();
        FieldRenderer renderer = new(field) { TriangleRenderer = commands };

        renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        Assert.Equal(4, commands.Triangles.Count);
        TriangleVertex nativeRight = commands.Triangles[0].B;
        Assert.False(commands.Triangles[0].Material.PerspectiveDepth);

        renderer.InspectionCamera = native;
        renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        Assert.Empty(commands.Triangles);

        renderer.InspectionBackdropEnabled = true;
        renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        Assert.Equal(4, commands.Triangles.Count);
        var opaque = commands.Triangles[0];
        var translucent = commands.Triangles[2];
        Assert.InRange(MathF.Abs(opaque.A.Position.X), 0, 0.001f);
        Assert.InRange(Vector2.Distance(nativeRight.Position, opaque.B.Position), 0, 0.001f);
        Assert.True(opaque.A.Uv.X > 0);
        Assert.True(opaque.Material.PerspectiveDepth);
        Assert.True(opaque.Material.DepthWrite);
        Assert.Equal(0.75f, opaque.Material.MinimumAlpha);
        Assert.False(translucent.Material.DepthWrite);
        Assert.Equal(TriangleBlend.Add, translucent.Material.Blend);
        Assert.Equal(0.75f, translucent.Material.MaximumAlpha);

        Matrix4x4 rotation = Matrix4x4.CreateRotationY(0.05f);
        renderer.InspectionCamera = native with { AxisX = Vector3.TransformNormal(native.AxisX, rotation), AxisZ = Vector3.TransformNormal(native.AxisZ, rotation) };
        renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        Assert.Equal(4, commands.Triangles.Count);
        Assert.NotEqual(opaque.A.Position, commands.Triangles[0].A.Position);
        Assert.NotEqual(commands.Triangles[0].A.ReciprocalW, commands.Triangles[0].B.ReciprocalW);

        renderer.InspectionCamera = native with { Translation = new Vector3(20, -30, -450) };
        renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        Assert.Empty(commands.Triangles);
    }

    private sealed class RecordingRenderer : ITriangleRenderer
    {
        internal List<(TriangleVertex A, TriangleVertex B, TriangleMaterial Material)> Triangles { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Triangles.Clear();
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Triangles.Add((a, b, material));
        }

        public void End()
        {
        }
    }
}
