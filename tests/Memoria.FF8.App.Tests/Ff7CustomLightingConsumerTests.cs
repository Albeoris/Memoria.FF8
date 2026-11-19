using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7CustomLightingConsumerTests(ITestOutputHelper output)
{
    [Fact]
    public void Pc_matrix_builders_match_asymmetric_native_helper_outputs()
    {
        byte[] colors = [32, 64, 96, 128, 64, 32, 16, 192, 48, 80, 24, 224];
        short[] directions = [4096, 1024, -2048, -512, 4096, 1536, 2048, -1024, 4096];
        Matrix4x4 part = new(0, -1, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);
        Ff7FieldLighting lighting = new(colors, directions, part);
        Assert.Equal(0x7F000000u, Pack(lighting.Shade(Vector3.UnitX, 0x7F80C840)));
        Assert.Equal(0x7F505112u, Pack(lighting.Shade(Vector3.UnitY, 0x7F80C840)));
        Assert.Equal(0x7F383859u, Pack(lighting.Shade(Vector3.UnitZ, 0x7F80C840)));
        Assert.Equal(0x7F4CFF49u, Pack(lighting.Shade(-2 * Vector3.UnitX, 0x7F80C840)));
        Assert.Equal(0x7F070046u, Pack(lighting.Shade(new(.25f, -.5f, .75f), 0x7F80C840)));
    }

    [Fact]
    public void Pc_custom_lighting_rounds_even_and_saturates_after_rgb_modulation()
    {
        byte[] colors = [0, 0, 0, 128, 0, 0, 0, 128, 0, 0, 0, 128];
        short[] directions = [4096, 0, 0, 0, 4096, 0, 0, 0, 4096];
        Ff7FieldLighting half = new(colors, directions, Matrix4x4.Identity);
        Assert.Equal(0x7F020404u, Pack(half.Shade(Vector3.One, 0x7F050709)));
        Ff7FieldLighting bright = new(colors, directions.Select(value => checked((short)(value * 3))).ToArray(), Matrix4x4.Identity);
        Assert.Equal(0x71FFFFFFu, Pack(bright.Shade(Vector3.One, 0x71CCCCCC)));
        Assert.Throws<InvalidDataException>(() => new Ff7FieldLighting([], directions, Matrix4x4.Identity));
        Assert.Throws<InvalidDataException>(() => new Ff7FieldLighting(colors, directions, new(float.NaN, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1)));
    }

    [Fact]
    public void Actual_custom_mesh_keeps_sampled_pose_colors_through_cold_restore_and_cancellation()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_CUSTOM_LIGHT_SAVE");
        Assert.SkipUnless(root is not null && path is not null, "Select the original installation and external custom-light field fixture.");
        byte[] original = File.ReadAllBytes(path!);
        using MemoryStream stream = new(original);
        CombinedSessionSave combined = CombinedSessionCodec.Read(stream);
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using Ff7Session live = Ff7Session.Restore(installation, combined.Ff7!);
        for (int callback = 0; callback < 4 && live.Host.Process.Actors.Single(actor => actor.Model == 9).NativeCustomLighting?.Displayed is null; callback++)
        {
            live.AdvanceTick();
        }
        Ff7Actor source = live.Host.Process.Actors.Single(actor => actor.Model == 9);
        Ff7NativeCustomLightingState state = Assert.IsType<Ff7NativeCustomLightingState>(source.NativeCustomLighting);
        Ff7NativeCustomLightingSample sample = Assert.IsType<Ff7NativeCustomLightingSample>(state.Displayed);
        Ff7NativeCustomLightingPose pose = Assert.IsType<Ff7NativeCustomLightingPose>(sample.SamplingPose);
        Assert.True(state.Pending);
        Assert.Equal(source.Model, pose.ModelIndex);
        Ff7FieldModelResources model = live.Models[source.Model];
        PcFieldModelDescriptor descriptor = live.Field.Models.Models[source.Model];
        Assert.Equal("hjhf.hrc", model.HrcName.ToLowerInvariant());
        Ff7BoneGeometry geometry = Assert.Single(model.Geometry);
        Assert.Equal(203, geometry.Polygon.Vertices.Count);
        Assert.Equal(60, geometry.Polygon.Normals.Count);
        Assert.Equal(116, geometry.Polygon.Polygons.Count);
        Assert.Single(geometry.Polygon.Groups);
        Ff7ModelRenderOverrides effects = Ff7Scene.CreateCustomLightingEffects(sample, descriptor, model, source.PartVisibility);
        uint[] baked = Colors(effects, model);
        Assert.True(effects.SuppressNativeLighting);
        Assert.True(baked.Distinct().Count() > 1);
        Assert.True(baked.Where((color, vertex) => color != geometry.Polygon.VertexColors[vertex]).Any());

        string checkpoint = JsonSerializer.Serialize(live.Capture());
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(checkpoint)!);
        Ff7Actor restored = cold.Host.Process.Actors.Single(actor => actor.Slot == source.Slot);
        Assert.Equal(JsonSerializer.Serialize(sample), JsonSerializer.Serialize(restored.NativeCustomLighting!.Displayed));
        Ff7ModelRenderOverrides restoredEffects = Ff7Scene.CreateCustomLightingEffects(restored.NativeCustomLighting!.Displayed!, cold.Field.Models.Models[restored.Model], cold.Models[restored.Model], restored.PartVisibility);
        Assert.Equal(baked, Colors(restoredEffects, cold.Models[restored.Model]));

        Ff7NativeCustomLightingPose laterPose = pose with { Direction = unchecked((byte)(pose.Direction + 64)) };
        Ff7NativeCustomLightingState canceled = Ff7NativeCustomLighting.Advance(state with { Pending = false }, true, laterPose);
        Assert.Same(sample, canceled.Displayed);
        Assert.Equal(baked, Colors(Ff7Scene.CreateCustomLightingEffects(canceled.Displayed!, descriptor, model), model));
        uint[] resampled = Colors(Ff7Scene.CreateCustomLightingEffects(sample with { SamplingPose = laterPose }, descriptor, model), model);
        Assert.False(baked.SequenceEqual(resampled));
        Assert.Equal(original, File.ReadAllBytes(path!));
        output.WriteLine($"Actual model {source.Model}: {baked.Length} baked vertices, {baked.Distinct().Count()} colors; frozen pose, cold restore and cancellation preserved source colors.");
    }

    private static uint[] Colors(Ff7ModelRenderOverrides effects, Ff7FieldModelResources resources)
    {
        Ff7BoneGeometry part = Assert.Single(resources.Geometry);
        return Enumerable.Range(0, part.Polygon.Vertices.Count).Select(vertex => effects.SourceVertexColor!(0, vertex, part.Polygon.VertexColors[vertex])).ToArray();
    }

    private static uint Pack(Vector4 color)
    {
        uint Channel(float value) => checked((uint)MathF.Round(value * 255, MidpointRounding.ToEven));
        return Channel(color.W) << 24 | Channel(color.X) << 16 | Channel(color.Y) << 8 | Channel(color.Z);
    }
}
