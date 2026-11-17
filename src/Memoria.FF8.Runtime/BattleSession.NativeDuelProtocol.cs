using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static BattleNativeAction CreateNativeDuelControl241(ushort actionId, ushort effect) => NativeBattleDuelRules.Control(actionId, effect);

    private static void ValidateNativeDuelSelectionDescriptors(BattleNativeDuelState state, NativeBattleGraphicsState graphics)
    {
        bool closing = state.Phase == BattleNativeDuelPhase.Finisher && state.Selection1D28DEF == 254;
        bool found = graphics.Descriptors.Queue.Any(index => graphics.Descriptors.Packets[index] is { Command: 10 } packet && (closing ? packet.Argument is 0x47DE70 or 0x47E120 : packet.Argument == 0x4852B0));
        if (!found)
        {
            throw new InvalidDataException("Saved Duel selection lacks its actual pending poll or closing event descriptor.");
        }
    }

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
        ValidateExpandedDuelOwners();
        foreach (BattleActionPresentation action in QueuedActions.Select(NativeCommandPresentation).Concat(ActiveAction is { } active ? [active] : []))
        {
            if (action.NativeAction is { Command: 241, ActionId: 65530 or 65532 } && (!nativeDuelProcesses.TryGetValue(action.ActorId, out BattleNativeDuelState? duel) || duel.Selection1D28DEF is null || duel.Phase != BattleNativeDuelPhase.Finisher))
            {
                throw new InvalidDataException("Saved produced Duel control lacks its closing input-process owner.");
            }
        }
    }
}
