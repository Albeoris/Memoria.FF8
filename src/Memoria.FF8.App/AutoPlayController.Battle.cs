using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class AutoPlayController
{
    private bool TryPlayBattle(GameWorld world)
    {
        if (world.Battle is null && world.Events.PendingInteractiveBattle is null && world.Events.PendingMenuBattle is null && session.State.RandomEncounters?.Pending is null)
        {
            return false;
        }
        if (session.State.FullBattlePlaythrough != true)
        {
            return false;
        }
        run.SettleFrames = 0;
        if (world.Battle is null)
        {
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
            Status = $"Battle {active.EncounterId} · frame {active.Frame} · {run.Steps} steps";
            BattleInput input = PlanBattleInput(active);
            if (input.Kind == BattleInputKind.Return)
            {
                SaveBundle($"battle-outcome-{run.Steps:D10}", CaptureState());
            }
            var before = BattleProgress(active);
            world.ApplyBattleInput(input);
            if (!ReferenceEquals(world.Battle, active))
            {
                ResetIdleDeadline();
                SaveBundle($"battle-return-{run.Steps:D10}", CaptureState());
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
        if (active.CanReturnToField)
        {
            return new(BattleInputKind.Return);
        }
        if (active.Outcome != BattleOutcome.Active)
        {
            throw new NotSupportedException($"Automatic combat stopped at its actual {active.Outcome} outcome.");
        }
        if (active.NativeEntryReady && active.NativeGraphics?.UiServices?.PauseDE9 != true)
        {
            BattleCombatant? actor = active.ReadyPlayerCommandOrder.FirstOrDefault(candidate => active.CanChoosePlayerCommand(candidate.Id) && candidate.IsReady && candidate.CanAct && !candidate.IsBusy && candidate.Abilities.Contains(BattleAbility.Attack));
            BattleCombatant? target = active.Combatants.FirstOrDefault(candidate => !candidate.IsPlayer && candidate.IsActive && candidate.IsAlive && !candidate.Untargetable);
            if (actor is not null && target is not null)
            {
                return new(BattleInputKind.Action, ActorId: actor.Id, TargetId: target.Id, Ability: BattleAbility.Attack);
            }
        }
        return new(BattleInputKind.Tick);
    }

    private static (bool EntryReady, BattleOutcome Outcome, string Actors) BattleProgress(BattleSession active)
    {
        return (active.NativeEntryReady, active.Outcome, string.Join('|', active.Combatants.Select(actor => $"{actor.Id}:{actor.CurrentHp}:{actor.CanAct}:{actor.IsAlive}:{actor.NativeCombatantFlags}")));
    }
}
