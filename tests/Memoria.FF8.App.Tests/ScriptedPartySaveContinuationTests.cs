using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ScriptedPartySaveContinuationTests
{
    [Theory]
    [InlineData("ScriptedPartySplit", 5)]
    [InlineData("ScriptedPartyReturn", 4)]
    public void Scoped_additional_selection_repair_preserves_progress_and_continues_after_cold_reload(string fixture, byte companion)
    {
        GameplaySaveComponentDocument original = Read(fixture + ".Source");
        GameplaySaveComponentDocument repaired = Read(fixture + ".Repaired");
        Assert.Equal(new byte[] { 1 }, original.Characters.AdditionalCombatParty);
        Assert.Null(repaired.Characters.AdditionalCombatParty);
        Assert.Null(repaired.Characters.AdditionalCombatPartyMinimumCapacities);
        GameplaySaveComponentDocument expected = original with { Characters = original.Characters with { AdditionalCombatParty = null, AdditionalCombatPartyMinimumCapacities = null } };
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected), JsonSerializer.SerializeToElement(repaired)));
        using (FileStream preview = File.OpenRead(TestResources.FixturePath(fixture + ".Repaired")))
        {
            Assert.DoesNotContain(1, GameplaySaveCodec.ReadSummary(preview).PartyPortraits);
        }

        using GameAssets assets = new(TestResources.GameDirectory);
        GameSession session = new(GameplaySaveSessionLoader.Restore(repaired, TestResources.GameDirectory, sharedAssets: assets));
        using GameWorld world = new(session, TestResources.GameDirectory, sharedAssets: assets);
        world.EnsureField();
        Assert.Equal(new byte[] { companion, 0, 255 }, session.State.CharacterVitals!.CombatParty);
        Assert.Equal(new byte[] { companion, 0 }, session.State.CharacterVitals.GetBattlePartyRows(4));
        Assert.Equal(new int[] { 0, companion, 255 }, world.Events.Scene.Party);
        GameplaySaveFieldScripts scripts = world.Events.CaptureGameplaySaveScripts();
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(repaired.Progression!.FieldContinuation!.Scripts), JsonSerializer.SerializeToElement(scripts)));

        string path = TestResources.ArtifactFile("party-selection-roundtrip.ff8");
        world.WriteGameplaySave(path);
        GameSession coldSession = new(GameplaySaveSessionLoader.Load(path, TestResources.GameDirectory, sharedAssets: assets));
        using GameWorld cold = new(coldSession, TestResources.GameDirectory, sharedAssets: assets);
        cold.EnsureField();
        Assert.Equal(session.State.CrtRandom, coldSession.State.CrtRandom);
        Assert.Equal(session.State.Economy!.Gil, coldSession.State.Economy!.Gil);
        for (int frame = 0; frame < 2; frame++)
        {
            Assert.Equal(world.Apply(new(PlayerActionKind.Wait)), cold.Apply(new(PlayerActionKind.Wait)));
            Assert.Null(world.Events.LastFault);
            Assert.Null(cold.Events.LastFault);
            Assert.Equal(new byte[] { companion, 0 }, coldSession.State.CharacterVitals!.GetBattlePartyRows(4));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(world.Events.CaptureGameplaySaveScripts()), JsonSerializer.SerializeToElement(cold.Events.CaptureGameplaySaveScripts())));
        }
    }

    private static GameplaySaveComponentDocument Read(string resource)
    {
        using FileStream source = File.OpenRead(TestResources.FixturePath(resource));
        return GameplaySaveCodec.Read(source);
    }
}
