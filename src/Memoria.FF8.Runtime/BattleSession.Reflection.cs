using static Memoria.FF8.Runtime.BattlePresentationProcess;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed record NativeMagicReflectionCast(BattleActionPresentation Command, BattleModelOwner Reflector);

public sealed partial class BattleSession
{
    private sealed record NativeMagicReflectionReceipt(BattleActionPresentation Source, BattleCombatant Reflector);

    private ImmutableArray<NativeMagicReflectionCast> PrepareNativeRepeatReflections(BattleNativeAction source, List<NativeMagicReflectionReceipt> receipts)
    {
        BattleNativeAction reflected = FindInstalledNativeAction(247, unchecked((byte)source.ActionId));
        byte flags = reflected.TargetFlags.GetValueOrDefault();
        var result = ImmutableArray.CreateBuilder<NativeMagicReflectionCast>();
        bool canPublish = true;
        //485610 samples the targets after the direct casts have produced their complete receipt list.
        foreach (NativeMagicReflectionReceipt receipt in receipts)
        {
            BattleSide side = receipt.Reflector.IsPlayer ? BattleSide.Enemies : BattleSide.Allies;
            BattleCombatant redirected;
            BattleTargetRequest request;
            if (originalParticipantSlotsInitialized && Roster(side).All(actor => !actor.IsActive || HasNativeRosterSlot(actor)))
            {
                if (ChooseOriginalRandomActionTarget(side) is not NativeBattleTargetRequestAdapter.DeferredOperands selected ||
                    NativeBattleTargetRequestAdapter.ReadAuthoredTargets(selected, ReadOriginalTargetOccupant).SingleOrDefault() is not { } reference)
                {
                    canPublish = false;
                    continue;
                }
                redirected = FindTargetActor(reference)!;
                request = NativeBattleTargetRequestAdapter.CreateOriginalRequest(new(selected, Random: (flags & 2) != 0), BattleTargetEligibility.SavedStatus, (flags & 1) != 0);
            }
            else
            {
                BattleCombatant[] available = ExpandedRandomTargets(side);
                if (available.Length == 0)
                {
                    canPublish = false;
                    continue;
                }
                redirected = available[NextRandomByte() % available.Length];
                request = (flags & 2) != 0
                    ? new(new BattleTargetSelection.RandomOnSide(side), BattleTargetEligibility.Living, (flags & 1) != 0, BattleTargetReplacement.None)
                    : SelectBattleTarget(redirected, (flags & 1) != 0);
            }
            if (!canPublish)
            {
                //485610 fills the remaining WORDs even though the secondary cast loop stops at the first zero.
                continue;
            }
            BattleActionPresentation command = receipt.Source with
            {
                ActionId = reflected.ActionId,
                TargetId = redirected.Id,
                TargetRequest = request,
                NativeSequence = 0,
                NativeQueuedCommand = 247,
                NativeAction = reflected,
                FreeNativeMagic = true,
                MagicReservationId = null
            };
            result.Add(new(command, CreatePresentationModelOwner(receipt.Reflector)));
        }
        return result.ToImmutable();
    }

    private void WriteNativeRepeatReflectionPacket(BattleCombatant actor, BattleNativeAction source, List<NativeMagicReflectionReceipt> receipts)
    {
        ImmutableArray<NativeMagicReflectionCast> casts = PrepareNativeRepeatReflections(source, receipts);
        if (casts.IsEmpty)
        {
            return;
        }
        BattleActionPresentation first = casts[0].Command;
        BattleAttackCommand secondary = new(actor.Id, first.TargetId, first.TargetRequest, BattleAbility.Magic, first.ActionId)
        {
            NativeQueuedCommand = 247,
            NativeAction = first.NativeAction,
            FreeNativeMagic = true,
            NativeEventOwner = first.NativeEventOwner,
            SecondaryReflectionCasts = casts
        };
        if (!TryWriteNativeSecondCommand(secondary))
        {
            throw new InvalidDataException("Native repeated reflection lost its selected secondary command bank.");
        }
    }

    private BattleAttackCommand ProduceNativeRepeatReflectionPacket(BattleAttackCommand secondary)
    {
        BattleCombatant actor = GetActor(secondary.ActorId);
        BattleNativeAction reflected = secondary.NativeAction!;
        BattleActionHeaderReference first = nativeActionPackets.NextHeader;
        BattleActionHeaderReference? previousEncounterCommand = encounterProducingCommandFirstHeader;
        encounterProducingCommandFirstHeader ??= first;
        nativeAffinityAction = null;
        try
        {
            int count = 0;
            foreach (NativeMagicReflectionCast cast in secondary.SecondaryReflectionCasts)
            {
                BattleCombatant target = GetActor(cast.Command.TargetId);
                var action = new BattleActionDefinition(BattleAbility.Magic, reflected.ActionId, reflected.Name ?? "Reflected magic", BattleActionEffect.Native, reflected.Power, actor.IsPlayer == target.IsPlayer) { NativeAction = reflected };
                ResolveInstalledNativeAction(actor, target, cast.Command, action, queueGraphics: count == 0, reflectionSource: cast.Reflector);
                count++;
            }
            nativeActionPackets = nativeActionPackets.WritePlayerRepeatMode(first, checked((byte)count));
            ObserveDiagnosticCounterPublishedHeaders(first);
            return secondary with { NativeHeader = first, NativeSequence = nativeActionPackets.ReadHeader(first).Sequence, SecondaryReflectionCasts = [] };
        }
        finally
        {
            encounterProducingCommandFirstHeader = previousEncounterCommand;
        }
    }

    private void StageExpandedRepeatReflection(BattleCombatant actor, BattleActionDefinition source, List<NativeMagicReflectionReceipt> receipts)
    {
        int firstOperation = expandedOperationCursor.Operations.Length;
        foreach (NativeMagicReflectionCast cast in PrepareNativeRepeatReflections(source.NativeAction!, receipts))
        {
            BattleActionPresentation command = cast.Command;
            BattleNativeAction reflected = command.NativeAction!;
            BattleCombatant target = GetActor(command.TargetId);
            var action = new BattleActionDefinition(BattleAbility.Magic, reflected.ActionId, reflected.Name ?? source.Name, BattleActionEffect.Native, reflected.Power, actor.IsPlayer == target.IsPlayer) { NativeAction = reflected };
            var plan = PlanNativeHits(actor, command, reflected);
            NativeBattleActionHeaderPreparation header = PrepareReflectedHeader(BuildExpandedActionHeaderPreparation(command, reflected), cast.Reflector);
            ReserveExpandedAiInvocation(command, header);
            resolvingNativeAction = command;
            nativeActionScratch = nativeActionScratch with { Command = 247, Action = reflected.ActionId };
            deferringExpandedImpacts = true;
            NativeContactPlanResult produced = ExecuteHostedContactPlan(actor, command, action, reflected, plan);
            deferringExpandedImpacts = false;
            StageExpandedHostedInvocation(command, header, ToHostedContacts(produced), null);
        }
        if (!TryGroupNativeMagicCasts(firstOperation, source.NativeAction!, command: 247))
        {
            throw new InvalidDataException("Hosted repeat reflection requires the prepared secondary spell headers.");
        }
    }

    private static NativeBattleActionHeaderPreparation PrepareReflectedHeader(NativeBattleActionHeaderPreparation header, BattleModelOwner reflector)
    {
        //The secondary485610 calls 48D200 with sequence0 and substitutes the retained reflector at header BYTE0.
        return header with { Source = reflector, ReflectedCaster = header.Source, Sequence = 0 };
    }

    private static bool NativeActionReflects(BattleCombatant recipient, BattleNativeAction native)
    {
        return NativeBattleReflectionRules.CanReflect(native.Command, native.AttackFlags, native.AttackType) && (recipient.NativeStatus1 & 0x80) != 0;
    }

    private void ResolveNativeReflection(BattleCombatant actor, BattleCombatant reflector, BattleActionPresentation command, BattleNativeAction source, BattleActionHeaderReference sourceHeader)
    {
        //487DF0 consumes the first491AD0/493280/491940/493110 reflection receipt, retaining its original caster.
        BattleNativeAction reflected = FindInstalledNativeAction(247, unchecked((byte)source.ActionId));
        BattleTargetSelection selection = ChooseOriginalRandomActionTarget(reflector.IsPlayer ? BattleSide.Enemies : BattleSide.Allies);
        if (selection is not NativeBattleTargetRequestAdapter.DeferredOperands initial)
        {
            return;
        }
        if (NativeBattleTargetRequestAdapter.ReadAuthoredTargets(initial, ReadOriginalTargetOccupant).SingleOrDefault() is not { } reference)
        {
            return;
        }
        BattleCombatant target = FindTargetActor(reference)!;
        byte flags = reflected.TargetFlags.GetValueOrDefault();
        BattleTargetRequest request = NativeBattleTargetRequestAdapter.CreateOriginalRequest(new(initial, Random: (flags & 2) != 0), BattleTargetEligibility.SavedStatus, (flags & 1) != 0);
        PendingDiagnosticReflection? diagnostic = PrepareDiagnosticReflection(sourceHeader, actor, reflector, target, source, reflected, request);
        BattleAttackCommand packet = new(actor.Id, target.Id, request, BattleAbility.Magic, reflected.ActionId) { NativeQueuedCommand = 247, NativeAction = reflected, FreeNativeMagic = true, NativeEventOwner = command.NativeEventOwner };
        if (nativeAiDepth == 0 && nativeCallbackTail is null)
        {
            if (diagnostic is not null)
            {
                diagnosticReflectedCommands ??= new();
                diagnosticReflectedCommands.Add(packet, diagnostic);
            }
            if (!TryWriteNativeSecondCommand(packet))
            {
                //TODO(TD-009): Components without a graphical command bank admit the same second485610 producer within their CPU dispatch.
                BattleNativeAction? affinity = nativeAffinityAction;
                ProduceNativeReflectedBankCommand(packet);
                nativeAffinityAction = affinity;
            }
            return;
        }
        BattleActionPresentation reflectedCommand = command with { Ability = BattleAbility.Magic, ActionId = reflected.ActionId, TargetId = target.Id, DrawCast = false, NativeDraw = null, DrawCastTargetId = null, NativeQueuedCommand = 247, NativeAction = reflected, TargetRequest = request };
        BattleActionDefinition reflectedDefinition = new(BattleAbility.Magic, reflected.ActionId, reflected.Name ?? "Reflected magic", BattleActionEffect.Native, reflected.Power, actor.IsPlayer == target.IsPlayer) { NativeAction = reflected };
        PendingDiagnosticReflection? previousDiagnostic = diagnosticReflectionProduction;
        NativeBattleActionProductionContext? previousProduction = nativeProductionContext;
        if (nativeAiDepth == 0 && previousProduction is not null)
        {
            nativeProductionContext = previousProduction with { RepeatMode = 0, DrawFollowUp = true };
        }
        diagnosticReflectionProduction = diagnostic;
        try
        {
            ResolveInstalledNativeAction(actor, target, reflectedCommand, reflectedDefinition);
        }
        finally
        {
            diagnosticReflectionProduction = previousDiagnostic;
            nativeProductionContext = previousProduction;
        }
    }

    private void ProduceNativeReflectedBankCommand(BattleAttackCommand command)
    {
        if (command.NativeQueuedCommand != 247 || command.NativeAction?.Command != 247 || !command.FreeNativeMagic)
        {
            throw new InvalidDataException("Native reflected second packet lacks its247/E0C provenance.");
        }
        NativeBattleActionProductionContext? previous = nativeProductionContext;
        byte lane = nativeActionScratch.EventLane ?? throw new NotSupportedException("Native reflected485610 requires its selected physical command bank.");
        PendingDiagnosticReflection? diagnostic = null;
        if (diagnosticReflectedCommands is { } commands)
        {
            commands.TryGetValue(command, out diagnostic);
            commands.Remove(command);
        }
        PendingDiagnosticReflection? previousDiagnostic = diagnosticReflectionProduction;
        diagnosticReflectionProduction = diagnostic;
        nativeProductionContext = new(0, lane, true, 0x47DE70);
        try
        {
            BattleAttackCommand produced = command.SecondaryReflectionCasts.IsEmpty
                ? ProduceNativeQueuedCommand(GetActor(command.ActorId), GetActor(command.TargetId), command)
                : ProduceNativeRepeatReflectionPacket(command);
            actions.Enqueue(produced);
            FlushEncounterCounterCommands();
        }
        finally
        {
            nativeProductionContext = previous;
            diagnosticReflectionProduction = previousDiagnostic;
        }
    }
}
