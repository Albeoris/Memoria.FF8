using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedPresentationRandomTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly NativeGlobalRandomState GlobalStart = new(123);
    private static readonly NativeCrtRandomState CrtStart = new(456);

    [Fact]
    public void Main_and_hosted_draws_continue_the_current_streams_in_order()
    {
        BattleSession owner = CreateLogical();
        BattlePresentationProcess process = CreateProcess(owner, [0xC3, 17, 0xE5, 0, 1, 0xC3, 17, 0xE5, 0, 2]);
        NativeBattleRandomState battle = Assert.IsType<NativeBattleRandomState>(owner.Capture().Random);
        NativeBattleUiRandomState? ui = owner.Capture().NativeBattleUiRandom;
        NativeGlobalRandomState expectedGlobal = GlobalStart.Draw(out ushort mainFirst);
        Assert.Equal(mainFirst, DrawGlobal(owner));
        NativeCrtRandomState expectedCrt = CrtStart.Draw(out _);
        SetField(owner, "nativeCrtRandom", expectedCrt);

        Advance(process);

        expectedGlobal = expectedGlobal.Draw(out ushort childFirst);
        Assert.Equal((short)childFirst, process.Camera.Variables[0]);
        AssertStreams(owner, process, expectedGlobal, 2, expectedCrt);
        expectedGlobal = expectedGlobal.Draw(out ushort mainSecond);
        Assert.Equal(mainSecond, DrawGlobal(owner));
        expectedCrt = expectedCrt.Draw(out _);
        SetField(owner, "nativeCrtRandom", expectedCrt);
        Assert.NotEqual((NativeCrtRandomState?)ReadField(owner, "nativeCrtRandom"), process.CurrentCrtRandom);
        Assert.Equal(2UL, process.GlobalRandomCalls);

        Advance(process);

        expectedGlobal = expectedGlobal.Draw(out ushort childSecond);
        Assert.Equal((short)childSecond, process.Camera.Variables[0]);
        AssertStreams(owner, process, expectedGlobal, 4, expectedCrt);
        Assert.Equal(battle, owner.Capture().Random);
        Assert.Equal(ui, owner.Capture().NativeBattleUiRandom);
    }

    [Fact]
    public void Crt_draws_round_trip_through_the_same_session_exchange()
    {
        BattleSession owner = CreateLogical();
        BattlePresentationProcess process = CreateProcess(owner, [2]);
        NativeCrtRandomState expected = CrtStart;
        for (int cycle = 0; cycle < 2; cycle++)
        {
            expected = expected.Draw(out ushort mainValue);
            Assert.Equal(mainValue, DrawCrt(owner));
            TransferIntoProcess(owner, process);
            expected = expected.Draw(out ushort childValue);
            Assert.Equal(childValue, DrawCrt(process));
            Assert.NotEqual(expected, (NativeCrtRandomState?)ReadField(owner, "nativeCrtRandom"));
            TransferIntoBattle(process, owner);
            Assert.Equal(expected, (NativeCrtRandomState?)ReadField(owner, "nativeCrtRandom"));
        }
    }

    [Fact]
    public void Before_core_sees_three_polls_and_graphics_uses_its_current_owner_facts()
    {
        BattleSession owner = CreateLogical();
        BattlePresentationProcess process = CreateProcess(owner, [0xC3, 17, 0xE5, 0, 2]);
        NativeBattleGraphicsState graphics = process.Graphics;
        SetProperty(process, nameof(BattlePresentationProcess.NativeGraphics), graphics with { UiServices = NativeBattleUiServices.Initialize().QueuePublication(3) });
        NativeGlobalRandomState expected = GlobalStart.Draw(out ushort mainDraw).Draw(out ushort childDraw);

        NativeBattleModelLifecycleServices services = Advance(process, beforeCore: () =>
        {
            Assert.Equal(3u, process.Graphics.UiServices!.Counter);
            Assert.True(process.Graphics.UiServices.Frame33);
            Assert.Equal(0, process.Graphics.Callback);
            Assert.Null(ReadField(process, "hostedFrameServices"));
            Assert.Throws<InvalidOperationException>(() => Advance(process));
            Assert.Throws<InvalidOperationException>(() => process.AdvanceHostedBattlePresentationFrame(null));
            Assert.Equal(mainDraw, DrawGlobal(owner));
            owner.Combatants[0].CurrentHp = 1;
        });

        Assert.Equal(4u, process.Graphics.UiServices!.Counter);
        Assert.False(process.Graphics.UiServices.Frame33);
        Assert.Equal(1, process.Graphics.Callback);
        Assert.Equal(1, services.Facts[OriginalOwner(0)]!.CurrentHp);
        Assert.Same(services, process.LastFrameServices);
        Assert.Equal((short)childDraw, process.Camera.Variables[0]);
        AssertStreams(owner, process, expected, 2, CrtStart);
    }

    [Fact]
    public void Failed_after_graphics_handoff_preserves_rng_and_timer_and_releases_the_frame_guards()
    {
        BattleSession owner = CreateLogical();
        BattlePresentationProcess process = CreateProcess(owner, [1, 2]);
        NativeBattleGraphicsState graphics = process.Graphics;
        SetProperty(process, nameof(BattlePresentationProcess.NativeGraphics), graphics with { UiServices = NativeBattleUiServices.Initialize() });
        NativeExamTimerState timer = NativeExamTimerState.FromColdProcess().SetRemaining(100).Display(4, 5);
        SetProperty(owner, nameof(BattleSession.NativeExamTimer), timer);
        SetField(owner, "expandedPresentationProcess", process);
        InvalidOperationException injected = new("Post-graphics canonical callback failed.");
        int called = 0;

        Assert.Same(injected, Assert.Throws<InvalidOperationException>(() => Advance(process, afterGraphics: (services, token) =>
        {
            called++;
            Assert.Null(token);
            Assert.Null(ReadField(process, "hostedFrameServices"));
            Assert.NotNull(ReadField(process, "nativePreRootStack"));
            Assert.Equal(1, process.Graphics.Callback);
            Assert.Equal(3u, process.Graphics.UiServices!.Counter);
            Assert.Throws<InvalidOperationException>(() => process.AdvanceHostedBattlePresentationFrame(timer));
            Assert.Throws<InvalidOperationException>(() => Advance(process));
            Assert.Throws<InvalidOperationException>(() => process.AccumulateElapsed(TimeSpan.FromSeconds(1)));
            Assert.Throws<InvalidOperationException>(() => process.SetActionMenu(true));
            DrawGlobal(owner);
            DrawCrt(owner);
            typeof(BattleSession).GetMethod("AdvanceNativeHostTime", PrivateInstance)!.Invoke(owner, []);
            throw injected;
        })));

        Assert.Equal(1, called);
        Assert.Null(process.LastFrameServices);
        Assert.Null(ReadField(process, "hostedFrameServices"));
        Assert.Null(ReadField(process, "nativePreRootStack"));
        Assert.False((bool)ReadField(process, "canonicalHandoff")!);
        AssertStreams(owner, process, GlobalStart.Advance(1), 1, CrtStart.Draw(out _));
        Assert.True(owner.NativeExamTimer!.HasSameClock(timer.AdvanceCallbacks(NativeTimerCallbackKind.Battle, 1)));
        Assert.Same(owner.NativeExamTimer, process.Timer);

        Advance(process);
        Assert.Equal(2, process.Graphics.Callback);
        Assert.Equal(7u, process.Graphics.UiServices!.Counter);
        Assert.Equal(1, called);
    }

    [Fact]
    public void Failed_before_core_preserves_main_draws_and_its_original_exception()
    {
        BattleSession owner = CreateLogical();
        BattlePresentationProcess process = CreateProcess(owner, [2]);
        NativeBattleGraphicsState graphics = process.Graphics;
        SetProperty(process, nameof(BattlePresentationProcess.NativeGraphics), graphics with { UiServices = NativeBattleUiServices.Initialize() });
        InvalidOperationException injected = new("Main callback failed after drawing.");
        NativeBattleUiRandomState mainUi = owner.Capture().NativeBattleUiRandom!.Draw(out _);

        Assert.Same(injected, Assert.Throws<InvalidOperationException>(() => Advance(process, beforeCore: () =>
        {
            DrawGlobal(owner);
            DrawCrt(owner);
            SetField(owner, "nativeBattleUiRandom", mainUi);
            throw injected;
        })));

        Assert.Equal(3u, process.Graphics.UiServices!.Counter);
        Assert.Equal(0, process.Graphics.Callback);
        Assert.Null(process.LastFrameServices);
        Assert.Null(ReadField(process, "hostedFrameServices"));
        AssertStreams(owner, process, GlobalStart.Advance(1), 1, CrtStart.Draw(out _));
        Assert.Equal(mainUi, owner.Capture().NativeBattleUiRandom);
        Assert.Equal(mainUi, ReadField(process, "nativeBattleUiRandom"));
        Advance(process);
        Assert.Equal(1, process.Graphics.Callback);
    }

    [Fact]
    public void Hosted_widget_draw_precedes_canonical_ui_rng_and_a_later_hook_failure_preserves_both()
    {
        BattleSession owner = CreateLogical();
        BattlePresentationProcess process = CreateProcess(owner, [2]);
        BattleDefinition selected = owner.ReadDefinition() with { LimitProgress = new(new string('0', 64), 0, 0, 0, 0, [0, 0, 0, 0, 0, 0]) { Options978 = 1 } };
        SetField(process, "resources", new BattlePresentationResources(selected));
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleUiServices ui = NativeBattleUiServices.Initialize();
        BattleNativeRenzokukenBlock block = new(0, [16, 9], NativeBattleRenzokukenRules.Timeline([16, 9]));
        NativeBattleTriggerWidget widget = NativeBattleTriggerWidget.Register(owner.ReadPresentationModelOwner("ally"), block, 1, ui, graphics.ActorModels.Roster) with { Input = new(6, 68, 2, 0, 0) };
        SetProperty(process, nameof(BattlePresentationProcess.NativeGraphics), graphics with { UiServices = ui, TriggerWidget = widget });
        NativeBattleUiRandomState childDraw = owner.Capture().NativeBattleUiRandom!.Draw(out _);
        NativeBattleUiRandomState mainDraw = childDraw.Draw(out _);
        NativeBattleRandomState? beforeBattleRandom = owner.Capture().Random;
        InvalidOperationException injected = new("The canonical hook failed after its UI draw.");

        Assert.Same(injected, Assert.Throws<InvalidOperationException>(() => Advance(process, beforeCore: () =>
        {
            Assert.Equal(childDraw, owner.Capture().NativeBattleUiRandom);
            Assert.Equal(3u, process.Graphics.UiServices!.Counter);
            SetField(owner, "nativeBattleUiRandom", mainDraw);
            throw injected;
        })));
        Assert.Equal(mainDraw, owner.Capture().NativeBattleUiRandom);
        Assert.Equal(mainDraw, ReadField(process, "nativeBattleUiRandom"));
        Assert.Equal(beforeBattleRandom, owner.Capture().Random);
        Assert.Null(process.LastFrameServices);
    }

    [Fact]
    public void Failed_camera_callback_exports_consumed_random_without_publishing_a_frame()
    {
        BattleSession owner = CreateLogical();
        BattlePresentationProcess process = CreateProcess(owner, [1, 0xC3, 17]);
        Advance(process);
        NativeBattlePreparedPresentation? beforeFrame = process.PreparedPresentation;
        NativeBattleEffectPresentationStep beforeStep = process.LastStep;
        long beforeCallback = process.Graphics.Callback;
        DrawGlobal(owner);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => Advance(process));

        Assert.Contains("camera control cursor", error.Message);
        Assert.Equal(beforeCallback + 1, process.Graphics.Callback);
        Assert.Same(beforeFrame, process.PreparedPresentation);
        Assert.Same(beforeStep, process.LastStep);
        Assert.False((bool)ReadField(process, "advancing")!);
        AssertStreams(owner, process, GlobalStart.Advance(2), 2, CrtStart);
        NativeGlobalRandomState expected = GlobalStart.Advance(2).Draw(out ushort next);
        Assert.Equal(next, DrawGlobal(owner));
        Assert.Equal(expected, owner.NativeGlobalRandomChange!.After);
    }

    [Fact]
    public void Reentrant_advance_cannot_overwrite_already_consumed_child_draws()
    {
        BattleSession owner = CreateLogical();
        BattlePresentationProcess process = CreateProcess(owner, [2]);
        NativeBattleGraphicsState graphics = process.Graphics;
        DrawGlobal(process);
        DrawCrt(process);
        SetField(process, "advancing", true);

        Assert.Throws<InvalidOperationException>(() => Advance(process));

        Assert.Same(graphics, process.Graphics);
        Assert.Equal(new NativeGlobalRandomChange(GlobalStart, GlobalStart, 0), owner.NativeGlobalRandomChange);
        Assert.Equal(CrtStart, (NativeCrtRandomState?)ReadField(owner, "nativeCrtRandom"));
        Assert.Equal(new NativeGlobalRandomChange(GlobalStart, GlobalStart.Advance(1), 1), process.GlobalRandomChange);
        Assert.Equal(CrtStart.Draw(out _), process.CurrentCrtRandom);
        Assert.True((bool)ReadField(process, "advancing")!);
    }

    [Fact]
    public void A_missing_shared_crt_stream_fails_before_importing_any_state()
    {
        BattleSession owner = CreateLogical(crt: false);
        BattlePresentationProcess process = CreateProcess(owner, [2]);
        NativeBattleGraphicsState before = process.Graphics;
        NativeGlobalRandomChange? ownerRandom = owner.NativeGlobalRandomChange;
        NativeCrtRandomState? ownerCrt = (NativeCrtRandomState?)ReadField(owner, "nativeCrtRandom");

        Assert.Throws<InvalidDataException>(() => Advance(process));

        Assert.Same(before, process.Graphics);
        Assert.Equal(new NativeGlobalRandomChange(GlobalStart, GlobalStart, 0), process.GlobalRandomChange);
        Assert.Equal(CrtStart, process.CurrentCrtRandom);
        Assert.Equal(ownerRandom, owner.NativeGlobalRandomChange);
        Assert.Equal(ownerCrt, (NativeCrtRandomState?)ReadField(owner, "nativeCrtRandom"));
        Assert.False((bool)ReadField(process, "advancing")!);
    }

    private static void AssertStreams(BattleSession owner, BattlePresentationProcess process, NativeGlobalRandomState after, ulong calls, NativeCrtRandomState crt)
    {
        NativeGlobalRandomChange expected = new(GlobalStart, after, calls);
        Assert.Equal(expected, owner.NativeGlobalRandomChange);
        Assert.Equal(expected, process.GlobalRandomChange);
        owner.NativeGlobalRandomChange!.Validate();
        Assert.Equal(crt, (NativeCrtRandomState?)ReadField(owner, "nativeCrtRandom"));
        Assert.Equal(crt, process.CurrentCrtRandom);
    }

    private static BattleSession CreateLogical(NativeGlobalRandomState? seed = null, bool crt = true) => new(new BattleDefinition
    {
        Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }],
        NativeGlobalRandom = seed ?? GlobalStart,
        NativeCrtRandom = crt ? CrtStart : null,
        NativeBattleUiRandom = new(789)
    }, BattleAtbMode.TurnBased);

    private static BattlePresentationProcess CreateProcess(BattleSession owner, ImmutableArray<byte> program)
    {
        BattleDefinition selected = owner.ReadDefinition() with
        {
            NativeStageCameraControl = program,
            NativeStageCameraBank = new([]),
            NativeCrtRandom = CrtStart,
            NativeGlobalRandom = GlobalStart
        };
        var (roster, aliases) = owner.ReadBattlePresentationPacketBindings();
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, _ => NativeBattleGraphicsModels.ClearUnloadedModel(null, null, null, null, null));
        BattlePresentationProcess process = NativeBattleHostedTestFixture.SeedProcess(owner, graphics with
        {
            EntryLifecycle = 4,
            EntryReady = true,
            PrimitiveFrame = NativeBattlePrimitiveFrame.Clear()
        }, selected);
        NativeBattleHostedTestFixture.SetProperty(process, nameof(BattlePresentationProcess.NativeFileIo), NativeBattleFileIoState.FromColdProcess());
        NativeBattleHostedTestFixture.SetProperty(process, nameof(BattlePresentationProcess.NativeEffectMemory), NativeBattleEffectMemory.FromColdProcess());
        SetProperty(process, nameof(BattlePresentationProcess.NativeCamera), NativeBattleCameraState.FromColdProcess() with { ControlCursor = 0 });
        NativeBattleHostedTestFixture.SetProperty(process, nameof(BattlePresentationProcess.NativeStage), NativeBattleStageState.FromColdProcess());
        SetProperty(process, nameof(BattlePresentationProcess.NativeMusicProcess), new NativeBattleMusicProcessState(new(1, null, null, null, null, null), 0, null).InitializeCommands501C60());
        return process;
    }

    private static NativeBattleModelLifecycleServices Advance(BattlePresentationProcess process, Action? beforeCore = null, Action<NativeBattleModelLifecycleServices, long?>? afterGraphics = null)
    {
        return process.Advance(beforeCore ?? (() => { }), afterGraphics ?? ((_, _) => { }));
    }

    private static void TransferIntoProcess(BattleSession owner, BattlePresentationProcess process)
    {
        object shared = typeof(BattleSession).GetMethod("ObserveBattlePresentationSharedState", PrivateInstance)!.Invoke(owner, [])!;
        typeof(BattlePresentationProcess).GetMethod("ImportSharedState", PrivateInstance)!.Invoke(process, [shared]);
    }

    private static void TransferIntoBattle(BattlePresentationProcess process, BattleSession owner)
    {
        object shared = typeof(BattlePresentationProcess).GetMethod("ObserveSharedState", PrivateInstance)!.Invoke(process, [])!;
        typeof(BattleSession).GetMethod("PublishBattlePresentationSharedState", PrivateInstance)!.Invoke(owner, [shared]);
    }

    private static ushort DrawGlobal(object owner)
    {
        return (ushort)owner.GetType().GetMethod("NextNativeGlobalRandom", PrivateInstance)!.Invoke(owner, [])!;
    }

    private static ushort DrawCrt(object owner)
    {
        NativeCrtRandomState state = (NativeCrtRandomState)ReadField(owner, "nativeCrtRandom")!;
        SetField(owner, "nativeCrtRandom", state.Draw(out ushort result));
        return result;
    }

    private static object? ReadField(object owner, string name) => owner.GetType().GetField(name, PrivateInstance)!.GetValue(owner);
    private static void SetField(object owner, string name, object? value) => owner.GetType().GetField(name, PrivateInstance)!.SetValue(owner, value);
    private static void SetProperty(object owner, string name, object value) => owner.GetType().GetProperty(name)!.SetValue(owner, value);
}
