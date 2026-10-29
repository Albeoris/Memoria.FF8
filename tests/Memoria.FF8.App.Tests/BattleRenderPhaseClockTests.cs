using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleRenderPhaseClockTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(144)]
    [InlineData(202)]
    [InlineData(319)]
    public void Completion_clock_follows_the_recognized_effect_registry(ushort effect)
    {
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        object owner = new();
        NativeBattlePreparedPresentation source = Frame(2, 10) with { PilotEffect = effect };
        timing.BeginPresentationFrame(owner, true, clock);
        timing.ObservePresentationSource(owner, source);
        clock.Advance(TimeSpan.FromMilliseconds(10));
        Assert.Equal(NativeBattleSmoothPresentation.Supports(effect) ? 0.155f : 0, timing.SamplePresentationPhase(owner, source, true), 6);
    }

    [Fact]
    public void Producer_work_and_fixed_step_debt_before_publication_do_not_consume_the_new_interval()
    {
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        object owner = new();
        timing.BeginPresentationFrame(owner, true, clock);
        timing.ObservePresentationSource(owner, Frame(1, 0));
        timing.Accumulate(TimeSpan.FromMilliseconds(90));
        clock.Advance(TimeSpan.FromMilliseconds(49));
        NativeBattlePreparedPresentation next = Frame(2, 10);
        timing.ObservePresentationSource(owner, next);
        Assert.Equal(0, timing.SamplePresentationPhase(owner, next, true));
        for (int draw = 1; draw <= 3; draw++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(16));
            Assert.Equal(draw * 0.248f, timing.SamplePresentationPhase(owner, next, true), 6);
        }
        Assert.Equal(TimeSpan.Zero, timing.SimulationTotal);
        Assert.True(timing.TryStep());
        Assert.Equal(PresentationTiming.SimulationStep, timing.SimulationTotal);
        Assert.Equal(0.744f, timing.SamplePresentationPhase(owner, next, true), 6);
        timing.BeginPresentationFrame(owner, true, clock);
        clock.Advance(TimeSpan.FromMilliseconds(4));
        Assert.Equal(0.806f, timing.SamplePresentationPhase(owner, next, true), 6);
    }

    [Fact]
    public void Unproduced_endpoints_clamp_without_advancing_callbacks_and_new_sources_publish_immediately()
    {
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        object owner = new();
        NativeBattlePreparedPresentation frame = Frame(2, 10);
        timing.BeginPresentationFrame(owner, true, clock);
        timing.ObservePresentationSource(owner, frame);
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(1, timing.SamplePresentationPhase(owner, frame, true));
        timing.ObservePresentationSource(owner, Frame(3, 20));
        Assert.Equal(0, timing.SamplePresentationPhase(owner, Frame(3, 20), true));
        timing.ObservePresentationSource(owner, null);
        Assert.Equal(0, timing.SamplePresentationPhase(owner, frame, true));
        Assert.Equal(TimeSpan.Zero, timing.SimulationTotal);
    }

    [Theory]
    [InlineData("pause")]
    [InlineData("capture")]
    [InlineData("focus")]
    public void Pauses_hold_the_last_drawn_pose_and_resume_without_rewind_or_blocked_wall_time(string boundary)
    {
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        object owner = new();
        NativeBattlePreparedPresentation first = Frame(1, 0);
        NativeBattlePreparedPresentation second = Frame(2, 10);
        timing.BeginPresentationFrame(owner, true, clock);
        timing.ObservePresentationSource(owner, second);
        BattlePilotPresentationTimeline timeline = new();
        timeline.Select(first, true, 0, owner);
        Capture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        clock.Advance(TimeSpan.FromMilliseconds(47));
        float visible = timing.SamplePresentationPhase(owner, second, true);
        Assert.Equal(0.7285f, visible, 6);
        TriangleVertex[] before = Draw(visible);
        clock.Advance(TimeSpan.FromMilliseconds(5));
        switch (boundary)
        {
            case "pause":
                timing.BeginPresentationFrame(owner, false, clock);
                break;
            case "capture":
                timing.FreezePresentationFrame();
                break;
            case "focus":
                timing.DiscardPendingTime();
                break;
        }
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(visible, timing.SamplePresentationPhase(owner, second, false));
        Assert.Equal(before, Draw(visible));
        timing.BeginPresentationFrame(owner, true, clock);
        Assert.Equal(visible, timing.SamplePresentationPhase(owner, second, true));
        timing.ObservePresentationSource(owner, second);
        Assert.Equal(visible, timing.SamplePresentationPhase(owner, second, true));
        clock.Advance(TimeSpan.FromMilliseconds(2));
        float resumed = timing.SamplePresentationPhase(owner, second, true);
        Assert.Equal(visible + 0.031f, resumed, 6);
        Assert.True(Draw(resumed)[0].Position.X > before[0].Position.X);
        Assert.Equal(TimeSpan.Zero, timing.SimulationTotal);

        TriangleVertex[] Draw(float alpha)
        {
            var selected = timeline.Select(second, true, alpha, owner);
            renderer.RenderNativePilotPrimitiveFrame(selected.Frame, selected.Next!, selected.Alpha);
            Assert.Equal(0, renderer.LastPilotFallbackCount);
            return capture.Vertices.ToArray();
        }
    }

    [Theory]
    [InlineData("session")]
    [InlineData("generation")]
    [InlineData("effect")]
    [InlineData("mode")]
    [InlineData("gap")]
    public void Completion_clock_cannot_retain_phase_across_source_or_mode_boundaries(string boundary)
    {
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        object owner = new();
        NativeBattlePreparedPresentation source = Frame(2, 10);
        timing.BeginPresentationFrame(owner, true, clock);
        timing.ObservePresentationSource(owner, source);
        clock.Advance(TimeSpan.FromMilliseconds(47));
        Assert.True(timing.SamplePresentationPhase(owner, source, true) > 0.7f);
        timing.FreezePresentationFrame();
        if (boundary == "session")
        {
            owner = new();
        }
        else if (boundary == "generation")
        {
            source = source with { PilotGeneration = 2 };
        }
        else if (boundary == "effect")
        {
            source = source with { PilotEffect = 3 };
        }
        else if (boundary == "gap")
        {
            source = source with { Callback = 4 };
        }
        else
        {
            Assert.Equal(0, timing.SamplePresentationPhase(owner, source, true, false));
        }
        timing.BeginPresentationFrame(owner, true, clock);
        timing.ObservePresentationSource(owner, source);
        Assert.Equal(0, timing.SamplePresentationPhase(owner, source, true));
    }

    [Fact]
    public void Observed_native_elapsed_ratio_preserves_effective_speed_and_mid_interval_rate_changes()
    {
        GameplayClock gameplay = new();
        gameplay.ToggleSpeed();
        Assert.Equal(3, gameplay.SpeedMultiplier);
        TimeSpan elapsed = TimeSpan.FromMilliseconds(10);
        TimeSpan scripted = gameplay.Advance(elapsed, SceneKind.Field, scriptedInterface: true);
        Assert.Equal(elapsed, scripted);
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        object owner = new();
        NativeBattlePreparedPresentation source = Frame(2, 10);
        timing.BeginPresentationFrame(owner, true, clock);
        timing.ObservePresentationSource(owner, source, scripted.Ticks / (double)elapsed.Ticks);
        clock.Advance(elapsed);
        Assert.Equal(0.155f, timing.SamplePresentationPhase(owner, source, true), 6);
        TimeSpan accelerated = gameplay.Advance(elapsed, SceneKind.Field);
        Assert.Equal(elapsed * 3, accelerated);
        timing.ObservePresentationSource(owner, source, accelerated.Ticks / (double)elapsed.Ticks);
        Assert.Equal(0.155f, timing.SamplePresentationPhase(owner, source, true), 6);
        clock.Advance(elapsed);
        Assert.Equal(0.62f, timing.SamplePresentationPhase(owner, source, true), 6);
        Assert.Equal(TimeSpan.Zero, timing.SimulationTotal);
    }

    [Fact]
    public void Zero_observed_elapsed_holds_the_drawn_phase_until_a_positive_native_update_resumes_it()
    {
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        object owner = new();
        NativeBattlePreparedPresentation source = Frame(2, 10);
        timing.BeginPresentationFrame(owner, true, clock);
        timing.ObservePresentationSource(owner, source);
        clock.Advance(TimeSpan.FromMilliseconds(10));
        float drawn = timing.SamplePresentationPhase(owner, source, true);
        timing.ObservePresentationSource(owner, source, 0);
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(drawn, timing.SamplePresentationPhase(owner, source, true));
        timing.BeginPresentationFrame(owner, true, clock);
        timing.ObservePresentationSource(owner, source);
        clock.Advance(TimeSpan.FromMilliseconds(10));
        Assert.Equal(drawn + 0.155f, timing.SamplePresentationPhase(owner, source, true), 6);
        Assert.Throws<ArgumentOutOfRangeException>(() => timing.ObservePresentationSource(owner, source, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => timing.ObservePresentationSource(owner, source, -1));
    }

    [Fact]
    public void Repeated_render_phase_sampling_changes_submitted_positions_without_mutating_battle_state_random_or_events()
    {
        BattleSession battle = new(new BattleDefinition { NativeSeed = 1, NativeCrtRandom = new(7), Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
        List<BattleEvent> events = [];
        battle.DiagnosticEventPublished += events.Add;
        BattleSnapshot before = battle.Capture();
        string serialized = JsonSerializer.Serialize(before);
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        timing.BeginPresentationFrame(battle, true, clock);
        Capture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        NativeBattlePreparedPresentation first = Frame(1, 0);
        NativeBattlePreparedPresentation second = Frame(2, 10);
        timing.ObservePresentationSource(battle, second);
        List<float> x = [];
        for (int draw = 0; draw < 3; draw++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(4));
            float alpha = timing.SamplePresentationPhase(battle, second, true);
            renderer.RenderNativePilotPrimitiveFrame(first, second, alpha);
            x.Add(capture.Vertices[0].Position.X);
        }
        Assert.True(x[0] < x[1] && x[1] < x[2]);
        Assert.Equal(0, renderer.LastPilotFallbackCount);
        Assert.Empty(events);
        Assert.Equal(before.Frame, battle.Frame);
        Assert.Equal(before.NativeHostCallbacks, battle.NativeHostCallbacks);
        Assert.Equal(serialized, JsonSerializer.Serialize(battle.Capture()));
        Assert.Equal(1, first.Callback);
        Assert.Equal(2, second.Callback);
        Assert.Equal(TimeSpan.Zero, timing.SimulationTotal);
    }

    [Theory]
    [InlineData("session")]
    [InlineData("generation")]
    [InlineData("effect")]
    [InlineData("mode")]
    [InlineData("gap")]
    public void A_held_phase_cannot_escape_its_owner_generation_effect_mode_or_completed_pair(string boundary)
    {
        object owner = new();
        BattlePilotPresentationTimeline timeline = new();
        NativeBattlePreparedPresentation first = Frame(1, 0);
        NativeBattlePreparedPresentation second = Frame(2, 10);
        Assert.Null(timeline.Select(first, true, 0, owner).Next);
        Assert.Equal(0.73f, timeline.Select(second, true, 0.73f, owner).Alpha);
        Assert.Equal(0.73f, timeline.Select(second, true, 0.47f, owner).Alpha);
        NativeBattlePreparedPresentation next;
        switch (boundary)
        {
            case "session":
                owner = new();
                next = second;
                break;
            case "generation":
                next = second with { PilotGeneration = 2 };
                break;
            case "effect":
                next = second with { PilotEffect = 3 };
                break;
            case "mode":
                Assert.Null(timeline.Select(second, false, 0.47f, owner).Next);
                next = second;
                break;
            case "gap":
                next = Frame(4, 30);
                break;
            default:
                throw new InvalidOperationException();
        }
        var reset = timeline.Select(next, true, 0.05f, owner);
        Assert.Null(reset.Next);
        Assert.Equal(0, reset.Alpha);
        Assert.Same(next, reset.Frame);
        var resumed = timeline.Select(next with { Callback = next.Callback + 1 }, true, 0.05f, owner);
        Assert.NotNull(resumed.Next);
        Assert.Equal(0.05f, resumed.Alpha);
    }

    private static NativeBattlePreparedPresentation Frame(long callback, float x)
    {
        NativeBattleLogicalCommand command = new(new("pilot", 0, 0), 1, 0x20, [0x200000FF, Point((int)(160 + x), 108), Point((int)(170 + x), 108), Point((int)(160 + x), 118)], 0, [], null)
        {
            OrderingBucket = 50,
            PilotGeometry = new(new(2, 1, 0, 1, 0), new(x, 0, 100), new(x + 10, 0, 100), new(x, 10, 100), default, 3)
        };
        NativeBattlePreparedPrimitiveFrame primitives = new([new(command, new(0), 1)], []) { LastDescriptorPerspective = false, LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(), LastColorState = NativeBattlePrimitiveColorState.FromColdProcess() };
        return new(callback, callback, callback, false, primitives, null, null, null, new(new(0, 0, 0), new(0, 0, 100), 100, 0), [], null, null, [], null)
        {
            PilotEffect = 2,
            PilotGeneration = 1,
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
