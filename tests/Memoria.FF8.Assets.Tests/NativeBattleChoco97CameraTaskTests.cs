using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco97CameraTaskTests
{
    [Fact]
    public void Original_camera_task_obeys_mode_waits_publication_order_child_hold_and_every_cold_next()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleChoco97CameraResources resources = game.ReadNativeChoco97CameraResources();
        ImmutableArray<byte> actor = [17, 0, 25, 255, 19, 0, 0xA5, 0x5A];
        ImmutableArray<byte> target = [51, 0, 21, 255, 91, 0, 0xBC, 0xDE];
        NativeBattleModulePoolTask task = new(1, 0, null, NativeBattleChoco97CameraTask.Callback, new byte[0x70 - 12].ToImmutableArray());
        NativeBattleMoogleCameraState? camera = null;
        Assert.Single(Step(0).Publications);
        Until(2, 0);
        string waiting = NativeBattleSnapshotJson.Serialize(camera);
        task = task.WriteWord(0x24, ushort.MaxValue);
        Assert.Empty(Step(-1).Publications);
        Assert.Equal((ushort)0, task.ReadWord(0x24));
        Assert.Equal(waiting, NativeBattleSnapshotJson.Serialize(camera));
        Assert.Empty(Step(1).Publications);
        Assert.Equal((byte)2, task.ReadByte(0x29));
        Assert.Single(Step(2).Publications);
        Assert.Equal((byte)3, task.ReadByte(0x29));
        Assert.Single(Step(-1).Publications);
        Assert.Equal((byte)3, task.ReadByte(0x29));
        NativeBattleChoco97CameraTaskStep transition = Step(4);
        Assert.Equal(2, transition.Publications.Length);
        Assert.Equal(resources.Sources[1].Address, Source(transition.Publications[0].State));
        Assert.Equal(resources.Sources[2].Address, Source(transition.Publications[1].State));
        Assert.Equal((byte)4, task.ReadByte(0x29));
        Until(5, 4);
        Assert.Equal(resources.Sources[3].Address, Source(camera!));
        Assert.Equal(target.ToArray(), NativeBattleStorageSnapshots.Capture(camera!).Data.AsSpan().Slice(0xA4, 8).ToArray());
        Assert.Single(Step(5).Publications);
        Assert.Equal((byte)5, task.ReadByte(0x29));
        Assert.Single(Step(6).Publications);
        Assert.Equal((byte)6, task.ReadByte(0x29));
        task = task.WriteByte(0x28, 1);
        NativeBattleChoco97CameraTaskStep held = Step(6);
        Assert.Empty(held.Publications);
        Assert.False(held.ReadyToComplete);
        Assert.Equal((byte)7, task.ReadByte(0x29));
        task = task.WriteByte(0x28, 0);
        NativeBattleChoco97CameraTaskStep done = Step(6);
        Assert.Empty(done.Publications);
        Assert.True(done.ReadyToComplete);

        void Until(byte phase, short mode)
        {
            int callbacks = 0;
            while (task.ReadByte(0x29) != phase && callbacks++ < 4096)
            {
                _ = Step(mode);
            }
            Assert.Equal(phase, task.ReadByte(0x29));
        }

        NativeBattleChoco97CameraTaskStep Step(short mode)
        {
            NativeBattleModulePoolTask coldTask = NativeBattleSnapshotJson.Deserialize<NativeBattleModulePoolTask>(NativeBattleSnapshotJson.Serialize(task))!;
            NativeBattleMoogleCameraState? coldCamera = camera is null ? null : NativeBattleSnapshotJson.Deserialize<NativeBattleMoogleCameraState>(NativeBattleSnapshotJson.Serialize(camera));
            NativeBattleChoco97CameraTaskStep live = NativeBattleChoco97CameraTask.Advance72FBC0(task, camera, resources, mode, actor, target);
            NativeBattleChoco97CameraTaskStep cold = NativeBattleChoco97CameraTask.Advance72FBC0(coldTask, coldCamera, resources, mode, actor, target);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(live), NativeBattleSnapshotJson.Serialize(cold));
            task = live.Task;
            camera = live.Camera;
            return live;
        }

        static uint Source(NativeBattleMoogleCameraState state) => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleStorageSnapshots.Capture(state).Data.AsSpan().Slice(0xAC));
    }
}
