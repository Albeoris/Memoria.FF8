using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class FieldColorInterpolationTests
{
    [Theory]
    [InlineData(false, NativeFadeDrawMode.Add)]
    [InlineData(true, NativeFadeDrawMode.Subtract)]
    public void Continuous_fade_retains_fractional_channels_and_exact_endpoints(bool subtract, NativeFadeDrawMode mode)
    {
        NativeFieldFadeState first = NativeFieldFadeState.FromColdProcess().IssueInterpolatedColor(subtract, 0, 10, 20, 12, 30, 48, 4).AdvanceCallback();
        NativeFieldFadeState last = first.AdvanceCallback();
        string original = JsonSerializer.Serialize(new[] { first, last });

        FieldFadeSample middle = FieldFadeSample.Interpolate(first, last, 0.5f);

        Assert.Equal(mode, middle.Mode);
        Assert.Equal(new Vector3(4.5f, 17.5f, 30.5f), middle.Color);
        Assert.Equal(FieldFadeSample.FromNative(first.Draw), FieldFadeSample.Interpolate(first, last, 0));
        Assert.Equal(FieldFadeSample.FromNative(last.Draw), FieldFadeSample.Interpolate(first, last, 1));
        Assert.Equal(original, JsonSerializer.Serialize(new[] { first, last }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Consecutive_direct_colors_with_the_same_operation_blend(bool subtract)
    {
        NativeFieldFadeState first = NativeFieldFadeState.FromColdProcess().IssueDirectColor(subtract, 1, 3, 5).AdvanceCallback();
        NativeFieldFadeState last = first.IssueDirectColor(subtract, 10, 20, 30).AdvanceCallback();

        FieldFadeSample middle = FieldFadeSample.Interpolate(first, last, 0.5f);

        Assert.Equal(last.Draw.Mode, middle.Mode);
        Assert.Equal(new Vector3(5.5f, 11.5f, 17.5f), middle.Color);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void None_transitions_use_zero_color_with_the_active_operation(bool subtract, bool reverse)
    {
        NativeFieldFadeState none = NativeFieldFadeState.FromColdProcess();
        NativeFieldFadeState active = none.IssueDirectColor(subtract, 1, 3, 5).AdvanceCallback();
        NativeFieldFadeState first = reverse ? active : none;
        NativeFieldFadeState last = reverse ? none : active;

        FieldFadeSample middle = FieldFadeSample.Interpolate(first, last, 0.5f);

        Assert.Equal(active.Draw.Mode, middle.Mode);
        Assert.Equal(new Vector3(0.5f, 1.5f, 2.5f), middle.Color);
        Assert.Equal(FieldFadeSample.FromNative(first.Draw), FieldFadeSample.Interpolate(first, last, 0));
        Assert.Equal(FieldFadeSample.FromNative(last.Draw), FieldFadeSample.Interpolate(first, last, 1));
    }

    [Fact]
    public void Fade_operation_changes_and_black_snap_to_the_current_draw()
    {
        NativeFieldFadeState first = NativeFieldFadeState.FromColdProcess().IssueInterpolatedColor(false, 0, 0, 0, 12, 20, 28, 4).AdvanceCallback();
        NativeFieldFadeState subtract = first.IssueInterpolatedColor(true, 0, 0, 0, 12, 20, 28, 4).AdvanceCallback();
        NativeFieldFadeState black = first.IssueBlack().AdvanceCallback();

        Assert.Equal(FieldFadeSample.FromNative(subtract.Draw), FieldFadeSample.Interpolate(first, subtract, 0.5f));
        Assert.Equal(FieldFadeSample.FromNative(black.Draw), FieldFadeSample.Interpolate(first, black, 0.5f));
        Assert.Equal(FieldFadeSample.FromNative(first.Draw), FieldFadeSample.Interpolate(black, first, 0.5f));
    }

    [Fact]
    public void Actor_polygon_color_blends_without_replacing_native_channels()
    {
        object scene = new();
        FieldActorSample actor = Actor(new object(), new Vector3(1, 3, 5));
        FieldPresentationFrame first = Frame(scene, 7, actor);
        FieldPresentationFrame last = Frame(scene, 8, actor with { Visual = actor.Visual with { PolygonColor = new Vector3(10, 20, 30) } });
        string original = JsonSerializer.Serialize(new[] { first, last });
        FieldPresentation presentation = Prime(first, last);

        ActorVisual middle = Assert.Single(presentation.Sample(last, 0.5f, false).Actors);

        Assert.Equal(new Vector3(5.5f, 11.5f, 17.5f), middle.PresentationPolygonColor);
        Assert.Equal(last.Actors[0].Visual.PolygonColor, middle.PolygonColor);
        Assert.Equal(first.Actors[0].Visual.PolygonColor, Assert.Single(presentation.Sample(last, 0, false).Actors).PresentationPolygonColor);
        ActorVisual endpoint = Assert.Single(presentation.Sample(last, 1, false).Actors);
        Assert.Equal(last.Actors[0].Visual.PolygonColor, endpoint.PresentationPolygonColor ?? endpoint.PolygonColor);
        Assert.Equal(original, JsonSerializer.Serialize(new[] { first, last }));
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("model")]
    [InlineData("placement")]
    [InlineData("visibility")]
    public void Discontinuous_actors_do_not_inherit_previous_polygon_color(string change)
    {
        object scene = new();
        FieldActorSample actor = Actor(new object(), new Vector3(1, 3, 5));
        FieldActorSample after = actor with { Visual = actor.Visual with { PolygonColor = new Vector3(10, 20, 30) } };
        after = change switch
        {
            "owner" => after with { Owner = new object() },
            "model" => after with { Visual = after.Visual with { ModelId = 1 } },
            "placement" => after with { PositionRevision = 1 },
            _ => after with { Visual = after.Visual with { Visible = false } }
        };
        FieldPresentationFrame first = Frame(scene, 7, actor);
        FieldPresentationFrame last = Frame(scene, 8, after);

        ActorVisual sample = Assert.Single(Prime(first, last).Sample(last, 0.5f, false).Actors);

        Assert.Equal(after.Visual.PolygonColor, sample.PresentationPolygonColor ?? sample.PolygonColor);
        Assert.Equal(after.Visual.PolygonColor, sample.PolygonColor);
    }

    [Fact]
    public void Background_shades_blend_shared_keys_and_use_only_current_keys()
    {
        object scene = new();
        FieldActorSample actor = Actor(new object(), Vector3.One);
        Dictionary<int, Vector3> before = new() { [1] = new(1, 3, 5), [2] = new(100, 110, 120) };
        Dictionary<int, Vector3> after = new() { [1] = new(10, 20, 30), [3] = new(40, 50, 60) };
        FieldPresentationFrame first = Frame(scene, 7, actor) with { BackgroundShades = before };
        FieldPresentationFrame last = Frame(scene, 8, actor) with { BackgroundShades = after };
        FieldPresentation presentation = Prime(first, last);

        IReadOnlyDictionary<int, Vector3> middle = presentation.Sample(last, 0.5f, false).BackgroundShades!;

        Assert.Equal(2, middle.Count);
        Assert.Equal(new Vector3(5.5f, 11.5f, 17.5f), middle[1]);
        Assert.Equal(after[3], middle[3]);
        Assert.False(middle.ContainsKey(2));
        Assert.Equal(before[1], presentation.Sample(last, 0, false).BackgroundShades![1]);
        Assert.Equal(after[1], presentation.Sample(last, 1, false).BackgroundShades![1]);
        Assert.Equal(new Vector3(1, 3, 5), before[1]);
        Assert.Equal(new Vector3(10, 20, 30), after[1]);
    }

    [Fact]
    public void Completed_callback_history_samples_fade_together_with_actor_and_background_colors()
    {
        object scene = new();
        FieldActorSample actor = Actor(new object(), new Vector3(1, 3, 5));
        NativeFieldFadeState fade = NativeFieldFadeState.FromColdProcess().IssueInterpolatedColor(false, 0, 10, 20, 12, 30, 48, 4).AdvanceCallback();
        FieldPresentationFrame first = Frame(scene, 7, actor) with { Fade = fade, BackgroundShades = new Dictionary<int, Vector3> { [1] = new(1, 3, 5) } };
        FieldPresentationFrame last = Frame(scene, 8, actor with { Visual = actor.Visual with { PolygonColor = new Vector3(10, 20, 30) } }) with
        {
            Fade = fade.AdvanceCallback(),
            BackgroundShades = new Dictionary<int, Vector3> { [1] = new(10, 20, 30) }
        };
        FieldPresentation presentation = Prime(first, last);

        FieldPresentationSample sample = presentation.Sample(last, 0.5f, false);

        Assert.Equal(new FieldFadeSample(NativeFadeDrawMode.Add, new Vector3(4.5f, 17.5f, 30.5f)), sample.Fade);
        Assert.Equal(new Vector3(5.5f, 11.5f, 17.5f), Assert.Single(sample.Actors).PresentationPolygonColor);
        Assert.Equal(new Vector3(5.5f, 11.5f, 17.5f), sample.BackgroundShades![1]);
        Assert.Equal(FieldFadeSample.FromNative(first.Fade.Draw), presentation.Sample(last, 0, false).Fade);
        Assert.Equal(FieldFadeSample.FromNative(last.Fade.Draw), presentation.Sample(last, 1, false).Fade);
    }

    [Theory]
    [InlineData("reset")]
    [InlineData("gap")]
    [InlineData("scene")]
    public void Reset_or_discontinuous_history_snaps_all_color_channels(string change)
    {
        object scene = new();
        FieldActorSample actor = Actor(new object(), new Vector3(1, 3, 5));
        NativeFieldFadeState fade = NativeFieldFadeState.FromColdProcess().IssueInterpolatedColor(false, 0, 10, 20, 12, 30, 48, 4).AdvanceCallback();
        FieldPresentationFrame first = Frame(scene, 7, actor) with { Fade = fade, BackgroundShades = new Dictionary<int, Vector3> { [1] = new(1, 3, 5) } };
        FieldPresentationFrame last = Frame(scene, change == "gap" ? 10 : 8, actor with { Visual = actor.Visual with { PolygonColor = new Vector3(10, 20, 30) } }) with
        {
            Owner = change == "scene" ? new object() : scene,
            Fade = fade.AdvanceCallback(),
            BackgroundShades = new Dictionary<int, Vector3> { [1] = new(10, 20, 30) }
        };
        FieldPresentation presentation = Prime(first, last);
        if (change == "reset")
        {
            presentation.Reset();
        }

        FieldPresentationSample sample = presentation.Sample(last, 0.5f, false);
        ActorVisual visual = Assert.Single(sample.Actors);

        Assert.Equal(last.Actors[0].Visual.PolygonColor, visual.PresentationPolygonColor ?? visual.PolygonColor);
        Assert.Equal(FieldFadeSample.FromNative(last.Fade!.Draw), sample.Fade);
        Assert.Equal(last.BackgroundShades![1], sample.BackgroundShades![1]);
    }

    [Theory]
    [InlineData(NativeFadeDrawMode.Add, 255, 10, 23)]
    [InlineData(NativeFadeDrawMode.Subtract, 244, 0, 17)]
    public void Cpu_composite_rounds_fractional_colors_once_and_clamps_without_changing_alpha(NativeFadeDrawMode mode, byte red, byte green, byte blue)
    {
        byte[] pixels = [250, 1, 20, 73];

        FieldColorComposite.Apply(pixels, new FieldFadeSample(mode, new Vector3(5.5f, 8.75f, 2.75f)));

        Assert.Equal(new byte[] { red, green, blue, 73 }, pixels);
    }

    [Theory]
    [InlineData(NativeFadeDrawMode.None, 0f)]
    [InlineData(NativeFadeDrawMode.None, 1f)]
    [InlineData(NativeFadeDrawMode.Add, 0f)]
    [InlineData(NativeFadeDrawMode.Add, 1f)]
    [InlineData(NativeFadeDrawMode.Subtract, 0f)]
    [InlineData(NativeFadeDrawMode.Subtract, 1f)]
    [InlineData(NativeFadeDrawMode.Black, 0f)]
    [InlineData(NativeFadeDrawMode.Black, 1f)]
    public void Cpu_composite_matches_native_overload_at_exact_endpoints_and_preserves_clip(NativeFadeDrawMode mode, float fraction)
    {
        NativeFieldFadeState first = NativeFieldFadeState.FromColdProcess() with { Draw = new(mode, 1, 3, 5) };
        NativeFieldFadeState last = first with { Draw = new(mode, 10, 20, 30) };
        byte[] original = [10, 20, 30, 41, 40, 50, 60, 71, 70, 80, 90, 101, 100, 110, 120, 131];
        byte[] expected = original.ToArray();
        byte[] actual = original.ToArray();
        PixelBounds clip = new(1, 0, 1, 2);
        NativeFadeDraw native = fraction == 0 ? first.Draw : last.Draw;
        FieldColorComposite.Apply(expected, native, 2, clip);

        FieldColorComposite.Apply(actual, FieldFadeSample.Interpolate(first, last, fraction), 2, clip);

        Assert.Equal(expected, actual);
        Assert.Equal(original[..4], actual[..4]);
        Assert.Equal(original[8..12], actual[8..12]);
        for (int index = 3; index < actual.Length; index += 4)
        {
            Assert.Equal(original[index], actual[index]);
        }
    }

    [Theory]
    [InlineData(NativeFadeDrawMode.Add, TriangleBlend.Add)]
    [InlineData(NativeFadeDrawMode.Subtract, TriangleBlend.Subtract)]
    public void Gpu_fade_quad_receives_fractional_color_without_byte_quantization(NativeFadeDrawMode mode, TriangleBlend blend)
    {
        FieldAssets field = new(0, "color-fixture", [], [], [], new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100), new FieldWalkmesh([]), []);
        Recorder recorder = new();
        FieldRenderer renderer = new(field, 320, 224, nativeMovieViewport: true) { TriangleRenderer = recorder };
        Vector3 color = new(5.5f, 11.5f, 17.5f);

        renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, presentationFade: new FieldFadeSample(mode, color));

        Assert.Equal(6, recorder.Vertices.Count);
        Assert.All(recorder.Vertices, vertex => Assert.Equal(new Vector4(color / 255, 1), vertex.Color));
        Assert.All(recorder.Materials, material => Assert.Equal(blend, material.Blend));
        Assert.Contains(recorder.Vertices, vertex => vertex.Position == Vector2.Zero);
        Assert.Contains(recorder.Vertices, vertex => vertex.Position == new Vector2(320, 224));
    }

    [Fact]
    public void Installed_model_accepts_fractional_presentation_tint_while_requiring_native_color_bytes()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Configured installed resources are required.");
        using GameAssets assets = new(directory);
        FieldModel model = assets.LoadField(895).Models[6];
        FieldAssets field = new(895, "model-color-fixture", [], [], [], new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0, 0, 2048), Vector2.Zero, 1024), new FieldWalkmesh([]), [model]);
        Recorder recorder = new();
        FieldRenderer renderer = new(field) { TriangleRenderer = recorder };
        ActorVisual actor = new(0, 0, 0, 0, 0, 192, 0, 0, true, PolygonColor: new Vector3(128), GroundShadowEnabled: false);
        renderer.Render(new Dictionary<int, int>(), [actor with { PresentationPolygonColor = new Vector3(128, 150, 128) }], TimeSpan.Zero, backgroundVisible: false);
        TriangleVertex[] first = recorder.Vertices.ToArray();
        recorder.Vertices.Clear();
        renderer.Render(new Dictionary<int, int>(), [actor with { PresentationPolygonColor = new Vector3(128, 151, 128) }], TimeSpan.Zero, backgroundVisible: false);
        TriangleVertex[] last = recorder.Vertices.ToArray();
        recorder.Vertices.Clear();

        renderer.Render(new Dictionary<int, int>(), [actor with { PresentationPolygonColor = new Vector3(128, 150.5f, 128) }], TimeSpan.Zero, backgroundVisible: false);

        Assert.NotEmpty(first);
        Assert.Equal(first.Length, recorder.Vertices.Count);
        Assert.Contains(Enumerable.Range(0, first.Length), index => first[index].Color.Y != last[index].Color.Y);
        for (int index = 0; index < first.Length; index++)
        {
            Assert.Equal((first[index].Color.Y + last[index].Color.Y) / 2, recorder.Vertices[index].Color.Y, 5);
        }
        Assert.Throws<NotSupportedException>(() => renderer.Render(new Dictionary<int, int>(), [actor with { PolygonColor = new Vector3(128, 150.5f, 128), PresentationPolygonColor = new Vector3(128) }], TimeSpan.Zero));
        Assert.Throws<NotSupportedException>(() => renderer.Render(new Dictionary<int, int>(), [actor with { PolygonColor = null, PresentationPolygonColor = new Vector3(128) }], TimeSpan.Zero));
    }

    private sealed class Recorder : ITriangleRenderer
    {
        public List<TriangleVertex> Vertices { get; } = [];
        public List<TriangleMaterial> Materials { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void End()
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Vertices.AddRange([a, b, c]);
            Materials.Add(material);
        }
    }

    private static FieldPresentation Prime(FieldPresentationFrame before, FieldPresentationFrame after)
    {
        FieldPresentation presentation = new();
        presentation.Sample(before, 1, false);
        presentation.Commit(after.Owner, after.Frame, () => after);
        return presentation;
    }

    private static FieldActorSample Actor(object owner, Vector3 color) => new(owner, 0, 0, new ActorVisual(1, 0, 0, 0, 0, 0, 0, 1, true, PolygonColor: color), 0, 4, true, 1);

    private static FieldPresentationFrame Frame(object owner, long frame, FieldActorSample actor) => new(owner, frame, 0, [actor], new BackgroundScrollFrame(new Dictionary<int, Vector2>(), Vector2.Zero));
}
