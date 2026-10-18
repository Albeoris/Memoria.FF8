using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco97SpriteResourcesTests
{
    [Fact]
    public void Original_projected_sprite_draws_its_twenty_frames_and_cold_next_without_crossing_the_IO_script()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        NativeBattleChoco97SpriteResources resources = game.ReadNativeChoco97SpriteResources();
        Assert.Same(resources, game.ReadNativeChoco97SpriteResources());
        Assert.Equal(0x152B5C8u, resources.Address);
        Assert.Equal(0x152B930u, resources.Address + (uint)resources.Bytes.Length);
        NativeBattleModuleResource projected = new(0, resources.Address, 0, 0, resources.Bytes);
        for (int frame = 0; frame < 20; frame++)
        {
            NativeBattleModuleSpriteFrame row = projected.ProjectedFrame571C80(frame);
            Assert.Equal(frame < 3 ? 20 : 40, row.Quads.Length);
            Assert.Equal(frame == 19, row.NextOffset < 0);
        }
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 16000, 4096);
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleModulePoolTask task = new(1, 0, null, 0x730C10, new byte[0x70 - 12].ToImmutableArray());
        int drawn = 0;
        NativeBattleChoco97SpriteStep? step = null;
        for (int callback = 0; callback < 27; callback++)
        {
            step = Advance(task);
            NativeBattleModulePoolTask restored = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(task))!;
            Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(Advance(restored)));
            if (step.Packets is { } packets)
            {
                Assert.NotEmpty(packets.Primitives);
                drawn++;
            }
            task = step.Task;
        }
        Assert.Equal(22, drawn);
        Assert.True(step!.ReadyToComplete);
        Assert.Null(step.Packets);
        Assert.Equal((ushort)19, task.ReadWord(0x50));
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco97SpriteResourcesReader.Read(new byte[128]));

        NativeBattleChoco97SpriteStep Advance(NativeBattleModulePoolTask current) => NativeBattleChoco97Sprite.Advance730C10(current, resources, [10, 0, 20, 0, 30, 0, 40, 0], view, projection, 256);
    }
}
