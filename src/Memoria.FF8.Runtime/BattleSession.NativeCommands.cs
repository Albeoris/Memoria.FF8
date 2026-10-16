namespace Memoria.FF8.Runtime;

public sealed record BattleNativeLimitState(string ActorId, byte CrisisLevel);

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
            else
            {
                throw new NotSupportedException($"Native Limit command {limit.Command} requires its original invocation process.");
            }
        }
        else
        {
            foreach (BattleNativeCommandBinding binding in actor.NativeCommands.Where(binding => binding.Command is not (1 or 2 or 3 or 4 or 6)))
            {
                choices.Add(FindInstalledNativeAction(binding.Command, binding.ActionId));
            }
        }
        BattleAbility ability = limits ? BattleAbility.LimitBreak : BattleAbility.NativeCommand;
        return choices.Select(action =>
        {
            byte targetFlags = action.TargetFlags ?? actor.NativeCommands.Single(binding => binding.Command == action.Command).TargetFlags;
            int id = (action.Command << 16) | action.ActionId;
            return new BattleActionDefinition(ability, id, action.Name ?? $"Command {action.Command}", BattleActionEffect.Native, action.Power, (targetFlags & 0x40) == 0, (targetFlags & 0x30) != 0x10 && (targetFlags & 2) == 0) { NativeAction = action, NativeAttackFlags = action.AttackFlags };
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
        return crisis;
    }

    public void QueueNativePlayerCommand(string actorId, string targetId, byte command, ushort actionId, byte sequence = 0, byte? crisis = null)
    {
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
        else if (!actor.NativeCommands.Any(binding => binding.Command == command && binding.ActionId == actionId))
        {
            throw new InvalidOperationException("Native command is not equipped in this character's saved command slots.");
        }
        if (target.NativeIndex is < 0 or >= 16)
        {
            throw new InvalidOperationException("Native player target has no allocated physical battle slot.");
        }
        BattleNativeAction action = FindInstalledNativeAction(command, actionId, crisis);
        byte targetFlags = action.TargetFlags ?? actor.NativeCommands.Single(binding => binding.Command == command).TargetFlags;
        QueueNativeCommand(actor, command, actionId, sequence, NativeBattleLimitRules.PlayerTargetMask(targetFlags, target.NativeIndex), crisis);
        nativeLimitLevels.Remove(actorId);
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
        else
        {
            throw new NotSupportedException($"Native Limit command {command} requires its original invocation process.");
        }
    }

    private BattleNativeLimitState[] CaptureNativeLimits()
    {
        return nativeLimitLevels.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new BattleNativeLimitState(pair.Key, pair.Value)).ToArray();
    }

    private void RestoreNativeLimits(BattleNativeLimitState[] states)
    {
        if (states is null || states.Any(state => state is null || state.CrisisLevel > 4 || !byId.TryGetValue(state.ActorId, out BattleCombatant? actor) || !actor.IsPlayer || actor.NativeLimit is null) || states.Select(state => state.ActorId).Distinct(StringComparer.Ordinal).Count() != states.Length)
        {
            throw new InvalidDataException("Saved Limit selection has invalid character ownership or crisis level.");
        }
        nativeLimitLevels.Clear();
        foreach (BattleNativeLimitState state in states)
        {
            nativeLimitLevels.Add(state.ActorId, state.CrisisLevel);
        }
    }
}
