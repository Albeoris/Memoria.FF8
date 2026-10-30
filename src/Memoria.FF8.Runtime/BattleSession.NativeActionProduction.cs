using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed record NativeProducedContacts(ImmutableArray<NativeBattleContactPacket> Contacts, byte PublishedCount, NativeBattleDeferredContactKind DeferredContact, byte? ReflectorIndex);
    private NativeBattleActionProductionContext? nativeProductionContext;

    private NativeBattleActionProductionResult ProduceNativeActionFacts(BattleCombatant actor, BattleCombatant target, BattleActionPresentation command, BattleActionDefinition action, NativeBattleActionHeaderPreparation header, NativeBattleActionProductionContext context)
    {
        if (header.Actor != actor.NativeIndex || header.Command != NativeHeaderCommand(command, action.NativeAction!) || context.CompletionCallback is not (0x47DDA0 or 0x47DE70 or 0x47E120))
        {
            throw new InvalidDataException("Native contact production contradicts its48D200 header or original completion callback.");
        }
        bool failedSpecial = NativeSpecialInvocationFailed(command, action.NativeAction!);
        BattleActionPresentation formulaCommand = command;
        BattleActionDefinition formulaAction = action;
        if (failedSpecial)
        {
            BattleNativeAction control = action.NativeAction! with { Command = 0, ActionId = 9, KernelSection = -1, KernelIndex = -1, MagicId = 0, AttackType = 0, Power = 0, HitCount = 1, AttackFlags = 3, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null };
            formulaCommand = command with { NativeQueuedCommand = 0, NativeAction = control };
            formulaAction = action with { NativeAction = control };
        }
        NativeProducedContacts produced = ProduceNativeInstalledContacts(actor, target, formulaCommand, formulaAction, context);
        byte count = command.IndependentGuardian?.FailureMessage is null && command.NativeDraw is not { Failed: true } && !failedSpecial ? produced.PublishedCount : (byte)0;
        return new(header, produced.Contacts, count, produced.DeferredContact, produced.ReflectorIndex);
    }

    private NativeProducedContacts ProduceNativeInstalledContacts(BattleCombatant actor, BattleCombatant target, BattleActionPresentation command, BattleActionDefinition action, NativeBattleActionProductionContext? context = null)
    {
        BattleNativeAction native = action.NativeAction!;
        ValidateNativeActionContract(native);
        if (actor.IsPlayer && native.Command is 2 or 6 or 254 && (command.NativeDraw is null || command.DrawCast && !command.NativeDraw.Failed))
        {
            nativeAffinityAction = native;
        }
        //48E830 resolves every random target/Cover mask before the first48FE20 formula consumes RNG.
        (BattleCombatant[] Targets, byte CoverSource)[] plan = new (BattleCombatant[], byte)[native.HitCount];
        for (int hit = 0; hit < plan.Length; hit++)
        {
            BattleCombatant[] targets;
            if (command.NativeAction is not null)
            {
                targets = ResolveNativeActionMask(command.NativeTargetMask);
            }
            else if ((native.TargetFlags.GetValueOrDefault() & 2) != 0)
            {
                BattleCombatant? randomTarget = ChooseNativeRandomActionTarget(target.IsPlayer);
                targets = randomTarget is null ? [] : [randomTarget];
            }
            else if (action.AllTargets)
            {
                targets = combatants.Where(candidate => candidate.IsActive && !candidate.Untargetable && candidate.IsPlayer == target.IsPlayer && (IsNativeRevive(action) ? candidate.CurrentHp == 0 : candidate.IsAlive)).OrderBy(candidate => candidate.NativeIndex).ToArray();
            }
            else
            {
                targets = target.IsAlive || IsNativeRevive(action) ? [target] : [];
            }
            plan[hit] = ResolveNativeCover(actor, command, native, targets);
        }
        foreach (BattleCombatant recipient in combatants.Where(candidate => candidate.NativeIndex is >= 0 and < 7))
        {
            recipient.NativeRemainingHits = unchecked((byte)plan.Count(hit => hit.Targets.Contains(recipient)));
        }
        NativeBattleDeferredContactKind deferred = NativeDeferredContact(actor, command, native);
        ImmutableArray<NativeBattleContactPacket>.Builder contacts = ImmutableArray.CreateBuilder<NativeBattleContactPacket>();
        BattleCombatant? firstReflector = null;
        byte publishedCount = 0;
        bool allowKo = command.NativeAction is not null ? (command.NativeTargetMask & 0x4000) != 0 : IsNativeRevive(action);
        bool aborted = false;
        for (int hit = 0; hit < plan.Length && !aborted; hit++)
        {
            (BattleCombatant[] targets, byte coverSource) = plan[hit];
            //The original adds the mask's population before checking first-hit admission or a later KO abort.
            publishedCount = unchecked((byte)(publishedCount + targets.Length));
            if (targets.Length == 0 || hit == 0 && !NativeFirstHitAdmitted(actor, command, context))
            {
                break;
            }
            foreach (BattleCombatant recipient in targets)
            {
                if ((NativeSavedStatusBits(recipient) & 1) != 0 && (!allowKo || recipient.NativeIndex >= 3))
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
                suppressNativeImmediateCounter |= deferred != NativeBattleDeferredContactKind.None && native.Command != 238 || native.Command is 237 or 241;
                try
                {
                    contacts.Add(ResolveInstalledNativeHitWithContact(actor, recipient, resolved, action, native, coverSource, deferred != NativeBattleDeferredContactKind.None));
                }
                finally
                {
                    resolvingNativeAction = previousAction;
                    suppressNativeImmediateCounter = previousSuppression;
                }
            }
        }
        return new(contacts.ToImmutable(), publishedCount, deferred, firstReflector is null ? null : checked((byte)firstReflector.NativeIndex));
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

    private static bool NativeFirstHitAdmitted(BattleCombatant actor, BattleActionPresentation command, NativeBattleActionProductionContext? context)
    {
        if (context is null || context.MultiCastMode == 0 || context.RepeatMode == 0 || context.DrawFollowUp)
        {
            return true;
        }
        if ((command.NativeQueuedCommand ?? command.NativeAction?.Command) == 16)
        {
            return (NativeSavedStatusBits(actor) & 1) == 0;
        }
        uint statuses = NativeBattleStatusBits(actor);
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
