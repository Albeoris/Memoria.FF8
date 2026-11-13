using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class CrossoverCompositeActionRenderingTests(ITestOutputHelper output)
{
    [Fact]
    public void Original_Tonberry_capture_packets_sample_the_actual_host_ground_and_render_at_both_display_sizes()
    {
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR")) && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")), "Select both original game installations.");
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using CrossoverArenaSurfaceCapture capture = new(() => new GpuTriangleRenderer(device.Device));
        bool sampled = false;
        bool rendered = false;
        int capturePackets = 0;
        int matchedTriangles = 0;
        int maximumChangedPixels = 0;
        using CrossoverCompositeActionTests.Fixture fixture = new("Tonberry", 0, 0, arenaCapture: capture);
        fixture.Execute(observe: Observe);
        fixture.FollowWithFire();
        Assert.True(sampled, "The original ground-capture callback must retain a nonempty image of actual host materials.");
        Assert.True(rendered, "The original ordering-table polygons sampling that image must themselves emit visible pixels at both display sizes.");
        Assert.True(capturePackets > 0 && matchedTriangles > 0);
        Assert.InRange(capture.RetainedImageBytes, 1, 128 * 256 * 4);
        capture.Reset();
        Assert.Equal(0, capture.RetainedImages);
        Assert.Equal(0, capture.RetainedImageBytes);
        output.WriteLine($"Tonberry: {capturePackets} native capture polygons, {matchedTriangles} sampled triangles, at most {maximumChangedPixels} visible pixels; 320x240 and 640x360 rendered in memory and following Fire completed.");

        void Observe(Ff7BattleRenderer renderer, Ff7BattleSession host, CrossoverGuestHostActions provider)
        {
            if (rendered || provider.EffectVisual is not { Ordering: { } ordering } visual)
            {
                return;
            }
            NativeBattleArenaCaptureMaterial[] requests = ordering.Packets.Select(packet => packet.PilotGeometry?.ForeignSurface?.Material).OfType<NativeBattleArenaCaptureMaterial>().Distinct().ToArray();
            if (requests.Length == 0)
            {
                return;
            }
            Ff7BattleArenaGeometry arena = renderer.CaptureArenaGeometry();
            HashSet<PixelImage> textures = [];
            foreach (NativeBattleArenaCaptureMaterial request in requests)
            {
                Assert.Equal((128, 256, 384, 256), (request.Width, request.Height, request.NativeX, request.NativeY));
                Assert.Equal((int)request.Projection.FocalLength!.Value, request.Projection.View!.Z);
                Assert.All(request.Geometry.Faces, face => Assert.Contains(arena.Faces, original => original.Id == face.Id && ReferenceEquals(original.Surface.Material, face.Surface.Material)));
                NativeBattleForeignRenderMaterial material = capture.Resolve(request);
                PixelImage image = Assert.IsType<PixelImage>(material.Material.Texture);
                if (!image.Pixels.Any(pixel => pixel.A != 0 && (pixel.R != 0 || pixel.G != 0 || pixel.B != 0)))
                {
                    NativeBattleActionMatrix view = request.Projection.View!;
                    Vector3 Transform(Vector3 point) => new((view.Rotation[0] * point.X + view.Rotation[1] * point.Y + view.Rotation[2] * point.Z) / 4096 + view.X, (view.Rotation[3] * point.X + view.Rotation[4] * point.Y + view.Rotation[5] * point.Z) / 4096 + view.Y, (view.Rotation[6] * point.X + view.Rotation[7] * point.Y + view.Rotation[8] * point.Z) / 4096 + view.Z);
                    Vector3[] points = request.Geometry.Faces.SelectMany(face => new[] { face.A, face.B, face.C }).Select(Transform).ToArray();
                    output.WriteLine($"Capture view [{string.Join(',', view.Rotation)}] + ({view.X},{view.Y},{view.Z}), H {request.Projection.FocalLength}, offset {request.Projection.OffsetX}/{request.Projection.OffsetY}; {request.Geometry.Faces.Length} ground faces; camera bounds {points.Aggregate(Vector3.Min)}..{points.Aggregate(Vector3.Max)}.");
                    foreach (NativeBattleArenaFace face in request.Geometry.Faces.Take(3))
                    {
                        output.WriteLine($"Face {face.Id}: world {face.A}/{face.B}/{face.C}; camera {Transform(face.A)}/{Transform(face.B)}/{Transform(face.C)}; colors {string.Join(',', face.Surface.Colors)}; material {face.Surface.Material}.");
                    }
                }
                Assert.Contains(image.Pixels, pixel => pixel.A != 0 && (pixel.R != 0 || pixel.G != 0 || pixel.B != 0));
                textures.Add(image);
                sampled = true;
            }
            capturePackets += ordering.Packets.Count(packet => packet.PilotGeometry?.ForeignSurface?.Material is NativeBattleArenaCaptureMaterial);
            bool bothSizes = true;
            foreach ((int width, int height) in new[] { (320, 240), (640, 360) })
            {
                gpu.Begin(width, height, new(0, 0, 0, 1), 0);
                gpu.End();
                Microsoft.Xna.Framework.Color[] baseline = GpuTestDevice.Read(gpu.Target);
                gpu.Begin(width, height, new(0, 0, 0, 1), 0);
                CaptureOnlyRenderer sampledGeometry = new(gpu, textures);
                visual.Draw(sampledGeometry, host.GuestRenderCameraOutput, width, height);
                gpu.End();
                Microsoft.Xna.Framework.Color[] drawn = GpuTestDevice.Read(gpu.Target);
                int changed = baseline.Where((pixel, index) => pixel != drawn[index]).Count();
                maximumChangedPixels = Math.Max(maximumChangedPixels, changed);
                matchedTriangles += sampledGeometry.Triangles;
                bothSizes &= sampledGeometry.Triangles > 0 && changed > 16;
                gpu.Begin(width, height, new(0, 0, 0, 1), 0);
                gpu.End();
                Assert.Equal(baseline, GpuTestDevice.Read(gpu.Target));
            }
            rendered = bothSizes;
        }
    }

    private sealed class CaptureOnlyRenderer(ITriangleRenderer target, HashSet<PixelImage> textures) : ITriangleRenderer
    {
        public int Triangles { get; private set; }

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            if (material.Texture is not { } texture || !textures.Contains(texture))
            {
                return;
            }
            Assert.True(float.IsFinite(a.Uv.X) && float.IsFinite(a.Uv.Y));
            Assert.True(float.IsFinite(b.Uv.X) && float.IsFinite(b.Uv.Y));
            Assert.True(float.IsFinite(c.Uv.X) && float.IsFinite(c.Uv.Y));
            Triangles++;
            target.Draw(a, b, c, material with { DepthComparison = material.DepthComparison == TriangleDepthComparison.Always ? TriangleDepthComparison.Always : TriangleDepthComparison.GreaterEqual });
        }

        public void End()
        {
        }
    }
}
