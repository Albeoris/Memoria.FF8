using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleRenderPhaseClockTests
{
    [Fact]
    public void Repeated_draws_sample_distinct_phases_without_another_fixed_update()
    {
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        object owner = new();
        Admit(timing, clock, owner);
        timing.Accumulate(TimeSpan.FromMilliseconds(5));
        Assert.False(timing.TryStep());
        float first = BattleScenePresentation.NativePilotAlpha(2_000_000, false, timing.RenderElapsed(owner, true));
        clock.Advance(TimeSpan.FromMilliseconds(4));
        float second = BattleScenePresentation.NativePilotAlpha(2_000_000, false, timing.RenderElapsed(owner, true));
        clock.Advance(TimeSpan.FromMilliseconds(4));
        float third = BattleScenePresentation.NativePilotAlpha(2_000_000, false, timing.RenderElapsed(owner, true));
        Assert.Equal(0.2775f, first, 6);
        Assert.Equal(0.3395f, second, 6);
        Assert.Equal(0.4015f, third, 6);
        Assert.Equal(TimeSpan.Zero, timing.SimulationTotal);
        Assert.False(timing.TryStep());
    }

    [Fact]
    public void Pending_remainder_and_pacer_update_work_are_included_once_and_rebased_at_the_next_host_tick()
    {
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        object owner = new();
        Admit(timing, clock, owner);
        timing.Accumulate(TimeSpan.FromMilliseconds(20));
        clock.Advance(TimeSpan.FromMilliseconds(4));
        timing.RunUpdates(PresentationTiming.SimulationStep, () => PresentationTiming.SimulationStep, _ =>
        {
            clock.Advance(TimeSpan.FromMilliseconds(3));
            timing.AdmitPresentationOwner(owner);
            return true;
        }, clock);
        TimeSpan first = TimeSpan.FromMilliseconds(20) - PresentationTiming.SimulationStep + TimeSpan.FromMilliseconds(7);
        Assert.Equal(first, timing.RenderElapsed(owner, true));
        Assert.Equal(PresentationTiming.SimulationStep, timing.SimulationTotal);
        timing.BeginPresentationFrame(owner, true, clock);
        timing.Accumulate(TimeSpan.FromMilliseconds(7));
        Assert.Equal(first, timing.RenderElapsed(owner, true));
        clock.Advance(TimeSpan.FromMilliseconds(2));
        Assert.Equal(first + TimeSpan.FromMilliseconds(2), timing.RenderElapsed(owner, true));
    }

    [Fact]
    public void Native_half_tick_and_default_phase_are_preserved_and_unproduced_callbacks_are_clamped_not_wrapped()
    {
        Assert.Equal(0.25f, BattleScenePresentation.NativePilotAlpha(2_500_000, false, TimeSpan.Zero));
        Assert.Equal(0.00000005f, BattleScenePresentation.NativePilotAlpha(0, true, TimeSpan.Zero));
        Assert.Equal(1f, BattleScenePresentation.NativePilotAlpha(9_000_000, true, TimeSpan.FromMilliseconds(20)));
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleScenePresentation.NativePilotAlpha(0, false, TimeSpan.FromTicks(-1)));
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        object owner = new();
        Admit(timing, clock, owner);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(PresentationTiming.MaximumElapsed, timing.RenderElapsed(owner, true));
        Assert.Equal(TimeSpan.Zero, timing.SimulationTotal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pause_at_host_entry_or_during_update_excludes_wait_and_resumes_only_after_native_presentation_is_readmitted(bool pausedAtHostEntry)
    {
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        object owner = new();
        Admit(timing, clock, owner);
        timing.Accumulate(TimeSpan.FromMilliseconds(5));
        clock.Advance(TimeSpan.FromMilliseconds(2));
        Assert.Equal(TimeSpan.FromMilliseconds(7), timing.RenderElapsed(owner, true));
        timing.BeginPresentationFrame(owner, !pausedAtHostEntry, clock);
        clock.Advance(TimeSpan.FromSeconds(10));
        timing.AdmitPresentationOwner(owner);
        Assert.Equal(TimeSpan.Zero, timing.RenderElapsed(owner, false));
        timing.BeginPresentationFrame(owner, true, clock);
        Assert.Equal(TimeSpan.Zero, timing.RenderElapsed(owner, true));
        timing.AdmitPresentationOwner(owner);
        Assert.Equal(TimeSpan.Zero, timing.RenderElapsed(owner, true));
        timing.BeginPresentationFrame(owner, true, clock);
        clock.Advance(TimeSpan.FromMilliseconds(3));
        Assert.Equal(TimeSpan.FromMilliseconds(8), timing.RenderElapsed(owner, true));
    }

    [Fact]
    public void Capture_fence_or_focus_discard_cancels_current_carry_and_a_new_session_cannot_inherit_it()
    {
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        object first = new();
        object second = new();
        Admit(timing, clock, first);
        timing.Accumulate(TimeSpan.FromMilliseconds(5));
        clock.Advance(TimeSpan.FromMilliseconds(2));
        timing.FreezePresentationFrame();
        Assert.Equal(TimeSpan.Zero, timing.RenderElapsed(first, true));
        timing.BeginPresentationFrame(first, true, clock);
        timing.AdmitPresentationOwner(first);
        timing.BeginPresentationFrame(first, true, clock);
        Assert.Equal(TimeSpan.FromMilliseconds(5), timing.RenderElapsed(first, true));
        Assert.Equal(TimeSpan.Zero, timing.RenderElapsed(second, true));
        timing.BeginPresentationFrame(second, true, clock);
        Assert.Equal(TimeSpan.Zero, timing.RenderElapsed(second, true));
        timing.AdmitPresentationOwner(second);
        timing.BeginPresentationFrame(second, true, clock);
        timing.DiscardPendingTime();
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.Zero, timing.RenderElapsed(second, true));
        timing.BeginPresentationFrame(second, true, clock);
        timing.AdmitPresentationOwner(second);
        timing.BeginPresentationFrame(second, true, clock);
        clock.Advance(TimeSpan.FromMilliseconds(3));
        Assert.Equal(TimeSpan.FromMilliseconds(3), timing.RenderElapsed(second, true));
    }

    [Fact]
    public void Persistent_field_triple_speed_does_not_accelerate_render_phase_when_admitted_native_elapsed_is_unscaled()
    {
        GameplayClock gameplay = new();
        gameplay.ToggleSpeed();
        Assert.Equal(3, gameplay.SpeedMultiplier);
        TimeSpan real = TimeSpan.FromMilliseconds(10);
        Assert.Equal(real * 3, gameplay.Advance(real, SceneKind.Field));
        TimeSpan battle = gameplay.Advance(real, SceneKind.Field, scriptedInterface: true);
        Assert.Equal(real, battle);
        NativeBattleGraphicsPresentationStep native = NativeBattleGraphicsPresentationClock.Advance(0, false, battle);
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        object owner = new();
        Admit(timing, clock, owner);
        timing.Accumulate(real);
        timing.AdmitPresentationOwner(owner, battle.Ticks / (double)real.Ticks);
        Assert.Equal(0.155f, BattleScenePresentation.NativePilotAlpha(0, false, timing.RenderElapsed(owner, true)), 6);
        Assert.Equal(0.155f, BattleScenePresentation.NativePilotAlpha(native.Subticks, native.HalfSubtick, TimeSpan.Zero), 6);
        Assert.Equal(0, native.Callbacks);
    }

    [Fact]
    public void Render_carry_uses_the_actual_admitted_native_elapsed_ratio_when_battle_updates_are_accelerated()
    {
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        object owner = new();
        Admit(timing, clock, owner);
        timing.AdmitPresentationOwner(owner, 3);
        timing.Accumulate(TimeSpan.FromMilliseconds(5));
        clock.Advance(TimeSpan.FromMilliseconds(2));
        Assert.Equal(TimeSpan.FromMilliseconds(21), timing.RenderElapsed(owner, true));
        Assert.Equal(0.3255f, BattleScenePresentation.NativePilotAlpha(0, false, timing.RenderElapsed(owner, true)), 6);
        timing.AdmitPresentationOwner(owner, 1);
        Assert.Equal(TimeSpan.FromMilliseconds(7), timing.RenderElapsed(owner, true));
        Assert.Equal(TimeSpan.Zero, timing.SimulationTotal);
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
        Admit(timing, clock, battle);
        Capture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        NativeBattlePreparedPresentation first = Frame(1, 0);
        NativeBattlePreparedPresentation second = Frame(2, 10);
        List<float> x = [];
        for (int draw = 0; draw < 3; draw++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(4));
            float alpha = BattleScenePresentation.NativePilotAlpha(2_000_000, false, timing.RenderElapsed(battle, true));
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
    [InlineData("pause")]
    [InlineData("capture")]
    [InlineData("focus")]
    public void Pause_capture_and_focus_boundaries_hold_the_last_visible_pose_until_same_pair_time_catches_up(string boundary)
    {
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        object owner = new();
        Admit(timing, clock, owner);
        NativeBattlePreparedPresentation first = Frame(1, 0);
        NativeBattlePreparedPresentation second = Frame(2, 10);
        BattlePilotPresentationTimeline timeline = new();
        Assert.Null(timeline.Select(first, true, 0, owner).Next);
        Capture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        clock.Advance(TimeSpan.FromMilliseconds(17));
        float visibleAlpha = BattleScenePresentation.NativePilotAlpha(4_700_000, false, timing.RenderElapsed(owner, true));
        TriangleVertex[] visible = Draw(visibleAlpha);
        Assert.Equal(0.7335f, visibleAlpha, 6);
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
            default:
                throw new InvalidOperationException();
        }
        clock.Advance(TimeSpan.FromSeconds(10));
        float nativeOnly = BattleScenePresentation.NativePilotAlpha(4_700_000, false, timing.RenderElapsed(owner, false));
        Assert.Equal(0.47f, nativeOnly, 6);
        Assert.Equal(visible, Draw(nativeOnly));
        timing.BeginPresentationFrame(owner, true, clock);
        timing.AdmitPresentationOwner(owner);
        timing.BeginPresentationFrame(owner, true, clock);
        clock.Advance(TimeSpan.FromMilliseconds(2));
        Assert.Equal(visible, Draw(BattleScenePresentation.NativePilotAlpha(4_700_000, false, timing.RenderElapsed(owner, true))));
        clock.Advance(TimeSpan.FromMilliseconds(20));
        TriangleVertex[] caughtUp = Draw(BattleScenePresentation.NativePilotAlpha(4_700_000, false, timing.RenderElapsed(owner, true)));
        Assert.True(caughtUp[0].Position.X > visible[0].Position.X);
        var nextPair = timeline.Select(Frame(3, 20), true, 0.05f, owner);
        Assert.Equal(0.05f, nextPair.Alpha);
        Assert.Same(second, nextPair.Frame);
        Assert.Equal(3, nextPair.Next!.Callback);
        Assert.Equal(TimeSpan.Zero, timing.SimulationTotal);

        TriangleVertex[] Draw(float alpha)
        {
            var selected = timeline.Select(second, true, alpha, owner);
            Assert.NotNull(selected.Next);
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

    private static void Admit(PresentationTiming timing, ManualTimeProvider clock, object owner)
    {
        timing.BeginPresentationFrame(owner, true, clock);
        timing.AdmitPresentationOwner(owner);
        timing.BeginPresentationFrame(owner, true, clock);
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
