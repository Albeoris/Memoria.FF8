using System.Text.Json;

namespace Memoria.Launcher.Tests;

public sealed class GameUpdateSessionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "memoria-session-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveRequiresMatchingAcknowledgmentAndExistingSave()
    {
        GameUpdateSession session = new(Path.Combine(root, "session"));
        Task saving = session.SaveAsync(() => false, TimeSpan.FromSeconds(5));
        await RespondAsync(session, request =>
        {
            Assert.Equal("save-for-update", request.GetProperty("Command").GetString());
            Assert.Equal(session.SavePath, request.GetProperty("Path").GetString());
            Assert.True(request.GetProperty("ExpiresAt").GetDateTimeOffset() > DateTimeOffset.UtcNow);
            File.WriteAllText(session.SavePath, "standalone save");
            return new { Id = request.GetProperty("Id").GetString(), Ok = true, Result = new { Saved = true, Path = session.SavePath } };
        });
        await saving;
        Assert.True(File.Exists(session.SavePath));
        Assert.Empty(System.IO.Directory.GetFiles(Path.Combine(session.ControlDirectory, "requests")));
    }

    [Fact]
    public async Task GameErrorPreservesActualReason()
    {
        GameUpdateSession session = new(Path.Combine(root, "session"));
        Task saving = session.SaveAsync(() => false, TimeSpan.FromSeconds(5));
        const string reason = "Saving is unavailable while the battle transition is active.";
        await RespondAsync(session, request => new { Id = request.GetProperty("Id").GetString(), Ok = false, Error = reason });
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => saving);
        Assert.Equal(reason, error.Message);
        Assert.Empty(System.IO.Directory.GetFiles(Path.Combine(session.ControlDirectory, "requests")));
    }

    [Fact]
    public async Task RetryingSaveUsesAnotherUniquePathAndPreservesPreviousSave()
    {
        GameUpdateSession session = new(Path.Combine(root, "session"));
        Task first = session.SaveAsync(() => false, TimeSpan.FromSeconds(5));
        string previous = session.SavePath;
        await RespondAsync(session, request =>
        {
            File.WriteAllText(previous, "previous state");
            return new { Id = request.GetProperty("Id").GetString(), Ok = true, Result = new { Saved = true, Path = previous } };
        });
        await first;
        Task second = session.SaveAsync(() => false, TimeSpan.FromSeconds(5));
        Assert.NotEqual(previous, session.SavePath);
        await RespondAsync(session, request =>
        {
            File.WriteAllText(session.SavePath, "current state");
            return new { Id = request.GetProperty("Id").GetString(), Ok = true, Result = new { Saved = true, Path = session.SavePath } };
        });
        await second;
        Assert.Equal("previous state", File.ReadAllText(previous));
        Assert.Equal("current state", File.ReadAllText(session.SavePath));
    }

    [Fact]
    public async Task AcknowledgmentWithoutSaveRefusesUpdate()
    {
        GameUpdateSession session = new(Path.Combine(root, "session"));
        Task saving = session.SaveAsync(() => false, TimeSpan.FromSeconds(5));
        await RespondAsync(session, request => new { Id = request.GetProperty("Id").GetString(), Ok = true, Result = new { Saved = true, Path = session.SavePath } });
        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(() => saving);
        Assert.Contains("did not confirm", error.Message);
        Assert.False(File.Exists(session.SavePath));
    }

    [Fact]
    public async Task SaveFromAnotherPathDoesNotSatisfyAcknowledgment()
    {
        GameUpdateSession session = new(Path.Combine(root, "session"));
        Task saving = session.SaveAsync(() => false, TimeSpan.FromSeconds(5));
        File.WriteAllText(session.SavePath, "standalone save");
        await RespondAsync(session, request => new { Id = request.GetProperty("Id").GetString(), Ok = true, Result = new { Saved = true, Path = Path.Combine(root, "another-save.json") } });
        await Assert.ThrowsAsync<InvalidDataException>(() => saving);
    }

    [Fact]
    public async Task TimeoutRemovesShutdownRequestBeforeGameCanProcessItLate()
    {
        GameUpdateSession session = new(Path.Combine(root, "session"));
        Task<JsonElement> shutdown = session.RequestAsync("shutdown-for-update", () => false, TimeSpan.FromMilliseconds(50));
        string requestPath = Assert.Single(System.IO.Directory.GetFiles(Path.Combine(session.ControlDirectory, "requests"), "*.json"));
        using (JsonDocument request = JsonDocument.Parse(File.ReadAllText(requestPath)))
        {
            Assert.Equal("shutdown-for-update", request.RootElement.GetProperty("Command").GetString());
        }
        await Assert.ThrowsAsync<TimeoutException>(() => shutdown);
        Assert.False(File.Exists(requestPath));
        Assert.Empty(System.IO.Directory.GetFiles(Path.Combine(session.ControlDirectory, "requests")));
    }

    [Fact]
    public async Task ExitedGameReportsErrorAndRemovesRequest()
    {
        GameUpdateSession session = new(Path.Combine(root, "session"));
        IOException error = await Assert.ThrowsAsync<IOException>(() => session.SaveAsync(() => true, TimeSpan.FromSeconds(5)));
        Assert.Contains("game closed", error.Message);
        Assert.Empty(System.IO.Directory.GetFiles(Path.Combine(session.ControlDirectory, "requests")));
    }

    [Fact]
    public void UpdateResumeRoundTripsGameAndStandaloneSave()
    {
        UpdateInstaller.Prepare(root);
        string game = Path.Combine(root, "game");
        System.IO.Directory.CreateDirectory(game);
        string save = Path.Combine(root, "automatic-save.json");
        File.WriteAllText(save, "standalone save");
        UpdateResume expected = new(game, save);
        expected.Write(root);
        Assert.Equal(expected, UpdateResume.Read(root));
        Assert.False(File.Exists(UpdateResume.IntentPath(root) + ".tmp"));
        UpdateInstaller.Prepare(root);
        Assert.Equal(expected, UpdateResume.Read(root));
        File.Delete(save);
        Assert.Throws<InvalidDataException>(() => UpdateResume.Read(root));
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(root))
        {
            System.IO.Directory.Delete(root, true);
        }
    }

    private static async Task RespondAsync(GameUpdateSession session, Func<JsonElement, object> response)
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        string requests = Path.Combine(session.ControlDirectory, "requests");
        while (true)
        {
            string? requestPath = System.IO.Directory.GetFiles(requests, "*.json").SingleOrDefault();
            if (requestPath is not null)
            {
                using JsonDocument request = JsonDocument.Parse(await File.ReadAllTextAsync(requestPath, deadline.Token));
                object result = response(request.RootElement);
                string path = Path.Combine(session.ControlDirectory, "responses", Path.GetFileName(requestPath));
                await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(result), deadline.Token);
                File.Move(path + ".tmp", path);
                File.Delete(requestPath);
                return;
            }
            await Task.Delay(10, deadline.Token);
        }
    }
}
