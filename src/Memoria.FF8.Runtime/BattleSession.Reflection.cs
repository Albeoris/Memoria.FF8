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
            if (ReadNativeRandomActionTarget(side) is not { } redirected)
            {
                canPublish = false;
                continue;
            }
            bool random = (flags & 2) != 0;
            bool allowsKnockedOut = (flags & 1) != 0;
            BattleTargetRequest request = redirected.NativeActorIndex is int alias
                ? NativeBattleTargetRequestAdapter.CreateOriginalRequest(new(NativeBattleTargetRequestAdapter.SelectOriginalSlot(NativeBattleSlots.Decode(alias)), Random: random), BattleTargetEligibility.SavedStatus, allowsKnockedOut)
                : random ? new(new BattleTargetSelection.RandomOnSide(side, samplesOriginalSlots: true), BattleTargetEligibility.SavedStatus, allowsKnockedOut, BattleTargetReplacement.None) : SelectBattleTarget(redirected, allowsKnockedOut);
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

}
