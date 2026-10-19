using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class ControlQueueTests
{
    [Fact]
    public void Producer_withdrawal_during_dispatch_does_not_crash_response_publication()
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-queue-withdrawal-" + Guid.NewGuid().ToString("N"));
        string published = Path.Combine(root, "requests", "withdrawn.json");
        ControlQueue queue = new(root, _ =>
        {
            File.Delete(published);
            return new { Saved = true };
        });
        try
        {
            File.WriteAllText(published, JsonSerializer.Serialize(new ControlRequest("withdrawn", "save-for-update")));
            queue.Poll();
            using JsonDocument response = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "responses", "withdrawn.json")));
            Assert.True(response.RootElement.GetProperty("Ok").GetBoolean());
            Assert.True(response.RootElement.GetProperty("Result").GetProperty("Saved").GetBoolean());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData(-60, false)]
    [InlineData(60, true)]
    public void Expired_requests_are_rejected_before_dispatch_and_keep_their_response(int seconds, bool expectedDispatch)
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-queue-expiry-" + Guid.NewGuid().ToString("N"));
        int calls = 0;
        ControlQueue queue = new(root, _ => ++calls);
        string published = Path.Combine(root, "requests", "deadline.json");
        try
        {
            string payload = JsonSerializer.Serialize(new ControlRequest("deadline", "save-for-update", ExpiresAt: DateTimeOffset.UtcNow.AddSeconds(seconds)));
            File.WriteAllText(published, payload);
            queue.Poll();
            string response = File.ReadAllText(Path.Combine(root, "responses", "deadline.json"));
            using JsonDocument document = JsonDocument.Parse(response);
            Assert.Equal(expectedDispatch, document.RootElement.GetProperty("Ok").GetBoolean());
            Assert.Equal(expectedDispatch ? 1 : 0, calls);
            if (!expectedDispatch)
            {
                Assert.Equal("TimeoutException", document.RootElement.GetProperty("Type").GetString());
            }
            File.WriteAllText(published, payload);
            queue.Poll();
            Assert.Equal(expectedDispatch ? 1 : 0, calls);
            Assert.Equal(response, File.ReadAllText(Path.Combine(root, "responses", "deadline.json")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Locked_published_request_stays_pending_and_dispatches_once_after_release()
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-queue-lock-" + Guid.NewGuid().ToString("N"));
        int calls = 0;
        var queue = new ControlQueue(root, request => ++calls);
        string published = Path.Combine(root, "requests", "locked.json");
        string response = Path.Combine(root, "responses", "locked.json");
        try
        {
            File.WriteAllText(published, "{\"Id\":\"locked\",\"Command\":\"status\"}");
            using (FileStream locked = new(published, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                queue.Poll();
                Assert.Equal(0, calls);
                Assert.True(File.Exists(published));
                Assert.False(File.Exists(response));
            }
            queue.Poll();
            queue.Poll();
            Assert.Equal(1, calls);
            Assert.True(File.Exists(response));
            Assert.False(File.Exists(published));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Queue_waits_for_atomic_publication_returns_errors_and_preserves_idempotent_responses()
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-queue-" + Guid.NewGuid().ToString("N"));
        int calls = 0;
        var queue = new ControlQueue(root, request =>
        {
            calls++;
            if (request.Command == "invalid")
            {
                throw new ArgumentException("Unknown command.");
            }
            return new { request.Id, request.Command };
        });
        try
        {
            string temporary = Path.Combine(root, "requests", "001.tmp");
            string published = Path.ChangeExtension(temporary, ".json");
            File.WriteAllText(temporary, "{\"Id\":\"one\",\"Command\":\"status\"}");
            queue.Poll();
            Assert.Equal(0, calls);
            File.Move(temporary, published);
            queue.Poll();
            string response = File.ReadAllText(Path.Combine(root, "responses", "001.json"));
            Assert.True(JsonDocument.Parse(response).RootElement.GetProperty("Ok").GetBoolean());
            File.WriteAllText(published, "{\"Id\":\"one\",\"Command\":\"status\"}");
            queue.Poll();
            Assert.Equal(1, calls);
            Assert.Equal(response, File.ReadAllText(Path.Combine(root, "responses", "001.json")));
            Assert.Equal(2, Directory.GetFiles(Path.Combine(root, "processed")).Length);
            File.WriteAllText(Path.Combine(root, "requests", "002.json"), "{\"Id\":\"two\",\"Command\":\"invalid\"}");
            queue.Poll();
            using JsonDocument error = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "responses", "002.json")));
            Assert.False(error.RootElement.GetProperty("Ok").GetBoolean());
            Assert.Equal("two", error.RootElement.GetProperty("Id").GetString());
            Assert.Equal("ArgumentException", error.RootElement.GetProperty("Type").GetString());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
