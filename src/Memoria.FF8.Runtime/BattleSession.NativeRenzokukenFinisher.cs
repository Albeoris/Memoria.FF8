namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void PrepareNativeRenzokukenFinisher(byte index, ushort targets)
    {
        NativeBattleTimedAction timed = nativeTimedAction ?? throw new InvalidOperationException("Native250 requires its admitted Renzokuken event.");
        BattleCombatant actor = GetActor(timed.Command.ActorId);
        BattleNativeAction selected = FindInstalledNativeAction(249, index);
        BattleNativeAction preliminary = selected with { Command = 250, ActionId = ushort.MaxValue, SupplementalKernelIndex = index };
        BattleActionPresentation command = timed.Command with { ActionId = ushort.MaxValue, NativeQueuedCommand = 250, NativeSequence = 0, NativeAction = preliminary, NativeTargetMask = targets, NativeHeaderIndex = null };
        if (nativeDispatchCommandLocation is { } location)
        {
            //48D200 command5 writes the selected finisher into this same row's second12-byte packet.
            BattleAttackCommand packet = new(actor.Id, command.TargetId, command.Ability, command.ActionId) { NativeQueuedCommand = 250, NativeAction = preliminary, NativeTargetMask = targets };
            nativeCommandBanks = nativeCommandBanks.WriteCommand(location, 1, checked((byte)actor.NativeIndex), 250, index, 0, ushort.MaxValue, [targets]).BindCommand(location, 1, packet);
        }
        uint callback = index == 3 ? 0x48F350u : 0x48F3F0u;
        if (NativeGraphics is { } graphics)
        {
            //485610 queues the formula callback after command5's104 and before command250's104.
            NativeGraphics = QueueNativeDescriptor(graphics, 10, callback);
        }
        NativeBattleActionProductionContext? previous = nativeProductionContext;
        nativeProductionContext = new(0, 1, false, 0x47DE70);
        try
        {
            int header = nativeActionPackets.ActionCount;
            BattleActionDefinition action = new(command.Ability, command.ActionId, selected.Name ?? "Renzokuken finisher", BattleActionEffect.Native, 0, false) { NativeAction = preliminary };
            ResolveInstalledNativeAction(actor, GetActor(command.TargetId), command, action);
            nativeActionPackets = nativeActionPackets.WritePlayerRepeatMode(header, nativeProductionContext.RepeatMode);
        }
        finally
        {
            nativeProductionContext = previous;
        }
        if (NativeGraphics is null)
        {
            //TODO(TD-009): Components without descriptors admit the same scheduled finisher callback before their UI projection starts.
            CompleteNativeRenzokukenFinisherCallback(callback);
        }
    }

    private void CompleteNativeRenzokukenFinisherCallback(uint callback)
    {
        byte index = nativeActionScratch.FinisherActionIndexE2E ?? throw new NotSupportedException("Native Renzokuken finisher requires its retained selected-index BYTE E2E.");
        if (index > 3 || callback != (index == 3 ? 0x48F350u : 0x48F3F0u))
        {
            throw new InvalidDataException("Native finisher callback contradicts its selected kernel record.");
        }
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers.Take(nativeActionPackets.ActionCount).FirstOrDefault(slot => slot.ReadByte(1) == 250) ?? throw new InvalidDataException("Native finisher callback lacks its original250 header.");
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native finisher callback requires its physical retained contact pointer.");
        BattleCombatant actor = FindNativeActor(header.ReadByte(0));
        BattleNativeAction native = FindInstalledNativeAction(249, index) with { ActionId = nativeActionScratch.Action ?? throw new NotSupportedException("Native finisher requires its retained AF4 action WORD.") };
        int count = index == 3 ? native.HitCount : nativeActionScratch.LastPublishedTargetCountE05 ?? throw new NotSupportedException("Native48F3F0 requires its retained E05 target count.");
        NativeBattleContactPacket[] recipients = Enumerable.Range(first, count).Select(nativeActionPackets.ReadContact).ToArray();
        if (index == 3)
        {
            FindNativeActor(recipients[0].Primary.ActorIndex).NativeRemainingHits = native.HitCount;
        }
        NativeBattleTimedAction timed = nativeTimedAction ?? throw new NotSupportedException("Native finisher callback requires its admitted event provenance.");
        BattleActionPresentation command = timed.Command with { NativeQueuedCommand = 250, NativeAction = native, ActionId = index };
        BattleActionDefinition action = new(command.Ability, command.ActionId, native.Name ?? "Renzokuken finisher", BattleActionEffect.Native, native.Power, false) { NativeAction = native };
        BattleActionPresentation? previous = resolvingNativeAction;
        nativeActionScratch = nativeActionScratch with { ActorIndex = header.ReadByte(0), Command = 249, DeferredEffectsE00 = false };
        nativeActionCommitDepth++;
        resolvingNativeAction = command;
        try
        {
            foreach (NativeBattleContactPacket recipient in recipients)
            {
                NativeBattleContactPacket packet = ResolveInstalledNativeHitWithContact(actor, FindNativeActor(recipient.Primary.ActorIndex), command, action, native, 255);
                (nativeActionPackets, _) = nativeActionPackets.AppendContact(packet);
            }
        }
        finally
        {
            resolvingNativeAction = previous;
            nativeActionCommitDepth--;
        }
    }

    private NativeBattleGraphicsState RunNativeRenzokukenFinisherCallback(NativeBattleGraphicsState graphics, uint callback)
    {
        NativeGraphics = graphics;
        CompleteNativeRenzokukenFinisherCallback(callback);
        return RequireNativeGraphics();
    }
}
