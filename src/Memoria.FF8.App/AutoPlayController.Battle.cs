using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal enum AutoPlayStopBoundary
{
    AfterCurrentBattle,
    Immediately
}

internal sealed partial class AutoPlayController
{
    private readonly bool? requestedBattlePolicy;
    private bool stopAfterCurrentBattle;

    internal bool WaitingToStopAfterBattle => stopAfterCurrentBattle;

    private void ApplyBattlePolicy(GameWorld world)
    {
        if (run.FullBattlePlaythroughOverride is not bool fullCombat)
        {
            return;
        }
        bool pendingInteractive = world.Events.PendingInteractiveBattle is not null || world.Events.PendingMenuBattle is not null || session.State.RandomEncounters?.Pending is not null;
        if ((session.State.FullBattlePlaythrough == true) != fullCombat || !fullCombat && pendingInteractive && world.Battle is null)
        {
            world.ApplyBattleInput(new(BattleInputKind.ConfigurePlaythrough, FullCombat: fullCombat));
            ResetIdleDeadline();
        }
    }

    private bool TryPlayBattle(GameWorld world, Action<BattleSession>? advanceBattlePresentation)
    {
        if (world.Battle is null && (stopAfterCurrentBattle || world.Events.PendingInteractiveBattle is null && world.Events.PendingMenuBattle is null && session.State.RandomEncounters?.Pending is null))
        {
            return false;
        }
        run.SettleFrames = 0;
        if (world.Battle is null)
        {
            if (session.State.FullBattlePlaythrough != true)
            {
                return false;
            }
            // The exploration seed also makes the submitted encounter seed reproducible.
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: session.State.GameplayPreferences.AtbMode, NativeSeed: checked((byte)run.Seed)));
            ResetIdleDeadline();
            return true;
        }
        for (int callback = 0; callback < 30; callback++)
        {
            BattleSession? active = world.Battle;
            if (active is null)
            {
                return true;
            }
            Status = stopAfterCurrentBattle
                ? $"Finishing current battle {active.EncounterId} before saving and stopping."
                : $"Battle {active.EncounterId} · frame {active.Frame} · {run.Steps} steps";
            BattleInput input = PlanBattleInput(active);
            if (!stopAfterCurrentBattle && session.State.FullBattlePlaythrough != true && active.Outcome == BattleOutcome.Active)
            {
                input = new(active.CanAdmitDebugOutcome ? BattleInputKind.InstantVictory : BattleInputKind.Tick);
            }
            var before = BattleProgress(active);
            if (input.Kind == BattleInputKind.Tick && advanceBattlePresentation is not null)
            {
                advanceBattlePresentation(active);
                if (!ReferenceEquals(world.Battle, active) || before != BattleProgress(active))
                {
                    ResetIdleDeadline();
                }
                return true;
            }
            world.ApplyBattleInput(input);
            if (!ReferenceEquals(world.Battle, active))
            {
                ResetIdleDeadline();
                return true;
            }
            if (input.Kind != BattleInputKind.Tick || before != BattleProgress(active))
            {
                ResetIdleDeadline();
            }
            if (input.Kind != BattleInputKind.Tick || active.Outcome != BattleOutcome.Active)
            {
                return true;
            }
        }
        return true;
    }

    internal static BattleInput PlanBattleInput(BattleSession active)
    {
        bool paused = active.Presentation?.UiState.Paused != false;
        bool canChoose = !active.CanReturnToField && active.Outcome == BattleOutcome.Active && active.NativeEntryReady && !paused;
        BattleCombatant[] actors = canChoose ? active.ReadyPlayerCommandOrder.ToArray() : [];
        BattleCombatant[] targets = canChoose ? active.Combatants.ToArray() : [];
        AutoPlayBattleDecision decision = AutoPlayBattlePolicy.Choose(active.CanReturnToField, active.Outcome == BattleOutcome.Active, active.Outcome.ToString(), active.NativeEntryReady, paused,
            actors.Select(actor => new AutoPlayBattleActor(active.CanChoosePlayerCommand(actor.Id), actor.IsReady, actor.CanAct, actor.IsBusy, actor.Abilities.Contains(BattleAbility.Attack))).ToArray(),
            targets.Select(target => new AutoPlayBattleTarget(!target.IsPlayer, target.IsActive, target.IsAlive, target.Untargetable)).ToArray());
        return decision.Kind switch
        {
            AutoPlayBattleDecisionKind.Return => new(BattleInputKind.Return),
            AutoPlayBattleDecisionKind.Attack => new(BattleInputKind.Action, ActorId: actors[decision.ActorIndex].Id, TargetId: targets[decision.TargetIndex].Id, Ability: BattleAbility.Attack),
            _ => new(BattleInputKind.Tick)
        };
    }

    private static (bool EntryReady, BattleOutcome Outcome, string Actors) BattleProgress(BattleSession active)
    {
        return (active.NativeEntryReady, active.Outcome, string.Join('|', active.Combatants.Select(actor => $"{actor.Id}:{actor.CurrentHp}:{actor.CanAct}:{actor.IsAlive}:{actor.NativeCombatantFlags}")));
    }
}
