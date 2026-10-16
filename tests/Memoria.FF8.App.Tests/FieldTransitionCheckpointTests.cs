using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class FieldTransitionCheckpointTests
{
    [Fact]
    public void CheckpointBetweenOriginalMapjumpAndDestinationLoadPreservesPersistentVmState()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        GameSession firstSession = new(GameSession.Load(source));
        using GameWorld first = new(firstSession, directory);
        first.EnsureField();
        int[] party = first.Events.Scene.Party.ToArray();
        Assert.NotEmpty(party);
        Assert.True(first.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: -1)).Accepted);
        Assert.Equal(139, firstSession.State.FieldId);
        Assert.Equal(-3172, firstSession.State.X);
        Assert.Equal(-5705, firstSession.State.Y);
        Assert.NotNull(firstSession.State.DepartingFieldState);
        Assert.Null(firstSession.State.EventState);

        string path = Path.Combine(Path.GetTempPath(), "memoria-transition-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            firstSession.Save(path);
            GameSession restoredSession = new(GameSession.Load(path));
            using GameWorld restored = new(restoredSession, directory);
            restored.EnsureField();
            first.EnsureField();
            Assert.Null(restoredSession.State.DepartingFieldState);
            Assert.Equal(party, restored.Events.Scene.Party);
            Assert.Contains(0, restored.Events.Scene.UnlockedTutorials);
            Assert.Equal(first.Events.Capture().GetRawText(), restored.Events.Capture().GetRawText());
            first.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 3));
            restored.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 3));
            Assert.Equal(first.Events.Capture().GetRawText(), restored.Events.Capture().GetRawText());
            Assert.Equal(8341, restored.Events.Actors.Single(actor => actor.ObjectId == 0).Z);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
