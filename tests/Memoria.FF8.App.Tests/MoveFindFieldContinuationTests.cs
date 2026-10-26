using System.Text.Json;
using System.Text.Json.Nodes;
using System.Numerics;
using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class MoveFindFieldContinuationTests
{
    [Fact]
    public void RevealedHiddenPointProducesVisiblePixelsFromStandaloneState()
    {
        string source = TestResources.FixturePath("App.Tests.MoveFindFieldContinuationTests.1");
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        Assert.True(session.CharacterVitals!.HasActivePassiveAbility(79));
        Assert.Equal(0, world.Events.Scene.DrawPointDisplayParameter);
        Assert.Equal((ushort)1, world.Events.Scene.NativeDrawPointDisplayMask);
        Assert.NotNull(world.Events.DrawPointParticleFrame);
        byte[] visible = world.Render(TimeSpan.Zero).ToArray();
        RevealRecorder recorder = new();
        world.Render(TimeSpan.Zero, triangleRenderer: recorder);
        Assert.Contains(recorder.Materials, material => material.Texture is null && material.Blend == TriangleBlend.Add && material.DepthComparison == TriangleDepthComparison.Always && !material.DepthWrite);
        FieldAssets component = new(100, "draw-point-reveal-component", [], [], [], new FieldCamera(Vector3.UnitX, -Vector3.UnitZ, Vector3.UnitY, new Vector3(0, 0, 1024), Vector2.Zero, 512), new FieldWalkmesh([]), []);
        FieldRenderer renderer = new(component) { TriangleRenderer = recorder };
        NativeDrawPointParticles particles = world.Events.DrawPointParticleFrame!;
        renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, drawPointParticles: particles, drawPointParticleTables: world.Events.DrawPointParticleTables, drawPointDisplayMask: 1);
        Assert.NotEmpty(recorder.Materials);
        Assert.All(recorder.Materials, material => Assert.Equal(TriangleDepthComparison.Less, material.DepthComparison));
        NativeDrawPointParticles flight = particles with { Drawings = particles.Drawings.Select(spark => spark with { RenderStatus = 2 }).ToImmutableArray() };
        renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, drawPointParticles: flight, drawPointParticleTables: world.Events.DrawPointParticleTables, drawPointDisplayMask: 0, revealHiddenDrawPoint: true);
        Assert.NotEmpty(recorder.Materials);
        Assert.All(recorder.Materials, material => Assert.Equal(TriangleDepthComparison.Less, material.DepthComparison));
        JsonElement saved = world.Events.Capture();
        JsonNode hidden = JsonNode.Parse(saved.GetRawText())!;
        hidden["Scene"]!["NativeDrawPointDisplayMask"] = 0;
        world.Events.Restore(JsonSerializer.SerializeToElement(hidden));
        byte[] plain = world.Render(TimeSpan.Zero).ToArray();
        world.Events.Restore(saved);
        Assert.True(JsonElement.DeepEquals(saved, world.Events.Capture()));
        int changed = Enumerable.Range(0, visible.Length / 4).Count(index => !visible.AsSpan(index * 4, 4).SequenceEqual(plain.AsSpan(index * 4, 4)));
        string? output = Environment.GetEnvironmentVariable("MEMORIA_DRAWPOINT_PARTICLE_EVIDENCE");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(output);
            SaveImage(Path.Combine(output, "revealed.png"), visible);
            SaveImage(Path.Combine(output, "hidden.png"), plain);
            File.WriteAllText(Path.Combine(output, "pixels.txt"), $"ChangedPixels={changed}; Field={world.Events.FieldId}; Frame={world.Events.Frame}");
        }
        Assert.True(changed > 4, $"Revealed point produced only {changed} changed pixels.");
    }

    private sealed class RevealRecorder : ITriangleRenderer
    {
        public List<TriangleMaterial> Materials { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Materials.Clear();
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Materials.Add(material);
        }

        public void End()
        {
        }
    }

    private static void SaveImage(string path, byte[] rgba)
    {
        using SkiaSharp.SKBitmap bitmap = new(320, 240);
        for (int y = 0; y < 240; y++)
        {
            for (int x = 0; x < 320; x++)
            {
                int offset = (y * 320 + x) * 4;
                bitmap.SetPixel(x, y, SkiaSharp.SKColor.FromArgb(rgba[offset + 3], rgba[offset], rgba[offset + 1], rgba[offset + 2]));
            }
        }
        bitmap.SavePng(path);
    }
}
