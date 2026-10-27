using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect218Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect218GuardianCameraTests
{
    [Fact]
    public void Synthetic_terminal_program_retires_without_camera_publication_or_CRT_draws_and_releases_432_byte_temporary()
    {
        NativeBattleCureFamilyPool profile = Pools[1];
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity);
        NativeBattleOriginalEffectState effect = new(218, 218, new(218, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = [pool] };
        NativeBattleEffect218Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { GuardianCameraProcess = NativeBattleGuardianCameraProgram.FromColdProcess() }, effect, new(0xBEEF), NativeBattleEffect218State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        NativeBattleEffect218Resources resources = new(null!, [], null!, null!, null!, null!, [], [], [], [], [], [], new("guardian-camera", [0, 0x80]), null!, 0, []);
        frame = BeginGuardianCamera63E960(frame, resources);
        uint address = frame.Effect.Pools[0].BankAddress + (uint)(frame.Effect.Pools[0].Head!.Value * frame.Effect.Pools[0].Stride);
        NativeBattleCameraPose pose = new(new(100, 200, 300), new(-400, 500, -600), 320, 12);
        NativeBattleEffect218RuntimeInputs inputs = new(null!, null!, false, _ => 0, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, _ => pose, (_, _) => throw new InvalidOperationException("Terminal program cannot publish a camera pose."));
        var actual = AdvanceGuardianCamera63E9C0(frame, address, resources, inputs);
        Assert.Equal(2, actual.Result);
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Equal((ushort)0x8000, actual.Frame.Memory.GuardianCameraProcess!.Target.Opcode);
        Assert.Equal((ushort)0, Read(actual.Frame, address).ReadWord(12));
        Assert.Equal(0xE25972u, Read(actual.Frame, address).ReadDword(20));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(actual.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Same(frame.Memory.Textures, actual.Frame.Memory.Textures);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void Roll_interpolation_preserves_signed_target_product_pause_and_snapshot_next_callback(int sign)
    {
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleCameraPose pose = new(new(1, 2, 3), new(4, 5, 6), 320, 300);
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(NativeBattleGuardianCameraProgram.FromColdProcess(), Words(0x89, 3, 0x8000, 0x40, 3, 0x8000), sign == 1 ? 0 : 2, () => 1);
        NativeBattleGuardianCameraStep first = NativeBattleGuardianCameraProgram.Advance63E9C0(task, matrix, pose, false);
        int delta = -32768 * sign - 300;
        Assert.Equal(unchecked((short)(300 + delta / 3)), first.Pose.Roll);
        Assert.Equal(delta, first.Task.Process.Target.Accumulator);
        Assert.Equal(3, first.Task.Process.Target.Step);
        Assert.Equal((short)300, first.Task.Process.Target.Curve);
        Assert.Equal((ushort)2, first.Task.Process.Target.Remaining);
        NativeBattleGuardianCameraStep paused = NativeBattleGuardianCameraProgram.Advance63E9C0(first.Task, matrix, first.Pose, true);
        Assert.Equal(first.Pose.Roll, paused.Pose.Roll);
        Assert.Equal(first.Task.Frame, paused.Task.Frame);
        Assert.Equal(first.Task.Process.Target, paused.Task.Process.Target);
        NativeBattleGuardianCameraTask restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleGuardianCameraTask>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(paused.Task)))!);
        NativeBattleGuardianCameraStep second = NativeBattleGuardianCameraProgram.Advance63E9C0(restored, matrix, paused.Pose, false);
        NativeBattleGuardianCameraStep expected = NativeBattleGuardianCameraProgram.Advance63E9C0(first.Task, matrix, first.Pose, false);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(expected)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(second)));
        Assert.Equal(unchecked((short)(300 + 2 * delta / 3)), second.Pose.Roll);
        NativeBattleGuardianCameraStep third = NativeBattleGuardianCameraProgram.Advance63E9C0(second.Task, matrix, second.Pose, false);
        Assert.Equal((short)-32768, third.Pose.Roll);
        NativeBattleGuardianCameraStep terminal = NativeBattleGuardianCameraProgram.Advance63E9C0(third.Task, matrix, third.Pose, false);
        Assert.Equal(2, terminal.Result);
        Assert.False(terminal.Published);
        Assert.Equal(third.Pose, terminal.Pose);
    }

    [Fact]
    public void Retained_roll_counter_is_sign_extended_from_its_WORD_before_interpolation()
    {
        NativeBattleGuardianCameraProcess process = NativeBattleGuardianCameraProgram.FromColdProcess();
        process = process with { Eye = process.Eye with { Remaining = 1 }, Target = process.Target with { Opcode = 9, Remaining = ushort.MaxValue, Accumulator = 9, Step = 3, Curve = 0 }, Sign = 1 };
        NativeBattleGuardianCameraTask task = new(process, Words(0x8000), 0, 1, false);
        NativeBattleGuardianCameraStep actual = NativeBattleGuardianCameraProgram.Advance63E9C0(task, NativeBattleCuragaMatrices.Identity8DD770(), new(new(0, 0, 0), new(0, 0, 0), 320, 0), false);
        Assert.Equal((short)15, actual.Pose.Roll);
        Assert.Equal((ushort)0xFFFE, actual.Task.Process.Target.Remaining);
    }

    private static ImmutableArray<byte> Words(params ushort[] words)
    {
        byte[] bytes = new byte[words.Length * 2];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2), words[index]);
        }
        return bytes.ToImmutableArray();
    }
}
