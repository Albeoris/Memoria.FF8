using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private IEnumerable<(int Ability, byte Command)> GuardianFormLearnedCommands(int guardianForceId)
    {
        return BattleGuardianFormProfiles.LearnedCommands(definition.MagicVitals, guardianForceId);
    }

    public IReadOnlyList<BattleGuardianFormCommand> GetGuardianFormCommands(string actorId)
    {
        if (GetGuardianForm(actorId) is not { EntryCompleted: true } form)
        {
            return [];
        }
        BattleCombatant actor = GetActor(actorId);
        List<BattleGuardianFormCommand> result = [];
        foreach (BattleGuardianFormActionBinding binding in GetGuardianFormBindings(form))
        {
            BattleActionDefinition action = ResolveGuardianFormAction(form, binding);
            bool available = CanSelectGuardianFormInnateAction(actor, form, binding, action) && combatants.Any(target => IsGuardianFormInnateTarget(actor, target, binding, action));
            result.Add(new((int)binding.Action, action with { Available = available }, TargetFlags: action.NativeAction?.TargetFlags));
        }
        foreach ((int ability, byte command) in GuardianFormLearnedCommands(form.GuardianForceId))
        {
            if (command == 38) // MiniMog
            {
                continue;
            }
            BattleNativeCommandBinding binding = ReadGuardianFormCommandBinding(form.GuardianForceId, command);
            BattleActionDefinition action = ResolveGuardianFormLearnedAction(actor, binding);
            bool available = CanSelectGuardianFormLearnedAction(actor, action) && combatants.Any(target => IsExpandedPlayerActionTarget(actor, target, action, binding.TargetFlags));
            result.Add(new(ability + BattleGuardianFormCommand.LearnedAbilityOffset, action with { Available = available }, ability, command, binding.TargetFlags));
        }
        return result;
    }

    public IReadOnlyList<BattleCombatant> GetGuardianFormCommandTargets(string actorId, int commandId)
    {
        BattleGuardianFormCommand? command = GetGuardianFormCommands(actorId).SingleOrDefault(row => row.Id == commandId);
        if (command is null || !command.Action.Available)
        {
            return [];
        }
        BattleCombatant actor = GetActor(actorId);
        if (command.LearnedAbilityId is not null)
        {
            return combatants.Where(target => IsExpandedPlayerActionTarget(actor, target, command.Action, command.TargetFlags)).ToArray();
        }
        BattleGuardianFormActionBinding binding = ReadGuardianFormBinding(GetGuardianForm(actorId)!, (BattleGuardianFormAction)command.Id);
        return combatants.Where(target => IsGuardianFormInnateTarget(actor, target, binding, command.Action)).ToArray();
    }

    private bool CanSelectGuardianFormInnateAction(BattleCombatant actor, BattleGuardianFormState form, BattleGuardianFormActionBinding binding, BattleActionDefinition action)
    {
        if (!action.Available || binding.OncePerInvocation && form.UsedActions.Contains(binding.Action) || binding.RequiresGrounded && HasNativeFloat(NativeBattleStatusBits(actor)) || binding.RequiresFullAtb && !actor.IsReady)
        {
            return false;
        }
        if (binding.Source == BattleGuardianFormActionSource.Magic && binding.Cost == BattleGuardianFormActionCost.Native && GetAvailableMagicCasts(actor.Id, action.Id) == 0)
        {
            return false;
        }
        return binding.Source is not (BattleGuardianFormActionSource.Magic or BattleGuardianFormActionSource.GuardianForce) || (NativeSavedStatusBits(actor) & 0x10) == 0;
    }

    private static bool IsGuardianFormInnateTarget(BattleCombatant actor, BattleCombatant target, BattleGuardianFormActionBinding binding, BattleActionDefinition action)
    {
        if (!target.IsActive || target.Untargetable || !HasValidTargetLifeState(action, target))
        {
            return false;
        }
        return binding.Target switch
        {
            BattleGuardianFormTarget.Self => target == actor,
            BattleGuardianFormTarget.SingleEnemy or BattleGuardianFormTarget.AllEnemies => actor.IsPlayer != target.IsPlayer,
            _ when binding.Source == BattleGuardianFormActionSource.Physical => actor.IsPlayer != target.IsPlayer,
            _ => IsValidSelectedActionTarget(actor, action, target)
        };
    }

    private BattleNativeCommandBinding ReadGuardianFormCommandBinding(int guardianForceId, byte command)
    {
        BattleGuardianFormResources resource = definition.GuardianFormResources.First(candidate => candidate.GuardianForceId == guardianForceId);
        if (!resource.CommandBindings.IsDefault)
        {
            return resource.CommandBindings.SingleOrDefault(binding => binding.Command == command) ??
                throw new NotSupportedException($"GF learned command {command} lacks its installed command binding.");
        }
        // Older standalone logical saves retain additional command headers in their kernel rows.
        if (definition.NativeActions.SingleOrDefault(action => action.Command == command && action.KernelSection == 10) is { Name: { } name, TargetFlags: byte flags } native)
        {
            return new(command, name, flags, checked((byte)native.ActionId));
        }
        return combatants.SelectMany(actor => actor.NativeCommands).FirstOrDefault(binding => binding.Command == command) ??
            throw new NotSupportedException($"GF learned command {command} requires its installed command table.");
    }

    private BattleNativeCommandBinding FindGuardianFormLearnedBinding(int guardianForceId, int abilityId)
    {
        foreach ((int ability, byte command) in GuardianFormLearnedCommands(guardianForceId))
        {
            if (ability == abilityId)
            {
                return ReadGuardianFormCommandBinding(guardianForceId, command);
            }
        }
        throw new InvalidOperationException($"GF ability {abilityId} is not a learned battle command.");
    }

    private BattleActionDefinition ResolveGuardianFormLearnedAction(BattleCombatant actor, BattleNativeCommandBinding binding, BattleNativeAction? admitted = null)
    {
        BattleNativeAction native;
        if (binding.Command == 28 && admitted is { KernelSection: 4 } && actor.NativeAttack is { } weapon)
        {
            // A previously admitted Darkside retains the weapon record captured by the original form implementation.
            native = weapon with { Command = binding.Command, DispatchCommand = binding.Command, Name = binding.Name, TargetFlags = binding.TargetFlags };
        }
        else
        {
            native = FindNativeCommandAction(actor, binding.Command, NativeBindingActionId(binding), learnedBinding: binding);
        }
        return CreateNativePlayerActionChoice(BattleAbility.NativeCommand, native, binding.TargetFlags, binding.Name);
    }

    private bool CanSelectGuardianFormLearnedAction(BattleCombatant actor, BattleActionDefinition action)
    {
        BattleNativeAction native = action.NativeAction!;
        return !IsPlayerCommandSealed(actor, native.Command) && SupportsExpandedOrdinaryCommand(native, guardianForm: true) &&
            (!IsExpandedMiniMogAction(native) || ReadMiniMogFailure() is null);
    }

    private void QueueGuardianFormLearnedCommand(BattleCombatant actor, BattleGuardianFormState form, BattleCombatant target, int abilityId)
    {
        BattleNativeCommandBinding binding = FindGuardianFormLearnedBinding(form.GuardianForceId, abilityId);
        BattleActionDefinition action = ResolveGuardianFormLearnedAction(actor, binding);
        if (!CanSelectGuardianFormLearnedAction(actor, action))
        {
            throw new InvalidOperationException("The learned GF command is currently unavailable.");
        }
        if (IsExpandedMiniMogAction(action.NativeAction))
        {
            throw new InvalidOperationException("MiniMog is unavailable as a form command.");
        }
        byte sequence = binding.Command == 28 ? GetGuardianFormPhysicalBinding(form).Sequence : ProfileForForm(form).SupportSequence;
        BattleAttackCommand command = PrepareExpandedPlayerAction(actor, target, action, 0, binding.TargetFlags, sequence) with { GuardianFormLearnedAbility = abilityId };
        SubmitExpandedPlayerAction(actor, action, command);
    }

    private int LearnedCommandGuardian(BattleCombatant actor, byte? command)
    {
        if (GetGuardianForm(actor.Id) is { } form)
        {
            return form.GuardianForceId;
        }
        // The original single-form implementation could queue MiniMog after returning Ifrit.
        return command == 38 ? 2 : throw new InvalidDataException("A learned form command has lost its GF owner.");
    }

    private void ValidateGuardianFormLearnedCommand(BattleAttackCommand command, int abilityId)
    {
        BattleCombatant actor = GetActor(command.ActorId);
        BattleGuardianFormState? form = GetGuardianForm(actor.Id);
        int guardianForceId = LearnedCommandGuardian(actor, command.NativeQueuedCommand);
        BattleNativeCommandBinding binding = FindGuardianFormLearnedBinding(guardianForceId, abilityId);
        BattleActionDefinition action = ResolveGuardianFormLearnedAction(actor, binding, command.NativeAction);
        bool miniMog = IsExpandedMiniMogAction(action.NativeAction);
        if (!GuardianFormsEnabled || !actor.IsPlayer || !actor.EquipmentIds.Contains(guardianForceId) || guardianForces.GetValueOrDefault(guardianForceId) is not { Acquired: true } ||
            (miniMog ? form is not null : form is not { EntryCompleted: true } || form.GuardianForceId != guardianForceId) ||
            command.GuardianFormAction is not null || command.GuardianFormInvocation is not null || command.FreeNativeMagic ||
            command.Ability != action.Ability || command.ActionId != action.Id || command.NativeQueuedCommand != binding.Command || command.NativeAction is null)
        {
            throw new InvalidDataException("Saved learned GF command differs from its installed action or form owner.");
        }
    }
}
