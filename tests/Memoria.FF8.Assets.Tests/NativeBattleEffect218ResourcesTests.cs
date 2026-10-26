using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect218ResourcesTests
{
    [Fact]
    public void Original_complete_camera_program_reaches_its_actual_terminator_and_restores_during_roll_interpolation()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original effect218 resources.");
        using GameAssets game = new(directory!);
        NativeBattleEffect218Resources resources = game.ReadNativeEffect218Resources();
        resources.Validate();
        Assert.Equal(354, resources.Camera.Data.Length);
        Assert.Equal((ushort)0x8000, BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan().Slice(352, 2)));
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(NativeBattleGuardianCameraProgram.FromColdProcess(), resources.Camera.Data, 0);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleCameraPose pose = new(new(1, 2, 3), new(4, 5, 6), 320, 0);
        bool restoredRoll = false;
        int callbacks = 0;
        while (!task.Complete && callbacks < 1024)
        {
            NativeBattleGuardianCameraStep step = NativeBattleGuardianCameraProgram.Advance63E9C0(task, matrix, pose, false);
            if (!restoredRoll && step.Task.Process.Target.Opcode == 9 && step.Task.Process.Target.Remaining > 0)
            {
                NativeBattleGuardianCameraTask restored = JsonSerializer.Deserialize<NativeBattleGuardianCameraTask>(JsonSerializer.Serialize(step.Task))!;
                NativeBattleGuardianCameraStep actual = NativeBattleGuardianCameraProgram.Advance63E9C0(restored, matrix, step.Pose, false);
                NativeBattleGuardianCameraStep expected = NativeBattleGuardianCameraProgram.Advance63E9C0(step.Task, matrix, step.Pose, false);
                Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
                restoredRoll = true;
            }
            Assert.Equal(step.Task.Complete ? 2 : 0, step.Result);
            Assert.Equal(!step.Task.Complete, step.Published);
            task = step.Task;
            pose = step.Pose;
            callbacks++;
        }
        Assert.True(task.Complete);
        Assert.Equal(580, callbacks);
        Assert.True(restoredRoll);
        Assert.Equal(354, task.Offset);
        Assert.Equal((short)0, pose.Roll);
    }
}
