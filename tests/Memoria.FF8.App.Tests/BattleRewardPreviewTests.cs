using System.Text.Json;
using System.Drawing;
using System.Drawing.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleRewardPreviewTests
{
    [Fact]
    public void PreviewIsPureAndMatchesTheSingleConfirmedRewardCommit()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).ToggleGuardianForceJunction(0, 0).RebuildParty().Rest();
        GameSession session = new(new GameState { NativeBattleMemory = assets.ReadColdProcessNativeBattleMemory(), Scene = SceneKind.Field, FieldId = 999, CharacterVitals = vitals, CharacterAppearance = assets.ReadNewGameCharacterAppearance(), Economy = assets.ReadNewGameEconomy(), BattleVariables = NativeBattleVariableState.CreateNewGame(), LimitProgress = assets.ReadNewGameLimitProgress(), SavedBattleFlags = assets.ReadNewGameSavedBattleFlags(), PocketStation = assets.ReadNewGamePocketStation(), ScannedModels = assets.ReadNewGameScannedModels(), ShotAvailability = assets.ReadNewGameShotAvailability(), GlobalRandom = assets.ReadNewGameGlobalRandom(), KnownMagic = assets.ReadNewGameKnownMagic() });
        using GameWorld setup = new(session, assets.GameDirectory);
        BattleDefinition original = setup.CreateEncounterBattle(512);
        // Explicit component combat inputs shorten this test without fabricating any defeat/reward receipts.
        BattleDefinition definition = original with { Combatants = original.Combatants.Select(actor => actor.IsPlayer ? actor with { Attack = 255, PhysicalPower = 255, Speed = 10_000, InitialAtb = 10_000 } : actor with { CurrentHp = 1, Speed = 1 }).ToArray() };
        string? resumeSnapshot = Environment.GetEnvironmentVariable("MEMORIA_FF8_ENCOUNTER_COMPONENT_SNAPSHOT");
        BattleSession active = resumeSnapshot is null ? GameWorld.CreateLiveBattleSession(definition, BattleAtbMode.Normal) : BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(resumeSnapshot))!);
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_ENCOUNTER_FIX_EVIDENCE");
        if (evidence is not null)
        {
            Directory.CreateDirectory(evidence);
            File.WriteAllText(Path.Combine(evidence, "formation512-frame0.json"), JsonSerializer.Serialize(active.Capture(), new JsonSerializerOptions { WriteIndented = true }));
        }
        for (int frame = 0; frame < 1000 && active.Outcome == BattleOutcome.Active; frame++)
        {
            BattleCombatant? player = active.Combatants.FirstOrDefault(actor => actor.IsPlayer && actor.IsReady);
            BattleCombatant? enemy = active.Combatants.FirstOrDefault(actor => !actor.IsPlayer && actor.IsAlive);
            if (player is not null && enemy is not null)
            {
                active.QueueAttack(player.Id, enemy.Id);
            }
            active.Tick();
        }
        Assert.Equal(BattleOutcome.Victory, active.Outcome);
        BattleEncounterOccurrence occurrence = new("reward-preview-component", 512, setup.BattleResources.FormationSourceHash(512), SceneKind.Field, 999, 0, new string('A', 64), null);
        session.SetPendingBattleEncounter(occurrence);
        session.SetBattle(active.Capture());
        using GameWorld world = new(session, assets.GameDirectory);
        string before = JsonSerializer.Serialize(session.State);
        string preview = JsonSerializer.Serialize(world.PreviewBattleRewards());
        Assert.Equal(preview, JsonSerializer.Serialize(world.PreviewBattleRewards()));
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
        Assert.NotEmpty(world.PreviewBattleRewards().CharacterExperience);
        Assert.NotEmpty(world.PreviewBattleRewards().GuardianForceAbilityPoints);
        Assert.Null(world.LastBattleRewards);
        if (evidence is not null)
        {
            File.WriteAllText(Path.Combine(evidence, $"formation512-victory-frame{active.Frame}.json"), JsonSerializer.Serialize(active.Capture(), new JsonSerializerOptions { WriteIndented = true }));
            using BattleAssets battles = new(assets.GameDirectory);
            BattleDefinition display = active.Capture().Definition;
            BattleScenePresentation scene = new(battles, display, true);
            using Bitmap bitmap = new(1280, 896, PixelFormat.Format32bppArgb);
            using (BattlePreviewCanvas canvas = new(bitmap, assets.GameDirectory))
            {
                canvas.DrawRgba(scene.Render(active, 1280, 896), 1280, 896, new UiRect(0, 0, 1280, 896));
                BattleHudController hud = new(active, active.QueueAttack, () => { }, () => active.AdvanceTurn(), previewRewards: world.PreviewBattleRewards, guardianForceName: id => "Quezacotl", itemName: id => display.NativeItemNames[id]!, cardName: id => display.NativeCardNames[id]!);
                UiView view = hud.BuildView();
                view.Resize(1280, 896);
                view.Draw(canvas, new UiTheme());
            }
            bitmap.Save(Path.Combine(evidence, "victory-rewards.png"), ImageFormat.Png);
        }
        BattleResult result = world.CaptureBattleOutcome(world.Battle!, occurrence);
        world.ApplyBattleResult(occurrence, result);
        Assert.Equal(preview, JsonSerializer.Serialize(world.LastBattleRewards));
        string committed = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidOperationException>(() => world.ApplyBattleResult(occurrence, result));
        Assert.Equal(committed, JsonSerializer.Serialize(session.State));
    }
}
