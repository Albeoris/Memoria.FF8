using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco98SpriteResourcesTests
{
    [Fact]
    public void Original_sprite_uses_separate_address_with_one_current_twenty_frame_algorithm_and_cold_next()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        NativeBattleChoco97SpriteResources resource = game.ReadNativeChoco98SpriteResources();
        Assert.Same(resource, game.ReadNativeChoco98SpriteResources());
        Assert.Equal(0x152AF40u, resource.Address);
        Assert.Equal(game.ReadNativeChoco97SpriteResources().Bytes.ToArray(), resource.Bytes.ToArray());
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 16000, 4096);
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleModulePoolTask task = new(1, 0, null, NativeBattleChoco98Sprite.Callback, new byte[0x70 - 12].ToImmutableArray());
        List<ushort> frames = [];
        NativeBattleChoco97SpriteStep? step = null;
        for (int callback = 0; callback < 27; callback++)
        {
            step = Advance(task);
            NativeBattleModulePoolTask restored = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(task))!;
            Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(Advance(restored)));
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
        Assert.Equal(resource.Address, task.ReadDword(0x4C));
        Assert.Throws<InvalidDataException>(() => Advance(task with { Callback = 0x730C10 }));
        Assert.Throws<InvalidDataException>(() => NativeBattleChoco98Sprite.Advance728780(task, game.ReadNativeChoco97SpriteResources(), [], view, projection, 0));

        NativeBattleChoco97SpriteStep Advance(NativeBattleModulePoolTask current) => NativeBattleChoco98Sprite.Advance728780(current, resource, [10, 0, 20, 0, 30, 0, 40, 0], view, projection, 256);
    }
}
