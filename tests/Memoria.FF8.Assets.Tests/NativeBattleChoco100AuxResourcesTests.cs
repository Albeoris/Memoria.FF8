using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco100AuxResourcesTests
{
    [Fact]
    public void Original_camera_Y_program_keeps_its_actual_address_and_shared_values()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        var program = game.ReadNativeChoco100CameraYProgram();
        Assert.Same(program, game.ReadNativeChoco100CameraYProgram());
        Assert.Equal(0x1529F70u, program.Address);
        Assert.Equal(new short[] { 88, -80, 72, -64, 56, -48, 56, -48, 127 }, program.Values);
        Assert.Equal(game.ReadNativeChoco99MeteoResources().CameraY.Values, program.Values);
        Assert.Equal(JsonSerializer.Serialize(program), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleChoco99CameraYProgram>(JsonSerializer.Serialize(program))));
    }
}
