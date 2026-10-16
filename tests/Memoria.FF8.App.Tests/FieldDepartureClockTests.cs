using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class FieldDepartureClockTests
{
    [Fact]
    public void LaterFieldDepartureReportsItsFieldFrameWhileRetainingTheEarlierWorldReceipt()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string prefix = Path.Combine(repository!.FullName, "../fixtures");
        var session = new GameSession(GameSession.Load(prefix + "tutorial-complete-checkpoint.json"));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(163, world.Events.Frame);
        Assert.Equal(1650, session.State.DepartingWorldState!.Frame);
        NativeSavedWorldFootReturn receipt = session.State.SavedWorld!.FootReturn!;
        using JsonDocument proof = JsonDocument.Parse(File.ReadAllText(prefix + "guards-continuation-proof.json"));
        JsonElement[] actions = proof.RootElement.GetProperty("field136Replay").GetProperty("acceptedActions").EnumerateArray().TakeLast(182).ToArray();
        foreach (JsonElement action in actions)
        {
            Assert.True(world.Apply(new PlayerAction(Enum.Parse<PlayerActionKind>(action.GetProperty("Kind").GetString()!), Horizontal: action.GetProperty("Horizontal").GetSingle(), Vertical: action.GetProperty("Vertical").GetSingle(), Frames: action.GetProperty("Frames").GetInt32())).Accepted);
        }
        Assert.Equal(new PlayerTrigger(PlayerTriggerKind.FieldTransition, 129, 344, null, null), world.NextTrigger);
        Assert.Equal(receipt, session.State.SavedWorld!.FootReturn);
        Assert.Equal(1650, session.State.DepartingWorldState!.Frame);
        GameState expected = GameSession.Load(prefix + "guards-arrival-checkpoint.json");
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected), JsonSerializer.SerializeToElement(session.State with { DepartingFieldState = world.Events.Capture() })));
    }
}
