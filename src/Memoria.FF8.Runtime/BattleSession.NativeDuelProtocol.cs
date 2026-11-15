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
            ProduceNativeDuel483400(actor, 241, selection == 255 ? (ushort)65532 : (ushort)65530, selection == 255 ? (ushort)0 : action, state.TargetRequest, null);
            if (selection != 255)
            {
                BattleNativeAction finisher = FindInstalledNativeAction(239, action);
                byte flags = finisher.TargetFlags ?? throw new InvalidDataException("Native48F220 requires the finisher target flags.");
                BattleTargetRequest targets = (flags & 0x30) == 0x10 ? state.TargetRequest : CreateAutomaticTargetRequest(flags);
                ProduceNativeDuel483400(actor, 239, action, finisher.MagicId, targets, NativeLimitCallback.DuelFinisher);
            }
            nativeDuelProcesses[actorId] = nativeDuelProcesses[actorId] with { Phase = BattleNativeDuelPhase.Finisher };
            QueueNativeActionClockCompletion(0x47DE70);
            return NativeGraphics!;
        }
        ProduceNativeDuel483400(actor, 241, action, FindInstalledNativeAction(241, action).MagicId, state.TargetRequest, NativeLimitCallback.DuelEffect);
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

    private static BattleNativeAction CreateNativeDuelControl241(ushort actionId, ushort effect) => NativeBattleDuelRules.Control(actionId, effect);

    private BattleNativeAction ValidateNativeDuelControl241(BattleCombatant actor, BattleActionHeaderReference? headerIndex, BattleNativeAction native)
    {
        if (actor.NativeLimit?.Command != 11 || headerIndex is not { } reference)
        {
            throw new InvalidDataException("Saved Duel control requires its original character and produced physical header.");
        }
        NativeBattleActionHeaderSlot header = nativeActionPackets.ReadHeader(reference);
        if (header.Source != CreatePresentationModelOwner(actor) || header.Command != 241 || header.Action != native.ActionId || header.Effect != native.MagicId || header.Data is not null)
        {
            throw new InvalidDataException("Saved Duel control contradicts its produced48D200 header.");
        }
        return CreateNativeDuelControl241(native.ActionId, native.MagicId);
    }

    private void ValidateNativeDuelControlOwners()
    {
        if (UsesExpandedRoster)
        {
            ValidateExpandedDuelOwners();
        }
        foreach (BattleActionPresentation action in QueuedActions.Select(NativeCommandPresentation).Concat(ActiveAction is { } active ? [active] : []))
        {
            if (action.NativeAction is { Command: 241, ActionId: 65530 or 65532 } && (!nativeDuelProcesses.TryGetValue(action.ActorId, out BattleNativeDuelState? duel) || duel.Selection1D28DEF is null || duel.Phase != BattleNativeDuelPhase.Finisher))
            {
                throw new InvalidDataException("Saved produced Duel control lacks its closing input-process owner.");
            }
        }
    }

    private void ProduceNativeDuel483400(BattleCombatant actor, byte command, ushort actionId, ushort effect, BattleTargetRequest targets, NativeLimitCallback? callback)
    {
        BattleNativeAction native = command == 241 && actionId is 65530 or 65532 ? CreateNativeDuelControl241(actionId, effect) : FindInstalledNativeAction(command, actionId);
        BattleCombatant target = ResolveBattleTargets(targets, sampleRandom: false).FirstOrDefault() ?? actor;
        BattleAttackCommand queued = new(actor.Id, target.Id, targets, BattleAbility.EnemyAbility, actionId) { NativeQueuedCommand = command, NativeAction = native, NativeLimitCallback = callback, NativeSequence = 0 };
        NativeBattleActionProductionContext? previous = nativeProductionContext;
        nativeProductionContext = new(0, 0, false, 0x47DE70);
        try
        {
            BattleActionHeaderReference header = nativeActionPackets.NextHeader;
            BattleActionDefinition action = new(BattleAbility.EnemyAbility, actionId, native.Name ?? "Duel", BattleActionEffect.Native, 0, false) { NativeAction = native };
            ResolveInstalledNativeAction(actor, target, NativeCommandPresentation(queued), action, queueGraphics: false);
            bool unavailable = nativeActionScratch.TargetUnavailableB0E ?? throw new NotSupportedException("Native483400 requires its actual48EE50/48EEB0 target result BYTE B0E.");
            if (!unavailable)
            {
                BeginNativeBattleActionPause();
                nativeActionPackets = nativeActionPackets.WritePlayerRepeatMode(header, 1);
                NativeGraphics = QueueNativeActionDescriptor(NativeGraphics!, header);
                NativeGraphics = QueueNativeDescriptor(NativeGraphics, 10, 0x47DD90);
                actions.Enqueue(queued with { NativeHeader = header, NativeSequence = nativeActionPackets.ReadHeader(header).Sequence, NativeLimitCallback = actionId == 65532 ? NativeLimitCallback.DuelFinisher : callback });
            }
        }
        finally
        {
            nativeProductionContext = previous;
        }
    }
}
