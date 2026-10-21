using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStageExtendedTests
{
    [Fact]
    public void Stage37InitialCallbackDoesNotAdvancePendingHpFadeAndRestoredFadeContinues()
    {
        NativeBattleStage37State state = NativeBattleStage37.UpdateHp518A80(new(), 1, 2);
        NativeBattleStage37Step initial = NativeBattleStage37.Advance518F50(state, 0, 8, 0);
        Assert.Equal((short)1, initial.Phase);
        Assert.Empty(initial.Draws);
        NativeBattleStage37Step armed = NativeBattleStage37.Advance518F50(initial.State, 1, 8, 0);
        Assert.Equal(5, armed.State.FadeRemaining);
        Assert.Empty(armed.Draws);
        state = JsonSerializer.Deserialize<NativeBattleStage37State>(JsonSerializer.Serialize(armed.State))!;
        int[] intensities = new int[5];
        for (int frame = 0; frame < 5; frame++)
        {
            NativeBattleStage37Step next = NativeBattleStage37.Advance518F50(state, 1, 8, 0);
            intensities[frame] = Assert.Single(next.Draws).Intensity;
            state = next.State;
        }
        Assert.Equal(new[] { 410, 820, 1229, 1639, 2048 }, intensities);
        Assert.Equal(2048, state.PreviousHp);
        Assert.Equal(0, state.FadeRemaining);
    }

    [Fact]
    public void Stage37PulseThenStripsRetainNativeOrderAndOnlyFirstWriterOwnsAvailableShake()
    {
        NativeBattleStage37State state = NativeBattleStage37.Dispatch518A80(NativeBattleStage37.Dispatch518A80(new(), 100), 102);
        NativeBattleStage37Step next = NativeBattleStage37.Advance518F50(state, 1, 8, 0);
        Assert.Equal((short)-1, next.CameraShake);
        Assert.True(next.State.PulseDirection);
        Assert.False(next.State.StripsDirection);
        Assert.Equal((short)86, next.RotationSpeed);
        Assert.Collection(next.Draws, draw => Assert.False(draw.Strips), draw => Assert.True(draw.Strips));
        next = NativeBattleStage37.Advance518F50(next.State, 1, 8, 9);
        Assert.Equal((short)9, next.CameraShake);
        Assert.True(next.State.PulseDirection);
    }

    [Fact]
    public void Stage137InitialCallbackConsumesOneRandomAndPausedDeformationDoesNotConsumeAny()
    {
        int calls = 0;
        uint Random()
        {
            calls++;
            return 59;
        }
        NativeBattleStage137Step initial = NativeBattleStage137.Advance50DFF0(new(), 0, 0, 0, 0, 0, 0, new(0, 0, 0), Random, () => throw new InvalidOperationException());
        Assert.Equal(59, initial.State.ShakePeriod);
        Assert.Equal(1, calls);
        NativeBattleStage137Step paused = NativeBattleStage137.Advance50DFF0(initial.State, 1, 8, 0, 0, 0, 0, new(0, 0, 0), Random, () => throw new InvalidOperationException());
        Assert.Null(paused.DeformTick);
        Assert.Equal(1, calls);
        NativeBattleStage137Step active = NativeBattleStage137.Advance50DFF0(paused.State, 1, 0, 0, 0, 0, 0, new(0, 0, 0), Random, () => throw new InvalidOperationException());
        Assert.Equal(0, active.DeformTick);
        Assert.Equal(1, active.State.DeformationTick);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Stage137DeformationMutatesOnlyPositiveYAndSurvivesRealJsonRestore()
    {
        NativeBattleRawMesh mesh = new([new(new(0, 1, 100), 0), new(new(0, 0, 100), 0), new(new(0, -1, 100), 0)], []);
        mesh = NativeBattleStage137.Deform50E120(mesh, 0);
        Assert.Equal((short)-3, mesh.Vertices[0].Position.Y);
        Assert.Equal((short)0, mesh.Vertices[1].Position.Y);
        Assert.Equal((short)-1, mesh.Vertices[2].Position.Y);
        mesh = JsonSerializer.Deserialize<NativeBattleRawMesh>(JsonSerializer.Serialize(mesh))!;
        mesh = NativeBattleStage137.Deform50E120(mesh, 1);
        Assert.Equal((short)-3, mesh.Vertices[0].Position.Y);
    }

    [Fact]
    public void Stage137WaveUsesSharedCounterSampleAndGlobalRandomSignThenCapturedCameraEye()
    {
        Queue<uint> values = new([0, 7, 0]);
        NativeBattleStage137Step step = NativeBattleStage137.Advance50DFF0(new() { Deforming = false }, 1, 0, 11, 0, 0, 0, new(100, 200, 300), values.Dequeue, () => 0);
        Assert.Empty(values);
        Assert.Equal(30, step.State.WaveDelay);
        Assert.Equal(97, step.State.WaveCounter);
        Assert.Equal(37, step.State.WaveRise);
        Assert.Equal(-256, step.State.WaveAmplitude);
        Assert.Equal(new NativeBattleVector(100, 200, 300), step.CameraEye);
        Assert.Equal((short)0, step.RotationSpeed);
    }

    [Fact]
    public void Stage147DescentUsesInitialPositionAndTwentyWordCounterSteps()
    {
        Assert.Equal((short)5701, NativeBattleStage147.Descent511EF0(0, 1));
        Assert.Equal((short)3000, NativeBattleStage147.Descent511EF0(0, 10));
        Assert.Equal((short)0, NativeBattleStage147.Descent511EF0(0, 20));
        NativeBattleStageExtendedState state = NativeBattleStageExtendedState.FromColdProcess();
        state = JsonSerializer.Deserialize<NativeBattleStageExtendedState>(JsonSerializer.Serialize(state))!;
        Assert.Equal(4096, state.Stage37.PreviousHp);
        Assert.True(state.Stage137.Deforming);
        Assert.Equal((short)0, state.Stage147.Subphase);
    }
}
