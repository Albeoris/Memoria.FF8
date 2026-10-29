using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco100SpriteResourcesTests
{
    [Fact]
    public void Original_sprite_uses_70_byte_task_and_6C_delay_through_actual_frames_and_cold_next()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        NativeBattleChoco97SpriteResources resource = game.ReadNativeChoco100SpriteResources();
        Assert.Same(resource, game.ReadNativeChoco100SpriteResources());
        Assert.Equal(0x1529804u, resource.Address);
        Assert.Equal(game.ReadNativeChoco97SpriteResources().Bytes.ToArray(), resource.Bytes.ToArray());
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 16000, 4096);
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, NativeBattleChoco100Sprite.Callback, new byte[0x70 - 12].ToImmutableArray());
        List<ushort> frames = [];
        NativeBattleChoco97SpriteStep? step = null;
        for (int callback = 1; callback <= 27; callback++)
        {
            NativeBattleModulePoolTask restored = NativeBattleSnapshotJson.Deserialize<NativeBattleModulePoolTask>(NativeBattleSnapshotJson.Serialize(task))!;
            step = Advance(task);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(step), NativeBattleSnapshotJson.Serialize(Advance(restored)));
            Assert.Equal(0x70 - 12, NativeBattleStorageSnapshots.Capture(step.Task).Payload.Length);
            if (callback == 1)
            {
                Assert.Equal((ushort)4, step.Task.ReadWord(0x6C));
            }
            if (callback == 5)
            {
                Assert.Equal((ushort)0, step.Task.ReadWord(0x6C));
            }
            if (step.Packets is { } packets)
            {
                Assert.NotEmpty(packets.Primitives);
                frames.Add(step.Task.ReadWord(0x50));
            }
            task = step.Task;
        }
        Assert.Equal(Enumerable.Range(0, 10).Select(value => (ushort)value).Concat(new ushort[] { 8, 9 }).Concat(Enumerable.Range(10, 10).Select(value => (ushort)value)).ToArray(), frames.ToArray());
        Assert.True(step!.ReadyToComplete);
        Assert.Null(step.Packets);
        NativeBattleModulePoolTask truncated = new(task.Flags, task.State, task.Next, task.Callback, NativeBattleStorageSnapshots.Capture(task).Payload.Take(0x6E - 12).ToImmutableArray());
        Assert.Throws<InvalidDataException>(() => Advance(truncated));
        Assert.Throws<InvalidDataException>(() => Advance(new(task.Flags, task.State, task.Next, 0x730C10, NativeBattleStorageSnapshots.Capture(task).Payload)));

        NativeBattleChoco97SpriteStep Advance(NativeBattleModulePoolTask current) => NativeBattleChoco100Sprite.Advance715AB0(current, resource, [10, 0, 20, 0, 30, 0, 40, 0], view, projection, 256);
    }
}
