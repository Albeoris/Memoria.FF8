using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class ControlQueueTests
{
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
