using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GraphicsRestartSnapshotTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "memoria-restart-title-" + Guid.NewGuid().ToString("N"));
    private string SavePath => Path.Combine(directory, "title.ff8state");

    [Fact]
    public void TitleSnapshotPreservesTheCurrentSessionAndValidatesItsSerializedState()
    {
        GameSession session = new(new GameState { Scene = SceneKind.Title, X = 17, Y = 29 });
        GameState original = session.State;
        UpdateSaveResult result = GraphicsRestartSnapshot.SaveTitle(session, SavePath, DateTimeOffset.UtcNow.AddMinutes(1));
        GameState resumed = GameSession.Load(result.Path);
        Assert.Same(original, session.State);
        Assert.Equal(SceneKind.Title, resumed.Scene);
        Assert.Equal(17, resumed.X);
        Assert.Equal(29, resumed.Y);
        Assert.Single(Directory.GetFiles(directory));
        Assert.Throws<IOException>(() => GraphicsRestartSnapshot.SaveTitle(session, SavePath, DateTimeOffset.UtcNow.AddMinutes(1)));
        Assert.Equal(17, GameSession.Load(SavePath).X);
    }

    [Fact]
    public void TitleExceptionDoesNotBypassGameplaySafetyOrExpiredRequests()
    {
        GameSession gameplay = new(new GameState { Scene = SceneKind.Field, FieldId = 180 });
        Assert.Throws<InvalidOperationException>(() => GraphicsRestartSnapshot.SaveTitle(gameplay, SavePath, DateTimeOffset.UtcNow.AddMinutes(1)));
        GameSession title = new(new GameState { Scene = SceneKind.Title });
        Assert.Throws<TimeoutException>(() => GraphicsRestartSnapshot.SaveTitle(title, SavePath, DateTimeOffset.UtcNow.AddSeconds(-1)));
        Assert.False(File.Exists(SavePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
