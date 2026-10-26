using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGuardianCameraProgramTests
{
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static readonly NativeBattleCameraPose Pose = new(new(1, 2, 3), new(4, 5, 6), 288, 0);

    [Fact]
    public void PairedCountersAnimateBeforeNativeEndAndDoNotPublishOnTermination()
    {
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(Process(), Words(0x81, 10, 20, 30, 0x41, 100, 200, 300, 0x82, 2, 14, 24, 34, 0x42, 2, 108, 208, 308, 0x8000), 0);
        NativeBattleGuardianCameraStep first = NativeBattleGuardianCameraProgram.Advance63E9C0(task, Identity, Pose, false);
        Assert.Equal(new NativeBattleCameraPoint(104, 204, 304), first.Pose.Eye);
        Assert.Equal(new NativeBattleCameraPoint(12, 22, 32), first.Pose.Target);
        Assert.Equal((ushort)1, first.Task.Process.Eye.Remaining);
        Assert.Equal((ushort)1, first.Task.Frame);
        NativeBattleGuardianCameraStep second = NativeBattleGuardianCameraProgram.Advance63E9C0(first.Task, Identity, first.Pose, false);
        Assert.Equal(new NativeBattleCameraPoint(108, 208, 308), second.Pose.Eye);
        Assert.Equal(new NativeBattleCameraPoint(14, 24, 34), second.Pose.Target);
        NativeBattleGuardianCameraStep end = NativeBattleGuardianCameraProgram.Advance63E9C0(second.Task, Identity, second.Pose, false);
        Assert.True(end.Task.Complete);
        Assert.Equal(2, end.Result);
        Assert.False(end.Published);
        Assert.Equal(second.Pose, end.Pose);
        Assert.Equal(second.Task.Frame, end.Task.Frame);
        Assert.Equal(second.Task.Process.PreviousPose, end.Task.Process.PreviousPose);
        Assert.Throws<InvalidDataException>(() => NativeBattleGuardianCameraProgram.Advance63E9C0(end.Task, Identity, end.Pose, false));
    }

    [Theory]
    [InlineData(3, 0, 1)]
    [InlineData(3, 1, -1)]
    [InlineData(3, 2, 1)]
    [InlineData(-3, 0, 1)]
    [InlineData(-3, 1, -1)]
    [InlineData(-3, 2, 1)]
    public void BeginConsumesActualRandomOnlyForNonzeroDivisor(int divisor, int randomValue, int expectedSign)
    {
        int calls = 0;
        NativeBattleGuardianCameraProcess owner = Process();
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(owner, Words(0x8000), divisor, () =>
        {
            calls++;
            return randomValue;
        });
        Assert.Equal(expectedSign, task.Process.Sign);
        Assert.Equal(1, calls);
        NativeBattleGuardianCameraTask fixedSign = NativeBattleGuardianCameraProgram.Begin63E960(owner, Words(0x8000), 0, () => throw new InvalidOperationException());
        Assert.Equal(1, fixedSign.Process.Sign);
        Assert.Throws<NotSupportedException>(() => NativeBattleGuardianCameraProgram.Begin63E960(owner, Words(0x8000), 3));
    }

    [Fact]
    public void FirstUnpausedCallbackClearsOnlyCountersWhilePauseStillPublishesRetainedVectors()
    {
        NativeBattleGuardianCameraProcess owner = Process() with { Eye = Bank(new(50, 60, 70)) with { Remaining = 33, Accumulator = 123, Step = 7, Origin = new(8, 9, 10) }, Target = Bank(new(-5, -6, -7)) with { Remaining = 44 } };
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(owner, Words(0x80, 2, 0x40, 2, 0x8000), 0);
        NativeBattleActionMatrix matrix = Identity with { X = 32768, Y = 10, Z = -20 };
        NativeBattleGuardianCameraStep paused = NativeBattleGuardianCameraProgram.Advance63E9C0(task, matrix, Pose, true);
        Assert.Equal(0, paused.Task.Offset);
        Assert.Equal((ushort)0, paused.Task.Frame);
        Assert.Equal((ushort)33, paused.Task.Process.Eye.Remaining);
        Assert.Equal(new NativeBattleCameraPoint(-32718, 70, 50), paused.Pose.Eye);
        Assert.Equal(Pose, paused.Task.Process.PreviousPose);
        NativeBattleGuardianCameraStep resumed = NativeBattleGuardianCameraProgram.Advance63E9C0(paused.Task, matrix, paused.Pose, false);
        Assert.Equal((ushort)1, resumed.Task.Process.Eye.Remaining);
        Assert.Equal(123, resumed.Task.Process.Eye.Accumulator);
        Assert.Equal(7, resumed.Task.Process.Eye.Step);
        Assert.Equal(new NativeBattleVector(8, 9, 10), resumed.Task.Process.Eye.Origin);
    }

    [Fact]
    public void NativeTaskFrameWrapReentersItsCounterResetBranch()
    {
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(Process(), Words(0x8000), 0);
        task = task with { Frame = ushort.MaxValue, Process = task.Process with { Eye = task.Process.Eye with { Remaining = 2 }, Target = task.Process.Target with { Remaining = 2 } } };
        NativeBattleGuardianCameraStep wrapped = NativeBattleGuardianCameraProgram.Advance63E9C0(task, Identity, Pose, false);
        Assert.Equal((ushort)0, wrapped.Task.Frame);
        Assert.Equal((ushort)1, wrapped.Task.Process.Eye.Remaining);
        NativeBattleGuardianCameraStep end = NativeBattleGuardianCameraProgram.Advance63E9C0(wrapped.Task, Identity, wrapped.Pose, false);
        Assert.Equal(2, end.Result);
        Assert.Equal((ushort)0, end.Task.Process.Eye.Remaining);
    }

    [Fact]
    public void ScalarIndirectionAndSignedVectorReflectionKeepNativeWordWidths()
    {
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(Process(), Words(0x81, -32768, -2, -3, 0x41, -7, -8, -9, 0x88, -32768, 0x46, -1, 0x80, -1, 0x40, 3), 2, () => 1);
        Assert.Throws<NotSupportedException>(() => NativeBattleGuardianCameraProgram.Advance63E9C0(task, Identity, Pose, false));
        NativeBattleGuardianCameraStep step = NativeBattleGuardianCameraProgram.Advance63E9C0(task, Identity, Pose, false, [3]);
        Assert.Equal(new NativeBattleCameraPoint(-32768, -2, -3), step.Pose.Target);
        Assert.Equal(new NativeBattleCameraPoint(7, -8, -9), step.Pose.Eye);
        Assert.Equal((short)-32768, step.Pose.Roll);
        Assert.Equal((short)-1, step.Pose.FocalLength);
        Assert.Equal((ushort)2, step.Task.Process.Target.Remaining);
    }

    [Fact]
    public void EyeDistanceUpdateObservesTargetBeforeItsSameCallbackLinearUpdate()
    {
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(Process(), Words(0x81, 0, 0, 0, 0x41, 10, 0, 0, 0x82, 2, 4, 0, 0, 0x45, 2, 14, 0, 0), 0);
        NativeBattleGuardianCameraStep first = NativeBattleGuardianCameraProgram.Advance63E9C0(task, Identity, Pose, false);
        Assert.Equal(new NativeBattleCameraPoint(10, 0, 0), first.Pose.Eye);
        Assert.Equal(new NativeBattleCameraPoint(2, 0, 0), first.Pose.Target);
        NativeBattleGuardianCameraStep second = NativeBattleGuardianCameraProgram.Advance63E9C0(first.Task, Identity, first.Pose, false);
        Assert.Equal(new NativeBattleCameraPoint(12, 0, 0), second.Pose.Eye);
        Assert.Equal(new NativeBattleCameraPoint(4, 0, 0), second.Pose.Target);
    }

    [Fact]
    public void UnimplementedKnownOpcodeFailsInsteadOfProducingAGenericCamera()
    {
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(Process(), Words(7), 0);
        Assert.Throws<NotSupportedException>(() => NativeBattleGuardianCameraProgram.Advance63E9C0(task, Identity, Pose, false));
    }

    [Theory]
    [InlineData(0x09)]
    [InlineData(0x49)]
    public void RollInterpolationRequiresBothDurationAndTargetOperands(int command)
    {
        NativeBattleGuardianCameraTask missingDuration = NativeBattleGuardianCameraProgram.Begin63E960(Process(), Words(command), 0);
        Assert.Throws<InvalidDataException>(() => NativeBattleGuardianCameraProgram.Advance63E9C0(missingDuration, Identity, Pose, false));
        NativeBattleGuardianCameraTask missingTarget = NativeBattleGuardianCameraProgram.Begin63E960(Process(), Words(command, 3), 0);
        Assert.Throws<InvalidDataException>(() => NativeBattleGuardianCameraProgram.Advance63E9C0(missingTarget, Identity, Pose, false));
    }

    [Fact]
    public void CaptureCurrentPoseUsesInverseTransformForBothBanksWithoutSignReflection()
    {
        NativeBattleActionMatrix matrix = new([0, 0, 4096, 0, 4096, 0, -4096, 0, 0], 100, -200, 300, 4096);
        NativeBattleCameraPose pose = new(new(110, -180, 270), new(60, -250, 360), 288, 0);
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(Process(), Words(0x0A, 0x4A, 0x80, 2, 0x40, 2, 0x8000), 2, () => 1);
        NativeBattleGuardianCameraStep first = NativeBattleGuardianCameraProgram.Advance63E9C0(task, matrix, pose, false, currentEyePadding: 123, currentTargetPadding: -456);
        Assert.Equal(-1, first.Task.Process.Sign);
        Assert.Equal(new NativeBattleVector(30, 20, 10), first.Task.Process.Eye.Current);
        Assert.Equal(new NativeBattleVector(-60, -50, -40), first.Task.Process.Target.Current);
        Assert.Equal((short)123, first.Task.Process.Eye.CurrentPadding);
        Assert.Equal((short)-456, first.Task.Process.Target.CurrentPadding);
        Assert.Equal(pose, first.Pose);
        Assert.True(first.Published);
        Assert.Equal(12, first.Task.Offset);
        NativeBattleGuardianCameraStep second = NativeBattleGuardianCameraProgram.Advance63E9C0(first.Task, matrix, first.Pose, false);
        Assert.Equal(pose, second.Pose);
        Assert.Equal(2, NativeBattleGuardianCameraProgram.Advance63E9C0(second.Task, matrix, second.Pose, false).Result);
    }

    [Fact]
    public void NativeEndHighByteIsNotMaskedIntoWaitAndTruncatedOperandsFail()
    {
        NativeBattleGuardianCameraTask end = NativeBattleGuardianCameraProgram.Begin63E960(Process(), Words(0x8000), 0);
        Assert.Equal(2, NativeBattleGuardianCameraProgram.Advance63E9C0(end, Identity, Pose, false).Result);
        NativeBattleGuardianCameraTask truncated = NativeBattleGuardianCameraProgram.Begin63E960(Process(), Words(0x81, 1), 0);
        Assert.Throws<InvalidDataException>(() => NativeBattleGuardianCameraProgram.Advance63E9C0(truncated, Identity, Pose, false));
        NativeBattleGuardianCameraTask missingNextCommand = NativeBattleGuardianCameraProgram.Begin63E960(Process(), Words(10), 0);
        Assert.Throws<InvalidDataException>(() => NativeBattleGuardianCameraProgram.Advance63E9C0(missingNextCommand, Identity, Pose, false));
        NativeBattleGuardianCameraTask zeroDivisor = NativeBattleGuardianCameraProgram.Begin63E960(Process(), Words(0x82, 0, 1, 2, 3), 0);
        Assert.Throws<NotSupportedException>(() => NativeBattleGuardianCameraProgram.Advance63E9C0(zeroDivisor, Identity, Pose, false));
    }

    [Fact]
    public void ImmediateFocalAndRollWritesSurviveTerminationWithoutCameraVectorPublication()
    {
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(Process(), Words(0x46, 900, 0x88, -20, 0x8000), 0);
        NativeBattleGuardianCameraStep end = NativeBattleGuardianCameraProgram.Advance63E9C0(task, Identity, Pose, false);
        Assert.False(end.Published);
        Assert.Equal(Pose.Eye, end.Pose.Eye);
        Assert.Equal(Pose.Target, end.Pose.Target);
        Assert.Equal((short)900, end.Pose.FocalLength);
        Assert.Equal((short)-20, end.Pose.Roll);
        Assert.Equal((ushort)0, end.Task.Frame);
    }

    [Theory]
    [InlineData(0, 2048)]
    [InlineData(511, 2896)]
    [InlineData(-511, 1200)]
    public void SignedEasingPreservesSineIterationsAndBlend(int curve, int expected)
    {
        Assert.Equal(expected, NativeBattleGuardianCameraProgram.Ease63F240(2048, (short)curve));
    }

    [Fact]
    public void RetainedTwoBankOwnerAndProgramRestoreIndependentlyForTheNextCallback()
    {
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(Process(), Words(0x83, 3, 256, 100, 200, 300, 0x43, 3, -256, -100, -200, -300, 0x8000), 0);
        NativeBattleGuardianCameraStep first = NativeBattleGuardianCameraProgram.Advance63E9C0(task, Identity, Pose, false);
        string json = JsonSerializer.Serialize(first.Task);
        NativeBattleGuardianCameraTask restored = JsonSerializer.Deserialize<NativeBattleGuardianCameraTask>(json)!;
        NativeBattleGuardianCameraStep expected = NativeBattleGuardianCameraProgram.Advance63E9C0(first.Task, Identity, first.Pose, false);
        NativeBattleGuardianCameraStep actual = NativeBattleGuardianCameraProgram.Advance63E9C0(restored, Identity, first.Pose, false);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleGuardianCameraTask>("{}"));
    }

    [Theory]
    [InlineData(0xCD069C, -19, -874, -5002, 3199, -3056, -390)]
    [InlineData(0xCD0788, -421, -1422, -4718, 3430, -894, -874)]
    [InlineData(0xCD0874, -421, -1422, -4718, 3430, -894, -874)]
    public void OriginalExternalProgramsReachTheirActualTerminatorAndRestoreNextCallback(int address, short targetX, short targetY, short targetZ, short eyeX, short eyeY, short eyeZ)
    {
        string? executable = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_EXECUTABLE");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(executable) && File.Exists(executable), "Set MEMORIA_FF8_NATIVE_EXECUTABLE to the optional original English executable.");
        byte[] bytes = File.ReadAllBytes(executable!);
        Assert.Equal(NativeFieldRuntimeProfile.FF8EnExecutableHash, Convert.ToHexString(SHA256.HashData(bytes)));
        using PEReader reader = new(new MemoryStream(bytes));
        long rva = (long)address - (long)reader.PEHeaders.PEHeader!.ImageBase;
        SectionHeader section = reader.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + 234 <= section.VirtualAddress + section.SizeOfRawData);
        int start = checked((int)(rva - section.VirtualAddress + section.PointerToRawData));
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(Process(), bytes.AsSpan(start, 234).ToArray().ToImmutableArray(), 0);
        NativeBattleCameraPose pose = Pose;
        for (int callback = 0; callback < 327; callback++)
        {
            NativeBattleGuardianCameraStep step = NativeBattleGuardianCameraProgram.Advance63E9C0(task, Identity, pose, false);
            Assert.Equal(0, step.Result);
            Assert.True(step.Published);
            if (callback == 60)
            {
                NativeBattleGuardianCameraTask restored = JsonSerializer.Deserialize<NativeBattleGuardianCameraTask>(JsonSerializer.Serialize(step.Task))!;
                NativeBattleGuardianCameraStep resumed = NativeBattleGuardianCameraProgram.Advance63E9C0(restored, Identity, step.Pose, false);
                NativeBattleGuardianCameraStep expected = NativeBattleGuardianCameraProgram.Advance63E9C0(step.Task, Identity, step.Pose, false);
                Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(resumed));
            }
            task = step.Task;
            pose = step.Pose;
        }
        Assert.Equal(new NativeBattleCameraPoint(targetX, targetY, targetZ), pose.Target);
        Assert.Equal(new NativeBattleCameraPoint(eyeX, eyeY, eyeZ), pose.Eye);
        NativeBattleGuardianCameraStep end = NativeBattleGuardianCameraProgram.Advance63E9C0(task, Identity, pose, false);
        Assert.Equal(2, end.Result);
        Assert.Equal(234, end.Task.Offset);
        Assert.False(end.Published);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void Polynomial_camera_uses_ordered_fixed_point_curve_samples_and_reaches_endpoint_before_termination(int sign)
    {
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(Process(), Words(0x81, 0, 0, 0, 0x41, 0, 0, -100, 0x84, 2, 2, 100, 0, 0, 100, 100, 0, 0x40, 2, 0x8000), sign == 1 ? 0 : 2, () => 1);
        NativeBattleGuardianCameraStep first = NativeBattleGuardianCameraProgram.Advance63E9C0(task, Identity, Pose, false);
        Assert.Equal(new NativeBattleCameraPoint(unchecked((short)(sign * 75)), 25, 0), first.Pose.Target);
        Assert.Equal(2048, first.Task.Process.Target.Accumulator);
        Assert.Equal(3, first.Task.Process.Target.ControlPoints!.Value.Length);
        NativeBattleGuardianCameraStep second = NativeBattleGuardianCameraProgram.Advance63E9C0(first.Task, Identity, first.Pose, false);
        Assert.Equal(new NativeBattleCameraPoint(unchecked((short)(sign * 100)), 100, 0), second.Pose.Target);
        NativeBattleGuardianCameraStep restored = NativeBattleGuardianCameraProgram.Advance63E9C0(JsonSerializer.Deserialize<NativeBattleGuardianCameraTask>(JsonSerializer.Serialize(first.Task))!, Identity, first.Pose, false);
        Assert.Equal(JsonSerializer.Serialize(second), JsonSerializer.Serialize(restored));
        Assert.Equal(2, NativeBattleGuardianCameraProgram.Advance63E9C0(second.Task, Identity, second.Pose, false).Result);
    }

    [Fact]
    public void Polynomial_pause_preserves_progress_and_full_control_points_without_reading_the_next_command()
    {
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(Process(), Words(0x84, 2, 2, 100, 0, 0, 100, 100, 0, 0x40, 2), 0);
        NativeBattleGuardianCameraStep first = NativeBattleGuardianCameraProgram.Advance63E9C0(task, Identity, Pose, false);
        NativeBattleGuardianCameraStep paused = NativeBattleGuardianCameraProgram.Advance63E9C0(first.Task, Identity, first.Pose, true);
        Assert.Equal(first.Task.Offset, paused.Task.Offset);
        Assert.Equal(first.Task.Frame, paused.Task.Frame);
        Assert.Equal(first.Task.Process.Target, paused.Task.Process.Target);
        Assert.Equal(first.Pose, paused.Pose);
    }

    [Fact]
    public void Following_camera_predicts_the_polynomial_bank_final_control_point_and_rejects_zero_duration()
    {
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(Process(), Words(0x81, 0, 0, 0, 0x41, 0, 0, -100, 0x84, 2, 2, 50, 0, 0, 100, 0, 0, 0x45, 2, 100, 0, -100), 0);
        NativeBattleGuardianCameraStep first = NativeBattleGuardianCameraProgram.Advance63E9C0(task, Identity, Pose, false);
        Assert.Equal(0, first.Task.Process.Eye.Step);
        Assert.Equal(100, first.Task.Process.Eye.Accumulator);
        NativeBattleGuardianCameraTask zero = NativeBattleGuardianCameraProgram.Begin63E960(Process(), Words(0x84, 0, 0), 0);
        Assert.Throws<NotSupportedException>(() => NativeBattleGuardianCameraProgram.Advance63E9C0(zero, Identity, Pose, false));
    }

    private static NativeBattleGuardianCameraBank Bank(NativeBattleVector position) => new(0, 0, 0, 0, position, default, default, 0);

    private static NativeBattleGuardianCameraProcess Process() => new(Bank(default), Bank(default), 1, null);

    private static ImmutableArray<byte> Words(params int[] words)
    {
        byte[] bytes = new byte[2 * words.Length];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2 * index, 2), unchecked((ushort)words[index]));
        }
        return bytes.ToImmutableArray();
    }
}
