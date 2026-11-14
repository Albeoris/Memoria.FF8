using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleAiRecipients(ImmutableArray<string> ActorIds)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? DeathSelfTargetId { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public BattleSide? RandomSide { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public BattleAiTargetPolicy TargetPolicy { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public BattleAiSelection Selection { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ushort? NativeSelectionMask { get; init; }
}

public enum BattleAiSelection
{
    Selected,
    Group,
    Physical,
    NativeMask
}

public enum BattleAiTargetPolicy
{
    Standard,
    DrawnMagic,
    Compelled
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

    private bool HasExpandedAiAction(BattleCombatant actor)
    {
        return ActiveAction is { NativeAiCallbackStage: not null } active && active.ActorId == actor.Id ||
            actions.Any(command => command.ActorId == actor.Id && command.NativeAiCallbackStage is not null);
    }

    private void CompleteExpandedAiRemovals()
    {
        foreach (BattleCombatant actor in enemies.Where(actor => actor.ExpandedAiRemovalPending && !HasExpandedAiAction(actor)))
        {
            // TODO(TD-009): Descriptor 105 waits for the native model callback; expanded actions own the logical wait.
            actor.ExpandedAiRemovalPending = false;
            ReleaseNativeParticipant(actor);
            actor.IsBusy = false;
        }
    }

    private void ValidateExpandedAiRemovals()
    {
        if (combatants.Any(actor => actor.ExpandedAiRemovalPending && (!UsesExpandedRoster || actor.IsPlayer || !actor.IsActive || actor.NativeAi is null || !HasExpandedAiAction(actor))))
        {
            throw new InvalidDataException("Saved expanded AI removal requires its active enemy and pending action.");
        }
    }

    private void CompleteExpandedEnemyDeath(BattleCombatant actor)
    {
        if (nativeAiCallbackStage != 3 && !UsesOriginalEnemySlots)
        {
            throw new NotSupportedException("Expanded AI death projection requires the enemy death callback.");
        }
        // TODO(TD-009): The expanded host uses its roster KO presentation in place of descriptor 106.
        actor.NativeStatus0 |= 1;
        actor.NativeCombatantFlags &= ~0xCu;
        actor.Atb = 0;
        if (nativeAiCallbackStage != 3)
        {
            Log("Defeated", actor.Id, actor.Id, 0, $"{actor.Name} is defeated.");
        }
        if (UsesOriginalEnemySlots && actor.NativeAi?.DeathSequenceReleasesAllocation == true)
        {
            // TODO(TD-009): The expanded host retains sequence 3's allocation release in the graphics FIFO.
            QueueExpandedAiDescriptor(actor, new(BattleExpandedAiDescriptorKind.DeathRelease));
        }
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
            204 => ResolveExpandedAiGroup(allies),
            205 => ResolveExpandedAiGroup(enemies),
            206 => ResolveExpandedAiGroup(combatants),
            207 => PickExpandedAiTarget(enemies.Where(candidate => candidate != actor && IsExpandedAiTarget(candidate)).DefaultIfEmpty(actor).Where(IsExpandedAiTarget)),
            208 => new([.. allies.Where(IsExpandedAiTarget).Select(candidate => candidate.Id)]) { RandomSide = BattleSide.Allies },
            >= 220 and <= 227 => ResolveExpandedAiLocalTarget(actor, selector - 220),
            209 when UsesOriginalEnemySlots => ResolveExpandedOriginalTarget(nativeLastAllocatedIndex),
            209 => throw new NotSupportedException($"Expanded AI selector {selector} requires a separately owned allocation."),
            _ => ResolveExpandedModelTarget(selector)
        };
    }

    private BattleAiRecipients ResolveExpandedAiGroup(IEnumerable<BattleCombatant> actors)
    {
        return new([.. actors.Where(IsExpandedAiTarget).Select(candidate => candidate.Id)]) { Selection = UsesOriginalEnemySlots ? BattleAiSelection.Group : BattleAiSelection.Selected };
    }

    private BattleAiRecipients ResolveExpandedModelTarget(int selector)
    {
        BattleCombatant? target = UsesOriginalEnemySlots ? ExpandedAiModelActor(selector) : combatants.FirstOrDefault(candidate => IsExpandedAiTarget(candidate) && NativeModelId(candidate) == selector);
        return target is null ? new([]) : new([target.Id]);
    }

    private BattleCombatant? ExpandedAiModelActor(int model)
    {
        IEnumerable<BattleCombatant> candidates = combatants.Where(candidate => candidate.Slot is not null && NativeModelId(candidate) == model);
        return UsesOriginalEnemySlots ? candidates.OrderBy(candidate => candidate.Side).ThenBy(candidate => candidate.Slot!.Value.Position).FirstOrDefault() : candidates.FirstOrDefault();
    }

    private BattleAiRecipients ResolveExpandedOriginalTarget(int index)
    {
        if (!UsesOriginalEnemySlots)
        {
            throw new NotSupportedException("Expanded AI direct actor targeting requires original enemy slots.");
        }
        BattleCombatant? target = FindActiveNativeActor(index);
        return target is null ? new([]) : new([target.Id]) { Selection = BattleAiSelection.Physical };
    }

    private void ValidateExpandedOriginalEnemySlots()
    {
        BattleCombatant[] allocated = enemies.Where(actor => !IsAddedEnemy(actor) && actor.Slot is not null).ToArray();
        if (allocated.Select(actor => actor.NativeActorIndex).Distinct().Count() != allocated.Length ||
            allocated.Any(actor => actor.NativeActorIndex is null || actor.ResolvedNativeLevel is null || actor.NativeAllocationPendingStatistics || actor.NativeTemplate is null) ||
            enemies.Any(actor => IsAddedEnemy(actor) && actor.NativeActorIndex.HasValue))
        {
            throw new InvalidDataException("Expanded original enemy slots require unique owners with resolved allocation statistics.");
        }
    }

    private static BattleAiRecipients ResolveExpandedAiLocalTarget(BattleCombatant actor, int index)
    {
        if (actor.AiActorReferences.TryGetValue(index, out string? targetId))
        {
            return new([targetId]);
        }
        return new([]) { Selection = BattleAiSelection.NativeMask, NativeSelectionMask = NativeAiIndexMask(unchecked((int)actor.AiVariables[index])) };
    }

    private static void StoreExpandedAiLastAttacker(BattleCombatant actor, int variable, bool add)
    {
        if (variable is < 220 or > 227 || add)
        {
            throw new NotSupportedException("Expanded battle AI can only assign an actor reference to a local variable.");
        }
        int index = variable - 220;
        string attackerId = actor.LastAttackerId ?? throw new NotSupportedException("Expanded AI requires the last contact owner to store an attacker reference.");
        actor.AiVariables[index] = 0;
        actor.AiActorReferences[index] = attackerId;
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

    internal BattleCombatant[] CurrentExpandedAiTargets(BattleAiRecipients recipients, BattleNativeAction? action = null)
    {
        ValidateExpandedAiTargetSelection(recipients);
        if (recipients.NativeSelectionMask is ushort mask)
        {
            return ResolveExpandedNativeTargetMask(ExpandedNativeTargetRequest(mask, action));
        }
        IEnumerable<BattleCombatant> targets = recipients.RandomSide is BattleSide side ? Roster(side) : recipients.ActorIds.Select(GetActor);
        if (recipients.TargetPolicy == BattleAiTargetPolicy.Compelled)
        {
            return targets.Where(candidate => IsExpandedCompelledTarget(candidate, action?.TargetFlags ?? 0)).ToArray();
        }
        if (recipients.TargetPolicy == BattleAiTargetPolicy.DrawnMagic)
        {
            bool includesKnockedOutAllies = (action?.TargetFlags.GetValueOrDefault() & 1) != 0;
            return targets.Where(candidate => IsExpandedDrawCastTarget(candidate, includesKnockedOutAllies)).ToArray();
        }
        if (UsesOriginalEnemySlots && recipients.RandomSide is null && recipients.Selection != BattleAiSelection.Group)
        {
            return ResolveExpandedOriginalAiTargets(recipients, action, targets.ToArray());
        }
        if (recipients.RandomSide is null && EnemyActionTargetsKnockedOutAllies(action))
        {
            // Attack flag 0x80 admits selected KO party recipients in the native target mask.
            return targets.Where(candidate => IsExpandedAiTarget(candidate) || candidate.IsPlayer && candidate.IsActive && !candidate.Untargetable && !candidate.IsEjected && !candidate.IsAlive).ToArray();
        }
        return targets.Where(target => IsExpandedAiTarget(target) || target.IsActive && target.Id == recipients.DeathSelfTargetId).ToArray();
    }

    private void ValidateExpandedAiTargetSelection(BattleAiRecipients recipients)
    {
        bool nativeMask = recipients.Selection == BattleAiSelection.NativeMask;
        bool requiresOriginalSlots = recipients.Selection is BattleAiSelection.Group or BattleAiSelection.Physical ||
            recipients.NativeSelectionMask is ushort mask && new NativeBattleTargetRequest(mask).Selection != 0;
        if (!Enum.IsDefined(recipients.Selection) ||
            requiresOriginalSlots && !UsesOriginalEnemySlots ||
            recipients.Selection != BattleAiSelection.Selected && (recipients.TargetPolicy != BattleAiTargetPolicy.Standard || recipients.RandomSide is not null) ||
            recipients.Selection == BattleAiSelection.Physical && recipients.ActorIds.Length != 1 ||
            nativeMask != recipients.NativeSelectionMask.HasValue ||
            nativeMask && (recipients.ActorIds.IsDefault || !recipients.ActorIds.IsEmpty || recipients.DeathSelfTargetId is not null))
        {
            throw new InvalidDataException("Expanded AI target selection has invalid roster or recipient ownership.");
        }
    }

    private static NativeBattleTargetRequest ExpandedNativeTargetRequest(ushort mask, BattleNativeAction? action)
    {
        if (action is { Command: 2 or 4 })
        {
            byte flags = action.TargetFlags.GetValueOrDefault();
            mask |= (ushort)(((flags & 1) != 0 ? 0x4000 : 0) | ((flags & 2) != 0 ? 0x2000 : 0));
        }
        else if (action is { Command: 8 } && (action.AttackFlags & 0x80) != 0)
        {
            mask |= 0x4000;
        }
        return new(mask);
    }

    private BattleCombatant[] ResolveExpandedNativeTargetMask(NativeBattleTargetRequest request)
    {
        if (request.SelectsRandomTarget)
        {
            return Roster(request.Side).Where(IsExpandedAiTarget).ToArray();
        }
        if (request.SelectsGroup)
        {
            IEnumerable<BattleCombatant> group = request.Selection == 255 ? combatants : Roster(request.Side);
            return group.Where(IsAvailable).ToArray();
        }

        ushort available = 0;
        foreach (BattleCombatant actor in NativeTargetActors().Where(IsAvailable))
        {
            available |= NativeAiIndexMask(actor.NativeActorIndex!.Value);
        }
        var (resolved, _) = NativeBattleActionTargetMask.Resolve(request, NativeBattlePhysicalTargets.FromNativeWord(available), nativeScriptState.DisableAutomaticTargetReplacement);
        if ((resolved >> NativeBattleSlots.Count) != 0)
        {
            throw new NotSupportedException($"Expanded AI resolved an unowned native target mask 0x{resolved:X4}.");
        }
        return NativeTargetActors().Where(actor => actor.IsActive && (resolved & NativeAiIndexMask(actor.NativeActorIndex!.Value)) != 0).OrderBy(actor => actor.NativeActorIndex).ToArray();

        bool IsAvailable(BattleCombatant actor)
        {
            return actor.IsActive && !actor.Untargetable && ((NativeSavedStatusBits(actor) & 1) == 0 || request.IncludesKnockedOutAllies && actor.IsPlayer);
        }
    }

    private BattleCombatant[] ResolveExpandedOriginalAiTargets(BattleAiRecipients recipients, BattleNativeAction? action, BattleCombatant[] selected)
    {
        bool includesKnockedOutAllies = EnemyActionTargetsKnockedOutAllies(action);
        BattleCombatant[] available = selected.Where(IsAvailable).ToArray();
        if (available.Length != 0)
        {
            return available;
        }
        if (!nativeScriptState.DisableAutomaticTargetReplacement && selected.FirstOrDefault() is { Slot: { } slot } source)
        {
            IEnumerable<BattleCombatant> roster = Roster(source.Side).Where(candidate => candidate.Slot is not null && IsAvailable(candidate));
            if (recipients.Selection == BattleAiSelection.Physical)
            {
                roster = roster.Where(HasNativeRosterSlot);
            }
            BattleCombatant? replacement = roster.OrderBy(candidate => candidate.Slot!.Value.Position).FirstOrDefault(candidate => candidate.Slot!.Value.Position > slot.Position)
                ?? roster.OrderByDescending(candidate => candidate.Slot!.Value.Position).FirstOrDefault(candidate => candidate.Slot!.Value.Position < slot.Position);
            if (replacement is not null)
            {
                return [replacement];
            }
        }
        // 48EE50/48EEB0 keep the selected mask when replacement is disabled or exhausts the side.
        // The contact loop owns KO rejection after resolution.
        return selected.Where(candidate => candidate.IsActive).ToArray();

        bool IsAvailable(BattleCombatant candidate)
        {
            return candidate.IsActive && (candidate.Id == recipients.DeathSelfTargetId ||
                !candidate.Untargetable && !candidate.IsEjected && (candidate.IsAlive || candidate.IsPlayer && includesKnockedOutAllies));
        }
    }

    private static bool EnemyActionTargetsKnockedOutAllies(BattleNativeAction? action)
    {
        return action is { Command: 8 or 252 } && (action.AttackFlags & 0x80) != 0;
    }

    private void QueueExpandedGraphicCommand(BattleCombatant actor, byte magicId, byte sequence, BattleAiRecipients? recipients)
    {
        EnqueueExpandedAiAction(actor, 252, 1, sequence, recipients ?? new([]), graphic: new(magicId));
    }

    private void QueueExpandedAiAction(BattleCombatant actor, byte command, ushort id, byte sequence, BattleAiRecipients? recipients)
    {
        BattleNativeAction native = FindNativeCommandAction(actor, command, id);
        if (!NativeBattleExpandedAi.SupportsAction(native, UsesOriginalEnemySlots))
        {
            throw new NotSupportedException($"Expanded AI command {command}/{id} requires an unsupported combat service.");
        }
        EnqueueExpandedAiAction(actor, native, sequence, recipients);
    }

    private void EnqueueExpandedAiAction(BattleCombatant actor, BattleNativeAction native, byte sequence, BattleAiRecipients? recipients)
    {
        EnqueueExpandedAiAction(actor, native.Command, native.ActionId, sequence, recipients, native);
    }

    private void EnqueueExpandedAiAction(BattleCombatant actor, byte command, ushort actionId, byte sequence, BattleAiRecipients? recipients, BattleNativeAction? native = null, BattleNativeGraphicCommand? graphic = null)
    {
        if (recipients is null || recipients.ActorIds.IsEmpty && recipients.Selection != BattleAiSelection.NativeMask && graphic is null)
        {
            return;
        }
        ValidateExpandedAiTargetSelection(recipients);
        if (nativeAiCallbackStage == 3 && recipients.RandomSide is null && recipients.ActorIds.Contains(actor.Id) && (NativeSavedStatusBits(actor) & 1) == 0)
        {
            recipients = recipients with { DeathSelfTargetId = actor.Id };
        }
        actor.Atb = 0;
        actor.IsBusy = true;
        actor.ExpandedPreviousAiTargetId = recipients.ActorIds.IsEmpty ? null : recipients.ActorIds[0];
        actions.Enqueue(new(actor.Id, actor.ExpandedPreviousAiTargetId ?? actor.Id, BattleAbility.EnemyAbility, actionId)
        {
            NativeQueuedCommand = command,
            NativeAction = native,
            NativeGraphic = graphic,
            NativeAiCallbackStage = nativeAiCallbackStage,
            NativeSequence = sequence,
            AiRecipients = recipients
        });
    }
}
