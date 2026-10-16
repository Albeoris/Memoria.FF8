namespace Memoria.FF8.Runtime;

public sealed record BattleNativeLimitState(string ActorId, byte CrisisLevel)
{
    public BattleNativeSlotsRoll? Slots { get; init; }
    public BattleNativeShotState? Shot { get; init; }
    public BattleNativeDuelState? Duel { get; init; }
    public BattleNativeRenzokukenState? Renzokuken { get; init; }
}

public sealed partial class BattleSession
{
    private readonly Dictionary<string, byte> nativeLimitLevels = new(StringComparer.Ordinal);

    public bool HasNativeLimitSelection(string actorId)
    {
        _ = GetActor(actorId);
        return nativeLimitLevels.ContainsKey(actorId);
    }

    public byte GetNativeLimitLevel(string actorId)
    {
        _ = GetActor(actorId);
        return nativeLimitLevels.GetValueOrDefault(actorId);
    }

    public IReadOnlyList<BattleActionDefinition> GetNativePlayerActionChoices(string actorId, bool limits)
    {
        BattleCombatant actor = GetActor(actorId);
        List<BattleNativeAction> choices = [];
        if (limits)
        {
            byte crisis = GetNativeLimitLevel(actorId);
            if (crisis == 0 || actor.NativeLimit is not { } limit)
            {
                return [];
            }
            if (limit.Command is 5 or 11)
            {
                BattleNativeCommandBinding binding = limit.InvocationChoices.Single();
                return [new(BattleAbility.LimitBreak, limit.Command << 16, binding.Name, BattleActionEffect.Native, 0, (binding.TargetFlags & 0x40) == 0)];
            }
            if (limit.Command == 15)
            {
                NativeLimitProgress progress = definition.LimitProgress ?? throw new NotSupportedException("Blue Magic requires the actual saved learned spell flags.");
                progress.Validate();
                choices.AddRange(definition.NativeActions.Where(action => action.Command == 15 && action.CrisisLevel == crisis && (progress.BlueMagicFlags & (1 << action.ActionId)) != 0));
            }
            else if (actor.ModelId is >= 6 and <= 10)
            {
                choices.Add(FindInstalledNativeAction(limit.Command, NativeBattleLimitRules.GuestActionId(actor.ModelId), crisis));
            }
            else if (limit.Command == 19)
            {
                choices.AddRange(GetNativeRinoaLimitActions(actor, crisis));
            }
            else if (limit.Command == 16)
            {
                if (nativeSlotsRolls.TryGetValue(actorId, out BattleNativeSlotsRoll? roll))
                {
                    choices.Add(FindInstalledNativeAction(16, roll.MagicId));
                }
            }
            else if (limit.Command == 14)
            {
                choices.AddRange(GetNativeShotChoices());
            }
            else
            {
                throw new NotSupportedException($"Native Limit command {limit.Command} requires its original invocation process.");
            }
        }
        else
        {
            foreach (BattleNativeCommandBinding binding in actor.NativeCommands.Where(binding => binding.Command is not (1 or 2 or 3 or 4 or 6)))
            {
                choices.Add(FindNativeCommandAction(actor, binding.Command, NativeBindingActionId(binding)));
            }
        }
        BattleAbility ability = limits ? BattleAbility.LimitBreak : BattleAbility.NativeCommand;
        return choices.Select(action =>
        {
            byte targetFlags = action.TargetFlags ?? actor.NativeCommands.Single(binding => binding.Command == action.Command).TargetFlags;
            int id = (action.Command << 16) | action.ActionId;
            return new BattleActionDefinition(ability, id, action.Name ?? $"Command {action.Command}", BattleActionEffect.Native, action.Power, (targetFlags & 0x40) == 0, (targetFlags & 0x30) != 0x10 && (targetFlags & 2) == 0) { NativeAction = action, NativeAttackFlags = action.AttackFlags, Available = action.Command != 14 || nativeItems.Any(stock => stock.ItemId == action.ActionId + 101 && stock.Count != 0), DisplayName = action.Command == 16 ? $"{action.Name} ×{nativeSlotsRolls[actorId].Repetitions}" : null };
        }).ToArray();
    }

    public byte RefreshNativeLimit(string actorId)
    {
        BattleCombatant actor = GetActor(actorId);
        if (!actor.IsPlayer || !actor.IsReady || actor.NativeLimit is not { } limit)
        {
            throw new InvalidOperationException("Native Limit selection requires a ready player with its original character record.");
        }
        bool disabled = definition.NativeLimitsDisabled ?? throw new NotSupportedException("Native Limit selection requires the actual battle prohibition flag.");
        int deadParty = combatants.Count(member => member.IsPlayer && member.IsActive && (NativeSavedStatusBits(member) & 1) != 0);
        ushort savedStatuses = NativeSavedStatusBits(actor);
        uint battleStatuses = NativeBattleStatusBits(actor);
        byte roll = disabled || (battleStatuses & 0x200) != 0 ? (byte)0 : (byte)NextRandomByte();
        byte crisis = NativeBattleLimitRules.CrisisLevel(limit, actor.CurrentHp, actor.MaximumHp, savedStatuses, battleStatuses, deadParty, roll, disabled);
        nativeLimitLevels[actorId] = crisis;
        nativeSlotsRolls.Remove(actorId);
        return crisis;
    }

    public void QueueNativePlayerCommand(string actorId, string targetId, byte command, ushort actionId, byte sequence = 0, byte? crisis = null)
    {
        RequireNativeEntryReady();
        BattleCombatant actor = GetActor(actorId);
        BattleCombatant target = GetActor(targetId);
        if (Outcome != BattleOutcome.Active || !actor.IsPlayer || !actor.IsReady)
        {
            throw new InvalidOperationException("Native player command requires an active battle and a ready player.");
        }
        bool limit = actor.NativeLimit?.Command == command;
        if (limit)
        {
            byte currentCrisis = nativeLimitLevels.GetValueOrDefault(actorId);
            if (currentCrisis == 0 || crisis is not null && crisis != currentCrisis)
            {
                throw new InvalidOperationException("Native Limit command has no current owned crisis selection.");
            }
            crisis = currentCrisis;
            ValidateNativeLimitSelection(actor, command, actionId);
        }
        else if (!actor.NativeCommands.Any(binding => binding.Command == command && NativeBindingActionId(binding) == actionId))
        {
            throw new InvalidOperationException("Native command is not equipped in this character's saved command slots.");
        }
        if (target.NativeIndex is < 0 or >= 16)
        {
            throw new InvalidOperationException("Native player target has no allocated physical battle slot.");
        }
        if (command == 5)
        {
            QueueNativeRenzokuken(actor, target, crisis!.Value);
            return;
        }
        if (command == 11)
        {
            BeginNativeDuel(actor, target, crisis!.Value);
            return;
        }
        BattleNativeAction action = FindNativeCommandAction(actor, command, actionId, crisis);
        byte targetFlags = action.TargetFlags ?? actor.NativeCommands.Single(binding => binding.Command == command).TargetFlags;
        OnNativePlayerCommandSubmitted(actor, command);
        if (command == 14)
        {
            BeginNativeShot(actor, target, action, crisis!.Value);
            return;
        }
        if (command == 19 && actionId == 4)
        {
            targetFlags = actor.NativeLimit!.InvocationChoices[1].TargetFlags;
        }
        if (command == 16)
        {
            QueueNativeSlots(actor, action, sequence, crisis);
            nativeLimitLevels.Remove(actorId);
            nativeSlotsRolls.Remove(actorId);
            return;
        }
        ushort targetMask = command == 19 && actionId != 4 ? NativeAutomaticActionTargetMask(targetFlags) : NativeBattleLimitRules.PlayerTargetMask(targetFlags, NativePlayerSelectedTarget(actor, target, targetFlags).NativeIndex);
        QueueNativeCommand(actor, command, actionId, sequence, targetMask, crisis, playerInput: true);
        nativeLimitLevels.Remove(actorId);
    }

    private static ushort NativeBindingActionId(BattleNativeCommandBinding binding)
    {
        return binding.Command is 1 or 12 or 28 ? (ushort)0 : binding.ActionId;
    }

    private void ValidateNativeLimitSelection(BattleCombatant actor, byte command, ushort actionId)
    {
        if (command == 15)
        {
            NativeLimitProgress progress = definition.LimitProgress ?? throw new NotSupportedException("Blue Magic requires the actual saved learned spell flags.");
            progress.Validate();
            if (actionId >= 16 || (progress.BlueMagicFlags & (1 << actionId)) == 0)
            {
                throw new InvalidOperationException("Blue Magic spell has not been learned.");
            }
        }
        else if (actor.ModelId is >= 6 and <= 10)
        {
            if (actionId != NativeBattleLimitRules.GuestActionId(actor.ModelId))
            {
                throw new InvalidOperationException("Guest Limit command selected another character's native row.");
            }
        }
        else if (command == 19)
        {
            if (!GetNativeRinoaLimitActions(actor, GetNativeLimitLevel(actor.Id)).Any(action => action.ActionId == actionId))
            {
                throw new InvalidOperationException("Rinoa Limit selection is not available in the current saved flags and crisis level.");
            }
        }
        else if (command == 16)
        {
            if (!nativeSlotsRolls.TryGetValue(actor.Id, out BattleNativeSlotsRoll? roll) || roll.MagicId != actionId)
            {
                throw new InvalidOperationException("Slots requires its current owned roll before accepting a magic choice.");
            }
        }
        else if (command == 14)
        {
            if (!GetNativeShotChoices().Any(action => action.ActionId == actionId) || !nativeItems.Any(stock => stock.ItemId == actionId + 101 && stock.Count != 0))
            {
                throw new InvalidOperationException("Shot requires the actual selected ammunition stock.");
            }
        }
        else if (command is 5 or 11)
        {
            if (actionId != 0)
            {
                throw new InvalidOperationException("Timed Limit invocation does not select an effect row before its native move process.");
            }
        }
        else
        {
            throw new NotSupportedException($"Native Limit command {command} requires its original invocation process.");
        }
    }

    private BattleNativeLimitState[] CaptureNativeLimits()
    {
        return nativeLimitLevels.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new BattleNativeLimitState(pair.Key, pair.Value) { Slots = nativeSlotsRolls.GetValueOrDefault(pair.Key), Shot = GetNativeShot(pair.Key), Duel = GetNativeDuel(pair.Key), Renzokuken = GetNativeRenzokuken(pair.Key) }).ToArray();
    }

    private void RestoreNativeLimits(BattleNativeLimitState[] states)
    {
        if (states is not null && states.Sum(state => (state?.Shot is null ? 0 : 1) + (state?.Duel is null ? 0 : 1) + (state?.Renzokuken is null ? 0 : 1)) > 1)
        {
            throw new InvalidDataException("Saved timed Limit processes contradict the single native UI process owner.");
        }
        if (states is null || states.Any(state => state is null || state.CrisisLevel > 4 || !byId.TryGetValue(state.ActorId, out BattleCombatant? actor) || !actor.IsPlayer || actor.NativeLimit is null) || states.Select(state => state.ActorId).Distinct(StringComparer.Ordinal).Count() != states.Length)
        {
            throw new InvalidDataException("Saved Limit selection has invalid character ownership or crisis level.");
        }
        nativeLimitLevels.Clear();
        nativeSlotsRolls.Clear();
        nativeShotProcesses.Clear();
        nativeDuelProcesses.Clear();
        nativeRenzokukenProcesses.Clear();
        foreach (BattleNativeLimitState state in states)
        {
            nativeLimitLevels.Add(state.ActorId, state.CrisisLevel);
            if (state.Slots is { } roll)
            {
                if (byId[state.ActorId].NativeLimit!.Command != 16 || state.CrisisLevel == 0 || roll.Repetitions == 0)
                {
                    throw new InvalidDataException("Saved Slots roll does not belong to a current Selphie Limit selection.");
                }
                _ = FindInstalledNativeAction(16, roll.MagicId);
                nativeSlotsRolls.Add(state.ActorId, roll);
            }
            if (state.Shot is { } shot)
            {
                RestoreNativeShot(state.ActorId, state.CrisisLevel, shot);
            }
            if (state.Duel is { } duel)
            {
                RestoreNativeDuel(state.ActorId, state.CrisisLevel, duel);
            }
            if (state.Renzokuken is { } renzokuken)
            {
                RestoreNativeRenzokuken(state.ActorId, state.CrisisLevel, renzokuken);
            }
        }
        ValidateNativeRenzokukenOwners();
        ValidateNativeLimitCallbacks();
    }
}
