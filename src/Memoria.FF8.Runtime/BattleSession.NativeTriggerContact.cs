namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void RecomputeNativeTriggerContact(NativeBattleTriggerReceipt receipt)
    {
        receipt.Validate();
        NativeBattleActionHeaderSlot first = nativeActionPackets.Headers[0];
        byte actorIndex = first.ReadByte(0);
        if (receipt.ActorIndex != actorIndex)
        {
            throw new InvalidDataException("Native485160 receipt contradicts header0's physical actor.");
        }
        BattleCombatant actor = FindNativeActor(actorIndex);
        BattleCombatant recipient = FindNativeActor(nativeActionPackets.ReadContact(0).Primary.ActorIndex);
        bool ordinary = receipt.TotalContacts == 1;
        byte command = ordinary ? first.ReadByte(1) switch { 12 => (byte)248, 28 => (byte)243, _ => (byte)253 } : (byte)251;
        bool last = receipt.ContactIndex == receipt.TotalContacts;
        //48F480 reads E03 only when the supplied contact index equals the total.
        bool finisher = last && (nativeActionScratch.FinisherPendingE03 ?? throw new NotSupportedException("Native48F480 requires the retained E03 finisher byte."));
        bool deferred = !last || finisher;
        byte sequence = deferred ? (byte)255 : command == 251 ? (NativeSavedStatusBits(recipient) & 1) != 0 ? (byte)4 : (byte)255 : (byte)4;
        nativeActionScratch = nativeActionScratch with { ActorIndex = actorIndex, Command = command, DeferredEffectsE00 = deferred, FinisherPendingE03 = last && finisher ? false : nativeActionScratch.FinisherPendingE03 };
        BattleNativeAction weapon = actor.NativeAttack ?? throw new NotSupportedException("Native485160 requires the retained actor's weapon/junction metadata.");
        byte power = ordinary ? weapon.Power : actor.NativePlayerStatRules?.CharacterRecord[33] ?? throw new NotSupportedException("Native251 requires its character kernel power byte33.");
        BattleNativeAction native = weapon with { Command = command, AttackType = 10, Power = power, HitCount = 1, Critical = 0, DamagePresentation = sequence };
        if (!ordinary)
        {
            native = native with { ActionId = nativeActionScratch.Action ?? throw new NotSupportedException("Native251 requires the retained AF4 action WORD."), AttackFlags = 0, Element = 0, ElementPercent = 0, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255 };
        }
        short timing = ordinary && (NativeSavedStatusBits(actor) & 8) != 0 ? (short)1 : receipt.Timing;
        BattleActionPresentation original = nativeTimedAction?.Command ?? ActiveAction ?? throw new NotSupportedException("Native485160 requires its currently admitted action provenance.");
        byte? queued = original.NativeQueuedCommand;
        if (original.NativeRenzokuken is not null)
        {
            ushort control = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(first.Payload.AsSpan()[4..]);
            queued = control == 65534 ? (byte)250 : (byte)5;
        }
        BattleActionPresentation contact = original with { NativeQueuedCommand = queued, NativeAction = native, NativeTargetMask = checked((ushort)(1 << recipient.NativeIndex)), GunbladeTriggerTiming = timing };
        BattleActionDefinition definition = new(contact.Ability, contact.ActionId, native.Name ?? "Gunblade", BattleActionEffect.Native, native.Power, false) { NativeAction = native };
        BattleActionPresentation? previous = resolvingNativeAction;
        nativeActionCommitDepth++;
        resolvingNativeAction = contact;
        try
        {
            nativeActionPackets = nativeActionPackets with { ContactCount = 0 };
            NativeBattleContactPacket packet = ResolveInstalledNativeHitWithContact(actor, recipient, contact, definition, native, 255);
            (nativeActionPackets, _) = nativeActionPackets.AppendContact(packet);
        }
        finally
        {
            resolvingNativeAction = previous;
            nativeActionCommitDepth--;
        }
    }

    private void CompleteNativeComponentTrigger(BattleActionPresentation command)
    {
        if (NativeGraphics is null && command.NativeHeaderIndex is not null && command.NativeQueuedCommand == 254)
        {
            _ = NativeGuardianBoostBinding(command);
            //TODO(TD-009): The component contact represents56DD70 phase5 without its effect module;4850A0 math remains shared.
            if (nativeActionScratch.GuardianFormulaE1E is not bool computed)
            {
                throw new NotSupportedException("Native component GF completion requires its actual E1E formula gate.");
            }
            if (!computed)
            {
                RecomputeNativeGuardianContacts(100);
            }
            return;
        }
        if (NativeGraphics is null && command.NativeHeaderIndex is not null && IsGunbladeAction(GetActor(command.ActorId), command))
        {
            //TODO(TD-009): The component windup supplies485160 admission until the original AB/UI callbacks are present. Its math/physical contact consumer is shared.
            BattleCombatant actor = GetActor(command.ActorId);
            short timing = ReadGunbladeTiming(actor, command);
            RecomputeNativeTriggerContact(new(checked((byte)actor.NativeIndex), 1, 1, timing, 32));
        }
    }

    private void RecomputeNativeGuardianContacts(byte boostPercent)
    {
        NativeBattleActionHeaderSlot first = nativeActionPackets.Headers[0];
        byte command = first.ReadByte(1);
        if (command != 254)
        {
            throw new InvalidDataException("Native4850A0 requires its original254 header0.");
        }
        ushort id = NativeBattleActionGraphics.Word(first, 4);
        if (id is < 64 or >= 80)
        {
            throw new InvalidDataException("Native4850A0 requires the physical GF action WORD64..79.");
        }
        BattleCombatant actor = FindNativeActor(first.ReadByte(0));
        BattleNativeAction native = FindInstalledNativeAction(command, id);
        ValidateNativeActionContract(native);
        BattleActionDefinition action = FindAction(BattleAbility.GuardianForce, id - 64);
        byte sequence = first.ReadByte(2);
        NativeBattleContactPacket[] retained = Enumerable.Range(0, first.ReadByte(16)).Select(nativeActionPackets.ReadContact).ToArray();
        BattleActionPresentation? previous = resolvingNativeAction;
        nativeActionScratch = nativeActionScratch with { ActorIndex = first.ReadByte(0), Command = command, Action = id, GuardianFormulaE1E = true, DeferredEffectsE00 = false, GuardianBoostPercent = boostPercent };
        nativeActionPackets = nativeActionPackets with { ContactCount = 0 };
        nativeActionCommitDepth++;
        try
        {
            foreach (NativeBattleContactPacket prior in retained)
            {
                BattleCombatant recipient = FindNativeActor(prior.Primary.ActorIndex);
                //4850A0 restores its invocation globals directly from header0 before48FE20; no queued presentation is consulted.
                BattleActionPresentation contact = new(actor.Id, recipient.Id, BattleActionPhase.Recovery, 0, BattleAbility.GuardianForce, id - 64) { NativeHeaderIndex = 0, NativeQueuedCommand = command, NativeAction = native, NativeSequence = sequence, NativeTargetMask = checked((ushort)(1 << recipient.NativeIndex)) };
                resolvingNativeAction = contact;
                NativeBattleContactPacket packet = ResolveInstalledNativeHitWithContact(actor, recipient, contact, action, native, 255);
                (nativeActionPackets, _) = nativeActionPackets.AppendContact(packet);
            }
        }
        finally
        {
            resolvingNativeAction = previous;
            nativeActionCommitDepth--;
        }
    }
}
