using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class UpdateSaveHandoffTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "memoria-update-save-" + Guid.NewGuid().ToString("N"));
    private string SavePath => Path.Combine(directory, "continuation.json");

    [Fact]
    public void Save_publishes_one_loadable_standalone_state_and_pauses_until_cancelled()
    {
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 180, X = 42 });
        int validations = 0;
        UpdateSaveHandoff handoff = new(() => session.State, candidate =>
        {
            validations++;
            Assert.NotSame(session.State, candidate);
            Assert.Equal(42, candidate.X);
        }, () => { });

        UpdateSaveResult result = handoff.Save(SavePath, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.True(result.Saved);
        Assert.Equal(Path.GetFullPath(SavePath), result.Path);
        Assert.True(handoff.Paused);
        Assert.Equal(1, validations);
        Assert.Equal(42, GameSession.Load(result.Path).X);
        Assert.Single(Directory.GetFiles(directory));
        Assert.Throws<InvalidOperationException>(() => handoff.Save(Path.Combine(directory, "second.json"), DateTimeOffset.UtcNow.AddMinutes(1)));
        handoff.Cancel();
        Assert.False(handoff.Paused);
        session.SetPosition(99, 0);
        Assert.Equal(42, GameSession.Load(result.Path).X);
    }

    [Fact]
    public void Existing_destination_is_preserved_without_capturing()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(SavePath, "existing save");
        UpdateSaveHandoff handoff = new(() => throw new InvalidOperationException("Must not capture"), _ => { }, () => { });

        Assert.Throws<IOException>(() => handoff.Save(SavePath, DateTimeOffset.UtcNow.AddMinutes(1)));

        Assert.Equal("existing save", File.ReadAllText(SavePath));
        Assert.False(handoff.Paused);
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public void Destination_created_during_validation_is_never_overwritten()
    {
        UpdateSaveHandoff handoff = new(() => new GameState { Scene = SceneKind.Field }, _ => File.WriteAllText(SavePath, "concurrent save"), () => { });

        Assert.Throws<IOException>(() => handoff.Save(SavePath, DateTimeOffset.UtcNow.AddMinutes(1)));

        Assert.Equal("concurrent save", File.ReadAllText(SavePath));
        Assert.Single(Directory.GetFiles(directory));
        Assert.False(handoff.Paused);
    }

    [Fact]
    public void Unsafe_game_refuses_before_capture_and_returns_actual_reason()
    {
        UpdateSaveHandoff handoff = new(() => throw new InvalidOperationException("Must not capture"), _ => { }, () => throw new InvalidOperationException("Finish the current battle before updating."));

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => handoff.Save(SavePath, DateTimeOffset.UtcNow.AddMinutes(1)));

        Assert.Equal("Finish the current battle before updating.", error.Message);
        Assert.False(handoff.Paused);
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void Failed_continuation_validation_leaves_no_save_or_pause()
    {
        GameSession session = new(new GameState { Scene = SceneKind.Field, X = 42 });
        GameState before = session.State;
        UpdateSaveHandoff handoff = new(() => session.State, _ => throw new NotSupportedException("Unsupported continuation"), () => { });

        NotSupportedException error = Assert.Throws<NotSupportedException>(() => handoff.Save(SavePath, DateTimeOffset.UtcNow.AddMinutes(1)));

        Assert.Equal("Unsupported continuation", error.Message);
        Assert.Same(before, session.State);
        Assert.False(handoff.Paused);
        Assert.Empty(Directory.GetFiles(directory));
    }

    [Fact]
    public void Deadline_expiring_during_validation_does_not_publish_or_pause()
    {
        MutableTimeProvider clock = new();
        DateTimeOffset deadline = clock.GetUtcNow().AddSeconds(1);
        UpdateSaveHandoff handoff = new(() => new GameState { Scene = SceneKind.Field }, _ => clock.Now = deadline, () => { }, clock);

        Assert.Throws<TimeoutException>(() => handoff.Save(SavePath, deadline));

        Assert.False(handoff.Paused);
        Assert.Empty(Directory.GetFiles(directory));
    }

    [Fact]
    public void Expired_or_missing_deadline_and_relative_path_refuse_before_capture()
    {
        UpdateSaveHandoff handoff = new(() => throw new InvalidOperationException("Must not capture"), _ => { }, () => { });

        Assert.Throws<TimeoutException>(() => handoff.Save(SavePath, DateTimeOffset.UtcNow.AddMinutes(-1)));
        Assert.Throws<ArgumentException>(() => handoff.Save(SavePath, null));
        Assert.Throws<ArgumentException>(() => handoff.Save("relative.json", DateTimeOffset.UtcNow.AddMinutes(1)));
        Assert.False(handoff.Paused);
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void Title_state_refuses_without_publishing_or_pausing()
    {
        UpdateSaveHandoff handoff = new(() => new GameState { Scene = SceneKind.Title }, _ => throw new InvalidOperationException("Must not validate"), () => { });

        Assert.Throws<InvalidOperationException>(() => handoff.Save(SavePath, DateTimeOffset.UtcNow.AddMinutes(1)));

        Assert.False(handoff.Paused);
        Assert.False(Directory.Exists(directory));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return Now;
        }
    }
}
