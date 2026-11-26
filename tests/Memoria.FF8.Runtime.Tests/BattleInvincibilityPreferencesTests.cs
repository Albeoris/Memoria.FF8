using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime.Tests;

public sealed class BattleInvincibilityPreferencesTests
{
    [Theory]
    [InlineData(BattleDifficulty.Normal, false)]
    [InlineData(BattleDifficulty.Hard, false)]
    [InlineData(BattleDifficulty.Hardcore, true)]
    public void Missing_saved_rule_defaults_to_on_only_for_Hardcore(BattleDifficulty difficulty, bool expected)
    {
        string path = Path.Combine(Path.GetTempPath(), $"ff8-invincibility-{Guid.NewGuid():N}.json");
        try
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.SetGameplayPreferences(new() { Difficulty = difficulty });
            Assert.Equal(expected, session.State.GameplayPreferences.InvincibilityOncePerBattle);
            session.Save(path);

            JsonObject saved = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            JsonObject preferences = saved["GameplayPreferences"]!.AsObject();
            preferences.Remove(nameof(GameplayPreferences.InvincibilityOncePerBattle));
            GameplayPreferences embedded = preferences.Deserialize<GameplayPreferences>(new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } })!;
            embedded.Validate();
            Assert.Equal(expected, embedded.InvincibilityOncePerBattle);
            File.WriteAllText(path, saved.ToJsonString());

            session = new GameSession(GameSession.Load(path));
            Assert.Equal(expected, session.State.GameplayPreferences.InvincibilityOncePerBattle);
            GameplayPreferences changed = session.State.GameplayPreferences with { InvincibilityOncePerBattle = !expected };
            if (expected)
            {
                Assert.Throws<InvalidOperationException>(() => session.SetGameplayPreferences(changed));
            }
            else
            {
                session.SetGameplayPreferences(changed);
                session.Save(path);
                Assert.True(GameSession.Load(path).GameplayPreferences.InvincibilityOncePerBattle);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }
}
