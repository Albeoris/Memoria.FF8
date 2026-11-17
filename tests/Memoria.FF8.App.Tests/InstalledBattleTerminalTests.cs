using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class InstalledBattleTerminalTests
{
    [Theory]
    [InlineData(BattleOutcome.Victory, (byte)4)]
    [InlineData(BattleOutcome.Defeat, (byte)1)]
    public void Debug_outcome_uses_the_current_terminal_and_can_fork(BattleOutcome outcome, byte nativeResult)
    {
        using InstalledBattle fixture = new();
        BattleSession battle = fixture.Battle;
        TickUntil(battle, () => battle.CanAdmitDebugOutcome);
        Assert.Equal(4, battle.Combatants.Count(actor => actor.IsPlayer && actor.IsActive));
        if (outcome == BattleOutcome.Victory)
        {
            battle.InstantVictory();
        }
        else
        {
            battle.InstantDefeat();
        }
        Assert.Equal(outcome, battle.DebugOutcome);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.False(battle.CanReturnToField);
        BattleSession fork = battle.ForkForContinuation();
        TickUntil(battle, () => battle.Outcome != BattleOutcome.Active);
        TickUntil(fork, () => fork.Outcome != BattleOutcome.Active);
        foreach (BattleSession completed in new[] { battle, fork })
        {
            Assert.Equal(outcome, completed.Outcome);
            Assert.Equal(nativeResult, completed.NativeResult);
            Assert.True(completed.Presentation!.TerminalFinished);
            Assert.NotNull(completed.CaptureRetainedBattleMemory(completed.Capture(), null).Stage);
        }
    }

    [Fact]
    public void Debug_outcome_rejects_an_active_action_without_mutating_either_owner()
    {
        using InstalledBattle fixture = new();
        BattleSession battle = fixture.Battle;
        BattleCombatant target = battle.Combatants.First(actor => !actor.IsPlayer && actor.IsActive && actor.IsAlive && !actor.Untargetable);
        TickUntil(battle, () => battle.Combatants.Any(actor => actor.IsPlayer && actor.IsReady && battle.CanChoosePlayerCommand(actor.Id)));
        BattleCombatant source = battle.Combatants.First(actor => actor.IsPlayer && actor.IsReady && battle.CanChoosePlayerCommand(actor.Id));
        battle.QueueAttack(source.Id, target.Id);
        TickUntil(battle, () => battle.Presentation!.HasActiveAction);
        var facts = battle.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.PersistentStatus, actor.Atb)).ToArray();
        BattleActionPresentation? action = battle.ActiveAction;
        NativeBattleGraphicsState graphics = battle.Presentation!.Graphics;
        Assert.False(battle.CanAdmitDebugOutcome);
        Assert.Throws<InvalidOperationException>(battle.InstantDefeat);
        Assert.Throws<InvalidOperationException>(battle.InstantVictory);
        Assert.Equal(facts, battle.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.PersistentStatus, actor.Atb)));
        Assert.Same(action, battle.ActiveAction);
        Assert.Same(graphics, battle.Presentation.Graphics);
        Assert.Null(battle.DebugOutcome);
    }

    private static void TickUntil(BattleSession battle, Func<bool> reached)
    {
        for (int callback = 0; callback < 1200 && !reached(); callback++)
        {
            battle.Tick();
        }
        Assert.True(reached());
    }

    private sealed class InstalledBattle : IDisposable
    {
        private readonly GameAssets assets;
        private readonly GameWorld world;

        public InstalledBattle()
        {
            string directory = TestResources.GameDirectory;
            assets = new GameAssets(directory);
            GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, assets.ReadColdProcessNativeBattleMemory()));
            session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
            world = new GameWorld(session, directory, sharedAssets: assets);
            ExpandedBattleInstalledEntry.Prepare(world, session, PrepareParty, allies: 4, additionalEnemies: 0);
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: ExpandedBattleInstalledEntry.Seed));
            Battle = Assert.IsType<BattleSession>(world.Battle);
            Battle.ConfigureDiagnosticScheduling(holdEnemyCommands: true);
            ExpandedBattleInstalledEntry.WaitForEntry(world, Battle);
        }

        public BattleSession Battle { get; }

        private static void PrepareParty(GameSession session)
        {
            NativeCharacterVitals vitals = session.State.CharacterVitals!.WithGuestBattleRecords().Rest();
            NativeCharacterVital[] records = vitals.Records.ToArray();
            for (int row = 0; row < 4; row++)
            {
                records[row] = records[row] with { MemberFlags = (ushort)(records[row].MemberFlags | 1) };
            }
            session.SetCharacterVitals((vitals with { Records = records }).WithBattleParty([0, 1, 2, 3], 4));
        }

        public void Dispose()
        {
            world.Dispose();
            assets.Dispose();
        }
    }
}
