using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleIdleMotionTests
{
    [Fact]
    public void Idle_body_and_linked_geometry_have_four_distinct_draws_without_changing_completed_sources()
    {
        NativeBattlePreparedPresentation first = Frame(1, 0);
        NativeBattlePreparedPresentation second = Frame(2, 12);
        string before = JsonSerializer.Serialize(new[] { first, second });
        Capture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        BattlePilotPresentationTimeline timeline = new();
        object owner = new();
        Assert.Null(timeline.Select(first, true, 0, owner).Next);
        HashSet<float> bodyPositions = [];
        HashSet<float> linkedPositions = [];
        foreach (float phase in new[] { 0f, 0.25f, 0.5f, 0.75f })
        {
            var selected = timeline.Select(second, true, phase, owner);
            Assert.Same(first, selected.Frame);
            Assert.Same(second, selected.Next);
            renderer.RenderNativePilotPrimitiveFrame(selected.Frame, selected.Next!, selected.Alpha);
            Assert.Equal(0, renderer.LastPilotFallbackCount);
            Assert.Equal(6, capture.Vertices.Count);
            bodyPositions.Add(capture.Vertices[0].Position.X);
            linkedPositions.Add(capture.Vertices[3].Position.X);
            Assert.Equal(160 + phase * 12, capture.Vertices[0].Position.X, 3);
            Assert.Equal(180 + phase * 12, capture.Vertices[3].Position.X, 3);
        }
        Assert.Equal(4, bodyPositions.Count);
        Assert.Equal(4, linkedPositions.Count);
        Assert.Equal(before, JsonSerializer.Serialize(new[] { first, second }));
    }

    [Fact]
    public void Idle_phase_uses_completion_time_and_freezes_without_advancing_simulation()
    {
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        object owner = new();
        NativeBattlePreparedPresentation source = Frame(2, 12);
        timing.BeginPresentationFrame(owner, true, clock);
        clock.Advance(TimeSpan.FromSeconds(1));
        timing.ObservePresentationSource(owner, source);
        Assert.Equal(0, timing.SamplePresentationPhase(owner, source, true));
        clock.Advance(TimeSpan.FromMilliseconds(16));
        float shown = timing.SamplePresentationPhase(owner, source, true);
        Assert.Equal(0.248f, shown, 6);
        timing.FreezePresentationFrame();
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(shown, timing.SamplePresentationPhase(owner, source, false));
        timing.BeginPresentationFrame(owner, true, clock);
        timing.ObservePresentationSource(owner, source);
        clock.Advance(TimeSpan.FromMilliseconds(16));
        Assert.Equal(shown + 0.248f, timing.SamplePresentationPhase(owner, source, true), 6);
        Assert.Equal(TimeSpan.Zero, timing.SimulationTotal);
        Assert.Equal(2, source.Callback);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("gap")]
    [InlineData("disabled")]
    [InlineData("effect")]
    [InlineData("unsupported")]
    public void Idle_interval_resets_at_owner_callback_mode_and_effect_boundaries(string boundary)
    {
        BattlePilotPresentationTimeline timeline = new();
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        object owner = new();
        NativeBattlePreparedPresentation first = Frame(1, 0);
        NativeBattlePreparedPresentation second = Frame(2, 12);
        timeline.Select(first, true, 0, owner);
        Assert.NotNull(timeline.Select(second, true, 0.5f, owner).Next);
        timing.BeginPresentationFrame(owner, true, clock);
        timing.ObservePresentationSource(owner, second);
        clock.Advance(TimeSpan.FromMilliseconds(16));
        Assert.True(timing.SamplePresentationPhase(owner, second, true) > 0);
        bool smooth = true;
        switch (boundary)
        {
            case "owner":
                owner = new();
                break;
            case "gap":
                second = second with { Callback = 4 };
                break;
            case "disabled":
                smooth = false;
                break;
            case "effect":
                second = second with { SceneMotion = false, PilotEffect = 2, PilotGeneration = 1 };
                break;
            case "unsupported":
                second = second with { SceneMotion = false, PilotEffect = 224 };
                break;
        }
        var selected = timeline.Select(second, smooth, 0.1f, owner);
        Assert.Null(selected.Next);
        Assert.Same(second, selected.Frame);
        timing.BeginPresentationFrame(owner, true, clock);
        timing.ObservePresentationSource(owner, second);
        Assert.Equal(0, timing.SamplePresentationPhase(owner, second, true, smooth));
    }

    private static NativeBattlePreparedPresentation Frame(long callback, float x)
    {
        NativeBattleLogicalResolvedCommand Body(uint bank, float offset)
        {
            NativeBattleLogicalCommand command = new(new("scene", 0, checked((int)bank)), 1, 0x20, [0x200000FF, Point((int)(160 + x + offset), 108), Point((int)(170 + x + offset), 108), Point((int)(160 + x + offset), 118)], 0, [], null)
            {
                OrderingBucket = 50,
                PilotGeometry = new(new(0, bank, 0, 0, 0), new(x + offset, 0, 100), new(x + offset + 10, 0, 100), new(x + offset, 10, 100), default, 3)
            };
            return new(command, new(0), 1);
        }
        NativeBattlePreparedPrimitiveFrame primitives = new([Body(0x502D40, 0), Body(0x507BF0, 20)], [])
        {
            LastDescriptorPerspective = false,
            LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(),
            LastColorState = NativeBattlePrimitiveColorState.FromColdProcess()
        };
        return new(callback, callback, callback, false, primitives, null, null, null, new(new(0, 0, 0), new(0, 0, 100), 100, 0), [], null, null, [], null)
        {
            SceneMotion = true,
            PilotCamera = new(new(0, 0, 0), new(0, 0, 100), 100, 0),
            PilotProjection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108)
        };
    }

    private static uint Point(int x, int y) => unchecked((ushort)(x * 8) | ((uint)(ushort)(y * 8) << 16));

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;

        internal void Advance(TimeSpan elapsed)
        {
            timestamp += elapsed.Ticks;
        }
    }

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
