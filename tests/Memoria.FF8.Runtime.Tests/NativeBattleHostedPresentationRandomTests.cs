using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;

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
        BattleSession child = CreateChild(owner, [0xC3, 17, 0xE5, 0, 1, 0xC3, 17, 0xE5, 0, 2]);
        ExpandedBattlePresentationProcess process = CreateFacade(owner, child);
        NativeBattleRandomState battle = Assert.IsType<NativeBattleRandomState>(owner.Capture().Random);
        NativeBattleUiRandomState? ui = owner.Capture().NativeBattleUiRandom;
        NativeGlobalRandomState expectedGlobal = GlobalStart.Draw(out ushort mainFirst);
        Assert.Equal(mainFirst, DrawGlobal(owner));
        NativeCrtRandomState expectedCrt = CrtStart.Draw(out _);
        SetField(owner, "nativeCrtRandom", expectedCrt);

        process.Advance(owner);

        expectedGlobal = expectedGlobal.Draw(out ushort childFirst);
        Assert.Equal((short)childFirst, process.Camera.Variables[0]);
        AssertStreams(owner, process, expectedGlobal, 2, expectedCrt);
        expectedGlobal = expectedGlobal.Draw(out ushort mainSecond);
        Assert.Equal(mainSecond, DrawGlobal(owner));
        expectedCrt = expectedCrt.Draw(out _);
        SetField(owner, "nativeCrtRandom", expectedCrt);
        Assert.NotEqual(owner.CurrentCrtRandom, process.CurrentCrtRandom);
        Assert.Equal(2UL, process.GlobalRandomCalls);

        process.Advance(owner);

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
        BattleSession child = CreateChild(owner, [2]);
        NativeCrtRandomState expected = CrtStart;
        for (int cycle = 0; cycle < 2; cycle++)
        {
            expected = expected.Draw(out ushort mainValue);
            Assert.Equal(mainValue, DrawCrt(owner));
            child.ImportHostedPresentationRandom(owner);
            expected = expected.Draw(out ushort childValue);
            Assert.Equal(childValue, DrawCrt(child));
            Assert.NotEqual(expected, owner.CurrentCrtRandom);
            child.ExportHostedPresentationRandom(owner);
            Assert.Equal(expected, owner.CurrentCrtRandom);
        }
    }

    [Fact]
    public void Before_core_sees_three_polls_and_graphics_uses_its_current_owner_facts()
    {
        BattleSession owner = CreateLogical();
        BattleSession child = CreateChild(owner, [0xC3, 17, 0xE5, 0, 2]);
        NativeBattleGraphicsState graphics = child.NativeGraphics!;
        SetProperty(child, nameof(BattleSession.NativeGraphics), graphics with { UiServices = NativeBattleUiServices.Initialize().QueuePublication(3) });
        ExpandedBattlePresentationProcess process = CreateFacade(owner, child);
        NativeGlobalRandomState expected = GlobalStart.Draw(out ushort mainDraw).Draw(out ushort childDraw);

        NativeBattleModelLifecycleServices services = process.Advance(owner, beforeCore: () =>
        {
            Assert.Equal(3u, process.Graphics.UiServices!.Counter);
            Assert.True(process.Graphics.UiServices.Frame33);
            Assert.Equal(0, process.Graphics.Callback);
            Assert.Null(ReadField(child, "hostedFrameServices"));
            Assert.Throws<InvalidOperationException>(() => process.Advance(owner));
            Assert.Throws<InvalidOperationException>(() => child.AdvanceHostedBattlePresentationFrame(owner, null));
            Assert.Equal(mainDraw, DrawGlobal(owner));
            owner.Combatants[0].CurrentHp = 1;
        });

        Assert.Equal(4u, process.Graphics.UiServices!.Counter);
        Assert.False(process.Graphics.UiServices.Frame33);
        Assert.Equal(1, process.Graphics.Callback);
        Assert.Equal(1, services.Facts[0]!.CurrentHp);
        Assert.Same(services, process.LastFrameServices);
        Assert.Equal((short)childDraw, process.Camera.Variables[0]);
        AssertStreams(owner, process, expected, 2, CrtStart);
    }

    [Fact]
    public void Failed_before_core_preserves_main_draws_and_its_original_exception()
    {
        BattleSession owner = CreateLogical();
        BattleSession child = CreateChild(owner, [2]);
        NativeBattleGraphicsState graphics = child.NativeGraphics!;
        SetProperty(child, nameof(BattleSession.NativeGraphics), graphics with { UiServices = NativeBattleUiServices.Initialize() });
        ExpandedBattlePresentationProcess process = CreateFacade(owner, child);
        InvalidOperationException injected = new("Main callback failed after drawing.");

        Assert.Same(injected, Assert.Throws<InvalidOperationException>(() => process.Advance(owner, beforeCore: () =>
        {
            DrawGlobal(owner);
            DrawCrt(owner);
            throw injected;
        })));

        Assert.Equal(3u, process.Graphics.UiServices!.Counter);
        Assert.Equal(0, process.Graphics.Callback);
        Assert.Null(process.LastFrameServices);
        Assert.Null(ReadField(child, "hostedFrameServices"));
        AssertStreams(owner, process, GlobalStart.Advance(1), 1, CrtStart.Draw(out _));
        process.Advance(owner);
        Assert.Equal(1, process.Graphics.Callback);
    }

    [Fact]
    public void Failed_camera_callback_exports_consumed_random_without_publishing_a_frame()
    {
        BattleSession owner = CreateLogical();
        BattleSession child = CreateChild(owner, [1, 0xC3, 17]);
        ExpandedBattlePresentationProcess process = CreateFacade(owner, child);
        process.Advance(owner);
        NativeBattlePreparedPresentation? beforeFrame = process.PreparedPresentation;
        NativeBattleEffectPresentationStep beforeStep = process.LastStep;
        long beforeCallback = process.Graphics.Callback;
        DrawGlobal(owner);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => process.Advance(owner));

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
        BattleSession child = CreateChild(owner, [2]);
        ExpandedBattlePresentationProcess process = CreateFacade(owner, child);
        NativeBattleGraphicsState graphics = process.Graphics;
        DrawGlobal(child);
        DrawCrt(child);
        SetField(process, "advancing", true);

        Assert.Throws<InvalidOperationException>(() => process.Advance(owner));

        Assert.Same(graphics, process.Graphics);
        Assert.Equal(new NativeGlobalRandomChange(GlobalStart, GlobalStart, 0), owner.NativeGlobalRandomChange);
        Assert.Equal(CrtStart, owner.CurrentCrtRandom);
        Assert.Equal(new NativeGlobalRandomChange(GlobalStart, GlobalStart.Advance(1), 1), process.GlobalRandomChange);
        Assert.Equal(CrtStart.Draw(out _), process.CurrentCrtRandom);
        Assert.True((bool)ReadField(process, "advancing")!);
    }

    [Fact]
    public void An_equal_roster_on_another_session_cannot_advance_or_import_random()
    {
        BattleSession owner = CreateLogical();
        BattleSession other = CreateLogical();
        BattleSession child = CreateChild(owner, [2]);
        ExpandedBattlePresentationProcess process = CreateFacade(owner, child);
        NativeBattleGraphicsState before = process.Graphics;
        DrawGlobal(other);
        DrawCrt(other);
        NativeGlobalRandomChange? otherRandom = other.NativeGlobalRandomChange;
        NativeCrtRandomState? otherCrt = other.CurrentCrtRandom;

        Assert.Throws<InvalidOperationException>(() => process.Advance(other));

        Assert.Same(before, process.Graphics);
        Assert.Equal(otherRandom, other.NativeGlobalRandomChange);
        Assert.Equal(otherCrt, other.CurrentCrtRandom);
        AssertStreams(owner, process, GlobalStart, 0, CrtStart);
    }

    [Theory]
    [InlineData("seed")]
    [InlineData("crt")]
    [InlineData("count")]
    public void Incompatible_random_contract_fails_before_importing_any_state(string invalid)
    {
        BattleSession owner = CreateLogical(seed: invalid == "seed" ? new(321) : GlobalStart, crt: invalid != "crt");
        BattleSession child = CreateChild(owner, [2]);
        ExpandedBattlePresentationProcess process = CreateFacade(owner, child);
        if (invalid == "count")
        {
            SetField(owner, "nativeGlobalRandomCalls", 2UL);
        }
        NativeBattleGraphicsState before = process.Graphics;
        NativeGlobalRandomChange? ownerRandom = owner.NativeGlobalRandomChange;
        NativeCrtRandomState? ownerCrt = owner.CurrentCrtRandom;

        Assert.Throws<InvalidDataException>(() => process.Advance(owner));

        Assert.Same(before, process.Graphics);
        Assert.Equal(new NativeGlobalRandomChange(GlobalStart, GlobalStart, 0), process.GlobalRandomChange);
        Assert.Equal(CrtStart, process.CurrentCrtRandom);
        Assert.Equal(ownerRandom, owner.NativeGlobalRandomChange);
        Assert.Equal(ownerCrt, owner.CurrentCrtRandom);
        Assert.False((bool)ReadField(process, "advancing")!);
    }

    private static void AssertStreams(BattleSession owner, ExpandedBattlePresentationProcess process, NativeGlobalRandomState after, ulong calls, NativeCrtRandomState crt)
    {
        NativeGlobalRandomChange expected = new(GlobalStart, after, calls);
        Assert.Equal(expected, owner.NativeGlobalRandomChange);
        Assert.Equal(expected, process.GlobalRandomChange);
        owner.NativeGlobalRandomChange!.Validate();
        Assert.Equal(crt, owner.CurrentCrtRandom);
        Assert.Equal(crt, process.CurrentCrtRandom);
    }

    private static BattleSession CreateLogical(NativeGlobalRandomState? seed = null, bool crt = true) => new(new BattleDefinition
    {
        UsesExpandedRoster = true,
        Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }],
        NativeGlobalRandom = seed ?? GlobalStart,
        NativeCrtRandom = crt ? CrtStart : null,
        NativeBattleUiRandom = new(789)
    }, BattleAtbMode.TurnBased);

    private static BattleSession CreateChild(BattleSession owner, ImmutableArray<byte> program)
    {
        BattleSession child = CreateLogical();
        BattleDefinition original = (BattleDefinition)ReadField(child, "definition")!;
        SetField(child, "definition", original with { NativeStageCameraControl = program, NativeStageCameraBank = new([]) });
        SetField(child, "hostedBattlePresentation", true);
        FieldInfo descriptors = typeof(BattleSession).GetField("hostedEntryDescriptors", PrivateInstance)!;
        descriptors.SetValue(child, Activator.CreateInstance(descriptors.FieldType));
        NativeBattleGraphicsRoster roster = owner.ReadExpandedPresentationRoster();
        SetProperty(child, nameof(BattleSession.NativeGraphics), new NativeBattleGraphicsState(NativeBattleGraphicsModels.ClearPresentationBank(roster).ToImmutableArray(), roster.Rows.Select(actor => actor?.ActorId).ToImmutableArray(), 0, 0)
        {
            PresentationRoster = roster,
            EntryLifecycle = 4,
            EntryReady = true,
            PrimitiveFrame = NativeBattlePrimitiveFrame.Clear()
        });
        SetProperty(child, nameof(BattleSession.NativeFileIo), NativeBattleFileIoState.FromColdProcess());
        SetProperty(child, nameof(BattleSession.NativeEffectMemory), NativeBattleEffectMemory.FromColdProcess());
        SetProperty(child, nameof(BattleSession.NativeCamera), NativeBattleCameraState.FromColdProcess() with { ControlCursor = 0 });
        SetProperty(child, nameof(BattleSession.NativeStage), NativeBattleStageState.FromColdProcess());
        SetProperty(child, nameof(BattleSession.NativeMusicProcess), new NativeBattleMusicProcessState(new(1, null, null, null, null, null), 0, null).InitializeCommands501C60());
        return child;
    }

    private static ExpandedBattlePresentationProcess CreateFacade(BattleSession owner, BattleSession child)
    {
        // Reuse the operative frame path without needing installed boot resources in the Runtime test project.
        var process = (ExpandedBattlePresentationProcess)RuntimeHelpers.GetUninitializedObject(typeof(ExpandedBattlePresentationProcess));
        SetField(process, "logicalOwner", owner);
        SetField(process, "presentation", child);
        return process;
    }

    private static ushort DrawGlobal(BattleSession session) => (ushort)typeof(BattleSession).GetMethod("NextNativeGlobalRandom", PrivateInstance)!.Invoke(session, [])!;

    private static ushort DrawCrt(BattleSession session)
    {
        SetField(session, "nativeCrtRandom", session.CurrentCrtRandom!.Draw(out ushort result));
        return result;
    }

    private static object? ReadField(object owner, string name) => owner.GetType().GetField(name, PrivateInstance)!.GetValue(owner);
    private static void SetField(object owner, string name, object? value) => owner.GetType().GetField(name, PrivateInstance)!.SetValue(owner, value);
    private static void SetProperty(BattleSession owner, string name, object value) => typeof(BattleSession).GetProperty(name)!.SetValue(owner, value);
}
