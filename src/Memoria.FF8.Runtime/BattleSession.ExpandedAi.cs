using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleAiRecipients(ImmutableArray<string> ActorIds)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? DeathSelfTargetId { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public BattleSide? RandomSide { get; init; }
}

public sealed partial class BattleSession
{
    private void RunExpandedEnemyDeathAi(BattleCombatant actor)
    {
        actor.AiDeathHandled = true;
        // 494410 publishes stage 3 with the enemy's KO bit clear after the immediate callback.
        actor.NativeStatus0 &= 0xFFFE;
        try
        {
            RunExpandedNativeAi(actor, 3);
        }
        finally
        {
            if (actor.CurrentHp == 0)
            {
                actor.NativeStatus0 |= 1;
            }
        }
    }

    private static bool IsExpandedEnemyDeathAction(BattleCombatant actor, byte? stage, BattleAiRecipients? recipients)
    {
        return stage == 3 && recipients is not null && actor.IsActive && !actor.IsPlayer && actor.NativeAi is { } ai && ai.Scripts[3].Length > 0 &&
            (actor.AiDeathHandled || actor.CurrentHp > 0) && (actor.NativeCombatantFlags & 0x20) != 0 && (NativeBattleStatusBits(actor) & 0x10000) == 0;
    }

    private static bool CanExecuteExpandedAction(BattleCombatant actor, byte? stage, BattleAiRecipients? recipients)
    {
        return CanNativeBattleActorAct(actor) || IsExpandedEnemyDeathAction(actor, stage, recipients);
    }

    private bool CanRetainBattleActionActor(BattleCombatant actor, BattleNativePlayerReaction? reaction, byte? stage, BattleAiRecipients? recipients)
    {
        return CanRetainNativeSupportActor(actor, reaction) || UsesExpandedRoster && IsExpandedEnemyDeathAction(actor, stage, recipients);
    }

    private bool HasExpandedEnemyDeathAction(BattleCombatant actor)
    {
        return ActiveAction is { } active && active.ActorId == actor.Id && IsExpandedEnemyDeathAction(actor, active.NativeAiCallbackStage, active.AiRecipients) ||
            actions.Any(command => command.ActorId == actor.Id && IsExpandedEnemyDeathAction(actor, command.NativeAiCallbackStage, command.AiRecipients));
    }

    private void CompleteExpandedEnemyDeath(BattleCombatant actor)
    {
        if (nativeAiCallbackStage != 3)
        {
            throw new NotSupportedException("Expanded AI death projection requires the enemy death callback.");
        }
        // TODO(TD-009): The expanded host uses its roster KO presentation in place of descriptor 106.
        actor.NativeStatus0 |= 1;
        actor.NativeCombatantFlags &= ~0xCu;
        actor.Atb = 0;
    }

    private void RunExpandedNativeAi(BattleCombatant actor, int callback)
    {
        if (callback == 1)
        {
            actor.AiTurnCount = unchecked((byte)(actor.AiTurnCount + 1));
        }
        byte? previous = nativeAiCallbackStage;
        nativeAiCallbackStage = checked((byte)callback);
        try
        {
            if (actor.NativeAi?.Scripts[callback].Length > 0)
            {
                InterpretNativeAi(actor, callback);
            }
        }
        finally
        {
            nativeAiCallbackStage = previous;
        }
    }

    private BattleAiRecipients ResolveExpandedAiTargets(BattleCombatant actor, byte selector)
    {
        return selector switch
        {
            200 => new([actor.Id]),
            201 => PickExpandedAiTarget(allies.Where(IsExpandedAiTarget)),
            202 => PickExpandedAiTarget(enemies.Where(IsExpandedAiTarget)),
            203 => actor.LastAttackerId is { } attacker ? new([attacker]) : new([]),
            204 => new([.. allies.Where(IsExpandedAiTarget).Select(candidate => candidate.Id)]),
            205 => new([.. enemies.Where(IsExpandedAiTarget).Select(candidate => candidate.Id)]),
            206 => new([.. combatants.Where(IsExpandedAiTarget).Select(candidate => candidate.Id)]),
            207 => PickExpandedAiTarget(enemies.Where(candidate => candidate != actor && IsExpandedAiTarget(candidate)).DefaultIfEmpty(actor).Where(IsExpandedAiTarget)),
            208 => new([.. allies.Where(IsExpandedAiTarget).Select(candidate => candidate.Id)]) { RandomSide = BattleSide.Allies },
            209 or >= 220 and <= 227 => throw new NotSupportedException($"Expanded AI selector {selector} requires a separately owned allocation or actor reference."),
            _ => new([.. combatants.Where(candidate => IsExpandedAiTarget(candidate) && NativeModelId(candidate) == selector).Take(1).Select(candidate => candidate.Id)])
        };
    }

    private BattleAiRecipients FilterExpandedAiTargets(byte allTargets, byte group, byte comparison, byte criterion)
    {
        var filter = NativeAiTargetFilter(combatants.Where(HasAiRosterSlot).ToArray(), group, comparison, criterion);
        IEnumerable<BattleCombatant> candidates = combatants.Where(candidate => !candidate.Untargetable && filter.Eligible(candidate));
        return allTargets != 0 ? new([.. candidates.Select(candidate => candidate.Id)]) : PickExpandedAiTarget(candidates);
    }

    private BattleAiRecipients PickExpandedAiTarget(IEnumerable<BattleCombatant> candidates)
    {
        BattleCombatant[] available = candidates.ToArray();
        return available.Length == 0 ? new([]) : new([available[NextRandomByte() % available.Length].Id]);
    }

    private static bool IsExpandedAiTarget(BattleCombatant candidate)
    {
        return candidate.IsActive && candidate.IsAlive && !candidate.Untargetable && (NativeSavedStatusBits(candidate) & 5) == 0;
    }

    private BattleCombatant[] CurrentExpandedAiTargets(BattleAiRecipients recipients)
    {
        IEnumerable<BattleCombatant> targets = recipients.RandomSide is BattleSide side ? Roster(side) : recipients.ActorIds.Select(GetActor);
        return targets.Where(target => IsExpandedAiTarget(target) || target.IsActive && target.Id == recipients.DeathSelfTargetId).ToArray();
    }

    private void QueueExpandedAiAction(BattleCombatant actor, byte command, ushort id, byte sequence, BattleAiRecipients? recipients)
    {
        BattleNativeAction native = FindNativeCommandAction(actor, command, id);
        if (!NativeBattleExpandedAi.SupportsAction(native))
        {
            throw new NotSupportedException($"Expanded AI command {command}/{id} requires an unsupported combat service.");
        }
        if (recipients is null || recipients.ActorIds.IsEmpty)
        {
            return;
        }
        if (nativeAiCallbackStage == 3 && recipients.RandomSide is null && recipients.ActorIds.Contains(actor.Id) && (NativeSavedStatusBits(actor) & 1) == 0)
        {
            recipients = recipients with { DeathSelfTargetId = actor.Id };
        }
        actor.Atb = 0;
        actor.IsBusy = true;
        actions.Enqueue(new(actor.Id, recipients.ActorIds[0], BattleAbility.EnemyAbility, id)
        {
            NativeQueuedCommand = command,
            NativeAction = native,
            NativeAiCallbackStage = nativeAiCallbackStage,
            NativeSequence = sequence,
            AiRecipients = recipients
        });
    }
}
