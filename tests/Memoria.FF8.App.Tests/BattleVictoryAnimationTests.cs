using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleVictoryAnimationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Victory_replaces_recent_attack_and_hit_sequences_on_the_first_result_frame(int character)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        using BattleAssets assets = new(directory);
        BattleModel model = assets.LoadPlayer(character);
        BattleSequenceSet sequences = assets.LoadPlayerSequences(character, null, model);
        BattleCombatantDefinition party = new() { Id = "party", Name = "Winner", IsPlayer = true, ModelId = character, InitialAtb = BattleSession.AtbMaximum, Attack = 255 };
        BattleCombatantDefinition enemy = new() { Id = "enemy", Name = "Last enemy", ModelId = -1, MaximumHp = 1, CurrentHp = 1, Speed = 1 };
        BattleSession session = new(new BattleDefinition { Combatants = [party, enemy] }, BattleAtbMode.Normal);
        BattleAnimationPresentation animation = new(model, sequences, party) { Melee = true };
        session.QueueAttack("party", "enemy");
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames && session.Outcome == BattleOutcome.Active; frame++)
        {
            session.Tick();
        }
        Assert.Equal(BattleOutcome.Victory, session.Outcome);
        Assert.Contains(session.Events, item => item.Kind == "Windup" && item.ActorId == party.Id && session.Frame - item.Frame < BattleSession.AttackWindupFrames + BattleSession.AttackRecoveryFrames);
        Assert.Null(session.ActiveAction);
        BattleSnapshot snapshot = session.Capture();
        BattleEvent recentHit = new(session.Frame, "Damage", enemy.Id, party.Id, 1, "Recent hit");
        BattleSnapshot recentHitSnapshot = snapshot with { Events = [.. snapshot.Events.Where(item => item.Kind != "Windup"), recentHit] };
        BattleSession restored = BattleSession.Restore(recentHitSnapshot);
        BattleAnimationPresentation restoredAnimation = new(model, sequences, party) { Melee = true };
        string unchanged = JsonSerializer.Serialize(restored.Capture());

        BattleAnimationSample first = animation.Sample(session, session.Combatants[0], Vector3.Zero, Vector3.UnitZ * 20);
        BattleAnimationSample firstRestored = restoredAnimation.Sample(restored, restored.Combatants[0], Vector3.Zero, Vector3.UnitZ * 20);

        Assert.Equal(sequences.Read(17).Animations[0], first.Animation);
        Assert.Equal(0, first.Frame);
        Assert.Equal(Vector3.Zero, first.Offset);
        Assert.False(first.Loop);
        Assert.Equal(first, firstRestored);
        animation.Advance(TimeSpan.FromSeconds(10));
        restoredAnimation.Advance(TimeSpan.FromSeconds(10));
        BattleAnimationSample settled = animation.Sample(session, session.Combatants[0], Vector3.Zero, Vector3.UnitZ * 20);
        Assert.Equal(sequences.Read(17).Animations[^1], settled.Animation);
        Assert.Equal(model.Animations[settled.Animation].Frames.Count - 1, settled.Frame);
        Assert.Equal(Vector3.Zero, settled.Offset);
        Assert.Equal(settled, restoredAnimation.Sample(restored, restored.Combatants[0], Vector3.Zero, Vector3.UnitZ * 20));
        Assert.Equal(snapshot.Frame, session.Frame);
        Assert.Equal(unchanged, JsonSerializer.Serialize(restored.Capture()));
    }
}
