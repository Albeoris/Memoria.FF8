using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class SceneColorGradeTests
{
    [Fact]
    public void HorizontalAndVerticalStripsFollowRgbAxesAndPreserveAlpha()
    {
        ColorLookupTable horizontal = ColorLookupTable.FromStrip(MakeStrip(false));
        ColorLookupTable vertical = ColorLookupTable.FromStrip(MakeStrip(true));
        ColorLookupTable identity = ColorLookupTable.FromStrip(MakeStrip(false, true));
        Assert.False(horizontal.IsIdentity);
        Assert.True(identity.IsIdentity);
        Vector3 color = new(0.1f, 0.25f, 0.8f);
        Vector3 expected = new(0.8f, 0.9f, 0.25f);
        Assert.True(Vector3.Distance(expected, horizontal.Apply(color)) < 0.0001f);
        Assert.Equal(horizontal.Apply(color), vertical.Apply(color));
        Assert.Equal(Vector3.UnitY, horizontal.Apply(Vector3.Zero));
        Assert.Equal(new Vector3(1, 0, 1), horizontal.Apply(Vector3.One));
        Assert.Equal(74, new SceneColorGrade(horizontal, null, 0, 0.4f).Apply(new Rgba32(44, 76, 110, 74)).A);
        Assert.Throws<InvalidDataException>(() => ColorLookupTable.FromStrip(new PixelImage(4, 4)));
    }

    [Fact]
    public void HardwareGradesSceneDrawAndLeavesUiDrawIntact()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device);
        SceneColorGrade grade = new(ColorLookupTable.FromStrip(MakeStrip(false)), null, 0.35f, 0.85f) { LinearTint = new(0.9f, 0.5f, 1) };
        Vector4 input = new(0.2f, 0.58f, 0.95f, 1);
        renderer.Begin(16, 8, Vector4.Zero);
        TrianglePrimitives.Quad(renderer, new(0, 0), new(8, 8), 0.5f, input, new TriangleMaterial(null) { ColorGrade = grade }, Vector2.One);
        TrianglePrimitives.Quad(renderer, new(8, 0), new(8, 8), 0.5f, input, new TriangleMaterial(null), Vector2.One);
        renderer.End();
        Color[] pixels = GpuTestDevice.Read(renderer.Target);
        Vector3 ungraded = new(input.X, input.Y, input.Z);
        Match(pixels[3 * 16 + 3], grade.Apply(ungraded));
        Match(pixels[3 * 16 + 11], ungraded);

        static void Match(Color pixel, Vector3 expected)
        {
            Assert.InRange(pixel.R, (int)MathF.Round(expected.X * 255) - 1, (int)MathF.Round(expected.X * 255) + 1);
            Assert.InRange(pixel.G, (int)MathF.Round(expected.Y * 255) - 1, (int)MathF.Round(expected.Y * 255) + 1);
            Assert.InRange(pixel.B, (int)MathF.Round(expected.Z * 255) - 1, (int)MathF.Round(expected.Z * 255) + 1);
        }
    }

    [Fact]
    public void PaintedTileFiltersInsideItsOwnTextureAndKeepsTransparentColorsOut()
    {
        PixelImage tile = new(2, 1, [new Rgba32(230, 27, 0, 255), new Rgba32(0, 0, 255, 0)]);
        Assert.Equal(new Rgba32(230, 27, 0, 128), FieldRenderer.SamplePreparedLayer(tile, new Vector2(0.5f, 0.5f)));
        Assert.Equal(default, FieldRenderer.SamplePreparedLayer(tile, new Vector2(0.99f, 0.5f)));
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device);
        renderer.Begin(9, 3, new Vector4(0, 1, 0, 1));
        TrianglePrimitives.Quad(renderer, Vector2.Zero, new(9, 3), 0.5f, Vector4.One, new TriangleMaterial(tile, Sampler: TriangleSampler.AlphaWeightedLinearClamp), Vector2.One);
        renderer.End();
        Color[] pixels = GpuTestDevice.Read(renderer.Target);
        Assert.Equal(new Color(230, 27, 0), pixels[13]);
        Assert.Equal(Color.Lime, pixels[17]);
    }

    [Fact]
    public void ResolvedSunUsesNativeArcAndDarkensBySurfaceDirection()
    {
        SceneLightingProfile profile = new()
        {
            ClockEnabled = true,
            SceneLightsEnabled = true,
            World = new SceneLightLook { Sun = 0.8f, SunAzimuth = 0, SunHeight = 90 }
        };
        SceneDirectionalLighting noon = SceneDirectionalLighting.Resolve(profile.ResolveScene(false, -1, 0, default, true, 240));
        Assert.Equal(0.8f, noon.Strength, 4);
        Assert.Equal(1, noon.Multiplier(-Vector3.UnitY), 4);
        Assert.Equal(0.6f, noon.Multiplier(Vector3.UnitX), 4);
        Assert.Equal(0.2f, noon.Multiplier(Vector3.UnitY), 4);
        Assert.Equal(0, SceneDirectionalLighting.Resolve(profile.ResolveScene(false, -1, 0, default, true, 0)).Strength, 4);
    }

    [Fact]
    public void OptionalExportSelectsEveryPhaseAndLunarSet()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_DEMASTER_DATA_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(Path.Combine(directory, "lut")), "Set the optional Demaster fixture directory.");
        ModFile[] files = Directory.GetFiles(Path.Combine(directory!, "lut"), "*.png")
            .Select(path => new ModFile("Demaster/Fixture", ModFormat.Demaster, "lut/" + Path.GetFileName(path), 1, () => File.ReadAllBytes(path)))
            .ToArray();
        List<ModDiagnostic> diagnostics = [];
        SceneColorGradeMods mods = SceneColorGradeMods.Load(new ModCatalogSnapshot(1, [], files), diagnostics);
        Assert.Empty(diagnostics);
        Assert.Single(mods.PackageIds);
        var profile = new SceneLightingProfile { GradeEnabled = true, ClockEnabled = true };
        Vector3 original = new(0.233f, 0.372f, 0.731f);
        Vector3[] grades = new[] { 0d, 200, 480, 600 }.Select(time =>
        {
            SceneLightingState state = profile.ResolveFieldState(new(SceneTime.Cycle, true), 0, time);
            return mods.Resolve("field/unused", state).Apply(original);
        }).ToArray();
        Assert.Equal(original, grades[1]);
        Assert.Equal(4, grades.Distinct().Count());
        SceneLightingState lunar = profile.ResolveFieldState(new(SceneTime.Cycle, true), 0, 200) with { LunarGrade = true };
        Assert.NotEqual(original, mods.Resolve("world", lunar).Apply(original));
    }

    private static PixelImage MakeStrip(bool vertical, bool identity = false)
    {
        PixelImage image = new(vertical ? 2 : 4, vertical ? 4 : 2);
        for (int b = 0; b < 2; b++)
        {
            for (int g = 0; g < 2; g++)
            {
                for (int r = 0; r < 2; r++)
                {
                    int x = vertical ? r : r + b * 2;
                    int y = vertical ? g + b * 2 : g;
                    image[x, y] = identity ? new((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), 255)
                        : new((byte)(b * 255), (byte)((1 - r) * 255), (byte)(g * 255), 255);
                }
            }
        }
        return image;
    }
}
