using System.Drawing.Imaging;
using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleTurnBasedPresentationClockTests
{
    [Fact]
    public void First_Ifrit_turn_does_not_make_idle_animation_wait_for_simulation_time_to_catch_up()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        using BattleAssets assets = new(directory);
        BattleEncounter encounter = assets.LoadEncounter(94);
        BattleEnemyPlacement ifrit = encounter.Enemies.Single(actor => actor.ModelId == 61);
        BattleCombatantDefinition party = new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, Speed = 20, MaximumHp = 1000, CurrentHp = 1000, StatusEffects = [new() { Kind = BattleStatusKind.Poison, DurationFrames = 1000, TickIntervalFrames = 30, Magnitude = 1 }] };
        BattleDefinition definition = new() { EncounterId = encounter.Id, StageId = encounter.StageId, Combatants = [party, new() { Id = "ifrit", Name = "Ifrit", ModelId = 61, SceneSlot = ifrit.Slot, Speed = 1 }] };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        BattleModel model = assets.LoadPlayer(0);
        BattleSequenceSet sequences = assets.LoadPlayerSequences(0, null, model);
        BattleAnimationPresentation animation = new(model, sequences, party);
        BattleScenePresentation scene = new(assets, definition, true);
        BattleAnimationSample before = animation.Sample(session, session.Combatants[0], Vector3.Zero, Vector3.UnitZ);
        byte[] firstPixels = scene.Render(session, 640, 448).ToArray();

        int dispatched = session.AdvanceTurn();

        Assert.True(dispatched >= 300, "The first readiness boundary must be far ahead of the presentation clock.");
        Assert.True(session.Combatants[0].IsReady);
        Assert.Null(session.ActiveAction);
        Assert.Empty(session.QueuedActions);
        Assert.Equal(before, animation.Sample(session, session.Combatants[0], Vector3.Zero, Vector3.UnitZ));
        Assert.Equal(firstPixels, scene.Render(session, 640, 448));
        string frozen = JsonSerializer.Serialize(session.Capture());
        long frozenFrame = session.Frame;
        for (int frame = 1; frame <= 8; frame++)
        {
            TimeSpan elapsed = TimeSpan.FromSeconds(1d / BattleSession.FramesPerSecond);
            animation.Advance(elapsed);
            scene.AdvancePresentation(elapsed);
            BattleAnimationSample idle = animation.Sample(session, session.Combatants[0], Vector3.Zero, Vector3.UnitZ);
            Assert.Equal(0, idle.Animation);
            Assert.True(idle.Loop);
            Assert.InRange(idle.Frame - before.Frame, frame * 0.5f - 0.001f, frame * 0.5f + 0.001f);
        }
        byte[] advancedPixels = scene.Render(session, 640, 448).ToArray();
        Assert.False(firstPixels.SequenceEqual(advancedPixels));
        Assert.Equal(frozenFrame, session.Frame);
        Assert.Equal(frozen, JsonSerializer.Serialize(session.Capture()));
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_PRESENTATION_EVIDENCE") is string evidence)
        {
            Directory.CreateDirectory(evidence);
            using var initial = BattlePreviewCanvas.BitmapFromRgba(firstPixels, 640, 448);
            initial.Save(Path.Combine(evidence, "ifrit-first-turn-idle-before.png"), ImageFormat.Png);
            using var advanced = BattlePreviewCanvas.BitmapFromRgba(advancedPixels, 640, 448);
            advanced.Save(Path.Combine(evidence, "ifrit-first-turn-idle-after.png"), ImageFormat.Png);
        }

        session.QueueAttack(party.Id, "ifrit");
        session.Tick();
        BattleAnimationSample attack = animation.Sample(session, session.Combatants[0], Vector3.Zero, Vector3.UnitZ);

        Assert.Equal(sequences.Read(13).Animations[0], attack.Animation);
        Assert.Equal(0, attack.Frame);
        Assert.False(attack.Loop);
        string committedAttack = JsonSerializer.Serialize(session.Capture());
        animation.Advance(TimeSpan.FromSeconds(1d / BattleSession.FramesPerSecond));
        BattleAnimationSample followingAttack = animation.Sample(session, session.Combatants[0], Vector3.Zero, Vector3.UnitZ);
        Assert.NotEqual(attack, followingAttack);
        Assert.Equal(committedAttack, JsonSerializer.Serialize(session.Capture()));
    }
}
