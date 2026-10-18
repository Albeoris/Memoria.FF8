using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco99MeteoResourcesTests
{
    [Fact]
    public void Original_projected_resources_and_both_programs_have_exact_reached_bounds_and_values()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        NativeBattleChoco99MeteoResources resources = game.ReadNativeChoco99MeteoResources();
        Assert.Same(resources, game.ReadNativeChoco99MeteoResources());
        resources.Validate();
        Assert.Equal(new short[] { 88, -80, 72, -64, 56, -48, 56, -48, 127 }, resources.CameraY.Values);
        Assert.Equal(new uint[] { 0x00C0C0C0, 0x00808080, 0x00404040, 0xFF000000 }, resources.Overlay.Commands);
        foreach (NativeBattleChoco97SpriteResources sprite in resources.Secondary.Prepend(resources.Meteo))
        {
            NativeBattleModuleResource module = new(0, sprite.Address, 0, 0, sprite.Bytes);
            int count = sprite.Address == resources.Meteo.Address ? 15 : 7;
            for (int frame = 0; frame < count; frame++)
            {
                Assert.NotNull(module.ProjectedFrame571C80(frame));
            }
        }
        NativeBattleChoco99MeteoResources restored = JsonSerializer.Deserialize<NativeBattleChoco99MeteoResources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Original_meteo_and_secondary_sprite_callbacks_preserve_actual_frames_and_cold_next_packets()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        NativeBattleChoco99MeteoResources resources = game.ReadNativeChoco99MeteoResources();
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 12000 };
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        foreach (uint callback in new uint[] { 0x720500, 0x7208D0 })
        {
            NativeBattleModulePoolTask task = new(1, 0, null, callback, new byte[0xB0 - 12].ToImmutableArray());
            List<ushort> frames = [];
            bool terminal = false;
            for (int index = 0; !terminal && index < 64; index++)
            {
                NativeBattleModulePoolTask restored = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(task))!;
                NativeBattleChoco97SpriteStep step = Advance(task);
                Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(Advance(restored)));
                if (step.Packets is not null)
                {
                    frames.Add(step.Task.ReadWord(0x50));
                }
                task = step.Task;
                terminal = step.ReadyToComplete;
            }
            Assert.True(terminal);
            Assert.Equal(Enumerable.Range(0, callback == 0x720500 ? 15 : 7).Select(value => (ushort)value).ToArray(), frames.ToArray());
            Assert.Equal((byte)5, task.ReadByte(0x26));
        }

        NativeBattleChoco97SpriteStep Advance(NativeBattleModulePoolTask task) => task.Callback == 0x720500 ? NativeBattleChoco99Meteo.Advance720500(task, resources, 0x19F0F40, (parent, operation) =>
        {
            Assert.Equal(new NativeBattleChoco97AppendProgram(0x720610, 0x19F0F40, 0x2C, 4, 0), operation);
            return parent;
        }, view, projection, 512) : NativeBattleChoco99Meteo.Advance7208D0(task, resources, _ => false, view, projection, 512);
    }
}
