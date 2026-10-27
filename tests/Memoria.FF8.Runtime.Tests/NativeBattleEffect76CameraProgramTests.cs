using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect76CameraProgramTests
{
    [Fact]
    public void Opcode10_consumes_only_its_command_and_imports_actual_world_points_and_padding()
    {
        NativeBattleActionMatrix matrix = Identity() with { X = 10, Y = 20, Z = 30 };
        NativeBattleCameraPose pose = new(new(101, 202, 303), new(401, 502, 603), 512, 7);
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(NativeBattleGuardianCameraProgram.FromColdProcess(), Words(0x8A, 0x4A, 0x80, 2, 0x40, 2, 0x8000), 0);
        NativeBattleEffectArena temporary = Temporary(0x1111, 0x2222);
        NativeBattleGuardianCameraStep step = NativeBattleGuardianCameraProgram.Advance63E9C0(task, matrix, pose, false, temporary432: temporary, currentEyePadding: 0x3333, currentTargetPadding: -17476);
        Assert.Equal(12, step.Task.Offset);
        Assert.Equal(new NativeBattleVector(91, 182, 273), step.Task.Process.Eye.Current);
        Assert.Equal(new NativeBattleVector(391, 482, 573), step.Task.Process.Target.Current);
        Assert.Equal((short)0x3333, step.Task.Process.Eye.CurrentPadding);
        Assert.Equal((short)-17476, step.Task.Process.Target.CurrentPadding);
        Assert.Equal(pose, step.Pose);
        Assert.Equal(0x1111, Word(step.Temporary432!, 6));
        Assert.Equal(0x2222, Word(step.Temporary432!, 14));
    }

    [Fact]
    public void Opcode10_uses_transposed_rotation_after_wrapped_translation_subtraction()
    {
        NativeBattleActionMatrix matrix = new([0, 0, 4096, 0, 4096, 0, -4096, 0, 0], 10, 20, 30, 4096);
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(NativeBattleGuardianCameraProgram.FromColdProcess(), Words(0x8A, 0x4A, 0x80, 1, 0x40, 1, 0x8000), 0);
        NativeBattleCameraPose pose = new(new(12, 23, 34), new(15, 26, 37), 288, -1);
        NativeBattleGuardianCameraStep step = NativeBattleGuardianCameraProgram.Advance63E9C0(task, matrix, pose, false, temporary432: Temporary(0, 0));
        Assert.Equal(new NativeBattleVector(-4, 3, 2), step.Task.Process.Eye.Current);
        Assert.Equal(new NativeBattleVector(-7, 6, 5), step.Task.Process.Target.Current);
        Assert.Equal(pose, step.Pose);
        Assert.Null(step.Task.Process.Eye.CurrentPadding);
        Assert.Null(step.Task.Process.Target.CurrentPadding);
    }

    [Fact]
    public void Case5_default_prediction_copies_other_bank_W_before_world_XYZ_publication()
    {
        NativeBattleGuardianCameraProcess process = NativeBattleGuardianCameraProgram.FromColdProcess();
        process = process with { Eye = process.Eye with { Current = new(10, 0, 0) }, Target = process.Target with { CurrentPadding = 0x3456 } };
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(process, Words(0x45, 2, 100, 0, 0, 0x80, 2, 0x8000), 0);
        NativeBattleGuardianCameraStep step = NativeBattleGuardianCameraProgram.Advance63E9C0(task, Identity(), Pose(), false, temporary432: Temporary(0x1111, 0x2222));
        Assert.Equal(0x1111, Word(step.Temporary432!, 6));
        Assert.Equal(0x3456, Word(step.Temporary432!, 14));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Case5_WORD_predictions_retain_the_temporary_W_instead_of_copying_bank_padding(ushort otherOpcode)
    {
        NativeBattleGuardianCameraProcess process = NativeBattleGuardianCameraProgram.FromColdProcess();
        process = process with { Eye = process.Eye with { Current = new(10, 0, 0) }, Target = process.Target with { Opcode = otherOpcode, Remaining = 2, Delta = new(1, 0, 0), CurrentPadding = 0x3456 } };
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(process, Words(0x45, 2, 100, 0, 0, 0x8000), 0) with { Frame = 1 };
        NativeBattleGuardianCameraStep step = NativeBattleGuardianCameraProgram.Advance63E9C0(task, Identity(), Pose(), false, temporary432: Temporary(0x1111, 0x2222));
        Assert.Equal(0x2222, Word(step.Temporary432!, 14));
    }

    [Fact]
    public void Default_prediction_copies_unknown_bank_padding_as_unknown()
    {
        NativeBattleGuardianCameraProcess process = NativeBattleGuardianCameraProgram.FromColdProcess();
        process = process with { Eye = process.Eye with { Current = new(10, 0, 0) } };
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(process, Words(0x45, 2, 100, 0, 0, 0x80, 2, 0x8000), 0);
        NativeBattleGuardianCameraStep step = NativeBattleGuardianCameraProgram.Advance63E9C0(task, Identity(), Pose(), false, temporary432: Temporary(0x1111, 0x2222));
        Assert.Throws<NotSupportedException>(() => step.Temporary432!.Read(14, 2));
        Assert.Equal(0x1111, Word(step.Temporary432!, 6));
    }

    [Fact]
    public void Imported_bank_padding_survives_JSON_and_the_next_callback()
    {
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(NativeBattleGuardianCameraProgram.FromColdProcess(), Words(0x8A, 0x4A, 0x80, 2, 0x40, 2, 0x8000), 0);
        NativeBattleGuardianCameraStep first = NativeBattleGuardianCameraProgram.Advance63E9C0(task, Identity(), Pose(), false, temporary432: Temporary(0x1111, 0x2222), currentEyePadding: -123, currentTargetPadding: 456);
        NativeBattleGuardianCameraTask restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleGuardianCameraTask>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first.Task)))!);
        NativeBattleGuardianCameraStep direct = NativeBattleGuardianCameraProgram.Advance63E9C0(first.Task, Identity(), first.Pose, false, temporary432: first.Temporary432);
        NativeBattleGuardianCameraStep next = NativeBattleGuardianCameraProgram.Advance63E9C0(restored, Identity(), first.Pose, false, temporary432: first.Temporary432);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(direct)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(next)));
        Assert.Equal((short)-123, next.Task.Process.Eye.CurrentPadding);
        Assert.Equal((short)456, next.Task.Process.Target.CurrentPadding);
    }

    [Fact]
    public void Pause_preserves_program_cursor_bank_padding_and_frame()
    {
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(NativeBattleGuardianCameraProgram.FromColdProcess(), Words(0x8A, 0x4A, 0x80, 2, 0x40, 2, 0x8000), 0);
        NativeBattleGuardianCameraStep step = NativeBattleGuardianCameraProgram.Advance63E9C0(task, Identity(), Pose(), true, temporary432: Temporary(0x1111, 0x2222), currentEyePadding: 123, currentTargetPadding: 456);
        Assert.Equal(0, step.Task.Offset);
        Assert.Equal(0, step.Task.Frame);
        Assert.Null(step.Task.Process.Eye.CurrentPadding);
        Assert.Null(step.Task.Process.Target.CurrentPadding);
        Assert.Equal(0x1111, Word(step.Temporary432!, 6));
        Assert.Equal(0x2222, Word(step.Temporary432!, 14));
    }

    private static ushort Word(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(source.Read(offset, 2));
    private static NativeBattleCameraPose Pose() => new(new(1, 2, 3), new(4, 5, 6), 512, 0);
    private static NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static ImmutableArray<byte> Words(params ushort[] words) => words.SelectMany(BitConverter.GetBytes).ToImmutableArray();
    private static NativeBattleEffectArena Temporary(ushort first, ushort second) => NativeBattleEffectArena.Clear().MarkUnknown(0, 432).Write(6, BitConverter.GetBytes(first)).Write(14, BitConverter.GetBytes(second));
}
