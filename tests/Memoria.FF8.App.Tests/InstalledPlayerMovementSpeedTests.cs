using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class InstalledPlayerMovementSpeedTests
{
    [Theory]
    [InlineData(50, false)]
    [InlineData(100, false)]
    [InlineData(150, false)]
    [InlineData(300, false)]
    [InlineData(50, true)]
    [InlineData(100, true)]
    [InlineData(150, true)]
    [InlineData(300, true)]
    public void Configured_speed_reaches_real_collision_adapter_and_survives_save_reload(int percent, bool walking)
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("App.Tests.InstalledPlayerMovementSpeedTests.1");
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(source));
        using GameAssets assets = new(directory);
        GameSession session = new(PlayerStateLoader.Load(source, directory, sharedAssets: assets));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        world.SetRenderSize(320, 224);
        world.ConfigureMovementSpeeds(percent, 100);
        Assert.Equal(775, world.Events.FieldId);
        Assert.Equal(1144, world.Events.Frame);
        Assert.Equal(new FieldFixedPosition(2867200, -8396800, 0), world.Events.Scene.NativePlayerPosition);
        // Continue only the idle interval between the concrete save and the reported input.
        for (int tick = 0; tick < 36; tick++)
        {
            Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        Assert.True(world.Apply(new(PlayerActionKind.FieldButtons, Value: walking ? 0x10 : 0)).Accepted);
        NativePlayerMovementQuery? actual = null;
        var resolve = world.Events.ResolveNativePlayerMovement!;
        world.Events.ResolveNativePlayerMovement = query =>
        {
            actual = query;
            return resolve(query);
        };
        PlayerAction move = new(PlayerActionKind.Move, Horizontal: -1);
        FieldFixedPosition before = world.Events.Scene.NativePlayerPosition!;
        Assert.True(world.Apply(move).Accepted);
        Assert.NotNull(actual);
        Assert.Equal(percent, actual.SpeedPercent);
        int speed = PlayerMovementSpeed.Scale(walking ? 2696 : 7929, percent);
        Assert.Equal(speed, actual.Speed);
        Assert.Equal(percent == 300 && !walking ? before : new FieldFixedPosition(before.X + 16 * speed, before.Y, before.Z), world.Events.Scene.NativePlayerPosition);
        Assert.Equal(1181, world.Events.Frame);
        Assert.Null(world.Events.LastFault);

        string saved = Path.Combine(Path.GetTempPath(), $"movement-speed-{Guid.NewGuid():N}.json");
        try
        {
            session.Save(saved, compact: true);
            string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOVEMENT_SAVE_OUTPUT");
            if (percent == 150 && !walking && output is not null)
            {
                File.Copy(saved, output, overwrite: false);
            }
            using GameWorld restored = new(new GameSession(GameSession.Load(saved)), directory, sharedAssets: assets);
            restored.EnsureField();
            restored.SetRenderSize(320, 224);
            restored.ConfigureMovementSpeeds(percent, 100);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
            Assert.True(world.Apply(new(PlayerActionKind.FieldButtons, Value: walking ? 0x10 : 0)).Accepted);
            Assert.True(restored.Apply(new(PlayerActionKind.FieldButtons, Value: walking ? 0x10 : 0)).Accepted);
            Assert.Equal(world.Apply(move), restored.Apply(move));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
            Console.WriteLine($"Field775 speed={percent}% walking={walking}: frame={world.Events.Frame}, position={world.Events.Scene.NativePlayerPosition}; real collision adapter and disk reload agree.");
        }
        finally
        {
            File.Delete(saved);
        }
        Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(source)));
    }
}
