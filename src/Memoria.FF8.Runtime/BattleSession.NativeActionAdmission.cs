namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static byte NativeHeaderCommand(BattleCombatant actor, BattleActionPresentation command, BattleNativeAction native)
    {
        return NativeMagicInvocationSilenced(actor, command, native) || command.NativeDraw is { Failed: true } || NativeSpecialInvocationFailed(command, native) ? (byte)0 : command.IndependentGuardian?.Action?.Command == 244 && command.NativeQueuedCommand is 4 or 13 ? (byte)244 : command.NativeQueuedCommand ?? native.Command;
    }

    private static bool NativeMagicInvocationSilenced(BattleCombatant actor, BattleActionPresentation command, BattleNativeAction native)
    {
        //48D200 case2 checks the live saved-status WORD after compelled command replacement.
        return (command.NativeQueuedCommand ?? native.Command) == 2 && (NativeSavedStatusBits(actor) & 0x10) != 0;
    }

    private static bool NativeSpecialInvocationFailed(BattleActionPresentation command, BattleNativeAction native)
    {
        return native.AttackType is 17 or 19 && command.NativeSpecial is { Failed: true };
    }

    internal NativeBattleActionHeaderPreparation BuildExpandedActionHeaderPreparation(BattleActionPresentation command, BattleNativeAction native, bool miniMogFailed = false)
    {
        BattleCombatant actor = GetActor(command.ActorId);
        BattleModelOwner owner = RequirePresentationModelOwner(ReadBattlePresentationRoster(), actor);
        if (command.NativeGraphic is { } graphic)
        {
            byte sequence = ResolveNativeDispatchSequence(actor, 252, command.NativeSequence ?? 0, 1);
            return new(owner, 252, sequence, -1, 1, graphic.MagicId, null);
        }
        return BuildNativeActionHeaderPreparation(actor, command, native, miniMogFailed);
    }

    private NativeBattleActionHeaderPreparation BuildNativeActionHeaderPreparation(BattleCombatant actor, BattleActionPresentation command, BattleNativeAction native, bool miniMogFailed)
    {
        BattleModelOwner owner = CreatePresentationModelOwner(actor);
        if (miniMogFailed)
        {
            NativeBattleActionDataReference title = new(0x47EBD0, 38) { FormattedText = definition.NativeCommandNames.ElementAtOrDefault(38) };
            return new(owner, 0, 10, -1, 9, 0, title);
        }
        if (NativeMagicInvocationSilenced(actor, command, native))
        {
            NativeBattleActionDataReference title = new(0x47E970, native.ActionId) { FormattedText = native.Name };
            return new(owner, 0, 10, -1, 9, 0, title);
        }
        if (NativeSpecialInvocationFailed(command, native))
        {
            ushort message = native.AttackType == 17 ? (ushort)50 : (ushort)112;
            NativeBattleActionDataReference title = new(0x47EC70, message) { FormattedText = NativeBattleGlobalText(message) };
            return new(owner, 0, 10, -1, 9, 0, title);
        }
        if (command.IndependentGuardian?.FailureMessage is not null)
        {
            BattleActionDefinition item = FindAction(BattleAbility.Item, command.ActionId);
            NativeBattleActionDataReference title = new(0x47EA30, checked((ushort)command.ActionId)) { FormattedText = item.NativeAction?.Name ?? item.Name };
            return new(owner, 0, 10, -1, 9, 0, title);
        }
        if (command.NativeDraw is { } draw)
        {
            byte drawCommand = draw.Failed ? (byte)0 : (byte)6;
            byte drawSequence = draw.Failed ? (byte)10 : ResolveNativeDispatchSequence(actor, command.DrawCast ? (byte)9 : (byte)10, command.NativeSequence ?? 0, native.ActionId);
            ushort drawAction = draw.Failed ? (ushort)9 : draw.Selection;
            ushort drawEffect = draw.Failed ? (ushort)0 : command.DrawCast ? native.MagicId : (ushort)15;
            NativeBattleActionDataReference drawTitle = command.DrawCast && !draw.Failed ? new(0x495280, 0) { FormattedText = NativeDrawResultText(actor, draw, true) } : new(0x47EBD0, 6) { FormattedText = definition.NativeCommandNames.ElementAtOrDefault(6) };
            return new(owner, drawCommand, drawSequence, -1, drawAction, drawEffect, drawTitle);
        }
        byte original = NativeHeaderCommand(actor, command, native);
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
                data = new(0x47EBD0, original);
                break;
            case 38:
                action = checked((ushort)native.KernelIndex);
                data = new(0x495050, action);
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
        return new(owner, original, sequence, camera, action, effect, data);
    }

    private byte ResolveNativeDispatchSequence(BattleCombatant actor, byte command, byte supplied, ushort action)
    {
        byte sequence = supplied;
        if (actor.GuardianFormVitals is not null && command is 1 or 12 && GetGuardianForm(actor.Id) is { } form)
        {
            sequence = ReadGuardianFormBinding(form, BattleGuardianFormAction.Attack).Sequence;
        }
        if (actor.IsPlayer && actor.GuardianFormVitals is null)
        {
            sequence = NativeBattleActionGraphics.ResolvePlayerSequence(command, supplied, action, definition.NativeCommandSequences);
        }
        return command is 244 or 254 && action is 15 or 70 ? (byte)11 : sequence;
    }

    private bool HasInstalledNativeAction(BattleCombatant actor, BattleAttackCommand command)
    {
        return command.Ability == BattleAbility.Draw && command.ActionId >= 64 && definition.NativeCommandSequences.Length == 39 || command.NativeAction is not null || command.NativeGraphic is not null || command.IndependentGuardian is not null || command.Ability == BattleAbility.Attack && actor.NativeAttack is not null || definition.ActionDefinitions.Any(action => action.Ability == (command.Ability == BattleAbility.Draw ? BattleAbility.Magic : command.Ability) && action.Id == command.ActionId && action.NativeAction is not null);
    }

    private static BattleActionPresentation NativeCommandPresentation(BattleAttackCommand command)
    {
        return new(command.ActorId, command.TargetId, BattleActionPhase.Windup, command.TargetRequest, command.Ability, command.ActionId, command.DrawCast) { MagicReservationId = command.MagicReservationId, SmartAttack = command.SmartAttack, NativeDuel = command.NativeDuel, NativeSlots = command.NativeSlots, NativeAiCallbackStage = command.NativeAiCallbackStage, NativePlayerReaction = command.NativePlayerReaction, NativeRenzokuken = command.NativeRenzokuken, NativeDraw = command.NativeDraw, DrawCastTargetId = command.DrawCastTargetId, NativeLimitCallback = command.NativeLimitCallback, MagicCasts = command.MagicCasts, NativeMagicCastMode = command.NativeMagicCastMode, NativeGraphic = command.NativeGraphic, IndependentGuardian = command.IndependentGuardian, NativeSpecial = command.NativeSpecial, NativeQueuedCommand = command.NativeQueuedCommand, NativeAction = command.NativeAction, NativeSequence = command.NativeSequence, GuardianFormAction = command.GuardianFormAction, GuardianFormLearnedAbility = command.GuardianFormLearnedAbility, GuardianFormInvocation = command.GuardianFormInvocation, FreeNativeMagic = command.FreeNativeMagic, ReservedNativeItemSlot = command.ReservedNativeItemSlot };
    }
}
