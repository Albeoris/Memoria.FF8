using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private ImmutableArray<(int Ability, byte Command)> ifritLearnedCommands;

    private ImmutableArray<(int Ability, byte Command)> IfritLearnedCommands
    {
        get
        {
            if (ifritLearnedCommands.IsDefault)
            {
                ifritLearnedCommands = BattleGuardianFormProfiles.LearnedCommands(definition.MagicVitals, 2).ToImmutableArray();
            }
            return ifritLearnedCommands;
        }
    }

    public IReadOnlyList<BattleGuardianFormCommand> GetGuardianFormCommands(string actorId)
    {
        if (GetGuardianForm(actorId) is not { } form)
        {
            return [];
        }
        BattleCombatant actor = GetActor(actorId);
        List<BattleGuardianFormCommand> result = [];
        foreach (BattleGuardianFormActionBinding binding in GetGuardianFormBindings(form))
        {
            BattleActionDefinition action = ResolveGuardianFormAction(form, binding);
            bool available = CanSelectGuardianFormInnateAction(actor, binding, action) && combatants.Any(target => IsGuardianFormInnateTarget(actor, target, action));
            result.Add(new((int)binding.Action, action with { Available = available }, TargetFlags: action.NativeAction?.TargetFlags));
        }
        foreach ((int ability, byte command) in IfritLearnedCommands)
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
        return command.LearnedAbilityId is null
            ? combatants.Where(target => IsGuardianFormInnateTarget(actor, target, command.Action)).ToArray()
            : combatants.Where(target => IsExpandedPlayerActionTarget(actor, target, command.Action, command.TargetFlags)).ToArray();
    }

    private static bool CanSelectGuardianFormInnateAction(BattleCombatant actor, BattleGuardianFormActionBinding binding, BattleActionDefinition action)
    {
        return action.Available && (binding.Source == BattleGuardianFormActionSource.Physical || (NativeSavedStatusBits(actor) & 0x10) == 0);
    }

    private static bool IsGuardianFormInnateTarget(BattleCombatant actor, BattleCombatant target, BattleActionDefinition action)
    {
        return target.IsActive && !target.Untargetable && HasValidTargetLifeState(action, target) && (actor.IsPlayer == target.IsPlayer) == action.Allies;
    }

    private BattleNativeCommandBinding ReadGuardianFormCommandBinding(int guardianForceId, byte command)
    {
        BattleGuardianFormResources resource = GuardianFormResources(guardianForceId);
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
        foreach ((int ability, byte command) in IfritLearnedCommands)
        {
            if (ability == abilityId)
            {
                return ReadGuardianFormCommandBinding(guardianForceId, command);
            }
        }
        throw new InvalidOperationException($"GF ability {abilityId} is not a learned battle command.");
    }

    private BattleActionDefinition ResolveGuardianFormLearnedAction(BattleCombatant actor, BattleNativeCommandBinding binding)
    {
        BattleNativeAction native = FindNativeCommandAction(actor, binding.Command, NativeBindingActionId(binding), learnedBinding: binding);
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
        bool miniMog = IsExpandedMiniMogAction(action.NativeAction);
        byte? sequence = miniMog ? null : binding.Command == 28 ? GetGuardianFormPhysicalBinding(form).Sequence : GetGuardianFormSpellBinding(form).Sequence;
        BattleAttackCommand command = PrepareExpandedPlayerAction(actor, target, action, 0, binding.TargetFlags, sequence) with { GuardianFormLearnedAbility = abilityId };
        if (miniMog)
        {
            ReturnGuardianForm(actor, false);
        }
        SubmitExpandedPlayerAction(actor, action, command);
    }

    private void ValidateGuardianFormLearnedCommand(BattleAttackCommand command, int abilityId)
    {
        const int ifritId = 2;
        BattleCombatant actor = GetActor(command.ActorId);
        BattleGuardianFormState? form = GetGuardianForm(actor.Id);
        BattleNativeCommandBinding binding = FindGuardianFormLearnedBinding(ifritId, abilityId);
        BattleActionDefinition action = ResolveGuardianFormLearnedAction(actor, binding);
        bool miniMog = IsExpandedMiniMogAction(action.NativeAction);
        if (!GuardianFormsEnabled || !actor.IsPlayer || !actor.EquipmentIds.Contains(ifritId) || guardianForces.GetValueOrDefault(ifritId) is not { Acquired: true } ||
            (miniMog ? form is not null : form is not { EntryCompleted: true, GuardianForceId: ifritId }) ||
            command.GuardianFormAction is not null || command.GuardianFormInvocation is not null || command.FreeNativeMagic ||
            command.Ability != action.Ability || command.ActionId != action.Id || command.NativeQueuedCommand != binding.Command || command.NativeAction is null)
        {
            throw new InvalidDataException("Saved learned GF command differs from its installed action or form owner.");
        }
    }
}
