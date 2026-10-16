using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class PartyJoinCheckpointTests
{
    [Fact]
    public void ReachedOriginalJoinRestoresItsFixedMovementAndTraceThroughHostCallbacks()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        var firstSession = new GameSession(GameSession.Load(source));
        using var first = new GameWorld(firstSession, directory);
        first.EnsureField();
        Assert.Equal(new[] { 0, 1, 255 }, first.Events.Scene.FieldPartyNativeActorIds);
        Assert.True(first.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Null(first.Events.LastFault);
        firstSession.SetEventState(first.Events.Capture());

        string checkpoint = Path.Combine(Path.GetTempPath(), "Memoria.FF8-party-join-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            firstSession.Save(checkpoint);
            var resumedSession = new GameSession(GameSession.Load(checkpoint));
            using var resumed = new GameWorld(resumedSession, directory);
            resumed.EnsureField();
            Assert.Equal(first.Events.Capture().GetRawText(), resumed.Events.Capture().GetRawText());
            for (int frame = 0; frame < 8 && first.Events.Scene.PartyTrace is null; frame++)
            {
                var next = new PlayerAction(PlayerActionKind.Wait);
                Assert.Equal(first.Apply(next), resumed.Apply(next));
                Assert.Equal(first.Events.Capture().GetRawText(), resumed.Events.Capture().GetRawText());
            }
            Assert.NotNull(first.Events.Scene.PartyTrace);
            Assert.Equal(new[] { 0, 5, 255 }, first.Events.Scene.Party);
            Assert.Null(first.Events.LastFault);
            Assert.Equal(139, firstSession.State.FieldId);
        }
        finally
        {
            File.Delete(checkpoint);
        }
    }
}
