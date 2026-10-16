namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static byte NativeHeaderCommand(BattleActionPresentation command, BattleNativeAction native)
    {
        return command.NativeDraw is { Failed: true } ? (byte)0 : command.IndependentGuardian?.Action?.Command == 244 && command.NativeQueuedCommand is 4 or 13 ? (byte)244 : command.NativeQueuedCommand ?? native.Command;
    }

    private NativeBattleActionHeaderPreparation BuildNativeActionHeaderPreparation(BattleCombatant actor, BattleActionPresentation command, BattleNativeAction native)
    {
        if (command.IndependentGuardian?.FailureMessage is not null)
        {
            BattleActionDefinition item = FindAction(BattleAbility.Item, command.ActionId);
            NativeBattleActionDataReference title = new(0x47EA30, checked((ushort)command.ActionId)) { FormattedText = item.NativeAction?.Name ?? item.Name };
            return new(checked((byte)actor.NativeIndex), 0, 10, -1, 9, 0, title);
        }
        if (command.NativeDraw is { } draw)
        {
            byte drawCommand = draw.Failed ? (byte)0 : (byte)6;
            byte drawSequence = draw.Failed ? (byte)10 : ResolveNativeDispatchSequence(actor, command.DrawCast ? (byte)9 : (byte)10, command.NativeSequence ?? 0, native.ActionId);
            ushort drawAction = draw.Failed ? (ushort)9 : draw.Selection;
            ushort drawEffect = draw.Failed ? (ushort)0 : command.DrawCast ? native.MagicId : (ushort)15;
            NativeBattleActionDataReference drawTitle = command.DrawCast && !draw.Failed ? new(0x495280, 0) { FormattedText = NativeDrawResultText(actor, draw, true) } : new(0x47EBD0, 6) { FormattedText = definition.NativeCommandNames.ElementAtOrDefault(6) };
            return new(checked((byte)actor.NativeIndex), drawCommand, drawSequence, -1, drawAction, drawEffect, drawTitle);
        }
        byte original = NativeHeaderCommand(command, native);
        byte sequence = ResolveNativeDispatchSequence(actor, original, command.NativeSequence ?? 0, native.ActionId);
        ushort action = original == 14 ? checked((ushort)(native.ActionId + 101)) : native.ActionId;
        ushort effect = original is 0 or 1 or 12 or 28 ? (ushort)0 : native.MagicId;
        sbyte camera = -1;
        NativeBattleActionDataReference? data = null;
        switch (original)
        {
            case 2:
            case 16:
                data = new(0x47E970, action);
                break;
            case 4:
            case 13:
                data = new(0x47EA30, action);
                break;
            case 8:
            case 236:
                if (native.RawRecord.Length < 10)
                {
                    throw new InvalidDataException("Native enemy header requires its original20-byte kernel record.");
                }
                camera = unchecked((sbyte)native.RawRecord[4]);
                data = (native.RawRecord[9] & 0x80) == 0 ? null : new(0x4950A0, action);
                break;
            case 5:
                data = new(0x47EBD0, original);
                break;
            case 11:
                action = 65531;
                break;
            case 14:
                data = new(0x47E590, native.ActionId);
                break;
            case 15:
                data = new(0x47E650, action);
                break;
            case 17:
            case 18:
            case 20:
            case 21:
            case 22:
                data = new(0x47E6B0, action);
                break;
            case 19:
                data = new(0x47E4C0, action);
                break;
            case 7:
            case 23:
            case 24:
            case 25:
            case 26:
            case 27:
            case 28:
            case 29:
            case 30:
            case 31:
            case 32:
            case 33:
            case 34:
            case 38:
                data = new(0x47EBD0, original);
                break;
            case 239:
            case 241:
                data = action is 65530 or 65532 ? null : new(0x47E530, action);
                break;
            case 240:
            case 245:
                data = new(0x495050, action);
                break;
            case 244:
                data = new(0x495050, native.ActionId);
                break;
            case 246:
                effect = 9;
                break;
            case 250:
                data = new(0x47E5F0, checked((ushort)(native.SupplementalKernelIndex ?? native.ActionId)));
                break;
            case 254:
                data = new(0x495070, action);
                break;
        }
        if (data is not null)
        {
            data = data with { FormattedText = ResolveNativeActionHeaderName(data, native) };
        }
        return new(checked((byte)actor.NativeIndex), original, sequence, camera, action, effect, data);
    }

    private byte ResolveNativeDispatchSequence(BattleCombatant actor, byte command, byte supplied, ushort action)
    {
        byte sequence = supplied;
        if (actor.NativeIndex < 3)
        {
            if (command < 39)
            {
                if (definition.NativeCommandSequences.Length != 39)
                {
                    throw new NotSupportedException("Native player header requires the authenticated39-byte B8150C command sequence table.");
                }
                sequence = definition.NativeCommandSequences[command];
            }
            else
            {
                sequence = command switch { 236 or 240 or 245 => 0, 239 => 28, 244 or 254 => 14, 247 => 11, _ => supplied };
            }
        }
        return command is 244 or 254 && action is 15 or 70 ? (byte)11 : sequence;
    }

    private BattleAttackCommand ProduceNativeQueuedCommand(BattleCombatant actor, BattleCombatant target, BattleAttackCommand command)
    {
        if (command.NativeHeaderIndex is not null || !HasInstalledNativeAction(actor, command))
        {
            return command;
        }
        int firstHeader = nativeActionPackets.ActionCount;
        BattleActionPresentation presentation = NativeCommandPresentation(command);
        NativeBattleActionProductionContext? previousContext = nativeProductionContext;
        if (previousContext is null && nativeActionCommitDepth == 0)
        {
            BeginNativeReactionBatch();
        }
        byte original = command.NativeQueuedCommand ?? command.NativeAction?.Command ?? (byte)1;
        byte lane = command.NativeAiCallbackStage is not null || command.NativePlayerReaction is not null ? (byte)0 : original is 5 or 11 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21 or 22 or 254 ? (byte)1 : (byte)2;
        if (previousContext is null)
        {
            SetNativeActionEvent(actor, lane);
        }
        nativeProductionContext ??= new(0, lane, command.DrawCast, command.NativeAiCallbackStage == 1 || lane != 0 ? 0x47DE70u : 0x47DDA0u);
        try
        {
            ResolveActionTransaction(actor, target, presentation);
            if (nativeActionPackets.ActionCount == firstHeader)
            {
                throw new NotSupportedException("Native invocation did not produce its original48E830 factual header.");
            }
            if (lane != 0)
            {
                nativeActionPackets = nativeActionPackets.WritePlayerRepeatMode(firstHeader, nativeProductionContext.RepeatMode);
            }
            return command with { NativeHeaderIndex = firstHeader, NativeSequence = nativeActionPackets.Headers[firstHeader].ReadByte(2) };
        }
        finally
        {
            nativeProductionContext = previousContext;
        }
    }

    private bool HasInstalledNativeAction(BattleCombatant actor, BattleAttackCommand command)
    {
        return command.Ability == BattleAbility.Draw && command.ActionId >= 64 && definition.NativeCommandSequences.Length == 39 || command.NativeAction is not null || command.NativeGraphic is not null || command.IndependentGuardian is not null || command.Ability == BattleAbility.Attack && actor.NativeAttack is not null || definition.ActionDefinitions.Any(action => action.Ability == (command.Ability == BattleAbility.Draw ? BattleAbility.Magic : command.Ability) && action.Id == command.ActionId && action.NativeAction is not null);
    }

    private void ValidateNativeHeaderReference(BattleCombatant actor, int? headerIndex)
    {
        if (headerIndex is null)
        {
            return;
        }
        if (headerIndex is < 0 or >= NativeBattleActionPacketsState.HeaderCapacity || nativeActionPackets.Headers[headerIndex.Value].ReadByte(0) != actor.NativeIndex)
        {
            throw new InvalidDataException("Saved factual action does not identify its physical actor/header slot.");
        }
        _ = nativeActionPackets.Headers[headerIndex.Value].ReadByte(16);
    }

    private void CompleteNativeComponentPacketPresentation(BattleActionPresentation command)
    {
        if (NativeGraphics is null && command.NativeHeaderIndex is not null && !actions.Any(queued => queued.NativeHeaderIndex is not null))
        {
            //TODO(TD-009): Graphicsless components admit48D1F0 at their declared presentation completion instead of descriptor10.
            nativeActionScratch = nativeActionScratch with { ComponentCompletion = nativeActionScratch.ComponentCompletion ?? 0x47DDA0 };
            ActiveAction = null;
            TryCompleteNativeComponentCommand();
        }
    }

    private static BattleActionPresentation NativeCommandPresentation(BattleAttackCommand command)
    {
        return new(command.ActorId, command.TargetId, BattleActionPhase.Windup, 0, command.Ability, command.ActionId, command.DrawCast, command.NativeTargetSelector) { NativeHeaderIndex = command.NativeHeaderIndex, NativeDuel = command.NativeDuel, NativeSlots = command.NativeSlots, NativeAiCallbackStage = command.NativeAiCallbackStage, NativePlayerReaction = command.NativePlayerReaction, NativeRenzokuken = command.NativeRenzokuken, NativeDraw = command.NativeDraw, DrawCastTargetId = command.DrawCastTargetId, NativeLimitCallback = command.NativeLimitCallback, NativeMagicTargets = command.NativeMagicTargets, NativeGraphic = command.NativeGraphic, IndependentGuardian = command.IndependentGuardian, NativeSpecial = command.NativeSpecial, NativeQueuedCommand = command.NativeQueuedCommand, NativeAction = command.NativeAction, NativeSequence = command.NativeSequence, NativeTargetMask = command.NativeTargetMask, FreeNativeMagic = command.FreeNativeMagic, ReservedNativeItemSlot = command.ReservedNativeItemSlot };
    }
}
