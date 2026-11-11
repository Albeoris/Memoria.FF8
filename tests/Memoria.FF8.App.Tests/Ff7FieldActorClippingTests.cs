using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7FieldActorClippingTests
{
    [Theory]
    [InlineData(640, 240, false)]
    [InlineData(640, 240, true)]
    [InlineData(641, 361, true)]
    public void ReplacementModelsAreClippedPerPixelAtBothBackgroundEdges(int width, int height, bool whiteFade)
    {
        PcFieldModelDescriptor descriptor = new("actor", 0, "actor.hrc", "512", Enumerable.Repeat(new PcFieldLight(0, Vector3.Zero), 3).ToArray(), 0, ["pose"]);
        Ff7FieldModelResources model = new("actor.hrc", null!, [], new Dictionary<string, PcFieldAnimation>());
        CheckPixels(descriptor, model, new TriangleAppearance(), width, height, whiteFade);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalModelsAreClippedPerPixelAtBothBackgroundEdges(bool whiteFade)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR for original field model clipping.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        Ff7FieldResources original = Ff7FieldResources.Load(installation, "md1stin");
        PcFieldModelDescriptor descriptor = original.Models.Models[0];
        using LgpArchive characters = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        Ff7FieldModelResources model = Ff7FieldModelResources.Load(characters, descriptor);
        CheckPixels(descriptor, model, null, 640, 240, whiteFade);
    }

    private static void CheckPixels(PcFieldModelDescriptor descriptor, Ff7FieldModelResources model, IFf7ActorAppearance? appearance, int width, int height, bool whiteFade)
    {
        PcFieldCamera camera = new(Vector3.UnitX * 4096, Vector3.UnitY * 4096, Vector3.UnitZ * 4096, 0, new Vector3(0, 0, 1000), 0, 100);
        PcFieldTriggers bounds = new("clipping", 0, 0, -160, -120, 160, 120, [], [], [], []);
        Ff7FieldResources field = new("clipping", null!, null!, new PcFieldModels(0, 512, [descriptor]), [camera], [], bounds, new(0, 0, 1, [[0]]), new(320, 240, [], [], []));
        Dictionary<int, Ff7FieldModelResources> models = new() { [0] = model };
        Ff7FieldRenderer clipped = new(field, models, new(50, 32000), appearance);
        Ff7FieldRenderer exposed = new(field with { Triggers = bounds with { CameraLeft = -480, CameraRight = 480 } }, models, new(50, 32000), appearance);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        Ff7FieldRenderFrame empty = new(0, Vector2.Zero, [], new ushort[256]) { QuadColorArgb = whiteFade ? 0x80FFFFFF : null };
        Color[] baseline = Draw(clipped, empty);
        Color[] controlBaseline = Draw(exposed, empty);
        float scale = height / 240f;
        float left = (width - 320 * scale) / 2;
        float right = left + 320 * scale;
        foreach (int worldX in new[] { -2200, -1600, 0, 1600, 2200 })
        {
            Ff7FieldRenderFrame frame = empty with { Actors = [new(0, 0, worldX * 4096, 0, 0, 0, 0, 0)] };
            Color[] actual = Draw(clipped, frame);
            Color[] control = Draw(exposed, frame);
            int changedInside = 0;
            int changedOutsideControl = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    bool inside = x + 0.5f >= left && x + 0.5f < right && y + 0.5f >= height * 8f / 240 && y + 0.5f < height * 232f / 240;
                    if (inside)
                    {
                        changedInside += actual[index] != baseline[index] ? 1 : 0;
                    }
                    else
                    {
                        Assert.Equal(Color.Black, actual[index]);
                        changedOutsideControl += control[index] != controlBaseline[index] ? 1 : 0;
                    }
                }
            }
            if (worldX == 0 || appearance is not null && Math.Abs(worldX) == 1600)
            {
                Assert.True(changedInside > 0, $"Actor at {worldX} must remain visible inside the image.");
            }
            if (Math.Abs(worldX) == 2200)
            {
                Assert.Equal(0, changedInside);
                Assert.True(changedOutsideControl > 0, $"Control must prove the actor at {worldX} would draw outside the background without clipping.");
            }
        }

        Color[] Draw(Ff7FieldRenderer renderer, Ff7FieldRenderFrame frame)
        {
            renderer.Render(gpu, width, height, frame);
            return GpuTestDevice.Read(gpu.Target);
        }
    }

    private sealed class TriangleAppearance : IFf7ActorAppearance
    {
        public bool TryCreate(Ff7ActorVisual actor, PcFieldModelDescriptor descriptor, Ff7FieldModelResources original, out IReadOnlyList<Ff7AppearanceTriangle> triangles)
        {
            Vector3 center = new(actor.X4096 / 4096f, actor.Y4096 / 4096f, actor.Z4096 / 4096f);
            Vector4 color = new(1, 0, 0, 1);
            triangles = [new(new(center + new Vector3(-100, -100, 0), Vector2.Zero, color), new(center + new Vector3(100, -100, 0), Vector2.Zero, color), new(center + new Vector3(0, 100, 0), Vector2.Zero, color), new TriangleMaterial(null))];
            return true;
        }
    }
}
