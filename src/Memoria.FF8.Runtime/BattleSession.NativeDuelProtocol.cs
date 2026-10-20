using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeDuelSelection4852B0(NativeBattleGraphicsState graphics)
    {
        if (nativeDuelProcesses.Count != 1)
        {
            throw new InvalidDataException("Native4852B0 requires its single Duel input owner.");
        }
        (string actorId, BattleNativeDuelState state) = nativeDuelProcesses.Single();
        byte selection = state.Selection1D28DEF ?? throw new NotSupportedException("Native4852B0 requires the retained selection BYTE1D28DEF; migrate the standalone save explicitly.");
        NativeGraphics = graphics;
        AcknowledgeNativeEventPresentations(CaptureNativeEventPresentation(), CaptureNativeQueuedEventPresentations());
        graphics = NativeGraphics!;
        if (selection == 254)
        {
            return QueueNativeDescriptor(graphics, 10, 0x4852B0);
        }
        nativeDuelProcesses[actorId] = state with { Selection1D28DEF = 254 };
        BattleCombatant actor = GetActor(actorId);
        byte action = selection == 255 ? (byte)255 : NativeBattleDuelRules.ActionId(actor.NativeLimit!, selection);
        if (selection == 255 || action >= 6)
        {
            ProduceNativeDuel483400(actor, 241, selection == 255 ? (ushort)65532 : (ushort)65530, selection == 255 ? (ushort)0 : action, state.TargetMask, null);
            if (selection != 255)
            {
                BattleNativeAction finisher = FindInstalledNativeAction(239, action);
                byte flags = finisher.TargetFlags ?? throw new InvalidDataException("Native48F220 requires the finisher target flags.");
                ushort mask = (flags & 0x30) == 0x10 ? state.TargetMask : NativeAutomaticActionTargetMask(flags);
                ProduceNativeDuel483400(actor, 239, action, finisher.MagicId, mask, NativeLimitCallback.DuelFinisher);
            }
            nativeDuelProcesses[actorId] = nativeDuelProcesses[actorId] with { Phase = BattleNativeDuelPhase.Finisher };
            QueueNativeActionClockCompletion(0x47DE70);
            return NativeGraphics!;
        }
        ProduceNativeDuel483400(actor, 241, action, FindInstalledNativeAction(241, action).MagicId, state.TargetMask, NativeLimitCallback.DuelEffect);
        return QueueNativeDescriptor(NativeGraphics!, 10, 0x4852B0);
    }

    private static void ValidateNativeDuelSelectionDescriptors(BattleNativeDuelState state, NativeBattleGraphicsState graphics)
    {
        bool closing = state.Phase == BattleNativeDuelPhase.Finisher && state.Selection1D28DEF == 254;
        bool found = graphics.Descriptors.Queue.Any(index => graphics.Descriptors.Packets[index] is { Command: 10 } packet && (closing ? packet.Argument is 0x47DE70 or 0x47E120 : packet.Argument == 0x4852B0));
        if (!found)
        {
            throw new InvalidDataException("Saved Duel selection lacks its actual pending poll or closing event descriptor.");
        }
    }

    private static BattleNativeAction CreateNativeDuelControl241(ushort actionId, ushort effect)
    {
        if (actionId == 65530 ? effect is < 6 or > 9 : actionId != 65532 || effect != 0)
        {
            throw new InvalidDataException("Native Duel control requires FFFA with its original finisher6..9 or FFFC with effect0.");
        }
        return new(241, actionId, -1, -1) { MagicId = effect, AttackType = 0, Power = 0, HitCount = 1, TargetFlags = null, AttackFlags = 3, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = ImmutableArray<byte>.Empty, SupplementalRecord = ImmutableArray<byte>.Empty };
    }

    private BattleNativeAction ValidateNativeDuelControl241(BattleCombatant actor, int? headerIndex, BattleNativeAction native)
    {
        if (actor.NativeLimit?.Command != 11 || headerIndex is not int index || index is < 0 or >= NativeBattleActionPacketsState.HeaderCapacity)
        {
            throw new InvalidDataException("Saved Duel control requires its original character and produced physical header.");
        }
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[index];
        if (header.ReadByte(0) != actor.NativeIndex || header.ReadByte(1) != 241 || NativeBattleActionGraphics.Word(header, 4) != native.ActionId || NativeBattleActionGraphics.Word(header, 6) != native.MagicId || header.Data is not null)
        {
            throw new InvalidDataException("Saved Duel control contradicts its produced48D200 header.");
        }
        return CreateNativeDuelControl241(native.ActionId, native.MagicId);
    }

    private void ValidateNativeDuelControlOwners()
    {
        foreach (BattleActionPresentation action in QueuedActions.Select(NativeCommandPresentation).Concat(ActiveAction is { } active ? [active] : []))
        {
            if (action.NativeAction is { Command: 241, ActionId: 65530 or 65532 } && (!nativeDuelProcesses.TryGetValue(action.ActorId, out BattleNativeDuelState? duel) || duel.Selection1D28DEF is null || duel.Phase != BattleNativeDuelPhase.Finisher))
            {
                throw new InvalidDataException("Saved produced Duel control lacks its closing input-process owner.");
            }
        }
    }

    private void ProduceNativeDuel483400(BattleCombatant actor, byte command, ushort actionId, ushort effect, ushort targetMask, NativeLimitCallback? callback)
    {
        BattleNativeAction native = command == 241 && actionId is 65530 or 65532 ? CreateNativeDuelControl241(actionId, effect) : FindInstalledNativeAction(command, actionId);
        BattleCombatant target = combatants.FirstOrDefault(candidate => candidate.NativeIndex is >= 0 and < 7 && (targetMask & (1 << candidate.NativeIndex)) != 0) ?? actor;
        BattleAttackCommand queued = new(actor.Id, target.Id, BattleAbility.EnemyAbility, actionId) { NativeQueuedCommand = command, NativeAction = native, NativeTargetMask = targetMask, NativeLimitCallback = callback, NativeSequence = 0 };
        NativeBattleActionProductionContext? previous = nativeProductionContext;
        nativeProductionContext = new(0, 0, false, 0x47DE70);
        try
        {
            int header = nativeActionPackets.ActionCount;
            BattleActionDefinition action = new(BattleAbility.EnemyAbility, actionId, native.Name ?? "Duel", BattleActionEffect.Native, 0, false) { NativeAction = native };
            ResolveInstalledNativeAction(actor, target, NativeCommandPresentation(queued), action, queueGraphics: false);
            bool unavailable = nativeActionScratch.TargetUnavailableB0E ?? throw new NotSupportedException("Native483400 requires its actual48EE50/48EEB0 target result BYTE B0E.");
            if (!unavailable)
            {
                BeginNativeBattleActionPause();
                nativeActionPackets = nativeActionPackets.WritePlayerRepeatMode(header, 1);
                NativeGraphics = QueueNativeDescriptor(NativeGraphics!, 104, checked(0x1D280C4u + 20u * (uint)header));
                NativeGraphics = QueueNativeDescriptor(NativeGraphics, 10, 0x47DD90);
                actions.Enqueue(queued with { NativeHeaderIndex = header, NativeSequence = nativeActionPackets.Headers[header].ReadByte(2), NativeLimitCallback = actionId == 65532 ? NativeLimitCallback.DuelFinisher : callback });
            }
        }
        finally
        {
            nativeProductionContext = previous;
        }
    }
}
