using System.Numerics;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattlePilotPresentationTests
{
    [Fact]
    public void Timeline_retains_one_completed_interval_and_resets_at_gaps_modes_and_spell_generations()
    {
        BattlePilotPresentationTimeline timeline = new();
        NativeBattlePreparedPresentation first = Frame(1, 0);
        NativeBattlePreparedPresentation second = Frame(2, 10);
        Assert.Null(timeline.Select(first, true, 0).Next);
        var sample = timeline.Select(second, true, 0.25f);
        Assert.Same(first, sample.Frame);
        Assert.Same(second, sample.Next);
        Assert.Equal(0.25f, sample.Alpha);
        Assert.Same(first, timeline.Select(second, true, 0.75f).Frame);
        Assert.Null(timeline.Select(second with { Callback = 4 }, true, 0.5f).Next);
        Assert.Null(timeline.Select(second with { Callback = 5, PilotGeneration = 2 }, true, 0.5f).Next);
        Assert.Null(timeline.Select(second with { Callback = 6, PilotGeneration = 2, PilotEffect = 105 }, true, 0.5f).Next);
        Assert.Null(timeline.Select(second, false, 0.5f).Next);
        Assert.Null(timeline.Select(second, true, 0.5f).Next);
    }

    [Theory]
    [InlineData((ushort)2)]
    [InlineData((ushort)3)]
    [InlineData((ushort)144)]
    public void Renderer_samples_world_geometry_to_fractional_pixels_and_holds_native_material_until_next_endpoint(ushort effect)
    {
        Capture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        NativeBattlePreparedPresentation first = Frame(1, 0, effect);
        NativeBattlePreparedPresentation second = Frame(2, 10, effect);
        NativeBattleLogicalResolvedCommand nextCommand = second.Primitives.LastCommands[0];
        nextCommand = nextCommand with { Command = nextCommand.Command with { Words = [0x2000FF00, Point(170, 108), Point(180, 108), Point(170, 118)] } };
        second = second with { Primitives = second.Primitives with { LastCommands = [nextCommand] } };
        renderer.RenderNativePrimitiveFrame(first.Primitives, null);
        TriangleVertex original = capture.Vertices[0];
        renderer.RenderNativePilotPrimitiveFrame(first, second, 0.525f);
        TriangleVertex sampled = capture.Vertices[0];
        Assert.Equal(165.25f, sampled.Position.X, 3);
        Assert.Equal(108, sampled.Position.Y);
        Assert.Equal(original.Color, sampled.Color);
        Assert.Equal(original.Uv, sampled.Uv);
        renderer.RenderNativePilotPrimitiveFrame(first, second, 0);
        Assert.Equal(original, capture.Vertices[0]);
        renderer.RenderNativePilotPrimitiveFrame(first, second, 1);
        Assert.Equal(170, capture.Vertices[0].Position.X);
        Assert.NotEqual(original.Color, capture.Vertices[0].Color);
        NativeBattleLogicalResolvedCommand heldCommand = first.Primitives.LastCommands[0];
        heldCommand = heldCommand with { Command = heldCommand.Command with { PilotGeometry = heldCommand.Command.PilotGeometry! with { Continuous = false } } };
        NativeBattlePreparedPresentation discrete = first with { Primitives = first.Primitives with { LastCommands = [heldCommand] } };
        renderer.RenderNativePilotPrimitiveFrame(discrete, second, 0.525f);
        Assert.Equal(160, capture.Vertices[0].Position.X);
        Assert.Equal(original.Color, capture.Vertices[0].Color);
    }

    [Fact]
    public void Fire_renderer_holds_the_authored_local_rectangle_while_sampling_the_particle_transform()
    {
        NativeBattlePreparedPresentation first = Frame(1, 0);
        NativeBattleLogicalResolvedCommand original = first.Primitives.LastCommands[0];
        NativeBattlePilotSpriteGeometry sprite = new(new(-10, 0, 0), new(10, 0, 0), new(-10, 10, 0), default, new(0, 0, 100), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
        NativeBattlePilotVisualGeometry geometry = original.Command.PilotGeometry! with { A = new(-10, 0, 100), Sprite = sprite };
        first = first with { Primitives = first.Primitives with { LastCommands = [original with { Command = original.Command with { PilotGeometry = geometry } }] } };
        NativeBattlePilotSpriteGeometry nextSprite = sprite with { LocalA = new(-100, 0, 0), Origin = new(20, 0, 100), AxisX = Vector3.UnitX * 3 };
        NativeBattlePilotVisualGeometry nextGeometry = geometry with { A = new(-280, 0, 100), Sprite = nextSprite };
        NativeBattlePreparedPresentation second = first with { Callback = 2, Primitives = first.Primitives with { LastCommands = [original with { Command = original.Command with { PilotGeometry = nextGeometry } }] } };
        Capture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        renderer.RenderNativePilotPrimitiveFrame(first, second, 0.5f);
        Assert.Equal(150, capture.Vertices[0].Position.X, 3);
        Assert.Equal(108, capture.Vertices[0].Position.Y, 3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Missing_world_geometry_in_either_endpoint_holds_the_whole_native_frame_and_reports_the_boundary(bool missingEarlier)
    {
        NativeBattlePreparedPresentation first = Frame(1, 0);
        NativeBattlePreparedPresentation second = Frame(2, 10) with { PilotCamera = new(new(10, 0, 0), new(10, 0, 100), 100, 0) };
        NativeBattlePreparedPresentation missing = missingEarlier ? first : second;
        NativeBattleLogicalResolvedCommand command = missing.Primitives.LastCommands[0];
        missing = missing with { Primitives = missing.Primitives with { LastCommands = [command with { Command = command.Command with { PilotGeometry = null } }] } };
        first = missingEarlier ? missing : first;
        second = missingEarlier ? second : missing;
        Capture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        renderer.RenderNativePrimitiveFrame(first.Primitives, null);
        TriangleVertex[] original = capture.Vertices.ToArray();
        renderer.RenderNativePilotPrimitiveFrame(first, second, 0.5f);
        Assert.Equal(original, capture.Vertices.ToArray());
        Assert.Equal(1, renderer.LastPilotFallbackCount);
        Assert.Contains("world-space", renderer.LastPilotFallbackReason!);
    }

    [Fact]
    public void Explicit_fullviewport_flash_and_draw_state_are_held_without_disabling_world_motion()
    {
        NativeBattleLogicalCommand flash = new(new("screen", 0, 0), 1, 0x28, [0x28000000, 0, 320, 216u << 16, 320 | 216u << 16], 1, [0, 0, 0, 0], null) { OrderingBucket = 0 };
        NativeBattleLogicalCommand state = new(new("screen", 0, 20), 1, 0xE1, [0xE1000000], 0, [], null);
        NativeBattlePreparedPresentation first = Frame(1, 0);
        first = first with { Primitives = first.Primitives with { LastCommands = first.Primitives.LastCommands.Add(new(flash, new(0), 1)).Add(new(state, new(0), 0)) } };
        BattleRenderer renderer = new(320, 216);
        Assert.True(renderer.CanSampleNativePilotInterval(first, Frame(2, 10)));
        Assert.Null(renderer.LastPilotFallbackReason);
        Assert.Equal(0, renderer.LastPilotFallbackCount);
    }

    [Fact]
    public void Sampled_shadow_depth_keeps_its_original_quarter_depth_quantization_and_bias()
    {
        NativeBattlePreparedPresentation first = ShadowFrame(1, 0);
        NativeBattlePreparedPresentation second = ShadowFrame(2, 10);
        Capture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        renderer.RenderNativePrimitiveFrame(first.Primitives, null);
        TriangleVertex original = capture.Vertices[0];
        renderer.RenderNativePilotPrimitiveFrame(first, second, 0.5f);
        Assert.Equal(1f / 724, capture.Vertices[0].ReciprocalW);
        Assert.Equal(original.Depth, capture.Vertices[0].Depth, 6);

        static NativeBattlePreparedPresentation ShadowFrame(long callback, float x)
        {
            NativeBattlePreparedPresentation frame = Frame(callback, x);
            NativeBattleLogicalResolvedCommand resolved = frame.Primitives.LastCommands[0];
            NativeBattlePilotVisualGeometry geometry = resolved.Command.PilotGeometry! with { A = new(x, 0, 1025), B = new(x + 10, 0, 1025), C = new(x, 10, 1025), DepthOffset = -300, DepthQuantum = 4 };
            resolved = resolved with { Command = resolved.Command with { PilotGeometry = geometry, VertexPayload4 = [724, 724, 724, 0] } };
            return frame with { Primitives = frame.Primitives with { LastDescriptorPerspective = true, LastCommands = [resolved] } };
        }
    }

    [Fact]
    public void Optional_submitted_FX_signatures_measure_actual_intermediate_geometry_and_exclude_scene_geometry()
    {
        Capture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        NativeBattlePreparedPresentation first = Frame(1, 0);
        NativeBattlePreparedPresentation second = Frame(2, 10);
        renderer.RenderNativePrimitiveFrame(first.Primitives, null);
        Assert.Equal(0, renderer.PilotSubmission.Corners);
        renderer.PilotDiagnosticsEnabled = true;
        renderer.ObservePilotSource(first, second, 0.25f, true);
        renderer.RenderNativePilotPrimitiveFrame(first, second, 0.25f);
        NativeBattlePilotSubmission earlier = renderer.PilotSubmission;
        renderer.ObservePilotSource(first, second, 0.75f, true);
        renderer.RenderNativePilotPrimitiveFrame(first, second, 0.75f);
        NativeBattlePilotSubmission later = renderer.PilotSubmission;
        Assert.NotEqual(earlier.WorldSignature, later.WorldSignature);
        Assert.NotEqual(earlier.ProjectedSignature, later.ProjectedSignature);
        Assert.Equal(earlier.MaterialSignature, later.MaterialSignature);
        Assert.Equal(3, later.CoveredContinuousCorners);
        Assert.Equal(1, later.SourceCallback);
        Assert.Equal(2, later.NextSourceCallback);
        Assert.Equal(0.75f, later.Alpha);

        NativeBattleLogicalResolvedCommand scene = first.Primitives.LastCommands[0];
        scene = scene with { Command = scene.Command with { PilotGeometry = scene.Command.PilotGeometry! with { Identity = scene.Command.PilotGeometry!.Identity with { Effect = 0 } } } };
        renderer.RenderNativePrimitiveFrame(first.Primitives, null);
        NativeBattlePilotSubmission withoutScene = renderer.PilotSubmission;
        renderer.RenderNativePrimitiveFrame(first.Primitives with { LastCommands = first.Primitives.LastCommands.Add(scene) }, null);
        Assert.Equal(withoutScene.WorldSignature, renderer.PilotSubmission.WorldSignature);
        Assert.Equal(withoutScene.ProjectedSignature, renderer.PilotSubmission.ProjectedSignature);
        Assert.Equal(withoutScene.MaterialSignature, renderer.PilotSubmission.MaterialSignature);
        Assert.Equal(withoutScene.Corners, renderer.PilotSubmission.Corners);
        NativeBattleLogicalResolvedCommand degenerate = first.Primitives.LastCommands[0];
        degenerate = degenerate with { Command = degenerate.Command with { Words = [0x200000FF, Point(160, 108), Point(160, 108), Point(160, 108)] } };
        renderer.RenderNativePrimitiveFrame(first.Primitives with { LastCommands = [degenerate] }, null);
        Assert.Empty(capture.Vertices);
        Assert.Equal(0, renderer.PilotSubmission.Corners);
    }

    [Fact]
    public void Discrete_Thunder_submissions_are_distinct_from_covered_continuous_samples()
    {
        NativeBattlePreparedPresentation first = Frame(1, 0, 3);
        NativeBattleLogicalResolvedCommand command = first.Primitives.LastCommands[0];
        first = first with { Primitives = first.Primitives with { LastCommands = [command with { Command = command.Command with { PilotGeometry = command.Command.PilotGeometry! with { Continuous = false } } }] } };
        NativeBattlePreparedPresentation second = Frame(2, 10, 3);
        Capture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture, PilotDiagnosticsEnabled = true };
        renderer.RenderNativePilotPrimitiveFrame(first, second, 0.25f);
        NativeBattlePilotSubmission before = renderer.PilotSubmission;
        renderer.RenderNativePilotPrimitiveFrame(first, second, 0.75f);
        Assert.Equal(before.WorldSignature, renderer.PilotSubmission.WorldSignature);
        Assert.Equal(before.MaterialSignature, renderer.PilotSubmission.MaterialSignature);
        Assert.Equal(0, renderer.PilotSubmission.CoveredContinuousCorners);
        Assert.Equal(3, renderer.PilotSubmission.DiscreteCorners);
    }

    private static NativeBattlePreparedPresentation Frame(long callback, float x, ushort effect = 2)
    {
        NativeBattlePilotVisualIdentity identity = new(effect, 1, 0, 1, 0);
        NativeBattleLogicalCommand command = new(new("pilot", 0, 0), 1, 0x20, [0x200000FF, Point((int)(160 + x), 108), Point((int)(170 + x), 108), Point((int)(160 + x), 118)], 0, [], null)
        {
            OrderingBucket = 50,
            PilotGeometry = new(identity, new(x, 0, 100), new(x + 10, 0, 100), new(x, 10, 100), default, 3)
        };
        NativeBattlePreparedPrimitiveFrame primitives = new([new(command, new(0), 1)], [])
        {
            LastDescriptorPerspective = false,
            LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(),
            LastColorState = NativeBattlePrimitiveColorState.FromColdProcess()
        };
        return new(callback, callback, callback, false, primitives, null, null, null, new(new(0, 0, 0), new(0, 0, 100), 100, 0), [], null, null, [], null)
        {
            PilotEffect = effect,
            PilotGeneration = 1,
            PilotCamera = new(new(0, 0, 0), new(0, 0, 100), 100, 0),
            PilotProjection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108)
        };
    }

    private static uint Point(int x, int y) => unchecked((ushort)(x * 8) | ((uint)(ushort)(y * 8) << 16));

    private sealed class Capture : ITriangleRenderer
    {
        internal List<TriangleVertex> Vertices { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Vertices.Clear();
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Vertices.Add(a);
            Vertices.Add(b);
            Vertices.Add(c);
        }

        public void End()
        {
        }
    }
}
