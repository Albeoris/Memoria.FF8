using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ParkedGardenReboardingInputTests
{
    [Fact]
    public void ActualCentraFootConfirmPublishesBoardingActivityBeforeWorldEventsAndRestores()
    {
        string source = TestResources.FixturePath("App.Tests.ParkedGardenReboardingInputTests.1");
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, TestResources.GameDirectory);
        Assert.NotNull(session.State.World!.NativeFootRuntime);
        Assert.Equal(2, session.State.World.NativeGardenRuntime!.BroadContact);
        Assert.True(world.Apply(new(PlayerActionKind.Confirm)).Accepted);
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        Assert.Null(session.State.World!.NativeFootRuntime);
        Assert.NotNull(session.State.World.NativeGardenRuntime);
        string snapshot = Path.Combine(Path.GetTempPath(), "Memoria-FF8-Garden-reboard-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(snapshot);
            GameSession restoredSession = new(GameSession.Load(snapshot));
            using GameWorld restored = new(restoredSession, TestResources.GameDirectory);
            Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.True(restored.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
        }
        finally
        {
            File.Delete(snapshot);
        }
    }

    private static string FindRootFile(string relative)
    {
        return TestResources.FixtureFile(relative);
    }
}
