using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class CompactCheckpointPersistenceTests
{
    [Fact]
    public void CompactAndDefaultCheckpointsRetainTheSameFullStateAndColdLoadWithStringEnums()
    {
        string prettyPath = Path.Combine(Path.GetTempPath(), "ff8-checkpoint-pretty-" + Guid.NewGuid().ToString("N") + ".json");
        string compactPath = Path.Combine(Path.GetTempPath(), "ff8-checkpoint-compact-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.SetGameplayPreferences(new GameplayPreferences { AtbMode = BattleAtbMode.Normal, GunbladeMode = BattleGunbladeMode.Manual });
            JsonElement stateBefore = JsonSerializer.SerializeToElement(session.State);

            session.Save(prettyPath);
            session.Save(compactPath, compact: true);

            string pretty = File.ReadAllText(prettyPath);
            using JsonDocument prettyDocument = JsonDocument.Parse(pretty);
            Assert.Contains('\n', pretty);
            Assert.True(new FileInfo(compactPath).Length < new FileInfo(prettyPath).Length);
            Assert.Equal(session.State.Scene, GameSession.ReadSummary(compactPath)!.Scene);
            Assert.Equal(JsonValueKind.String, prettyDocument.RootElement.GetProperty(nameof(GameState.Scene)).ValueKind);
            Assert.Equal(session.State.Scene.ToString(), prettyDocument.RootElement.GetProperty(nameof(GameState.Scene)).GetString());

            GameSession coldPretty = new(GameSession.Load(prettyPath));
            GameSession coldCompact = new(GameSession.Load(compactPath));
            Assert.True(JsonElement.DeepEquals(stateBefore, JsonSerializer.SerializeToElement(coldPretty.State)));
            Assert.True(JsonElement.DeepEquals(stateBefore, JsonSerializer.SerializeToElement(coldCompact.State)));
            Assert.True(JsonElement.DeepEquals(stateBefore, JsonSerializer.SerializeToElement(session.State)));
            Assert.False(File.Exists(prettyPath + ".tmp"));
            Assert.False(File.Exists(compactPath + ".tmp"));

            session.Save(prettyPath);
            Assert.Equal(pretty, File.ReadAllText(prettyPath));
        }
        finally
        {
            File.Delete(prettyPath);
            File.Delete(prettyPath + ".tmp");
            File.Delete(compactPath);
            File.Delete(compactPath + ".tmp");
        }
    }
}
