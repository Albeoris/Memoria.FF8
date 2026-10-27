using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325PuffTests
{
    [Fact]
    public void Camera_zero_changes_only_effect_globals_and_reset_preserves_unused_record_bytes()
    {
        NativeBattleDiablos325Frame frame = Frame();
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(0x4C5C, Enumerable.Repeat((byte)0xCC, 2560).ToArray()) } };
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { EyePaddingF6 = 7, TargetPaddingFE = 9 };
        var preserved = NativeBattleStorageSnapshots.Capture(frame);
        string original = NativeBattleTestSnapshots.Json(preserved);
        NativeBattleDiablos325TailStep step = NativeBattleDiablos325OpeningTail.Advance656203(frame, camera, 0);
        NativeBattleCameraPose expected = new(new(0, -1796, 5667), new(0, -3166, -3315), 288, 0);
        Assert.Equal(camera.WriteEffectGlobals(expected), step.Camera);
        Assert.Equal(original, NativeBattleTestSnapshots.Json(preserved));
        Assert.Equal((byte)0, step.Frame.Memory.Diablos325!.Mode51F0);
        for (int index = 0; index < 160; index++)
        {
            Assert.Equal(Enumerable.Repeat((byte)0xCC, 15), step.Frame.Memory.Arena!.Read(0x4C5C + index * 16, 15));
            Assert.Equal((byte)0, step.Frame.Memory.Arena.Read(0x4C6B + index * 16, 1)[0]);
        }
    }

    [Fact]
    public void Birth_consumes_four_draws_per_slot_and_rise_uses_signed_velocities_then_wraps_acceleration()
    {
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Puffs.Reset6545B0(Frame());
        NativeCrtRandomState random = frame.Random!;
        random = random.Draw(out ushort life);
        random = random.Draw(out ushort shade);
        random = random.Draw(out ushort position);
        random = random.Draw(out _);
        int x = (position & 0x1FFF) - 4096;
        int count = (life & 7) + 20;
        int cosine = unchecked((sbyte)NativeBattleCameraMath.Cosine(x >> 4));
        NativeBattleDiablos325Frame born = NativeBattleDiablos325Puffs.Spawn658B90(frame, 1, -3072, false);
        Assert.Equal(random, born.Random);
        byte[] bytes = born.Memory.Arena!.Read(0x4C5C, 16);
        Assert.Equal(0xFD67A0u, BinaryPrimitives.ReadUInt32LittleEndian(bytes));
        Assert.Equal((short)x, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
        Assert.Equal(unchecked((short)(-3072 - cosine)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)));
        Assert.Equal((byte)((shade & 15) + 64), bytes[11]);
        Assert.Equal(unchecked((byte)(x / ((-1 - count) * 4))), bytes[12]);
        Assert.Equal((byte)count, bytes[15]);
        NativeBattleDiablos325Frame preservedBorn = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(born));
        NativeBattleDiablos325Frame moved = NativeBattleDiablos325Puffs.Rise658D30(born);
        byte[] after = moved.Memory.Arena!.Read(0x4C5C, 16);
        Assert.Equal(unchecked((short)(x + (unchecked((sbyte)bytes[12]) << 2))), BinaryPrimitives.ReadInt16LittleEndian(after.AsSpan(4)));
        Assert.Equal((byte)13, after[13]);
        Assert.Equal((byte)(count - 1), after[15]);
        Assert.Equal(unchecked((byte)(bytes[11] - 2)), after[11]);
        Assert.Equal(random, moved.Random);
        Assert.Equal((byte)16, NativeBattleDiablos325Puffs.Shrink658C80(preservedBorn).Memory.Arena!.Read(0x4C69, 1)[0]);
    }

    [Fact]
    public void Full_bank_consumes_no_random_and_busy_cursor_search_restarts_at_first_slot()
    {
        NativeBattleDiablos325Frame frame = Frame();
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        for (int index = 0; index < 160; index++)
        {
            arena = arena.Write(0x4C6B + index * 16, [1]);
        }
        frame = frame with { Memory = frame.Memory with { Arena = arena, Diablos325 = frame.Memory.Diablos325! with { ParticleCursor51F4 = NativeBattleEffectNodeMemory.ArenaAddress + 0x4CAC } } };
        Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(NativeBattleDiablos325Puffs.Spawn658B90(frame, 4, 0, false)));
        frame = frame with { Memory = frame.Memory with { Arena = arena.Write(0x4C6B, [0]) } };
        NativeBattleDiablos325Frame result = NativeBattleDiablos325Puffs.Spawn658B90(frame, 1, 0, false);
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x4C6Cu, result.Memory.Diablos325!.ParticleCursor51F4);
        Assert.Same(frame, NativeBattleDiablos325Puffs.Spawn658B90(frame, 0, 0, false));
    }

    [Fact]
    public void Counter_thirty_prewarm_runs_twenty_spawn_shrink_steps_and_preserves_camera_focal_and_roll()
    {
        NativeBattleDiablos325Frame frame = Frame();
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { FocalLength = 123, Roll = 17 };
        NativeCrtRandomState expected = frame.Random!;
        for (int index = 0; index < 320; index++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleDiablos325TailStep step = NativeBattleDiablos325OpeningTail.Advance656203(frame, camera, 30);
        Assert.Equal(expected, step.Frame.Random);
        Assert.Equal(camera.WriteEffectGlobals(new(new(0, -18240, 2996), new(0, -16384, -2100), 123, 17)), step.Camera);
        Assert.Equal((byte)3, step.Frame.Memory.Diablos325!.Mode51F0);
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x515Cu, step.Frame.Memory.Diablos325.ParticleCursor51F4);
    }

    private static NativeBattleDiablos325Frame Frame()
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        return frame with { Random = new(1), Memory = frame.Memory with { Diablos325 = frame.Memory.Diablos325! with { CenterZ51D4 = -2100 } } };
    }
}
