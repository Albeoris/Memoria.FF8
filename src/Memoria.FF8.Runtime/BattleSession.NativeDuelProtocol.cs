using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static BattleNativeAction CreateNativeDuelControl241(ushort actionId, ushort effect) => NativeBattleDuelRules.Control(actionId, effect);

    private BattleNativeAction ValidateNativeDuelControl241(BattleCombatant actor, BattleNativeAction native)
    {
        if (actor.NativeLimit?.Command != 11)
        {
            throw new InvalidDataException("Saved Duel control requires its original character.");
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
