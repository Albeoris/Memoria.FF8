using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.App.Tests;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class EffectComponentPreviewTests
{
    [Fact]
    public void Source_request_requires_its_exact_hash_effect_and_no_ability_override()
    {
        EffectPreviewRequest request = new(1, 0, 219) { SourceSnapshot = Path.Combine(Path.GetTempPath(), "component.battle.json"), SourceSha256 = new('A', 64) };
        request.Validate();
        foreach (EffectPreviewRequest invalid in new[] { request with { SourceSha256 = null }, request with { SourceSha256 = "invalid" }, request with { SourceSnapshot = null }, request with { SourceSnapshot = "relative.json" }, request with { EffectId = null }, request with { AbilityId = 1 }, request with { CommandId = 2 } })
        {
            Assert.Throws<InvalidDataException>(invalid.Validate);
        }
    }

    [Fact]
    public void File_hash_is_checked_before_any_snapshot_is_accepted()
    {
        string path = Path.Combine(Path.GetTempPath(), "component-hash-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, "null");
            EffectPreviewRequest request = new(1, 0, 219) { SourceSnapshot = path, SourceSha256 = new('A', 64) };
            InvalidDataException mismatch = Assert.Throws<InvalidDataException>(() => GameWorld.ReadEffectComponentSource(request));
            Assert.Contains("SHA256", mismatch.Message);
            string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            InvalidDataException empty = Assert.Throws<InvalidDataException>(() => GameWorld.ReadEffectComponentSource(request with { SourceSha256 = hash }));
            Assert.Contains("snapshot is null", empty.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Original_enemy_caster_component_restores_its_exact_formation_owners_and_next_callback()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for exact component-source preview validation.");
        string output = Path.Combine(Path.GetTempPath(), "component-preview-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        try
        {
            using GameAssets assets = new(directory!);
            using BattleAssets battleAssets = new(directory!);
            BattleEncounter formation = battleAssets.LoadEncounter(462);
            BattleEnemyPlacement caster = NativeBattleEffectFixtureExporter.SelectEnemy(formation);
            NativeBattleAiDefinition ai = battleAssets.ReadEnemyAi(caster.ModelId, caster.Level);
            BattleNativeAction action = Assert.Single(battleAssets.NativeActions, row => row.Command == 8 && row.MagicId == 219 && ai.Abilities.Any(ability => ability.Command == row.Command && ability.Id == row.ActionId));
            NativeEffectFixtureRequest input = new("original-enemy-component", 219, action.Command, action.ActionId, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 3 };
            NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(input, directory!, output);
            Assert.True(exported.Status != "blocked", exported.FirstBlocker);
            string path = Path.Combine(output, "queued-action104.battle.json");
            byte[] bytes = File.ReadAllBytes(path);
            string hash = Convert.ToHexString(SHA256.HashData(bytes));
            EffectPreviewRequest request = new(1, 0, 219) { SourceSnapshot = path, SourceSha256 = hash };
            BattleSnapshot source = GameWorld.ReadEffectComponentSource(request);
            string frozen = JsonSerializer.Serialize(source);
            GameSession session = GameWorld.CreateColdProcess(directory!);
            session.StartNewGame();
            using GameWorld world = new(session, directory!);
            BattleSession active = world.StartEffectPreview(request);
            Assert.True(world.IsEffectComponentPreview);
            Assert.Same(active, world.Battle);
            Assert.Equal(462, active.Capture().Definition.EncounterId);
            Assert.Equal(source.Definition.StageId, active.Capture().Definition.StageId);
            Assert.Equal(caster.ModelId, active.Combatants.Single(actor => actor.NativeIndex == 3).ModelId);
            Assert.NotEqual(request.EnemyId, caster.ModelId);
            Assert.Equal(frozen, JsonSerializer.Serialize(active.Capture()));
            Assert.Null(session.State.PendingBattleEncounter);
            Assert.Null(world.LastBattleRewards);
            Assert.Null(session.State.Battle);
            DiagnosticPocketStationPreparationResult pocket = GameWorld.PrepareDiagnosticPocketStation(session.State.PocketStation!, new(true, false, 1, 0));
            Assert.Throws<InvalidOperationException>(() => session.SetDiagnosticPocketStation(pocket.State));
            Assert.Throws<InvalidDataException>(() => GameWorld.ValidateEffectComponentSource(source, active, 220));
            Assert.Throws<InvalidOperationException>(() => active.PublishDiagnosticEffectPreview(219));
            BattleSession direct = BattleSession.Restore(source);
            direct.AttachNativeEffectResourceResolver(effect => assets.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            using IDisposable visibleArea = BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.Native);
            active.Tick();
            direct.Tick();
            world.SynchronizeBattlePresentation();
            Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(active.Capture()));
            Assert.Equal(frozen, JsonSerializer.Serialize(source));
            Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
            Assert.Null(session.State.PendingBattleEncounter);
            Assert.Null(world.LastBattleRewards);
        }
        finally
        {
            Directory.Delete(output, true);
        }
    }
}
