using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed record NativeProducedContacts(ImmutableArray<NativeBattleContactPacket> Contacts, int PublishedCount, NativeBattleDeferredContactKind DeferredContact, BattleModelOwner? Reflector, bool RepeatAborted);
    private sealed record NativeContactPlanResult(ImmutableArray<NativeBattleContactPacket> Contacts, int PublishedCount, BattleCombatant? Reflector, bool RepeatAborted);
    private NativeBattleActionProductionContext? nativeProductionContext;

    private static BattleNativeAction NativeFailedInvocationControl(BattleNativeAction native)
    {
        return native with { Command = 0, ActionId = 9, KernelSection = -1, KernelIndex = -1, MagicId = 0, AttackType = 0, Power = 0, HitCount = 1, AttackFlags = 3, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null };
    }

    private NativeBattleActionProductionResult ProduceNativeActionFacts(BattleCombatant actor, BattleCombatant target, BattleActionPresentation command, BattleActionDefinition action, NativeBattleActionHeaderPreparation header, NativeBattleActionProductionContext context, bool miniMogFailed)
    {
        if (header.Source != CreatePresentationModelOwner(actor) || header.Command != (miniMogFailed ? 0 : NativeHeaderCommand(actor, command, action.NativeAction!)) || context.CompletionCallback is not (0x47DDA0 or 0x47DE70 or 0x47E120))
        {
            throw new InvalidDataException("Native contact production contradicts its48D200 header or original completion callback.");
        }
        bool silencedMagic = NativeMagicInvocationSilenced(actor, command, action.NativeAction!);
        bool failedSpecial = miniMogFailed || NativeSpecialInvocationFailed(command, action.NativeAction!);
        BattleActionPresentation formulaCommand = command;
        BattleActionDefinition formulaAction = action;
        if (failedSpecial || silencedMagic)
        {
            BattleNativeAction control = NativeFailedInvocationControl(action.NativeAction!);
            formulaCommand = command with { NativeQueuedCommand = 0, NativeAction = control };
            formulaAction = action with { NativeAction = control };
        }
        else if (header.Command == 38)
        {
            BattleNativeAction dance = action.NativeAction! with { ActionId = header.Action };
            formulaCommand = command with { NativeAction = dance };
            formulaAction = action with { NativeAction = dance };
        }
        if (silencedMagic && definition.NativeGlobalMessages.Length != 0)
        {
            QueueNativeSpecialText(actor, target, NativeBattleGlobalText(100));
        }
        NativeProducedContacts produced = ProduceNativeInstalledContacts(actor, target, formulaCommand, formulaAction, context);
        int count = command.IndependentGuardian?.FailureMessage is null && command.NativeDraw is not { Failed: true } && !failedSpecial && !silencedMagic ? produced.PublishedCount : 0;
        return new(header, produced.Contacts, count, produced.DeferredContact, produced.Reflector) { RepeatAborted = produced.RepeatAborted };
    }

    private NativeProducedContacts ProduceNativeInstalledContacts(BattleCombatant actor, BattleCombatant target, BattleActionPresentation command, BattleActionDefinition action, NativeBattleActionProductionContext? context = null)
    {
        BattleNativeAction native = action.NativeAction!;
        ValidateNativeActionContract(native);
        if (HasNativeAffinityAction(actor, command, native))
        {
            nativeAffinityAction = native;
        }
        //48E830 resolves every random target/Cover mask before the first48FE20 formula consumes RNG.
        (BattleCombatant[] Targets, BattleModelOwner? CoverSource)[] plan = new (BattleCombatant[], BattleModelOwner?)[native.HitCount];
        for (int hit = 0; hit < plan.Length; hit++)
        {
            BattleCombatant[] targets = ResolveBattleTargets(command.TargetRequest);
            plan[hit] = ResolveNativeCover(actor, command, native, targets);
        }
        NativeBattleDeferredContactKind deferred = NativeDeferredContact(actor, command, native);
        NativeContactPlanResult result = ExecuteNativeContactPlan(actor, command, action, native, plan, deferred != NativeBattleDeferredContactKind.None, context);
        return new(result.Contacts, result.PublishedCount, deferred, result.Reflector is { } reflector ? CreatePresentationModelOwner(reflector) : null, result.RepeatAborted);
    }

    private NativeContactPlanResult ExecuteNativeContactPlan(BattleCombatant actor, BattleActionPresentation command, BattleActionDefinition action, BattleNativeAction native, (BattleCombatant[] Targets, BattleModelOwner? CoverSource)[] plan, bool preliminary, NativeBattleActionProductionContext? context = null)
    {
        foreach (BattleCombatant recipient in combatants.Where(candidate => candidate.Slot is not null))
        {
            recipient.NativeRemainingHits = plan.Count(hit => hit.Targets.Contains(recipient));
        }
        ValidateEncounterContactCapacity(plan, native, preliminary);
        ImmutableArray<NativeBattleContactPacket>.Builder contacts = ImmutableArray.CreateBuilder<NativeBattleContactPacket>();
        BattleCombatant? firstReflector = null;
        int publishedCount = 0;
        bool allowKo = command.TargetRequest.AllowsKnockedOutAllies;
        bool aborted = false;
        for (int hit = 0; hit < plan.Length && !aborted; hit++)
        {
            (BattleCombatant[] targets, BattleModelOwner? coverSource) = plan[hit];
            //The original adds the mask's population before checking first-hit admission or a later KO abort.
            publishedCount = checked(publishedCount + targets.Length);
            if (targets.Length == 0 || hit == 0 && !NativeFirstHitAdmitted(actor, command, context))
            {
                aborted = true;
                break;
            }
            BeginEncounterDamageHit();
            try
            {
                foreach (BattleCombatant recipient in targets)
                {
                    bool deathCallbackSelf = recipient == actor && command.NativeAiCallbackStage == 3 && command.TargetRequest.DeathCallbackSelf == TargetActorReference(recipient);
                    if (UsesExpandedRoster && !recipient.IsActive || (NativeSavedStatusBits(recipient) & 1) != 0 && !deathCallbackSelf && (!allowKo || !recipient.IsPlayer))
                    {
                        aborted = true;
                        break;
                    }
                    if (hit == 0 && (command.NativeQueuedCommand ?? native.Command) == 12)
                    {
                        ResolveNativeMug(actor, recipient);
                    }
                    if (NativeActionReflects(recipient, native))
                    {
                        DecrementNativeRemainingHits(recipient);
                        firstReflector ??= recipient;
                        recipient.NativeCombatantFlags |= 0x4000;
                        recipient.LastNativeHealing = 1;
                        RecordNativeActionMemory(actor, recipient, command);
                        DispatchNativeActionCounter(recipient);
                        contacts.Add(CreateNativeReflectedContact(recipient, native));
                        Log("Reflect", actor.Id, recipient.Id, 0, $"{recipient.Name} reflects {action.Name}.");
                        continue;
                    }
                    BattleActionPresentation resolved = command.NativeAction is null ? command with { NativeAction = native } : command;
                    BattleActionPresentation? previousAction = resolvingNativeAction;
                    bool previousSuppression = suppressNativeImmediateCounter;
                    resolvingNativeAction = resolved;
                    suppressNativeImmediateCounter |= preliminary && native.Command != 238 || native.Command is 237 or 241;
                    try
                    {
                        SetEncounterContactPosition(contacts.Count);
                        contacts.Add(ResolveInstalledNativeHitWithContact(actor, recipient, resolved, action, native, coverSource, preliminary));
                    }
                    finally
                    {
                        resolvingNativeAction = previousAction;
                        suppressNativeImmediateCounter = previousSuppression;
                    }
                }
                publishedCount = checked(publishedCount + CompleteEncounterDamageHit(contacts));
            }
            finally
            {
                encounterHitTransaction = null;
            }
        }
        return new(contacts.ToImmutable(), publishedCount, firstReflector, aborted);
    }

    private static bool HasNativeAffinityAction(BattleCombatant actor, BattleActionPresentation command, BattleNativeAction native)
    {
        return actor.IsPlayer && native.Command is 2 or 6 or 254 && (command.NativeDraw is null || command.DrawCast && !command.NativeDraw.Failed);
    }

    private NativeBattleDeferredContactKind NativeDeferredContact(BattleCombatant actor, BattleActionPresentation command, BattleNativeAction native)
    {
        byte original = command.NativeQueuedCommand ?? native.Command;
        return original switch
        {
            1 or 12 or 28 when actor.ModelId is 0 or 6 => NativeBattleDeferredContactKind.OrdinaryGunblade,
            5 => NativeBattleDeferredContactKind.RenzokukenInitial,
            250 => NativeBattleDeferredContactKind.RenzokukenFinisher,
            11 => NativeBattleDeferredContactKind.DuelControl,
            14 or 238 => NativeBattleDeferredContactKind.ShotControl,
            254 when nativeActionScratch.GuardianFormulaE1E != true => NativeBattleDeferredContactKind.GuardianForceInitial,
            _ => NativeBattleDeferredContactKind.None
        };
    }

    private bool NativeFirstHitAdmitted(BattleCombatant actor, BattleActionPresentation command, NativeBattleActionProductionContext? context)
    {
        if (context is null || context.MultiCastMode == 0 || context.RepeatMode == 0 || context.DrawFollowUp)
        {
            return true;
        }
        if ((command.NativeQueuedCommand ?? command.NativeAction?.Command) == 16)
        {
            return (NativeSavedStatusBits(actor) & 1) == 0;
        }
        uint statuses = EncounterMulticastStatusBits(actor);
        if (context.RepeatMode == 1 && (statuses & 0x60000) == 0 || context.RepeatMode == 2 && (statuses & 0x40000) == 0)
        {
            return false;
        }
        return (NativeSavedStatusBits(actor) & 0x35) == 0 && (statuses & 0x4009) == 0;
    }

    private static void DecrementNativeRemainingHits(BattleCombatant recipient)
    {
        if (recipient.NativeRemainingHits is > 0)
        {
            recipient.NativeRemainingHits--;
        }
    }

    private bool NativeImmediateCounterAllowed(BattleCombatant recipient)
    {
        if (suppressNativeImmediateCounter)
        {
            return false;
        }
        if (nativeContactWorking is not null && (nativeActionScratch.DeferredEffectsE00 != false || nativeActionScratch.EventLane == 0 || nativeContactWorking.CommittingSecondary || nativeActionScratch.Command is 0 or 14))
        {
            return false;
        }
        if (nativeContactWorking is not null && recipient.NativeRemainingHits is null)
        {
            throw new NotSupportedException("Native surviving immediate callback4 requires its retained BD9 remaining-hit byte.");
        }
        return recipient.NativeRemainingHits is null or 0;
    }
}
