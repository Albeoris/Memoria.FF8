using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleAiRecipients(ImmutableArray<string> ActorIds);

public sealed partial class BattleSession
{
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
            208 => PickExpandedAiTarget(allies.Where(IsExpandedAiTarget)),
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
        return recipients.ActorIds.Select(GetActor).Where(IsExpandedAiTarget).ToArray();
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
