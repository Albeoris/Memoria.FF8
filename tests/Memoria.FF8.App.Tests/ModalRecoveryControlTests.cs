using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class ModalRecoveryControlTests
{
    [Theory]
    [InlineData("status", null)]
    [InlineData("world-status", null)]
    [InlineData("world-observe", null)]
    [InlineData("world-snapshot", null)]
    [InlineData("world-autoplay", "status")]
    [InlineData("WORLD-AUTOPLAY", "STATUS")]
    [InlineData("quit", null)]
    [InlineData("cancel-update", null)]
    public void Modal_recovery_allows_observation_snapshot_and_exit(string command, string? name)
    {
        Assert.True(ModalRecoveryControlPolicy.Allows(new("request", command, Name: name)));
    }

    [Theory]
    [InlineData("world-autoplay-step", null)]
    [InlineData("world-autoplay", null)]
    [InlineData("world-autoplay", "start")]
    [InlineData("world-step-actions", null)]
    [InlineData("world-switch", null)]
    [InlineData("action", null)]
    [InlineData("ui-open", null)]
    [InlineData("save-for-update", null)]
    public void Modal_recovery_rejects_gameplay_and_new_modal_actions(string command, string? name)
    {
        Assert.False(ModalRecoveryControlPolicy.Allows(new("request", command, Name: name)));
    }

    [Fact]
    public void Rejected_modal_input_does_not_starve_a_later_status_or_snapshot()
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-modal-queue-" + Guid.NewGuid().ToString("N"));
        List<string> dispatched = [];
        ControlQueue queue = new(root, request =>
        {
            dispatched.Add(request.Command);
            return new { request.Command };
        });
        try
        {
            foreach (var (id, command) in new[] { ("001", "world-autoplay-step"), ("002", "world-status"), ("003", "world-snapshot") })
            {
                File.WriteAllText(Path.Combine(root, "requests", id + ".json"), JsonSerializer.Serialize(new ControlRequest(id, command)));
            }
            queue.Poll(ModalRecoveryControlPolicy.Allows);
            using JsonDocument rejected = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "responses", "001.json")));
            Assert.False(rejected.RootElement.GetProperty("Ok").GetBoolean());
            Assert.Equal("InvalidOperationException", rejected.RootElement.GetProperty("Type").GetString());
            Assert.Empty(dispatched);
            queue.Poll(ModalRecoveryControlPolicy.Allows);
            queue.Poll(ModalRecoveryControlPolicy.Allows);
            Assert.Equal(new[] { "world-status", "world-snapshot" }, dispatched);
            Assert.Empty(Directory.GetFiles(Path.Combine(root, "requests")));
            Assert.Equal(3, Directory.GetFiles(Path.Combine(root, "processed")).Length);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
