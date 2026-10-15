using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeDoorSuspensionTests
{
    [Fact]
    public void AuthenticEarthquakeCrossingSuspendsAndResumesMeaningfulDefaultAcrossRestore()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        GameState recreated = SnapshotFixtures.Require("MEMORIA_FF8_DOOR_SUSPENSION_SNAPSHOT", 328, 95);
        var session = new GameSession(recreated);
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(95, world.Events.Frame);
        JsonElement originalDefault = DefaultFiber(world.Events.Capture());
        Assert.Equal(1359, originalDefault.GetProperty("Position").GetInt32());
        Assert.Equal(7929, originalDefault.GetProperty("Results").GetProperty("6").GetInt32());
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -0.46f, Vertical: -0.88f)).Accepted);
        JsonElement pending = world.Events.Capture();
        JsonElement suspendedDefault = DefaultFiber(pending);
        Assert.Equal(1359, suspendedDefault.GetProperty("Position").GetInt32());
        using var restored = new GameWorld(new GameSession(session.State with { EventMemory = session.State.EventMemory, EventState = pending }), directory);
        restored.EnsureField();
        int earthquakeStarts = 0;
        int earthquakeStops = 0;
        world.Events.CommandExecuted += command =>
        {
            if (command.ObjectId == 14 && command.ScriptId == 81)
            {
                if (command.Name == "SHAKE")
                {
                    earthquakeStarts++;
                }
                if (command.Name == "SHAKEOFF")
                {
                    earthquakeStops++;
                }
            }
        };
        for (int tick = 0; tick < 70; tick++)
        {
            var wait = new PlayerAction(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(wait), restored.Apply(wait));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            if (tick == 0)
            {
                Assert.True(JsonElement.DeepEquals(suspendedDefault, DefaultFiber(world.Events.Capture())), "Earthquake slot changed the suspended polling default.");
                using var midScript = new GameWorld(new GameSession(session.State with { EventMemory = session.State.EventMemory, EventState = world.Events.Capture() }), directory);
                midScript.EnsureField();
                Assert.Equal(world.Render(TimeSpan.Zero), midScript.Render(TimeSpan.Zero));
            }
        }
        Assert.Equal(1, earthquakeStarts);
        Assert.Equal(1, earthquakeStops);
        Assert.Equal(328, session.State.FieldId);
        Assert.Equal(95, session.State.Variables[256]);
        Assert.Null(world.Events.LastFault);
        Assert.True(world.Events.IsUserControlEnabled);
        Assert.DoesNotContain(world.Events.Capture().GetProperty("Fibers").EnumerateArray(), fiber => fiber.GetProperty("ObjectId").GetInt32() == 14 && fiber.GetProperty("ScriptId").GetInt32() == 81);
        JsonElement resumedDefault = DefaultFiber(world.Events.Capture());
        Assert.Equal(1359, resumedDefault.GetProperty("Position").GetInt32());
        Assert.Equal(7929, resumedDefault.GetProperty("Results").GetProperty("6").GetInt32());
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
    }

    private static JsonElement DefaultFiber(JsonElement state) => state.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 14 && fiber.GetProperty("ScriptId").GetInt32() == 78);
}
