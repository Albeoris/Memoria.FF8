using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.App.Tests;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class EffectComponentPreviewTests
{
    [Fact]
    public void Effect_preview_json_rejects_legacy_live_source_fields()
    {
        const string legacy = """
            {"EnemyId":1,"AllyId":0,"EffectId":219,"SourceSnapshot":"battle.json","SourceSha256":"ignored"}
            """;
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EffectPreviewRequest>(legacy));
    }

    [Fact]
    public void Battle_preview_rejects_legacy_snapshot_argument()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => BattlePreview.Run(["--snapshot", "battle.json", "--output", "battle.png"]));
        Assert.Contains("Unknown battle preview argument '--snapshot'", error.Message);
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
            (_, BattleSnapshot source) = NativeBattleEffectFixtureExporter.Prepare(input, directory!, output);
            string hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input)));
            EffectPreviewRequest request = new(1, 0, 219);
            string frozen = JsonSerializer.Serialize(source);
            GameSession session = GameWorld.CreateColdProcess(directory!);
            session.StartNewGame();
            using GameWorld world = new(session, directory!);
            BattleSession active = world.StartEffectPreview(request, new(source, hash));
            Assert.True(world.IsEffectComponentPreview);
            Assert.Same(active, world.Battle);
            Assert.Equal(462, active.Capture().Definition.EncounterId);
            Assert.Equal(source.Definition.StageId, active.Capture().Definition.StageId);
            Assert.Equal(caster.ModelId, active.Combatants.Single(actor => actor.NativeActorIndex == 3).ModelId);
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
            Assert.Null(session.State.PendingBattleEncounter);
            Assert.Null(world.LastBattleRewards);
        }
        finally
        {
            foreach (string file in Directory.EnumerateFiles(output))
            {
                File.Delete(file);
            }
            Directory.Delete(output);
        }
    }
}
