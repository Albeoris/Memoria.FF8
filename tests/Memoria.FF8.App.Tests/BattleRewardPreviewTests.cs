using System.Text.Json;
using Bitmap = SkiaSharp.SKBitmap;
using Color = SkiaSharp.SKColor;

using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleRewardPreviewTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    public void PreviewIsPureAndMatchesTheSingleConfirmedRewardCommit(int shivaHpBefore, int shivaHpAfter)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(1).AcquireGuardianForce(2).ToggleGuardianForceJunction(0, 1).ToggleGuardianForceJunction(0, 2).RebuildParty().Rest();
        vitals.GuardianForces[1] = vitals.GuardianForces[1] with { HitPoints = checked((ushort)shivaHpBefore) };
        GameSession session = new(new GameState { InputConfiguration = assets.ReadNewGameInputConfiguration(), NativeBattleMemory = assets.ReadColdProcessNativeBattleMemory(), Scene = SceneKind.Field, FieldId = 999, CharacterVitals = vitals, CharacterAppearance = assets.ReadNewGameCharacterAppearance(), Economy = assets.ReadNewGameEconomy(), BattleVariables = NativeBattleVariableState.CreateNewGame(), LimitProgress = assets.ReadNewGameLimitProgress(), SavedBattleFlags = assets.ReadNewGameSavedBattleFlags(), PocketStation = assets.ReadNewGamePocketStation(), ScannedModels = assets.ReadNewGameScannedModels(), ShotAvailability = assets.ReadNewGameShotAvailability(), GlobalRandom = assets.ReadNewGameGlobalRandom(), KnownMagic = assets.ReadNewGameKnownMagic() });
        using GameWorld setup = new(session, assets.GameDirectory);
        BattleDefinition original = setup.CreateEncounterBattle(512);
        // Explicit component combat inputs shorten this test without fabricating any defeat/reward receipts.
        BattleDefinition definition = original with { NativeGraphics = null, NativeClockRules = null, Combatants = original.Combatants.Select(actor => actor.IsPlayer ? actor with { Attack = 255, PhysicalPower = 255, Speed = 10_000, InitialAtb = 10_000 } : actor with { CurrentHp = 1, Speed = 1 }).ToArray() };
        string? resumeSnapshot = Environment.GetEnvironmentVariable("MEMORIA_FF8_ENCOUNTER_COMPONENT_SNAPSHOT");
        BattleSession active = resumeSnapshot is null ? GameWorld.CreateLiveBattleSession(definition, BattleAtbMode.Normal) : BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(resumeSnapshot))!);
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_ENCOUNTER_FIX_EVIDENCE");
        if (evidence is not null)
        {
            Directory.CreateDirectory(evidence);
            File.WriteAllText(TestResources.ArtifactFile("App.Tests.BattleRewardPreviewTests.1.json"), JsonSerializer.Serialize(active.Capture(), new JsonSerializerOptions { WriteIndented = true }));
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
        // Preserve the original HP owner while supplying the completed component's death/revival state.
        BattleSnapshot completed = active.Capture();
        completed = completed with { GuardianForces = completed.GuardianForces.Select(gf => gf.Id == 1 ? gf with { HitPoints = checked((ushort)shivaHpAfter) } : gf).ToArray() };
        active = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(completed))!);
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
        Assert.Equal(shivaHpAfter > 0 ? new[] { 1, 2 } : new[] { 2 }, world.PreviewBattleRewards().GuardianForceAbilityPoints.Keys.Order());
        Assert.Null(world.LastBattleRewards);
        if (evidence is not null)
        {
            File.WriteAllText(Path.Combine(evidence, $"formation512-victory-frame{active.Frame}.json"), JsonSerializer.Serialize(active.Capture(), new JsonSerializerOptions { WriteIndented = true }));
            using BattleAssets battles = new(assets.GameDirectory);
            BattleDefinition display = active.Capture().Definition;
            BattleScenePresentation scene = new(battles, display, true);
            using Bitmap bitmap = new(1280, 896, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul);
            using (BattlePreviewCanvas canvas = new(bitmap, assets.GameDirectory))
            {
                canvas.DrawRgba(scene.Render(active, 1280, 896), 1280, 896, new UiRect(0, 0, 1280, 896));
                BattleHudController hud = new(active, active.QueueAttack, () => { }, () => active.AdvanceTurn(), previewRewards: world.PreviewBattleRewards, guardianForceName: id => id == 1 ? "Shiva" : "Ifrit", itemName: id => display.NativeItemNames[id]!, cardName: id => display.NativeCardNames[id]!);
                hud.ConfigureResults(battles.ReadNativeGlobalBattleMessages());
                hud.AdvancePresentation(TimeSpan.FromSeconds(16.0 / BattleSession.FramesPerSecond + 0.000001));
                UiView view = hud.BuildView();
                view.Resize(1280, 896);
                view.Draw(canvas, new UiTheme());
            }
            bitmap.SavePng(TestResources.ArtifactFile("App.Tests.BattleRewardPreviewTests.2.png"));
        }
        BattleResult result = world.CaptureBattleOutcome(world.Battle!, occurrence);
        world.ApplyBattleResult(occurrence, result);
        Assert.Equal(shivaHpAfter, session.State.CharacterVitals!.GuardianForces[1].HitPoints);
        Assert.Equal(preview, JsonSerializer.Serialize(world.LastBattleRewards));
        string committed = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidOperationException>(() => world.ApplyBattleResult(occurrence, result));
        Assert.Equal(committed, JsonSerializer.Serialize(session.State));
    }
}
