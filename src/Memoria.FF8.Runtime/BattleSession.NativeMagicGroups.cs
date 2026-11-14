using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed record ExpandedMagicCastPresentation(BattleActionPresentation Cast, ImmutableArray<ExpandedBattleContact> Contacts, bool UsesNativeEffect)
    {
        public NativeBattleEffectPresentation? PreparedEffect { get; set; }
    }

    private Queue<ExpandedMagicCastPresentation>? expandedMagicPresentation;
    private string expandedMagicCompletionMessage = "Repeated magic completed.";

    public static bool CanSelectEitherTargetSide(BattleActionDefinition action)
    {
        return action.NativeAction?.TargetFlags is byte flags ? NativeBattleLimitRules.CanSelectEitherSide(flags) : !action.AllTargets;
    }

    public static bool IsValidSelectedActionTarget(BattleCombatant actor, BattleActionDefinition action, BattleCombatant target)
    {
        if (!target.IsActive || target.Untargetable || !HasValidTargetLifeState(action, target))
        {
            return false;
        }
        if (action.NativeAction?.TargetFlags is byte flags && NativeBattleLimitRules.IsFixedAllyTarget(flags))
        {
            return target.Id == actor.Id;
        }
        return CanSelectEitherTargetSide(action) || (target.IsPlayer == actor.IsPlayer) == action.Allies;
    }

    public static bool HasValidTargetLifeState(BattleActionDefinition action, BattleCombatant target)
    {
        if (!target.IsActive)
        {
            return false;
        }
        if (!IsNativeRevive(action))
        {
            return target.IsAlive;
        }
        //491940/4935A0 route Life/Full-life against Zombie through491AD0 damage.
        return !target.IsAlive || action.NativeAction?.AttackType is 5 or 6 && (NativeSavedStatusBits(target) & 0x40) != 0;
    }

    public IReadOnlyList<int> GetNativeMagicCastCounts(string actorId, int spellId)
    {
        BattleCombatant actor = GetActor(actorId);
        int stock = GetAvailableMagicCasts(actorId, spellId);
        return NativeBattleMagicRepetition.NativeCounts(NativeBattleStatusBits(actor), stock);
    }

    public IReadOnlyList<int> GetPlayerMagicCastCounts(string actorId, int spellId)
    {
        BattleCombatant actor = GetActor(actorId);
        int available = GetAvailableMagicCasts(actorId, spellId);
        return NativeBattleMagicRepetition.PlayerCounts(NativeBattleStatusBits(actor), available, actor.NativePlayerStatRules?.NativeStatusPassiveFlags ?? 0, magicVitals?.MagicSystem.InfiniteMagic == true, UnlimitedMagic);
    }

    private (ImmutableArray<ushort> Native, ImmutableArray<string>? Expanded) PreparePlayerMagicTargets(BattleCombatant actor, BattleActionDefinition action, string firstTarget, IReadOnlyList<string>? targets)
    {
        if (targets is null)
        {
            return ([], null);
        }
        if (action.Ability != BattleAbility.Magic || action.NativeAction is not { Command: 2, TargetFlags: byte flags } || targets.Count is < 1 or > 3 || targets[0] != firstTarget ||
            !(!UsesExpandedRoster && GetNativeMagicCastCounts(actor.Id, action.Id).Contains(targets.Count) || actor.IsPlayer && GetPlayerMagicCastCounts(actor.Id, action.Id).Any(count => targets.Count <= count)))
        {
            throw new InvalidOperationException("Magic target groups require the current native Double/Triple status, sufficient stock and one installed spell.");
        }
        ImmutableArray<ushort>.Builder masks = ImmutableArray.CreateBuilder<ushort>(targets.Count);
        foreach (string targetId in targets)
        {
            BattleCombatant target = GetActor(targetId);
            if (!IsValidSelectedActionTarget(actor, action, target))
            {
                throw new InvalidOperationException("Every selected magic target must satisfy the original spell's target contract.");
            }
            if (!UsesExpandedRoster)
            {
                masks.Add(NativeBattleLimitRules.PlayerTargetMask(flags, target.EncodeNativeSlot()));
            }
        }
        return UsesExpandedRoster ? ([], targets.Count == 1 ? null : targets.ToImmutableArray()) : (masks.MoveToImmutable(), null);
    }

    private void ResolveNativeMagicGroup(BattleCombatant actor, BattleActionPresentation command)
    {
        BattleActionDefinition action = FindAction(BattleAbility.Magic, command.ActionId);
        uint statuses = EncounterMulticastStatusBits(actor);
        int mode = command.NativeMagicCastMode == 0 ? command.NativeMagicTargets.Length : command.NativeMagicCastMode;
        int count = mode >= 3 && (statuses & 0x40000) != 0 ? command.NativeMagicTargets.Length : command.NativeMagicTargets.Length >= 2 && (statuses & 0x20000) != 0 ? 2 : 1;
        if (!actor.IsPlayer && HasEncounterPolicy(actor))
        {
            count = Math.Min(command.NativeMagicTargets.Length, (statuses & 0x40000) != 0 ? 3 : (statuses & 0x20000) != 0 ? 2 : 1);
        }
        uint flags = count > 1 && actor.IsPlayer ? actor.NativePlayerStatRules?.NativeStatusPassiveFlags ?? throw new NotSupportedException("Native repeated magic requires its equipped passive flags.") : 0;
        int costMode = command.NativeMagicCastMode != 0 && (statuses & 0x40000) != 0 ? command.NativeMagicCastMode : count;
        bool singleCost = (magicVitals?.MagicSystem.InfiniteMagic != true || magicVitals?.MagicSystem.UnlimitedMagic == true) && (count > 1 && costMode == 2 && (flags & 0x20) != 0 || count > 1 && costMode == 3 && (flags & 0x40) != 0);
        int completed = 0;
        for (int index = 0; index < count; index++)
        {
            if (command.MagicReservationId is not null && !singleCost && GetAvailableMagicCasts(actor.Id, command.ActionId, command.MagicReservationId) == 0)
            {
                break;
            }
            statuses = EncounterMulticastStatusBits(actor);
            if (index > 0 && ((NativeSavedStatusBits(actor) & 0x35) != 0 || (statuses & 0x4009) != 0 || index == 1 && (statuses & 0x60000) == 0 || index == 2 && (statuses & 0x40000) == 0))
            {
                break;
            }
            ushort mask = command.NativeMagicTargets[index];
            BattleCombatant target = combatants.FirstOrDefault(candidate => candidate.IsActive && candidate.EncodeNativeSlot() >= 0 && (mask & (1 << candidate.EncodeNativeSlot())) != 0) ?? byId[command.TargetId];
            if (!target.IsAlive && (command.SmartAttack is not null || !combatants.Any(candidate => candidate.IsActive && candidate.IsAlive && !candidate.Untargetable && candidate.IsPlayer == target.IsPlayer)))
            {
                break;
            }
            BattleActionPresentation cast = command with { TargetId = target.Id, NativeTargetMask = mask, NativeMagicTargets = [], NativeMagicCastMode = 0, NativeAction = action.NativeAction };
            nativeAffinityAction = null;
            //485610 submits one104 for the group; the effect consumes the following headers itself.
            if (!ResolveInstalledNativeAction(actor, target, cast, action, queueGraphics: index == 0))
            {
                break;
            }
            completed++;
            //486A10 returns FF after clearing the last stock as well as when no stock exists.
            //485610 exits before the next cast and this cast's affinity update in either case.
            if (command.MagicReservationId is not null)
            {
                CommitMagicReservation(command.MagicReservationId, singleCost ? 1 : completed);
                CompleteGuardianForceAffinity(actor, cast);
                continue;
            }
            if (actor.IsPlayer && !singleCost && !UnlimitedMagic && (!actor.TryConsumeMagic(command.ActionId) || !actor.NativeMagicInventory.Any(stock => stock.SpellId == command.ActionId && stock.Count > 0)))
            {
                break;
            }
            CompleteGuardianForceAffinity(actor, cast);
        }
        ReleaseMagicReservation(command.MagicReservationId);
        if (actor.IsPlayer && command.MagicReservationId is null && singleCost && completed != 0 && !UnlimitedMagic)
        {
            actor.TryConsumeMagic(command.ActionId);
        }
    }

    private void ResolveExpandedMagicGroup(BattleCombatant actor, BattleActionPresentation command, ImmutableArray<string> targets)
    {
        bool hosted = OwnsExpandedHostedInvocation;
        BattleActionDefinition action = FindAction(BattleAbility.Magic, command.ActionId);
        BattleNativeAction native = action.NativeAction!;
        uint statuses = NativeBattleStatusBits(actor);
        int mode = command.NativeMagicCastMode == 0 ? targets.Length : command.NativeMagicCastMode;
        int count = mode == 3 && (statuses & 0x40000) != 0 ? targets.Length : (statuses & 0x20000) != 0 ? 2 : 1;
        bool singleCost = count > 1 && MagicCost(actor, command.NativeMagicCastMode != 0 && (statuses & 0x40000) != 0 ? mode : count) == 1;
        string? reservationId = command.MagicReservationId;
        int reserved = reservationId is null ? count : magicVitals!.MagicSystem.Reservations.FirstOrDefault(reservation => reservation.ActionId == reservationId)?.Count ?? 0;
        int completed = 0;
        Queue<ExpandedMagicCastPresentation> presentations = new();

        // 485610 produces the whole group before 484180 post-action status work or pending reactions.
        BeginNativeReactionBatch();
        nativeActionCommitDepth++;
        try
        {
            for (int index = 0; index < count; index++)
            {
                statuses = NativeBattleStatusBits(actor);
                if (!CanNativeBattleActorAct(actor) || (NativeSavedStatusBits(actor) & 0x10) != 0 ||
                    index > 0 && ((NativeSavedStatusBits(actor) & 0x35) != 0 || (statuses & 0x4009) != 0 || !NativeBattleMagicRepetition.AdmitsRepeat(statuses, index + 1)) ||
                    (singleCost ? 0 : index) >= reserved || GetAvailableMagicCasts(actor.Id, command.ActionId, reservationId) == 0)
                {
                    break;
                }
                BattleCombatant selected = GetActor(targets[index]);
                if (command.SmartAttack is not null && !IsValidSelectedActionTarget(actor, action, selected))
                {
                    break;
                }
                BattleCombatant? target = SelectExpandedMagicGroupTarget(actor, action, selected);
                if (target is null)
                {
                    break;
                }
                BattleCombatant[] recipients = ExpandedActionTargets(actor, target.Id, BattleAbility.Magic, command.ActionId, null, native);
                if (recipients.Length == 0)
                {
                    break;
                }
                BattleCombatant[][] hitPlan = PlanExpandedHits(recipients, native.HitCount, (native.TargetFlags.GetValueOrDefault() & 2) != 0);
                BattleNativeAction? reflected = null;
                if (hitPlan.Any(hit => hit.Any(recipient => NativeActionReflects(recipient, native))))
                {
                    reflected = definition.NativeActions.SingleOrDefault(candidate => candidate.Command == 247 && candidate.ActionId == native.ActionId && candidate.CrisisLevel is null);
                    if (reflected is null || !ExpandedBattleActionRules.SupportsContacts(reflected))
                    {
                        break;
                    }
                }

                BattleActionPresentation cast = command with { TargetId = target.Id, ExpandedMagicTargets = null, NativeMagicCastMode = 0, NativeAction = native };
                resolvingNativeAction = cast;
                nativeActionScratch = nativeActionScratch with { DeferredEffectsE00 = false, EventLane = 2, Command = 2, Action = native.ActionId };
                NativeBattleActionHeaderPreparation? hostedHeader = hosted ? BuildExpandedActionHeaderPreparation(cast, native) : null;
                bool presentsNativeEffect = hosted || expandedPresentationHost is not null && ExpandedBattleActionRules.HasNativeSpellPresentation(native, hitPlan[0].Length);
                deferringExpandedImpacts = presentsNativeEffect;
                (ImmutableArray<ExpandedBattleContact> contacts, BattleCombatant? reflector) = ResolveExpandedHitPlan(actor, cast, action, native, hitPlan);
                deferringExpandedImpacts = false;
                (BattleActionPresentation? reflection, ImmutableArray<ExpandedBattleContact> reflectedContacts, bool presentsReflection, NativeBattleActionHeaderPreparation? reflectionHeader) = ResolveExpandedReflection(actor, cast, action, reflector, reflected);
                completed++;
                bool continueAffinity = true;
                if (!singleCost)
                {
                    continueAffinity = SpendExpandedMagicGroupStock(actor, command.ActionId, reservationId, completed);
                }
                if (continueAffinity)
                {
                    nativeAffinityAction = native;
                    CompleteGuardianForceAffinity(actor, cast);
                }
                ExpandedBattleContact? first = contacts.FirstOrDefault() ?? reflectedContacts.FirstOrDefault();
                BattlePendingEnemyDraw? hostedDraw = hosted && first is not null ? TakePendingHostedEnemyDraw() : null;
                if (!hosted && first is not null)
                {
                    CommitExpandedEnemyDraw(first.RecipientId, ref pendingEnemyDraw);
                }
                if (hostedHeader is not null)
                {
                    StageExpandedHostedInvocation(cast, hostedHeader, contacts, contacts.IsEmpty ? null : hostedDraw);
                    if (reflection is not null)
                    {
                        StageExpandedHostedInvocation(reflection, reflectionHeader!, reflectedContacts, contacts.IsEmpty ? hostedDraw : null);
                    }
                }
                else
                {
                    presentations.Enqueue(new(cast, contacts, presentsNativeEffect && !contacts.IsEmpty));
                    if (reflection is not null)
                    {
                        presentations.Enqueue(new(reflection, reflectedContacts, presentsReflection && !reflectedContacts.IsEmpty));
                    }
                }
                if (!continueAffinity)
                {
                    break;
                }
            }
            if (singleCost && completed > 0)
            {
                SpendExpandedMagicGroupStock(actor, command.ActionId, reservationId, 1);
            }
            if (completed > 0)
            {
                resolvingNativeAction = command;
                CompleteNativeActionStatuses(actor);
            }
        }
        finally
        {
            deferringExpandedImpacts = false;
            ReleaseMagicReservation(reservationId);
            resolvingNativeAction = null;
            nativeActionCommitDepth--;
        }
        FlushExpandedEnemyReactions();
        if (completed == 0)
        {
            CompleteActionPresentation(command, "Expanded action cancelled.");
            return;
        }

        expandedMagicCompletionMessage = "Repeated magic completed.";
        ActiveAction = command with { Phase = BattleActionPhase.Recovery, RemainingFrames = 1, MagicReservationId = null };
        if (hosted)
        {
            return;
        }
        expandedMagicPresentation = presentations;
        AdvanceExpandedMagicPresentation(ActiveAction, preparing: true);
    }

    private void AdvanceExpandedMagicPresentation(BattleActionPresentation group, bool preparing = false)
    {
        while (expandedMagicPresentation!.TryDequeue(out ExpandedMagicCastPresentation? presentation))
        {
            BattleActionPresentation cast = presentation.Cast;
            if (presentation.UsesNativeEffect)
            {
                if (!preparing && presentation.PreparedEffect is null)
                {
                    throw new InvalidOperationException("A committed expanded magic cast lacks its prepared presentation.");
                }
                BeginExpandedEffect(GetActor(cast.ActorId), cast, cast.NativeAction!, cast.NativeSequence ?? 1, presentation.Contacts, presentation.PreparedEffect);
                return;
            }
            PublishExpandedImpacts(cast, presentation.Contacts);
        }
        expandedMagicPresentation = null;
        CompleteActionPresentation(group, expandedMagicCompletionMessage);
    }

    private bool SpendExpandedMagicGroupStock(BattleCombatant actor, int spell, string? reservationId, int completed)
    {
        if (reservationId is not null)
        {
            CommitMagicReservation(reservationId, completed);
            return InfiniteMagic || UnlimitedMagic || actor.MagicStocks.Any(stock => stock.Id == spell && stock.Count > 0);
        }
        if (UnlimitedMagic)
        {
            return true;
        }
        bool consumed = actor.TryConsumeMagic(spell);
        ReprojectNativePlayerStatistics(actor);
        return consumed && actor.MagicStocks.Any(stock => stock.Id == spell && stock.Count > 0);
    }

    private BattleCombatant? SelectExpandedMagicGroupTarget(BattleCombatant actor, BattleActionDefinition action, BattleCombatant selected)
    {
        if (IsValidSelectedActionTarget(actor, action, selected))
        {
            return selected;
        }
        if (nativeScriptState.DisableAutomaticTargetReplacement && !action.AllTargets)
        {
            return null;
        }
        int position = selected.Slot?.Position ?? -1;
        BattleCombatant[] available = Roster(selected.Side).Where(candidate => IsValidSelectedActionTarget(actor, action, candidate)).ToArray();
        return available.FirstOrDefault(candidate => candidate.Slot?.Position > position) ?? available.LastOrDefault();
    }

    private void ValidateExpandedMagicTargets(string actorId, string targetId, BattleAbility ability, int actionId, ImmutableArray<ushort> nativeTargets, ImmutableArray<string>? expandedTargets, int mode)
    {
        if (expandedTargets is not { } targets)
        {
            ValidateNativeMagicTargets(ability, actionId, nativeTargets, mode);
            return;
        }
        if (!UsesExpandedRoster || !GetActor(actorId).IsPlayer || ability != BattleAbility.Magic || !nativeTargets.IsEmpty || targets.IsDefaultOrEmpty || targets.Length is < 2 or > 3 || targets[0] != targetId || mode != 0 && (mode != 3 || targets.Length != 2) ||
            targets.Any(id => id is null || !byId.ContainsKey(id)) || FindAction(ability, actionId) is not { NativeAction.Command: 2 } action || !SupportsExpandedAction(action))
        {
            throw new InvalidDataException("Saved repeated magic lacks its original spell and logical recipient order.");
        }
        BattleCombatant actor = GetActor(actorId);
        if (targets.Any(id => NativeBattleLimitRules.IsFixedAllyTarget(action.NativeAction.TargetFlags!.Value) ? id != actorId :
            !CanSelectEitherTargetSide(action) && (GetActor(id).IsPlayer == actor.IsPlayer) != action.Allies))
        {
            throw new InvalidDataException("Saved repeated magic targets a side unavailable to its spell.");
        }
    }

    private void ValidateNativeMagicTargets(BattleAbility ability, int actionId, ImmutableArray<ushort> targets, int mode = 0)
    {
        if (mode != 0 && (mode != 3 || targets.Length != 2 || ability != BattleAbility.Magic))
        {
            throw new InvalidDataException("An abbreviated Triple command requires exactly two selected magic targets.");
        }
        if (targets.IsDefault || targets.Length > 3 || targets.Length != 0 && ability != BattleAbility.Magic || targets.Any(mask => (mask & 0x1FFF) == 0 || (mask & 0x1F80) != 0))
        {
            throw new InvalidDataException("Native magic groups require one to three original physical target masks.");
        }
        if (targets.Length != 0 && FindAction(BattleAbility.Magic, actionId).NativeAction is not { Command: 2 })
        {
            throw new InvalidDataException("Native repeated magic requires its original installed command2 effect.");
        }
    }
}
