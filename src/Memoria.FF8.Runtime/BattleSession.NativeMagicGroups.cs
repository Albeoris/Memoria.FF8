using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleMagicCastInvocation
{
    public BattleMagicCastInvocation(BattleTargetRequest targets, BattleTargetSelection preflightSelection, BattleActorReference fallbackAnchor)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(fallbackAnchor);
        if (preflightSelection is not (BattleTargetSelection.Explicit or NativeBattleTargetRequestAdapter.DeferredOperands))
        {
            throw new ArgumentException("Magic cast preflight requires an explicit actor or original operand selection.", nameof(preflightSelection));
        }
        Targets = targets;
        PreflightSelection = preflightSelection;
        FallbackAnchor = fallbackAnchor;
    }

    public BattleTargetRequest Targets { get; }
    public BattleTargetSelection PreflightSelection { get; }
    public BattleActorReference FallbackAnchor { get; }
}

public sealed partial class BattleSession
{

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

    private ImmutableArray<BattleMagicCastInvocation> PreparePlayerMagicTargets(BattleCombatant actor, BattleActionDefinition action, string firstTarget, IReadOnlyList<string>? targets)
    {
        if (targets is null)
        {
            return [];
        }
        if (action.Ability != BattleAbility.Magic || action.NativeAction is not { Command: 2, TargetFlags: byte flags } || targets.Count is < 1 or > 3 || targets[0] != firstTarget ||
            !(!UsesExpandedRoster && GetNativeMagicCastCounts(actor.Id, action.Id).Contains(targets.Count) || actor.IsPlayer && GetPlayerMagicCastCounts(actor.Id, action.Id).Any(count => targets.Count <= count)))
        {
            throw new InvalidOperationException("Magic target groups require the current native Double/Triple status, sufficient stock and one installed spell.");
        }
        ImmutableArray<BattleMagicCastInvocation>.Builder casts = ImmutableArray.CreateBuilder<BattleMagicCastInvocation>(targets.Count);
        BattleActorReference anchor = TargetActorReference(GetActor(firstTarget));
        foreach (string targetId in targets)
        {
            BattleCombatant target = GetActor(targetId);
            if (!IsValidSelectedActionTarget(actor, action, target))
            {
                throw new InvalidOperationException("Every selected magic target must satisfy the original spell's target contract.");
            }
            BattleTargetRequest request = CreatePlayerTargetRequest(actor, target, flags);
            BattleTargetSelection preflight = UsesExpandedRoster
                ? new BattleTargetSelection.Explicit([TargetActorReference(target)])
                : NativeBattleTargetRequestAdapter.CreatePlayerRequest(flags, target.RequireNativeActorIndex()).OriginalProjection!.Operands;
            casts.Add(new(request, preflight, UsesExpandedRoster ? TargetActorReference(target) : anchor));
        }
        return UsesExpandedRoster && targets.Count == 1 ? [] : casts.MoveToImmutable();
    }

    private BattleCombatant ReadMagicPreflightAnchor(BattleMagicCastInvocation cast, bool requireActive)
    {
        BattleCombatant? active = cast.PreflightSelection switch
        {
            BattleTargetSelection.Explicit explicitActors => explicitActors.Actors.Select(FindTargetActor).FirstOrDefault(actor => actor is not null && (!requireActive || actor.IsActive)),
            NativeBattleTargetRequestAdapter.DeferredOperands original => ReadOriginalSelectedActor(original, requireActive),
            _ => throw new InvalidDataException("Magic cast preflight has no recognized target selection.")
        };
        return active ?? FindTargetActor(cast.FallbackAnchor) ?? throw new InvalidDataException("Magic cast preflight lost its fallback actor.");
    }

    private BattleCombatant? ReadOriginalSelectedActor(NativeBattleTargetRequestAdapter.DeferredOperands selection, bool activeOnly)
    {
        BattleActorReference? candidate = NativeBattleTargetRequestAdapter.ReadFirstActiveMagicAnchor(selection, ReadOriginalTargetOccupant, reference => FindTargetActor(reference) is { } actor && (!activeOnly || actor.IsActive));
        return candidate is null ? null : FindTargetActor(candidate);
    }

    private void ResolveNativeMagicGroup(BattleCombatant actor, BattleActionPresentation command)
    {
        BattleActionDefinition action = FindAction(BattleAbility.Magic, command.ActionId);
        uint statuses = EncounterMulticastStatusBits(actor);
        int mode = command.NativeMagicCastMode == 0 ? command.MagicCasts.Length : command.NativeMagicCastMode;
        int count = mode >= 3 && (statuses & 0x40000) != 0 ? command.MagicCasts.Length : command.MagicCasts.Length >= 2 && (statuses & 0x20000) != 0 ? 2 : 1;
        if (!actor.IsPlayer && HasEncounterPolicy(actor))
        {
            count = Math.Min(command.MagicCasts.Length, (statuses & 0x40000) != 0 ? 3 : (statuses & 0x20000) != 0 ? 2 : 1);
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
            BattleMagicCastInvocation invocation = command.MagicCasts[index];
            BattleCombatant target = ReadMagicPreflightAnchor(invocation, requireActive: true);
            if (!target.IsAlive && (command.SmartAttack is not null || !combatants.Any(candidate => candidate.IsActive && candidate.IsAlive && !candidate.Untargetable && candidate.IsPlayer == target.IsPlayer)))
            {
                break;
            }
            BattleActionPresentation cast = command with { TargetId = target.Id, TargetRequest = invocation.Targets, MagicCasts = [], NativeMagicCastMode = 0, NativeAction = action.NativeAction };
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

    private void ResolveExpandedMagicGroup(BattleCombatant actor, BattleActionPresentation command, ImmutableArray<BattleMagicCastInvocation> casts)
    {
        bool hosted = OwnsExpandedHostedInvocation;
        BattleActionDefinition action = FindAction(BattleAbility.Magic, command.ActionId);
        BattleNativeAction native = action.NativeAction!;
        uint statuses = NativeBattleStatusBits(actor);
        int mode = command.NativeMagicCastMode == 0 ? casts.Length : command.NativeMagicCastMode;
        int count = mode == 3 && (statuses & 0x40000) != 0 ? casts.Length : (statuses & 0x20000) != 0 ? 2 : 1;
        bool singleCost = count > 1 && MagicCost(actor, command.NativeMagicCastMode != 0 && (statuses & 0x40000) != 0 ? mode : count) == 1;
        string? reservationId = command.MagicReservationId;
        int reserved = reservationId is null ? count : magicVitals!.MagicSystem.Reservations.FirstOrDefault(reservation => reservation.ActionId == reservationId)?.Count ?? 0;
        int completed = 0;
        List<(BattleActionPresentation Command, ImmutableArray<ExpandedBattleContact> Contacts)> componentImpacts = [];

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
                BattleMagicCastInvocation invocation = casts[index];
                BattleCombatant selected = ReadMagicPreflightAnchor(invocation, requireActive: false);
                if (command.SmartAttack is not null && !IsValidSelectedActionTarget(actor, action, selected))
                {
                    break;
                }
                BattleCombatant? target = SelectExpandedMagicGroupTarget(actor, action, selected);
                if (target is null)
                {
                    break;
                }
                BattleCombatant[] recipients = ResolveBattleTargets(invocation.Targets, sampleRandom: false);
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

                BattleActionPresentation cast = command with { TargetId = target.Id, TargetRequest = invocation.Targets, MagicCasts = [], NativeMagicCastMode = 0, NativeAction = native };
                resolvingNativeAction = cast;
                nativeActionScratch = nativeActionScratch with { DeferredEffectsE00 = false, EventLane = 2, Command = 2, Action = native.ActionId };
                NativeBattleActionHeaderPreparation? hostedHeader = hosted ? BuildExpandedActionHeaderPreparation(cast, native) : null;
                ReserveExpandedAiInvocation(cast, hostedHeader);
                deferringExpandedImpacts = hosted;
                (ImmutableArray<ExpandedBattleContact> contacts, BattleCombatant? reflector) = ResolveExpandedHitPlan(actor, cast, action, native, hitPlan);
                deferringExpandedImpacts = false;
                (BattleActionPresentation? reflection, ImmutableArray<ExpandedBattleContact> reflectedContacts, NativeBattleActionHeaderPreparation? reflectionHeader) = ResolveExpandedReflection(actor, cast, action, reflector, reflected);
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
                    componentImpacts.Add((cast, contacts));
                    if (reflection is not null)
                    {
                        componentImpacts.Add((reflection, reflectedContacts));
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
            if (!hosted && completed > 0)
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
        if (!hosted)
        {
            FlushExpandedEnemyReactions();
        }
        if (completed == 0)
        {
            if (hosted)
            {
                nativeReactionOrder = null;
            }
            CompleteActionPresentation(command, "Expanded action cancelled.");
            return;
        }

        if (hosted)
        {
            ActiveAction = command with { Phase = BattleActionPhase.Recovery, RemainingFrames = 1, MagicReservationId = null };
            return;
        }
        foreach ((BattleActionPresentation cast, ImmutableArray<ExpandedBattleContact> contacts) in componentImpacts)
        {
            PublishExpandedImpacts(cast, contacts);
        }
        CompleteActionPresentation(command, "Repeated magic completed.");
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

    private void ValidateExpandedMagicTargets(string actorId, string targetId, BattleAbility ability, int actionId, ImmutableArray<BattleMagicCastInvocation> casts, int mode)
    {
        ValidateNativeMagicTargets(ability, actionId, casts, mode);
        if (!UsesExpandedRoster || casts.IsEmpty)
        {
            return;
        }
        BattleCombatant actor = GetActor(actorId);
        BattleActionDefinition action = FindAction(ability, actionId);
        if (!actor.IsPlayer || casts.Length < 2 || casts[0].FallbackAnchor.ActorId != targetId || !SupportsExpandedAction(action))
        {
            throw new InvalidDataException("Saved repeated magic lacks its original spell and logical recipient order.");
        }
        foreach (BattleMagicCastInvocation cast in casts)
        {
            if (cast.PreflightSelection is not BattleTargetSelection.Explicit { Actors: [var selected] } || cast.FallbackAnchor != selected || FindTargetActor(selected) is not { } target ||
                (NativeBattleLimitRules.IsFixedAllyTarget(action.NativeAction!.TargetFlags!.Value) ? target.Id != actorId : !CanSelectEitherTargetSide(action) && (target.IsPlayer == actor.IsPlayer) != action.Allies))
            {
                throw new InvalidDataException("Saved repeated magic targets an actor or side unavailable to its spell.");
            }
        }
    }

    private void ValidateNativeMagicTargets(BattleAbility ability, int actionId, ImmutableArray<BattleMagicCastInvocation> casts, int mode = 0)
    {
        if (mode != 0 && (mode != 3 || casts.Length != 2 || ability != BattleAbility.Magic))
        {
            throw new InvalidDataException("An abbreviated Triple command requires exactly two selected magic targets.");
        }
        if (casts.IsDefault || casts.Length > 3 || casts.Length != 0 && ability != BattleAbility.Magic ||
            casts.Any(cast => cast is null || cast.Targets.Selection is BattleTargetSelection.None || FindTargetActor(cast.FallbackAnchor) is null))
        {
            throw new InvalidDataException("Native magic groups require one to three bound target requests.");
        }
        if (casts.Length != 0 && FindAction(BattleAbility.Magic, actionId).NativeAction is not { Command: 2 })
        {
            throw new InvalidDataException("Native repeated magic requires its original installed command2 effect.");
        }
    }
}
