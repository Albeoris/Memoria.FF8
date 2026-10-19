using System.Numerics;
using System.Reflection;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Rendering;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class WorldTerrainMeshTests
{
    private const int Size = 96;
    private const float Focal = 64;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Segment_culling_rejects_boxes_wholly_behind_or_beside_the_camera(bool nativeCurvature)
    {
        WorldTerrainRenderer renderer = new(null!, Size, Size);
        Set(renderer, "view", Matrix4x4.Identity);
        Set(renderer, "focal", Focal);
        Set(renderer, "far", 5000f);
        Set(renderer, "nativeTerrain", nativeCurvature);
        MethodInfo visible = typeof(WorldTerrainRenderer).GetMethod("TerrainVisible", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.False((bool)visible.Invoke(renderer, [new Vector3(-10, -10, 10), new Vector3(10, 10, 30)])!);
        Assert.False((bool)visible.Invoke(renderer, [new Vector3(1000, -10, -100), new Vector3(1100, 10, -50)])!);
        Assert.True((bool)visible.Invoke(renderer, [new Vector3(-10, -10, -100), new Vector3(10, 10, -50)])!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Segment_culling_preserves_visible_samples_across_camera_orbits_and_horizon(bool nativeCurvature)
    {
        WorldTerrainRenderer renderer = new(null!, Size, Size);
        Set(renderer, "focal", Focal);
        Set(renderer, "far", 5000f);
        Set(renderer, "nativeTerrain", nativeCurvature);
        MethodInfo visible = typeof(WorldTerrainRenderer).GetMethod("TerrainVisible", BindingFlags.Instance | BindingFlags.NonPublic)!;
        int visibleSamples = 0;
        int rejected = 0;
        foreach (float heading in new[] { 0f, 0.7f, 1.8f, 3.5f })
        {
            foreach (float pitch in new[] { -0.25f, 0f, 0.35f })
            {
                Matrix4x4 view = Matrix4x4.CreateRotationY(heading) * Matrix4x4.CreateRotationX(pitch);
                Set(renderer, "view", view);
                Matrix4x4.Invert(view, out Matrix4x4 inverse);
                foreach (float distance in new[] { -200f, 1f, 600f, 1303.75f, 1304f, 1450f, 2400f, 4000f, 5001f })
                {
                    foreach (float height in new[] { -200f, 0f, 300f, 1900f })
                    {
                        Vector3 center = Vector3.Transform(new Vector3(0, height, -distance), inverse);
                        Vector3 minimum = center - new Vector3(220, 150, 220);
                        Vector3 maximum = center + new Vector3(220, 150, 220);
                        bool accepted = (bool)visible.Invoke(renderer, [minimum, maximum])!;
                        if (!accepted)
                        {
                            rejected++;
                        }
                        for (int x = 0; x <= 4; x++)
                        {
                            for (int y = 0; y <= 4; y++)
                            {
                                for (int z = 0; z <= 4; z++)
                                {
                                    Vector3 point = minimum + (maximum - minimum) * new Vector3(x / 4f, y / 4f, z / 4f);
                                    Vector3 camera = TerrainCamera(point, new WorldTerrainView(view, Vector3.Zero, Focal, nativeCurvature));
                                    float depth = -camera.Z;
                                    if (depth >= 1 && depth <= 5000 && MathF.Abs(camera.X * Focal) <= Size / 2f * depth && MathF.Abs(camera.Y * Focal) <= Size / 2f * depth)
                                    {
                                        visibleSamples++;
                                        Assert.True(accepted, $"Visible point {point} was culled at heading {heading}, pitch {pitch}, distance {distance}.");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        Assert.True(visibleSamples > 100);
        Assert.True(rejected > 0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Retained_terrain_matches_cpu_projection_clipping_curvature_and_fog(int scene)
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device);
        (WorldTerrainMesh mesh, WorldTerrainView view, float far) = Scene(scene);
        PixelImage texture = new(2, 2, [new Rgba32(230, 80, 30, 255), new Rgba32(30, 220, 80, 255), new Rgba32(40, 70, 240, 255), new Rgba32(240, 210, 50, 255)]);
        TriangleMaterial material = Material(texture, far);
        renderer.Begin(Size, Size, new Vector4(0, 0, 0, 1), 0);
        DrawReference(renderer, mesh, view, material);
        renderer.End();
        Color[] expected = GpuTestDevice.Read(renderer.Target);
        Assert.True(expected.Count(pixel => pixel != Color.Black) > 30, "The reference terrain must cover a meaningful image area.");
        renderer.Begin(Size, Size, new Vector4(0, 0, 0, 1), 0);
        renderer.DrawTerrain(mesh, view, material);
        renderer.End();
        Assert.Equal(1, renderer.FrameTerrainUploads);
        AssertImages(expected, GpuTestDevice.Read(renderer.Target), renderer, $"scene-{scene}-first");
        renderer.Begin(Size, Size, new Vector4(0, 0, 0, 1), 0);
        renderer.DrawTerrain(mesh, view, material);
        renderer.End();
        Assert.Equal(0, renderer.FrameTerrainUploads);
        AssertImages(expected, GpuTestDevice.Read(renderer.Target), renderer, $"scene-{scene}-reused");
    }

    [Fact]
    public void Retained_and_streamed_draws_preserve_submission_order_and_material_changes()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device);
        (WorldTerrainMesh mesh, WorldTerrainView view, float far) = Scene(0);
        PixelImage texture = new(1, 1, [new Rgba32(220, 80, 40, 255)]);
        TriangleMaterial terrain = Material(texture, far);
        Color[] Render(bool retained)
        {
            renderer.Begin(Size, Size, new Vector4(0, 0, 0, 1), 0);
            Overlay(renderer, new Vector4(0, 1, 0, 1));
            if (retained)
            {
                renderer.DrawTerrain(mesh, view, terrain);
            }
            else
            {
                DrawReference(renderer, mesh, view, terrain);
            }
            Overlay(renderer, new Vector4(0, 0, 1, 0.5f));
            if (retained)
            {
                renderer.DrawTerrain(mesh, view, terrain with { Blend = TriangleBlend.Alpha, AlphaMultiplier = 0.5f, DepthWrite = false });
            }
            else
            {
                DrawReference(renderer, mesh, view, terrain with { Blend = TriangleBlend.Alpha, AlphaMultiplier = 0.5f, DepthWrite = false });
            }
            Overlay(renderer, new Vector4(1, 1, 1, 0.25f));
            renderer.End();
            return GpuTestDevice.Read(renderer.Target);
        }
        Color[] expected = Render(false);
        Color[] actual = Render(true);
        Assert.Equal(1, renderer.FrameTerrainUploads);
        AssertImages(expected, actual, renderer, "interleaved");
    }

    [Fact]
    public void Terrain_ranges_select_triangles_and_share_one_retained_buffer()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device);
        WorldTerrainVertex[] vertices =
        [
            new(new Vector3(-6, -3, -10), Vector2.Zero), new(new Vector3(-1, -3, -10), Vector2.UnitX), new(new Vector3(-4, 3, -10), Vector2.UnitY),
            new(new Vector3(1, -3, -10), Vector2.Zero), new(new Vector3(6, -3, -10), Vector2.UnitX), new(new Vector3(4, 3, -10), Vector2.UnitY)
        ];
        WorldTerrainMesh mesh = new(vertices);
        WorldTerrainView view = new(Matrix4x4.Identity, Vector3.Zero, Focal, false);
        TriangleMaterial material = Material(new PixelImage(1, 1, [new Rgba32(220, 80, 40, 255)]), 100);
        Color[]? first = null;
        foreach (int start in new[] { 0, 3 })
        {
            renderer.Begin(Size, Size, new Vector4(0, 0, 0, 1), 0);
            DrawReference(renderer, new WorldTerrainMesh(vertices.AsSpan(start, 3).ToArray()), view, material);
            renderer.End();
            Color[] expected = GpuTestDevice.Read(renderer.Target);
            Assert.True(expected.Count(pixel => pixel != Color.Black) > 100);
            renderer.Begin(Size, Size, new Vector4(0, 0, 0, 1), 0);
            renderer.DrawTerrain(mesh, view, material, start, start == 0 ? 3 : -1);
            renderer.End();
            Color[] actual = GpuTestDevice.Read(renderer.Target);
            Assert.Equal(start == 0 ? 1 : 0, renderer.FrameTerrainUploads);
            AssertImages(expected, actual, renderer, $"range-{start}");
            if (first is not null)
            {
                Assert.False(first.SequenceEqual(actual));
            }
            first = actual;
        }
        renderer.Begin(Size, Size, new Vector4(0, 0, 0, 1), 0);
        foreach ((int start, int count) in new[] { (-3, 3), (1, 3), (9, 0), (0, -2), (0, 1), (3, 6), (0, int.MaxValue) })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => renderer.DrawTerrain(mesh, view, material, start, count));
        }
        renderer.DrawTerrain(mesh, view, material, 6);
        renderer.DrawTerrain(mesh, view, material, 0, 0);
        renderer.End();
        Assert.Equal(0, renderer.FrameTerrainUploads);
        Assert.Equal(0, renderer.FrameDrawCalls);
        Assert.All(GpuTestDevice.Read(renderer.Target), pixel => Assert.Equal(Color.Black, pixel));
    }

    private static (WorldTerrainMesh Mesh, WorldTerrainView View, float Far) Scene(int scene)
    {
        Vector3[] points;
        Matrix4x4 view = Matrix4x4.Identity;
        Vector3 offset = Vector3.Zero;
        bool native = scene is 1 or 2;
        float far;
        switch (scene)
        {
            case 0:
                points = [new(-0.4f, -0.3f, -0.5f), new(1.5f, -0.8f, -3), new(-0.8f, 1.5f, -2)];
                far = 4;
                break;
            case 1:
                points = [new(-700, -250, -1250), new(700, -250, -1500), new(0, 600, -1500)];
                far = 1800;
                break;
            case 2:
                points = [new(-1800, 280, -4000), new(1800, 280, -4000), new(0, 1800, -4000)];
                view = Matrix4x4.CreateTranslation(0, -1500, 0);
                far = 4500;
                break;
            case 3:
                points = [new(-10700, -500, -1000), new(-9300, -500, -1600), new(-10000, 700, -1300)];
                offset = new Vector3(10000, 0, 0);
                view = Matrix4x4.CreateRotationY(0.12f) * Matrix4x4.CreateRotationX(-0.08f);
                far = 2000;
                break;
            case 4:
                points = [new(-0.4f, -0.3f, 0.5f), new(1.5f, -0.8f, -3), new(-0.8f, 1.5f, -2)];
                far = 4;
                break;
            default:
                points = [new(-0.4f, -0.3f, -0.000001f), new(1.5f, -0.8f, -3), new(-0.8f, 1.5f, -2)];
                far = 4;
                break;
        }
        WorldTerrainMesh mesh = new([new(points[0], Vector2.Zero), new(points[1], Vector2.UnitX), new(points[2], Vector2.UnitY)]);
        return (mesh, new WorldTerrainView(view, offset, Focal, native), far);
    }

    private static TriangleMaterial Material(PixelImage texture, float far)
    {
        return new TriangleMaterial(texture, DepthComparison: TriangleDepthComparison.Greater, Sampler: TriangleSampler.PointClamp)
        {
            DepthMapping = TriangleDepthMapping.Positive,
            DepthScale = 1,
            MinimumReciprocalW = 1 / far,
            FogColor = new Vector3(0.2f, 0.3f, 0.5f),
            FogStart = far * 0.6f,
            FogEnd = far
        };
    }

    private static Vector3 TerrainCamera(Vector3 source, WorldTerrainView view)
    {
        Vector3 position = source + view.Offset;
        if (view.NativeCurvature)
        {
            int depth = (int)MathF.Round(-Vector3.Transform(position, view.View).Z * 16);
            int distance = (Math.Clamp(depth, 0, 65535) >> 2) - 5216;
            if (distance >= 0)
            {
                int height = -(int)(source.Y * 16);
                int curved = (distance >> 6) * (distance >> 6) + (((4096 - distance) * height) >> 12);
                position.Y = -unchecked((short)curved) / 16f;
            }
        }
        return Vector3.Transform(position, view.View);
    }

    private static void DrawReference(ITriangleRenderer renderer, WorldTerrainMesh mesh, WorldTerrainView view, TriangleMaterial material)
    {
        ReadOnlySpan<WorldTerrainVertex> vertices = mesh.Vertices.Span;
        for (int index = 0; index < vertices.Length; index += 3)
        {
            List<(Vector3 Camera, Vector2 Uv)> polygon = [];
            for (int corner = 0; corner < 3; corner++)
            {
                WorldTerrainVertex vertex = vertices[index + corner];
                polygon.Add((TerrainCamera(vertex.Position, view), vertex.Uv));
            }
            List<(Vector3 Camera, Vector2 Uv)> clipped = [];
            var previous = polygon[^1];
            foreach (var current in polygon)
            {
                bool inside = current.Camera.Z <= -1;
                if (inside != (previous.Camera.Z <= -1))
                {
                    float t = (-1 - previous.Camera.Z) / (current.Camera.Z - previous.Camera.Z);
                    clipped.Add((Vector3.Lerp(previous.Camera, current.Camera, t), Vector2.Lerp(previous.Uv, current.Uv, t)));
                }
                if (inside)
                {
                    clipped.Add(current);
                }
                previous = current;
            }
            TriangleVertex Project((Vector3 Camera, Vector2 Uv) vertex)
            {
                float reciprocal = -1 / vertex.Camera.Z;
                return new TriangleVertex(new Vector2(Size / 2f + vertex.Camera.X * reciprocal * view.Focal, Size / 2f - vertex.Camera.Y * reciprocal * view.Focal), reciprocal, reciprocal, vertex.Uv, Vector4.One);
            }
            for (int corner = 1; corner + 1 < clipped.Count; corner++)
            {
                renderer.Draw(Project(clipped[0]), Project(clipped[corner]), Project(clipped[corner + 1]), material);
            }
        }
    }

    private static void Overlay(ITriangleRenderer renderer, Vector4 color)
    {
        TriangleMaterial material = new(null, TriangleBlend.Alpha, TriangleDepthComparison.Always, false);
        renderer.Draw(new(Vector2.Zero, 0, 1, Vector2.Zero, color), new(new Vector2(Size, 0), 0, 1, Vector2.Zero, color), new(new Vector2(0, Size), 0, 1, Vector2.Zero, color), material);
    }

    private static void AssertImages(Color[] expected, Color[] actual, GpuTriangleRenderer renderer, string name)
    {
        long total = 0;
        int different = 0;
        for (int index = 0; index < expected.Length; index++)
        {
            int error = Math.Max(Math.Abs(expected[index].R - actual[index].R), Math.Max(Math.Abs(expected[index].G - actual[index].G), Math.Abs(expected[index].B - actual[index].B)));
            total += error;
            if (error > 3)
            {
                different++;
            }
        }
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_WORLD_PERFORMANCE_OUTPUT");
        if (output is not null && (total / (double)expected.Length > 1 || different > expected.Length / 100))
        {
            Directory.CreateDirectory(output);
            using Microsoft.Xna.Framework.Graphics.Texture2D image = new(renderer.Target.GraphicsDevice, Size, Size);
            image.SetData(expected);
            using (FileStream file = File.Create(Path.Combine(output, $"terrain-{name}-reference.png")))
            {
                image.SaveAsPng(file, Size, Size);
            }
            image.SetData(actual);
            using (FileStream file = File.Create(Path.Combine(output, $"terrain-{name}-retained.png")))
            {
                image.SaveAsPng(file, Size, Size);
            }
        }
        Assert.InRange(total / (double)expected.Length, 0, 1);
        Assert.InRange(different, 0, expected.Length / 100);
    }

    private static void Set(WorldTerrainRenderer renderer, string name, object value)
    {
        typeof(WorldTerrainRenderer).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(renderer, value);
    }
}
