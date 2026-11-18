using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleHudSavedWorldPartyTests
{
    [Fact]
    public void Saved_world_four_member_party_draws_vitals_without_a_party_panel()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_HUD_WORLD_SAVE");
        Assert.SkipUnless(source is not null && File.Exists(source), "An optional standalone world save with four selected battle members is required.");
        byte[] originalHash = SHA256.HashData(File.ReadAllBytes(source!));
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameState saved = PlayerStateLoader.Load(source!, directory, sharedAssets: assets);
        Assert.Equal(SceneKind.WorldMap, saved.Scene);
        GameSession session = new(saved);
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        WorldMapScene scene = Assert.IsType<WorldMapScene>(world.WorldScene);
        RandomEncounterSessionState random = Assert.IsType<RandomEncounterSessionState>(session.State.RandomEncounters);
        session.SetRandomEncounters(random with { Pending = new(SceneKind.WorldMap, session.State.FieldId, scene.State.Frame, 717, scene.AllocationProgramSha256) });
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        ExpandedBattleInstalledEntry.WaitForEntry(world, battle);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        for (int callback = 0; callback < 160 && (process.UiState.CommandMenuHidden || !process.UiState.VitalsVisible); callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }

        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.False(process.UiState.CommandMenuHidden);
        Assert.True(process.UiState.VitalsVisible);
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => battle.AdvanceTurn(), readPresentationUi: () => process.UiState);
        Assert.Equal(4, hud.Party.Count);
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        BattleUiTests.TestCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        BattleHudLayout layout = new(new(0, 0, 960, 672));
        for (int index = 0; index < hud.Party.Count; index++)
        {
            BattleCombatant actor = hud.Party[index];
            BattleHudPartyRow row = BattleHudLayout.PartyRow(index, hud.Party.Count, hud.TextScale);
            BattlePresentedVital vital = battle.ReadDisplayedVital(actor.Id);
            Assert.Contains(canvas.TextDraws, draw => draw.Text == actor.Name && draw.Bounds == layout.Rect(520, row.Top, 150, row.VitalHeight));
            Assert.Contains(canvas.TextDraws, draw => draw.Text == vital.HitPoints.ToString() && draw.Bounds.Y == row.Top && draw.Bounds.X >= 674 && draw.Bounds.Right <= 730);
            Assert.Contains(canvas.TextDraws, draw => draw.Text == vital.MaximumHitPoints.ToString() && draw.Bounds == layout.Rect(760, row.Top, 64, row.VitalHeight));
            Assert.Contains(canvas.Fills, fill => fill.Bounds == layout.AtbGauge(row.Top, row.GaugeOffset));
        }
        Assert.DoesNotContain(canvas.Fills, fill => fill.Bounds.X >= BattleHudLayout.PartyLeft && fill.Bounds.Y >= 516 && fill.Bounds.Width >= 100 && fill.Bounds.Height > 12);
        Assert.Equal(originalHash, SHA256.HashData(File.ReadAllBytes(source!)));
    }
}
