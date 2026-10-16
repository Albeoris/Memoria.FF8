using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeGardenTransferTests
{
    [Fact]
    public void ReachedWakeAngleUsesTheProvedPrecision53StoredIntegerContract()
    {
        Assert.Equal((2244, -3427), NativeGardenTransform.Offset(4096, 5766));
        Assert.Equal(-3427, NativePrecision53.RoundInteger(-3426.5000430720015));
    }

    [Fact]
    public void LoweringAddsItsRoundedWordOnAllThirtyOneCallbacksAndRestoresIndependently()
    {
        NativeGardenTransferState state = NativeGardenTransferState.Create(10, 2, 0, new NativeWorldPathPosition(700, -200, 0));
        NativeWorldPathPosition garden = new(0, -1000, 0);
        NativeGardenTransferState? resumed = null;
        NativeWorldPathPosition resumedGarden = default;
        for (int index = 0; index < 31; index++)
        {
            NativeGardenTransferStep step = state.StepLowering(10 + index, garden, -200);
            state = step.State;
            garden = step.GardenPosition;
            Assert.Equal(index == 30, step.Completed);
            Assert.Equal<short?>(26, state.LoweringStep);
            if (index == 12)
            {
                resumed = JsonSerializer.Deserialize<NativeGardenTransferState>(JsonSerializer.Serialize(state))!;
                resumed.Validate();
                resumedGarden = garden;
            }
            else if (index > 12)
            {
                NativeGardenTransferStep copy = resumed!.StepLowering(10 + index, resumedGarden, -200);
                resumed = copy.State;
                resumedGarden = copy.GardenPosition;
                Assert.Equal(state, resumed);
                Assert.Equal(garden, resumedGarden);
            }
        }
        Assert.Equal(-194, garden.Y);
        Assert.Equal(31, state.Counter);
        Assert.Equal<long?>(40, state.LastLoweringFrame);
        Assert.Equal(new NativeWorldPathPosition(0, -1000, 0), state.InitialGardenPosition);
        Assert.Equal(garden, state.LastGardenPosition);
        Assert.Throws<InvalidOperationException>(() => state.StepLowering(41, garden, -200));
        Assert.Throws<InvalidDataException>(() => (state with { LoweringStep = 27 }).Validate());
    }

    [Fact]
    public void NativeWordStepWrapAndCallbackOwnershipAreExplicit()
    {
        NativeGardenTransferState state = NativeGardenTransferState.Create(7, 2, 1, new NativeWorldPathPosition(3, 4, 5));
        NativeGardenTransferStep first = state.StepLowering(7, new(0, 0, 0), 1_000_000);
        Assert.Equal<short?>(unchecked((short)33333), first.State.LoweringStep);
        Assert.Equal(unchecked((short)33333), first.GardenPosition.Y);
        Assert.Throws<InvalidOperationException>(() => first.State.StepLowering(7, first.GardenPosition, 0));
        Assert.Throws<InvalidOperationException>(() => state.StepLowering(6, default, 0));
    }

    [Fact]
    public void InputRequiresAnActualEdgeAndPreservesTheSeparateViewAndScriptPaths()
    {
        NativeGardenTransferInput input = new(0, 0, 48, 0, 0, false, -1, 0, 0, false);
        NativeGardenButtonHistory history = NativeGardenButtonHistory.UnknownAtLoad();
        Assert.False(history.Evaluate(input).Action);
        Assert.Throws<NotSupportedException>(() => history.Evaluate(input with { CurrentButtons = 0x40 }));
        history = history.Observe(0);
        Assert.True(history.Evaluate(input with { CurrentButtons = 0x40 }).Action);
        history = history.Observe(0x40);
        Assert.False(history.Evaluate(input with { CurrentButtons = 0x40 }).Action);
        Assert.True(history.Evaluate(input with { CurrentButtons = 2 }).ToggleView);
        Assert.False(history.Evaluate(input with { CurrentButtons = 2 }).Action);
        Assert.True(NativeGardenTransferState.EvaluateInput(input with { CurrentButtons = 0x42 }).Action);
        Assert.False(NativeGardenTransferState.EvaluateInput(input with { CurrentButtons = 0x42, EntityType = 0 }).Action);
        NativeGardenTransferInput busy = input with { CurrentButtons = 0x40, PreviousButtons = 0, ScriptMode = 8, TransitionPhase = int.MinValue, Activity = true, ActiveWorldScript = 3, ScriptRequest = 0x40 };
        Assert.True(NativeGardenTransferState.EvaluateInput(busy).Action);
        Assert.True(NativeGardenTransferState.EvaluateInput(busy).ConsumeScriptRequest);
        Assert.False(NativeGardenTransferState.EvaluateInput(busy with { ScriptRequest = 0 }).Action);
        Assert.False(NativeGardenTransferState.EvaluateInput(busy with { Cooldown = 1 }).Action);
        NativeGardenButtonHistory restored = JsonSerializer.Deserialize<NativeGardenButtonHistory>(JsonSerializer.Serialize(history))!;
        Assert.Equal(history.Evaluate(input), restored.Evaluate(input));
    }

    [Theory]
    [InlineData(199, true)]
    [InlineData(200, false)]
    [InlineData(-199, true)]
    [InlineData(-200, false)]
    public void SurfaceAndBoardingHeightGatesAreStrict(int delta, bool accepted)
    {
        const uint flags = 0x820000;
        Assert.Equal(accepted, NativeGardenTransferState.PassesPlacementGate(48, 0, flags, -1000, -1000 + delta, true));
        Assert.Equal(accepted, NativeGardenTransferState.PassesBoardingGate(0, 48, flags, -1000, -1000 + delta, true));
        Assert.False(NativeGardenTransferState.PassesPlacementGate(48, 0, 0x800000, 0, 0, true));
        Assert.False(NativeGardenTransferState.PassesPlacementGate(48, 0, 0x020000, 0, 0, true));
        Assert.True(NativeGardenTransferState.PassesPlacementGate(48, 0, 0, 0, 0, false));
    }

    [Fact]
    public void CallbackControlsRetainTheirLoaderOwnerAndReadTheActualEventLatch()
    {
        NativeGardenTransferControlState controls = new(1, NativeGardenTransferControlState.CurrentRule, 0, -1, false);
        NativeWorldEntryMemory memory = NativeWorldEntryMemory.CreateNewGame();
        NativeGardenButtonHistory buttons = NativeGardenButtonHistory.UnknownAtLoad().Observe(0);
        Assert.False(buttons.Evaluate(controls.PrepareInput(0x40, 48, 1, null, memory)).Action);
        Assert.True(buttons.Evaluate(controls.PrepareInput(0x40, 48, 24, null, memory)).Action);
        memory = memory.WriteBytes(264, 1, 0x40);
        NativeGardenTransferState transfer = NativeGardenTransferState.Create(24, 2, 0, default);
        NativeGardenTransferInput scripted = controls.BeginLowering().PrepareInput(0, 48, 24, transfer, memory);
        Assert.Equal((byte)8, scripted.ScriptMode);
        Assert.Equal(int.MinValue, scripted.TransitionPhase);
        Assert.True(buttons.Evaluate(scripted).Action);
        NativeGardenTransferControlState restored = JsonSerializer.Deserialize<NativeGardenTransferControlState>(JsonSerializer.Serialize(controls.BeginLowering()))!;
        Assert.Equal(0, restored.AfterLowering().TransitionPhase);
        Assert.Throws<NotSupportedException>(() => (controls with { ActiveWorldScript = 0 }).Validate());
        Assert.Throws<NotSupportedException>(() => (controls with { ViewToggleBlocked = true }).Validate());
    }

    [Fact]
    public void ReboardCompletionCertifiesEveryActiveCameraBitBeforeClearingItsPhase()
    {
        NativeGardenTransferControlState controls = new(1, NativeGardenTransferControlState.CurrentRule, 0, -1, false);
        NativeGardenCameraDefaults defaults = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, -112, 0, -341, -7168);
        NativeGardenReboardCompletion result = controls.CompleteReboarding(1032, 5344, -128, -853, -7680, defaults);
        Assert.Equal((0, (short)-112, 0, 32, (short)-341, (short)-7168), (result.Controls.TransitionPhase, result.Pitch, result.Speed, result.SpeedParameter, result.OffsetY, result.OffsetZ));
        Assert.Throws<NotSupportedException>(() => controls.CompleteReboarding(1033, 5216, -112, -341, -7168, defaults));
        Assert.Throws<NotSupportedException>(() => controls.CompleteReboarding(1024, 5345, -112, -341, -7168, defaults));
        Assert.Throws<NotSupportedException>(() => controls.CompleteReboarding(1024, 5216, -129, -341, -7168, defaults));
        Assert.Throws<NotSupportedException>(() => controls.CompleteReboarding(1024, 5216, -112, -854, -7168, defaults));
        Assert.Throws<NotSupportedException>(() => controls.CompleteReboarding(1024, 5216, -112, -341, -7681, defaults));
    }

    [Theory]
    [InlineData(0, 0, 700)]
    [InlineData(1024, 700, 0)]
    [InlineData(2048, 0, -700)]
    [InlineData(-1024, -700, 0)]
    public void PlacementUsesTheNativeRoundedShortVector(int yaw, int x, int z)
    {
        Assert.Equal((x, z), NativeGardenTransform.Offset(700, (short)yaw));
    }
}
