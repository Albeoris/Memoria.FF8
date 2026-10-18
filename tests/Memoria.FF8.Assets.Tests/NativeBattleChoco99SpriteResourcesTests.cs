using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco99SpriteResourcesTests
{
    [Fact]
    public void Original_B0_sprite_uses_AC_delay_and_preserves_6C_and_tail_through_actual_frames_and_cold_next()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        NativeBattleChoco97SpriteResources resource = game.ReadNativeChoco99SpriteResources();
        Assert.Same(resource, game.ReadNativeChoco99SpriteResources());
        Assert.Equal(0x1529FB4u, resource.Address);
        Assert.Equal(game.ReadNativeChoco97SpriteResources().Bytes.ToArray(), resource.Bytes.ToArray());
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 16000, 4096);
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, NativeBattleChoco99Sprite.Callback, new byte[0xB0 - 12].ToImmutableArray()).WriteWord(0x6C, 0xBEEF).WriteWord(0x70, 0xCAFE).WriteWord(0xAE, 0xABCD);
        List<ushort> frames = [];
        NativeBattleChoco97SpriteStep? step = null;
        for (int callback = 1; callback <= 27; callback++)
        {
            step = Advance(task);
            NativeBattleModulePoolTask restored = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(task))!;
            Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(Advance(restored)));
            Assert.Equal(0xB0 - 12, step.Task.Payload.Length);
            Assert.Equal((ushort)0xBEEF, step.Task.ReadWord(0x6C));
            Assert.Equal((ushort)0xCAFE, step.Task.ReadWord(0x70));
            Assert.Equal((ushort)0xABCD, step.Task.ReadWord(0xAE));
            if (callback == 1)
            {
                Assert.Equal((ushort)4, step.Task.ReadWord(0xAC));
            }
            if (callback == 5)
            {
                Assert.Equal((ushort)0, step.Task.ReadWord(0xAC));
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
        NativeBattleModulePoolTask truncated = task with { Payload = task.Payload.Take(0x70 - 12).ToImmutableArray() };
        Assert.Throws<InvalidDataException>(() => Advance(truncated));
        Assert.Throws<InvalidDataException>(() => Advance(task with { Callback = 0x730C10 }));

        NativeBattleChoco97SpriteStep Advance(NativeBattleModulePoolTask current) => NativeBattleChoco99Sprite.Advance71EE40(current, resource, [10, 0, 20, 0, 30, 0, 40, 0], view, projection, 256);
    }
}
