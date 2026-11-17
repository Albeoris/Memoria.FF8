using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void RecomputeNativeTriggerContact(NativeBattleTriggerReceipt receipt)
    {
        receipt.Validate(nativeActionPackets.Roster);
        BattleActionHeaderReference firstReference = new(0);
        NativeBattleActionHeaderSlot first = nativeActionPackets.ReadHeader(firstReference);
        BattleModelOwner source = first.Source;
        if (receipt.Actor != source)
        {
            throw new InvalidDataException("Native485160 receipt contradicts header0's physical actor.");
        }
        BattleCombatant actor = GetActor(RequireCanonicalGraphicsActorId(source));
        BattleCombatant recipient = GetActor(RequireCanonicalGraphicsActorId(nativeActionPackets.ReadContact(first.FirstContact).Primary.Recipient));
        (BattleNativeAction native, short timing) = PrepareNativeTriggerHit(actor, recipient, first.Command, receipt);
        BattleActionPresentation original = nativeTimedAction?.Command ?? ActiveAction ?? throw new NotSupportedException("Native485160 requires its currently admitted action provenance.");
        byte? queued = original.NativeQueuedCommand;
        if (original.NativeRenzokuken is not null)
        {
            ushort control = first.Action;
            queued = control == 65534 ? (byte)250 : (byte)5;
        }
        BattleActionPresentation contact = original with { NativeQueuedCommand = queued, NativeAction = native, TargetRequest = SelectBattleTarget(recipient), GunbladeTriggerTiming = timing };
        BattleActionDefinition definition = new(contact.Ability, contact.ActionId, native.Name ?? "Gunblade", BattleActionEffect.Native, native.Power, false) { NativeAction = native };
        BattleActionPresentation? previous = resolvingNativeAction;
        nativeActionCommitDepth++;
        resolvingNativeAction = contact;
        try
        {
            nativeActionPackets = nativeActionPackets.RestartContactPublication();
            NativeBattleContactPacket packet = ResolveInstalledNativeHitWithContact(actor, recipient, contact, definition, native);
            (nativeActionPackets, BattleContactOccurrence contactIndex) = nativeActionPackets.AppendContact(packet);
            ObserveDiagnosticCounterAppend(packet, contactIndex, triggerRecomputed: true);
        }
        finally
        {
            resolvingNativeAction = previous;
            nativeActionCommitDepth--;
        }
    }

    private (BattleNativeAction Action, short Timing) PrepareNativeTriggerHit(BattleCombatant actor, BattleCombatant recipient, byte headerCommand, NativeBattleTriggerReceipt receipt, bool hosted = false)
    {
        bool ordinary = receipt.TotalContacts == 1;
        byte command = ordinary ? headerCommand switch { 12 => (byte)248, 28 => (byte)243, _ => (byte)253 } : (byte)251;
        bool last = receipt.ContactIndex == receipt.TotalContacts;
        //48F480 reads E03 only when the supplied contact index equals the total.
        bool finisher = last && (nativeActionScratch.FinisherPendingE03 ?? throw new NotSupportedException("Native48F480 requires the retained E03 finisher byte."));
        bool deferred = !last || finisher;
        byte sequence = deferred ? (byte)255 : command == 251 ? (NativeSavedStatusBits(recipient) & 1) != 0 ? (byte)4 : (byte)255 : (byte)4;
        nativeActionScratch = nativeActionScratch with { ActorIndex = hosted ? nativeActionScratch.ActorIndex : RequireTriggerSourceOperand(receipt.Actor), Command = command, DeferredEffectsE00 = deferred, FinisherPendingE03 = last && finisher ? false : nativeActionScratch.FinisherPendingE03 };
        BattleNativeAction weapon = actor.NativeAttack ?? throw new NotSupportedException("Native485160 requires the retained actor's weapon/junction metadata.");
        byte power = ordinary ? weapon.Power : actor.NativePlayerStatRules?.CharacterRecord[33] ?? throw new NotSupportedException("Native251 requires its character kernel power byte33.");
        BattleNativeAction native = weapon with { Command = command, AttackType = 10, Power = power, HitCount = 1, Critical = 0, DamagePresentation = sequence };
        if (!ordinary)
        {
            native = native with { ActionId = nativeActionScratch.Action ?? throw new NotSupportedException("Native251 requires the retained AF4 action WORD."), AttackFlags = 0, Element = 0, ElementPercent = 0, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255 };
        }
        short timing = ordinary && (NativeSavedStatusBits(actor) & 8) != 0 ? (short)1 : receipt.Timing;
        return (native, timing);
    }

    private byte RequireTriggerSourceOperand(BattleModelOwner owner)
    {
        return nativeActionPackets.ModelAliases.TryEncodeExact(owner, out byte original)
            ? original
            : throw new InvalidDataException("Native trigger scratch requires its exact original source operand.");
    }
}
