using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCameraTests
{
    [Theory]
    [InlineData(0u)]
    [InlineData(0x20u)]
    public void CurrentEyeAndTargetAliasesUseTranslationOnlyEvenForNonzeroLocalPoints(uint flags)
    {
        NativeBattleCameraPose retained = new(new(100, 200, 300), new(400, 500, 600), 512, 0);
        NativeBattleCameraState state = NativeBattleCameraState.Initialize() with { CurrentPose = retained };
        state = state.Start(PoseBank(0x40, [], [1, 0xF3, 10, 20, 30, 0xF4, 40, 50, 60]), 0).Tick(flags);
        Assert.Equal(retained.Eye, state.CurrentPose!.Eye);
        Assert.Equal(retained.Target, state.CurrentPose.Target);
    }

    [Fact]
    public void ZeroHoldFinalKnotDoesNotFabricateASampleAfterTrackCompletion()
    {
        NativeBattleCameraState state = NativeBattleCameraState.Initialize().Start(PoseBank(0x40, [], [1, 0xF1, 0, 0, 0, 0xF1, 0, 10, 0, 0, 0xF1, 100, 0, 0, 0xF1, 100, 10, 0]), 0).Tick(0);
        Assert.Equal((short)0, state.CurrentPose!.Eye.X);
        Assert.Equal((ushort)16, state.Tasks[0]!.Elapsed);
        Assert.Equal((ushort)16, state.Tasks[0]!.Duration);
        state = state.Tick(0);
        Assert.Empty(state.Order);
        Assert.Equal((short)0, state.CurrentPose!.Eye.X);
    }

    [Fact]
    public void CameraSamplesCoordinatesBeforeIncrementingButLensAfterIncrementing()
    {
        NativeBattleCameraState state = NativeBattleCameraState.Initialize().Start(PoseBank(0xC0, [400, 800], [1, 0xF1, 0, 0, 0, 0xF1, 0, 10, 0, 1, 0xF1, 100, 0, 0, 0xF1, 100, 10, 0]), 0);
        state = state.Tick(0);
        Assert.Equal(new NativeBattleCameraPose(new(0, 0, 0), new(0, 10, 0), 600, 0), state.CurrentPose);
        state = state.Tick(0);
        Assert.Equal(new NativeBattleCameraPose(new(100, 0, 0), new(100, 10, 0), 800, 0), state.CurrentPose);
        NativeBattleCameraState restored = JsonSerializer.Deserialize<NativeBattleCameraState>(JsonSerializer.Serialize(state))!;
        state = state.Tick(0);
        Assert.Empty(state.Order);
        Assert.Equal(restored.CurrentPose, state.CurrentPose);
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored.Tick(0)));
        state = (state with { ControlCursor = 0 }).AdvanceControl(Bank(1), [3, 2], new());
        Assert.Equal(state.CurrentPose, state.CapturedPose);
    }

    [Fact]
    public void CameraCapturesReferencesAtSegmentLoadOnlyWhenFlagOneIsSet()
    {
        short origin = 10;
        NativeBattleCameraPoseInputs inputs = new() { TransformReference = (_, point) => point with { X = (short)(point.X + origin) } };
        ushort[] knot = [3, 0xF2, 1, 2, 3, 0xF2, 4, 5, 6];
        NativeBattleCameraState frozen = NativeBattleCameraState.Initialize().Start(PoseBank(0x41, [], knot), 0).Tick(0, inputs);
        NativeBattleCameraState moving = NativeBattleCameraState.Initialize().Start(PoseBank(0x40, [], knot), 0).Tick(0, inputs);
        Assert.Equal((short)11, frozen.CurrentPose!.Eye.X);
        Assert.Equal((short)11, moving.CurrentPose!.Eye.X);
        origin = 100;
        frozen = frozen.Tick(0, inputs);
        moving = moving.Tick(0, inputs);
        Assert.Equal((short)11, frozen.CurrentPose!.Eye.X);
        Assert.Equal((short)101, moving.CurrentPose!.Eye.X);
        Assert.Equal((byte)0xFB, frozen.Tasks[0]!.Pose!.Knots[0].EyeReference);
    }

    [Fact]
    public void StageControlStartsTheSelectedTrackAndWaitsOnItsActualLowByteMask()
    {
        System.Collections.Immutable.ImmutableArray<byte> program = [0x05, 0x00, 0xC3, 0x10, 0xE5, 0x7F, 0xC1, 0x00, 0xCB, 0x7F, 0xEA, 0x06, 0xE6, 0x07, 0xE6, 0x02, 0x01, 0xE6, 0xF1, 0x03, 0x02, 0x00, 0x00, 0x00];
        NativeBattleCameraState state = NativeBattleCameraState.Initialize() with { ControlCursor = 0 };
        for (int callback = 0; callback < 3; callback++)
        {
            state = state.AdvanceControl(Bank(2), program, new()).Tick(0);
            Assert.NotNull(state.ControlCursor);
            Assert.True(state.ForceCamera);
            string json = JsonSerializer.Serialize(state);
            state = JsonSerializer.Deserialize<NativeBattleCameraState>(json)!;
        }
        Assert.Empty(state.Order);
        Assert.Equal((ushort)0, state.Mask);
        state = state.AdvanceControl(Bank(2), program, new()).Tick(0);
        Assert.Null(state.ControlCursor);
        Assert.False(state.ForceCamera);
        Assert.Equal(1ul, state.PoseCaptureCount);
    }

    [Fact]
    public void CameraControlUsesSignedWordsFreshTemporariesAndOnlyTheLowMaskByte()
    {
        NativeBattleCameraState state = NativeBattleCameraState.Initialize() with { ControlCursor = 0, Mask = 0xFF80 };
        state = state.AdvanceControl(Bank(1), [0xC3, 16, 0xE5, 0, 0xC0, 0xFF, 0xFF, 0xE5, 1, 1], new());
        Assert.Equal(128, state.Variables[0]);
        Assert.Equal(-1, state.Variables[1]);
        state = state with { ControlCursor = 0 };
        Assert.Throws<InvalidDataException>(() => state.AdvanceControl(Bank(1), [0xC3, 128, 1], new()));
        Assert.Throws<NotSupportedException>(() => state.AdvanceControl(Bank(1), [4, 10, 1], new()));
        Assert.Throws<InvalidDataException>(() => state.AdvanceControl(Bank(1), [5, 128, 1], new()));
    }

    [Fact]
    public void CameraEndPreservesTheMaskAndBlendUnlikeAnUnknownCommand()
    {
        NativeBattleCameraState initial = NativeBattleCameraState.Initialize() with { ControlCursor = 0, Mask = 0x8040, BlendProgress = 123 };
        NativeBattleCameraState ended = initial.AdvanceControl(Bank(1), [2], new());
        Assert.Null(ended.ControlCursor);
        Assert.Equal(initial.Mask, ended.Mask);
        Assert.Equal(initial.BlendProgress, ended.BlendProgress);
        NativeBattleCameraState unknown = initial.AdvanceControl(Bank(1), [10], new());
        Assert.Null(unknown.ControlCursor);
        Assert.Equal((ushort)0, unknown.Mask);
        Assert.Equal((ushort)4096, unknown.BlendProgress);
        byte? scheduled = null;
        NativeBattleCameraState yielding = initial.AdvanceControl(Bank(1), [4, 17, 1], new() { ScheduleBlend = value => scheduled = value });
        Assert.Equal((byte)17, scheduled);
        Assert.Equal(3, yielding.ControlCursor);
    }

    [Fact]
    public void OriginalTrackDurationAdvancesBySixteenAndClearsOnlyItsChannelOnTheNextPoll()
    {
        NativeBattleCameraState state = NativeBattleCameraState.Initialize().Start(Bank(2), 3) with { Mask = 0x8008 };
        state = state.Tick(0);
        Assert.Equal((ushort)16, state.Tasks[0]!.Elapsed);
        Assert.Equal((ushort)32, state.Tasks[0]!.Duration);
        state = state.Tick(0);
        Assert.Equal((ushort)0x8008, state.Mask);
        state = state.Tick(0);
        Assert.Equal((ushort)0x8000, state.Mask);
        Assert.All(state.Tasks, task => Assert.Null(task));
        Assert.Empty(state.Order);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(0x100u)]
    public void GraphicsPauseLoadsTheSegmentButFreezesElapsedAndRestoresExactly(uint flags)
    {
        NativeBattleCameraState state = NativeBattleCameraState.Initialize().Start(Bank(2), 0).Tick(flags);
        Assert.Equal((ushort)0, state.Tasks[0]!.Elapsed);
        Assert.Equal((ushort)32, state.Tasks[0]!.Duration);
        string json = JsonSerializer.Serialize(state);
        NativeBattleCameraState restored = JsonSerializer.Deserialize<NativeBattleCameraState>(json)!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(state.Tick(0)), JsonSerializer.Serialize(restored.Tick(0)));
    }

    [Fact]
    public void NativeMaskIsWrittenByCallbacksRatherThanInferredFromOtherTasks()
    {
        NativeBattleCameraState state = NativeBattleCameraState.Initialize().Start(Bank(1), 2).Start(Bank(2), 2);
        state = state.Tick(0).Tick(0);
        Assert.Equal((ushort)0, state.Mask);
        Assert.Null(state.Tasks[0]);
        Assert.NotNull(state.Tasks[1]);
        state = state.Start(Bank(1), 7);
        Assert.Equal(new[] { 1, 0 }, state.Order);
        Assert.Same(state, state.Start(Bank(1), 0));
    }

    [Fact]
    public void MissingGroupsDoNothingAndMalformedDirectoriesFail()
    {
        NativeBattleCameraState state = NativeBattleCameraState.Initialize();
        Assert.Same(state, state.Start(Bank(2), 0x10));
        Assert.Throws<InvalidDataException>(() => state.Start(new NativeBattleCameraBank([1, 0]), 0));
        state = state.Start(Bank(2), 0);
        Assert.Throws<InvalidDataException>(() => (state with { Order = [0, 0] }).Validate());
    }

    private static NativeBattleCameraBank Bank(ushort frames)
    {
        ushort[] words = [1, 4, 8, 8, 8, 8, 8, 8, 8, 8, 0, frames, 0xF1, 1, 2, 3, 0xF1, 4, 5, 6, 0xFFFF, 0xFFFF];
        byte[] bytes = new byte[words.Length * 2];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2), words[index]);
        }
        return new(bytes.ToImmutableArray());
    }

    private static NativeBattleCameraBank PoseBank(ushort flags, ushort[] parameters, ushort[] knots)
    {
        ushort[] directory = [1, 4, 8, 8, 8, 8, 8, 8, 8, 8];
        ushort[] words = directory.Concat(new[] { flags }).Concat(parameters).Concat(knots).Concat(new ushort[] { 0xFFFF, 0xFFFF }).ToArray();
        byte[] bytes = new byte[words.Length * 2];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2), words[index]);
        }
        return new(bytes.ToImmutableArray());
    }
}
