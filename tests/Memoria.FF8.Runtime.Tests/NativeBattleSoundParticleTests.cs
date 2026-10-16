using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSoundParticleTests
{
    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 3)]
    public void ProducerDrawsOnceAndUsesAbsoluteThresholdsAndBothSuppressionBitsAcrossRestore(bool soundTwo, ushort sound)
    {
        NativeGlobalRandomState initial = new(7);
        NativeGlobalRandomState expected = initial.Draw(out ushort roll);
        NativeBattleAuxiliaryProgram program = new([2, 0x44, 0x86, 255], 0);
        (NativeBattleAuxiliaryState state, NativeGlobalRandomState after, bool allocated) = NativeBattleAuxiliaryState.Clear().AppendCameraBlend(10).AppendSoundParticle(3, soundTwo, 240, program, initial);
        Assert.True(allocated);
        Assert.Equal(expected, after);
        Assert.Equal((int?)1, state.Slots[0].Next);
        NativeBattleAuxiliaryState restored = JsonSerializer.Deserialize<NativeBattleAuxiliaryState>(JsonSerializer.Serialize(state))!;
        NativeBattleAuxiliaryInputs inputs = new() { ReadBone = (model, bone, fraction) =>
        {
            Assert.Equal(3, model);
            Assert.Equal((byte)240, bone);
            Assert.Equal((short)0, fraction);
            return new(12, 99, 34);
        }, ReadPan = point =>
        {
            Assert.Equal(new NativeBattleVector(12, 0, 34), point);
            return 123;
        } };
        for (int callback = 1; callback <= 6; callback++)
        {
            NativeBattleAuxiliaryStep step = state.Advance(inputs);
            NativeBattleAuxiliaryStep independent = restored.Advance(inputs);
            Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(independent));
            if (callback % 2 != 0)
            {
                Assert.Empty(step.Emissions);
            }
            else
            {
                NativeBattleAuxiliaryEmission emission = Assert.Single(step.Emissions);
                Assert.Equal((byte)((roll & 7) + 9), emission.Size);
                Assert.Equal(callback != 4, emission.Particle);
                Assert.Equal(callback == 6 ? null : (ushort?)sound, emission.Sound);
                Assert.Equal(callback == 6 ? null : (byte?)123, emission.Pan);
            }
            state = step.State;
            restored = independent.State;
        }
        Assert.Equal((ushort)0, state.Slots[1].Flags);
        Assert.Equal(3, state.Slots[1].Program!.Offset);
        Assert.Equal(0xF000u, state.Slots[1].UnknownByteMask);
    }

    [Fact]
    public void PoolExhaustionSkipsTheFfProgramWithoutConsumingRandomOrGrowingAnotherQueue()
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear();
        for (int slot = 0; slot < 16; slot++)
        {
            state = state.AppendCameraBlend(30);
        }
        NativeGlobalRandomState random = new(0);
        var result = state.AppendSoundParticle(0, true, 0, new([1, 2, 255, 0xAA], 0), random);
        Assert.False(result.Allocated);
        Assert.Equal(random, result.Random);
        Assert.Same(state, result.State);
        Assert.Equal(3, NativeBattleAuxiliaryState.SkipSoundParticleProgram([1, 2, 255, 0xAA], 0));
        Assert.Equal(1, NativeBattleAuxiliaryState.SkipSoundParticleProgram([255, 0xAA], 0));
    }

    [Theory]
    [InlineData(0xB1, true)]
    [InlineData(0x99, false)]
    public void PublicVmProducerRetainsTheOriginalOperandResourceAndSkipsItsFfTerminator(byte opcode, bool soundTwo)
    {
        NativeBattleGraphicsResource resource = new([1, 0, 4, 0, opcode, 240, 1, 0x82, 255, 0xAA, 0xA1]) { AnimationFrames = [] };
        NativeBattleGraphicsModel model = new(3, 3) { Flags2 = 4, Flags8 = 0, ModelId4 = 37, Sequence = 1, ResourceFlags44 = 1, SequenceState = new() { Pose = 0, PoseFrame114 = 0, PoseLimit115 = 1 } };
        NativeBattleAuxiliaryState tasks = NativeBattleAuxiliaryState.Clear();
        NativeGlobalRandomState random = new(0);
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, resource, new(null, null) { ScheduleSoundParticle = (program, mode, bone) =>
        {
            Assert.Equal(soundTwo, mode);
            var result = tasks.AppendSoundParticle(3, mode, bone, program, random);
            tasks = result.State;
            random = result.Random;
        } });
        Assert.Single(step.Cues, cue => cue.Kind == NativeBattleSequenceCueKind.ActionCallback);
        Assert.Equal(6, tasks.Slots[0].Program!.Offset);
        Assert.Equal(resource.SequenceSection, tasks.Slots[0].Program!.Source);
        Assert.Equal((ushort)7, step.Model.SequenceState!.Offset46);
    }

    [Fact]
    public void CameraViewAndNativePanUseThePublishedGteFieldsAndRequireUnknownInputs()
    {
        NativeBattleCameraPose pose = new(new(0, 0, -1000), new(0, 0, 0), 320, 0);
        NativeBattleActionMatrix view = NativeBattlePoseMatrices.CameraView(pose);
        Assert.Equal(ImmutableArray.Create<short>(4096, 0, 0, 0, 4096, 0, 0, 0, 4096), view.Rotation);
        Assert.Equal(1000, view.Z);
        NativeBattleCameraState camera = NativeBattleCameraState.Initialize() with { CurrentPose = pose, FocalLength = 320 };
        camera = camera.PublishProjection(4);
        Assert.Throws<NotSupportedException>(() => camera.Projection!.ReadPan(new(0, 0, 0)));
        camera = camera.PublishProjection(4);
        //45FAA0 publishes eighth-pixel SXY;5011B0 consumes that signed WORD
        //directly before its original80..175 clamp.
        Assert.Equal((byte)175, camera.Projection!.ReadPan(new(0, 0, 0)));
        Assert.Equal((byte)80, camera.Projection.ReadPan(new(-1000, 0, 0)));
        Assert.Throws<NotSupportedException>(() => (camera.Projection with { FocalLength = null }).ReadPan(new(0, 0, 0)));
    }
}
