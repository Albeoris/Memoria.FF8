using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattlePlayerSaveCompatibilityTests
{
    [Fact]
    public void Legacy_player_checkpoint_preserves_field_continuation_and_progress()
    {
        string input = TestResources.FixturePath("App.Tests.BattlePlayerSaveCompatibilityTests.1");
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        byte[] before = SHA256.HashData(File.ReadAllBytes(input));
        GameState source = GameSession.Load(input);
        GameState loaded = PlayerStateLoader.Load(input, directory, sharedAssets: assets);
        Assert.Equal(SceneKind.Field, loaded.Scene);
        Assert.Equal(source.FieldId, loaded.FieldId);
        Assert.Null(loaded.Battle);
        Assert.Equal(source.GlobalRandom, loaded.GlobalRandom);
        Assert.Equal(source.CrtRandom, loaded.CrtRandom);
        Assert.Equal(source.MenuRandom, loaded.MenuRandom);
        Assert.Equal(JsonSerializer.Serialize(source.EventRandom), JsonSerializer.Serialize(loaded.EventRandom));
        Assert.Equal(JsonSerializer.Serialize(source.CharacterVitals), JsonSerializer.Serialize(loaded.CharacterVitals));
        Assert.Equal(JsonSerializer.Serialize(source.Economy), JsonSerializer.Serialize(loaded.Economy));
        Assert.Equal(JsonSerializer.Serialize(source.CardInventory), JsonSerializer.Serialize(loaded.CardInventory));
        Assert.Equal(source.AppliedBattleResultIds, loaded.AppliedBattleResultIds);
        Assert.Equal(JsonSerializer.Serialize(source.BattleStatistics), JsonSerializer.Serialize(loaded.BattleStatistics));
        JsonElement sourceEvent = source.EventState!.Value;
        JsonElement loadedEvent = loaded.EventState!.Value;
        Assert.Equal(sourceEvent.GetProperty("Frame").GetInt64(), loadedEvent.GetProperty("Frame").GetInt64());
        Assert.True(JsonElement.DeepEquals(sourceEvent.GetProperty("Fibers"), loadedEvent.GetProperty("Fibers")));

        GameSession session = new(loaded);
        using GameWorld resumed = new(session, directory, sharedAssets: assets);
        resumed.EnsureField();
        long frame = resumed.Events.Frame;
        resumed.Events.Step();
        Assert.Equal(frame + 1, resumed.Events.Frame);
        Assert.Null(resumed.Events.LastFault);
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(input)));
    }
}
