using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class FieldLightingGpuTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void Empty_active_scene_is_black_and_unlit_background_is_unchanged(int samples)
    {
        using GpuTestDevice device = new(samples);
        using GpuTriangleRenderer renderer = new(device.Device);
        renderer.Begin(64, 64, new Vector4(0.25f, 0.5f, 0.75f, 1));
        renderer.SetFieldLighting(FieldLightingScene.Empty("scene_a"));
        DrawPlane(renderer, 16, 48, 0, new TriangleMaterial(null) { FieldLighting = FieldLightingPass.Model });
        renderer.End();
        Color[] pixels = GpuTestDevice.Read(renderer.Target);
        Assert.Equal(Color.Black, pixels[32 * 64 + 32]);
        Assert.Equal(new Color(0.25f, 0.5f, 0.75f, 1), pixels[4 * 64 + 4]);
    }

    [Fact]
    public void Actual_spot_position_aim_range_and_independent_colors_control_pixels()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device);
        FieldSpotLight lamp = Lamp();
        Color Sample(params FieldSpotLight[] lights)
        {
            renderer.Begin(64, 64, Vector4.One);
            renderer.SetFieldLighting(new FieldLightingScene { FieldName = "scene_a", Lights = lights });
            DrawPlane(renderer, 0, 64, 0, new TriangleMaterial(null) { FieldLighting = FieldLightingPass.Model });
            renderer.End();
            return GpuTestDevice.Read(renderer.Target)[32 * 64 + 32];
        }
        Color near = Sample(lamp);
        Color far = Sample(lamp with { Position = new Vector3(0, 0, 20) });
        Assert.True(near.R > far.R);
        Assert.Equal(Color.Black, Sample(lamp with { Direction = Vector3.UnitZ }));
        Assert.Equal(Color.Black, Sample(lamp with { Range = 5 }));
        Assert.Equal(Color.Black, Sample(lamp with { Enabled = false }));
        Color red = Sample(lamp with { Color = Vector3.UnitX });
        Color combined = Sample(lamp with { Color = Vector3.UnitX }, lamp with { Id = "blue", Position = new Vector3(5, 0, 10), Color = Vector3.UnitZ });
        Assert.True(red.R > 0 && red.G == 0 && red.B == 0);
        Assert.True(combined.R > 0 && combined.B > 0 && combined.G == 0);
    }

    [Fact]
    public void Perspective_primary_map_unions_casters_and_obeys_receiver_ordering()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device);
        PixelImage white = new(1, 1, [new Rgba32(255, 255, 255, 255)]);
        FieldSpotLight lamp = Lamp();
        FieldLightingScene scene = new() { FieldName = "scene_a", Lights = [lamp], PrimaryLightId = lamp.Id, GroundShadow = new() { Opacity = 1, Softness = 0 } };
        TriangleVertex Vertex(float x, float y) => new(Vector2.Zero, 0, 1, Vector2.Zero, Vector4.One) { WorldPosition = new(x, y, 5) };
        FieldShadowTriangle first = new(Vertex(-1, -1), Vertex(1, -1), Vertex(-1, 1), new(white));
        FieldShadowTriangle second = new(Vertex(1, -1), Vertex(1, 1), Vertex(-1, 1), new(white));
        Color[] Render(bool duplicate, bool foreground)
        {
            renderer.Begin(64, 64, Vector4.One);
            renderer.SetFieldLighting(scene);
            Assert.True(renderer.PrepareFieldShadow(duplicate ? [first, second, first, second] : [first, second], out string? reason), reason);
            if (foreground)
            {
                DrawPlane(renderer, 0, 64, 0, new TriangleMaterial(null), 0.1f);
            }
            DrawPlane(renderer, 0, 64, 0, new TriangleMaterial(null, TriangleBlend.Alpha, DepthWrite: false) { FieldLighting = FieldLightingPass.GroundShadow }, 0.5f);
            renderer.End();
            return GpuTestDevice.Read(renderer.Target);
        }
        Color[] once = Render(false, false);
        Color[] twice = Render(true, false);
        Assert.InRange((int)once[32 * 64 + 32].R, 1, 254);
        Assert.Equal(once, twice);
        Assert.Equal(Color.White, once[4 * 64 + 4]);
        Assert.Equal(Color.White, Render(false, true)[32 * 64 + 32]);
        scene = scene with { Lights = [lamp with { Position = new Vector3(0, 0, -10), Direction = Vector3.UnitZ }] };
        Assert.Equal(Color.White, Render(false, false)[32 * 64 + 32]);
        scene = scene with { Lights = [lamp with { Position = new Vector3(4, 0, 10) }] };
        Color[] moved = Render(false, false);
        Assert.Equal(Color.White, moved[32 * 64 + 32]);
        Assert.True(moved[32 * 64 + 16].R < 255);
    }

    [Fact]
    public void Material_and_wrap_previews_are_local_to_authored_model_shading()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device);
        FieldSpotLight lamp = Lamp();
        FieldLightingScene scene = new() { FieldName = "scene_a", Lights = [lamp] };
        (Color Model, Color Background) Sample(bool gray, bool wrap, Vector3 normal, FieldSpotLight activeLamp)
        {
            renderer.PreviewGrayFieldMaterial = gray;
            renderer.PreviewWrappedFieldDiffuse = wrap;
            renderer.Begin(64, 64, new Vector4(0.25f, 0.5f, 0.75f, 1));
            renderer.SetFieldLighting(scene with { Lights = [activeLamp] });
            TriangleVertex Vertex(float x, float y) => new(new Vector2(x, y), 0.5f, 1, Vector2.Zero, new Vector4(1, 0, 0, 1)) { WorldPosition = Vector3.Zero, WorldNormal = normal };
            renderer.Draw(Vertex(8, 8), Vertex(56, 8), Vertex(8, 56), new TriangleMaterial(null) { FieldLighting = FieldLightingPass.Model });
            renderer.End();
            Color[] pixels = GpuTestDevice.Read(renderer.Target);
            return (pixels[16 * 64 + 16], pixels[4 * 64 + 4]);
        }

        var nativeDiffuse = Sample(false, false, Vector3.UnitZ, lamp);
        var grayMaterial = Sample(true, false, Vector3.UnitZ, lamp);
        Assert.True(nativeDiffuse.Model.R > 0 && nativeDiffuse.Model.G == 0);
        Assert.True(grayMaterial.Model.R == grayMaterial.Model.G && grayMaterial.Model.G == grayMaterial.Model.B && grayMaterial.Model.R > 0);
        Assert.Equal(nativeDiffuse.Background, grayMaterial.Background);
        Assert.Equal(Color.Black, Sample(false, false, Vector3.UnitX, lamp).Model);
        Assert.True(Sample(false, true, Vector3.UnitX, lamp).Model.R > 0);
        Assert.Equal(Color.Black, Sample(false, true, Vector3.UnitX, lamp with { Direction = Vector3.UnitZ }).Model);
        Assert.Equal(nativeDiffuse, Sample(false, false, Vector3.UnitZ, lamp));
    }

    private static FieldSpotLight Lamp() => new() { Id = "lamp", Name = "Lamp", Position = new(0, 0, 10), Range = 30 };

    [Theory]
    [InlineData(0f)]
    [InlineData(0.8f)]
    public void Primary_does_not_occlude_its_own_lamp_facing_surface(float slope)
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device);
        PixelImage white = new(1, 1, [new Rgba32(255, 255, 255, 255)]);
        FieldSpotLight lamp = Lamp();
        FieldLightingScene scene = new() { FieldName = "scene_a", Lights = [lamp], PrimaryLightId = lamp.Id };
        Vector3 normal = Vector3.Normalize(new Vector3(-slope, 0, 1));
        TriangleVertex Vertex(float x, float y) => new(new Vector2(x * 4 + 32, y * 4 + 32), 0.5f, 1, Vector2.Zero, Vector4.One) { WorldPosition = new(x, y, 5 + slope * x), WorldNormal = normal };
        TriangleVertex a = Vertex(-4, -4);
        TriangleVertex b = Vertex(4, -4);
        TriangleVertex c = Vertex(-4, 4);
        TriangleVertex d = Vertex(4, 4);
        Color Render(bool shadow)
        {
            renderer.Begin(64, 64, Vector4.One);
            renderer.SetFieldLighting(scene);
            if (shadow)
            {
                Assert.True(renderer.PrepareFieldShadow([new(a, b, c, new(white)), new(b, d, c, new(white))], out string? reason), reason);
            }
            TriangleMaterial material = new(null) { FieldLighting = FieldLightingPass.Model };
            renderer.Draw(a, b, c, material);
            renderer.Draw(b, d, c, material);
            renderer.End();
            return GpuTestDevice.Read(renderer.Target)[32 * 64 + 32];
        }
        Color unshadowed = Render(false);
        Color shadowed = Render(true);
        Assert.InRange((int)shadowed.R, unshadowed.R - 3, unshadowed.R + 3);
    }

    [Fact]
    public void Per_pixel_world_position_and_normal_use_perspective_interpolation()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device);
        FieldLightingScene scene = new() { FieldName = "scene_a", Lights = [Lamp()] };
        renderer.Begin(64, 64, Vector4.One);
        renderer.SetFieldLighting(scene);
        TriangleVertex a = new(Vector2.Zero, 0.5f, 1, Vector2.Zero, Vector4.One) { WorldPosition = Vector3.Zero, WorldNormal = Vector3.UnitZ };
        TriangleVertex b = new(new Vector2(64, 0), 0.5f, 0.1f, Vector2.Zero, Vector4.One) { WorldPosition = new(20, 0, 0), WorldNormal = Vector3.UnitX };
        TriangleVertex c = new(new Vector2(0, 64), 0.5f, 0.1f, Vector2.Zero, Vector4.One) { WorldPosition = new(0, 20, 0), WorldNormal = Vector3.UnitY };
        renderer.Draw(a, b, c, new TriangleMaterial(null) { FieldLighting = FieldLightingPass.Model });
        renderer.End();
        float weightB = 16.5f / 64;
        float weightC = weightB;
        float weightA = 1 - weightB - weightC;
        float reciprocal = weightA + (weightB + weightC) * 0.1f;
        Vector3 position = (a.WorldPosition * weightA + b.WorldPosition * (weightB * 0.1f) + c.WorldPosition * (weightC * 0.1f)) / reciprocal;
        Vector3 normal = (a.WorldNormal * weightA + b.WorldNormal * (weightB * 0.1f) + c.WorldNormal * (weightC * 0.1f)) / reciprocal;
        int expected = (int)MathF.Round(FieldLightingMath.LinearToSrgb(FieldLightingMath.Illumination(scene, position, normal).X) * 255);
        Color pixel = GpuTestDevice.Read(renderer.Target)[16 * 64 + 16];
        Assert.InRange((int)pixel.R, expected - 1, expected + 1);
    }

    private static void DrawPlane(GpuTriangleRenderer renderer, float start, float end, float z, TriangleMaterial material, float depth = 0.5f)
    {
        TriangleVertex Vertex(float x, float y) => new(new Vector2(x, y), depth, 1, Vector2.Zero, Vector4.One) { WorldPosition = new((x - 32) / 4, (y - 32) / 4, z), WorldNormal = Vector3.UnitZ };
        renderer.Draw(Vertex(start, start), Vertex(end, start), Vertex(start, end), material);
        renderer.Draw(Vertex(end, start), Vertex(end, end), Vertex(start, end), material);
    }
}
