using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeDisplayOffsetTests
{
    [Fact]
    public void Reached_chamber_linear_offset_consumes_original_operands_and_restores_VM_and_pixels()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        byte[] original = File.ReadAllBytes(source);
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        world.SetRenderSize(320, 240);
        Assert.Equal(130, world.Events.FieldId);
        Assert.Equal(4, world.Events.Frame);
        Assert.Equal(NativeDisplayOffsetState.AllocatedZero, world.Events.Actors[9].NativeDisplayOffset);
        PlayerAction exactPreviouslyRefused = new(PlayerActionKind.Move, Horizontal: 0.5331318f, Vertical: -0.84603226f, Frames: 1);
        Assert.True(world.Apply(exactPreviouslyRefused).Accepted);
        Assert.Equal(5, world.Events.Frame);
        Assert.Null(world.Events.LastFault);
        Assert.Equal((0, 0, 0, 0, 700, -932, 1, 1, 1), Words(world.Events.Actors[9].NativeDisplayOffset!));
        Assert.Equal("LOFFSET", world.Events.Actors[9].NativeDisplayOffset!.Command!.Opcode);
        Assert.Equal(251, world.Events.Actors[9].NativeDisplayOffset!.Command!.Operation);
        session.SetEventState(world.Events.Capture());
        string save = Path.Combine(Path.GetTempPath(), "Memoria.FF8-offset-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(save);
            using GameWorld resumed = new(new GameSession(GameSession.Load(save)), directory);
            resumed.EnsureField();
            resumed.SetRenderSize(320, 240);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
            for (int tick = 0; tick < 12; tick++)
            {
                PlayerAction wait = new(PlayerActionKind.Wait);
                Assert.Equal(world.Apply(wait), resumed.Apply(wait));
                Assert.Null(world.Events.LastFault);
                Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
                Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
                Assert.Equal((0, 700, -932, 0, 700, -932, 1, 1, 3), Words(world.Events.Actors[9].NativeDisplayOffset!));
            }
        }
        finally
        {
            if (File.Exists(save))
            {
                File.Delete(save);
            }
        }
        Assert.Equal(original, File.ReadAllBytes(source));
    }

    private static (int X, int Y, int Z, int TargetX, int TargetY, int TargetZ, int Duration, int Counter, int Mode) Words(NativeDisplayOffsetState state)
    {
        return (state.X, state.Y, state.Z, state.TargetX, state.TargetY, state.TargetZ, state.Duration, state.Counter, state.Mode);
    }
}
