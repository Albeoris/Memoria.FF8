using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AuthoredRosterActivationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Installed_death_action_activates_the_same_original_enemy(bool attached)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game) { UseAttachedBattlePresentation = attached };
        ExpandedBattleInstalledEntry.Prepare(world, session, _ => { }, 63, allies: 3, additionalEnemies: 0);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant caster = battle.Combatants.First(actor => actor.IsPlayer);
        BattleCombatant carrier = battle.Combatants.Single(actor => !actor.IsPlayer && actor.ModelId == 68);
        BattleCombatant reserve = battle.Combatants.Single(actor => !actor.IsPlayer && actor.ModelId == 77);
        Assert.Equal(6, reserve.NativeActorIndex);
        Assert.True(reserve.IsActive && reserve.Hidden && reserve.Suspended && reserve.Untargetable);
        TickUntil(world, () => battle.NativeEntryReady && battle.ActiveAction is null && battle.QueuedActions.Count == 0);
        caster.Atb = BattleSession.AtbMaximum;
        TickUntil(world, () => battle.CanChoosePlayerCommand(caster.Id));
        carrier.CurrentHp = 1;
        world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: caster.Id, TargetId: carrier.Id) { ForcePhysical = true });

        bool reachedEffect = false;
        BattleModelOwner reserveOwner = battle.ReadPresentationModelOwner(reserve.Id);
        TickUntil(world, () =>
        {
            NativeBattleGraphicsState graphics = battle.ExpandedPresentationProcess?.Graphics ?? battle.NativeGraphics!;
            if (graphics.OriginalEffect is { ReturnedDescriptor: > 0, RequestedEffect: 20 })
            {
                Assert.Equal((byte)93, graphics.ActorModels.Get(reserveOwner).ModelId4);
                reachedEffect = true;
            }
            return reachedEffect && !reserve.Suspended && battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.ExpandedPresentationProcess?.HasActiveAction != true;
        });
        Assert.Equal(6, reserve.NativeActorIndex);
        Assert.True(reserve.IsActive && !reserve.Hidden && !reserve.Untargetable);
        Assert.Single(battle.Events, entry => entry.Kind == "Activated" && entry.ActorId == reserve.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Installed_death_callback_allocates_the_same_replacement(bool attached)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        GameWorld world = new(session, directory, sharedAssets: game) { UseAttachedBattlePresentation = attached };
        try
        {
            ExpandedBattleInstalledEntry.Prepare(world, session, _ => { }, 511, allies: 3, additionalEnemies: 0);
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            Assert.Equal(attached, battle.ExpandedPresentationProcess is not null);
            BattleCombatant caster = battle.Combatants.First(actor => actor.IsPlayer);
            BattleCombatant predecessor = battle.Combatants.Single(actor => !actor.IsPlayer && actor.ModelId == 121 && actor.IsActive);
            Assert.DoesNotContain(battle.Combatants, actor => !actor.IsPlayer && actor.ModelId == 122 && actor.IsActive);
            TickUntil(world, () => battle.NativeEntryReady && battle.ActiveAction is null && battle.QueuedActions.Count == 0);
            caster.Atb = BattleSession.AtbMaximum;
            TickUntil(world, () => battle.CanChoosePlayerCommand(caster.Id));
            predecessor.CurrentHp = 1;
            world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: caster.Id, TargetId: predecessor.Id) { ForcePhysical = true });

            TickUntil(world, () => battle.Combatants.Any(actor => actor is { IsPlayer: false, ModelId: 122, IsActive: true, IsAlive: true, Suspended: false, Untargetable: false }) &&
                battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.ExpandedPresentationProcess?.HasActiveAction != true);
            BattleCombatant replacement = battle.Combatants.Single(actor => actor is { IsPlayer: false, ModelId: 122, IsActive: true });
            Assert.Equal(5, replacement.NativeActorIndex);
            Assert.Equal(2, replacement.SceneSlot);
            Assert.NotNull(replacement.ResolvedNativeLevel);
            Assert.Single(battle.Events, entry => entry.Kind == "NativeSpawn" && entry.ActorId == predecessor.Id && entry.TargetId == replacement.Id);
            Assert.Contains(battle.Events, entry => entry.Kind == "NativeText" && entry.ActorId == predecessor.Id);

            BattleSnapshot checkpoint = battle.Capture();
            BattleSession restored = BattleSession.Restore(checkpoint);
            BattleCombatant restoredReplacement = restored.Combatants.Single(actor => actor.Id == replacement.Id);
            Assert.Equal(replacement.NativeActorIndex, restoredReplacement.NativeActorIndex);
            Assert.Equal(replacement.ResolvedNativeLevel, restoredReplacement.ResolvedNativeLevel);
            Assert.Equal(replacement.AiTurnCount, restoredReplacement.AiTurnCount);
            Assert.Equal(replacement.CurrentHp, restoredReplacement.CurrentHp);
            Assert.Equal(checkpoint.Random!.Indexes.ToArray(), restored.Capture().Random!.Indexes.ToArray());
            world.Dispose();
        }
        catch
        {
            try
            {
                world.Dispose();
            }
            catch (InvalidOperationException)
            {
                // Preserve a failure from the active native callback.
            }
            throw;
        }
    }

    private static void TickUntil(GameWorld world, Func<bool> condition)
    {
        for (int callback = 0; callback < 1200 && !condition(); callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.True(condition(), string.Join(", ", world.Battle!.Events.TakeLast(12).Select(entry => $"{entry.Kind}/{entry.ActorId}")));
    }
}
