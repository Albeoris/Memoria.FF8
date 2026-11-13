namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void RequireNativeLimitCallback(string actorId, NativeLimitCallback? expected)
    {
        if (expected is not null && !QueuedActions.Any(command => command.ActorId == actorId && command.NativeLimitCallback == expected) && !(ActiveAction?.ActorId == actorId && ActiveAction.NativeLimitCallback == expected))
        {
            throw new InvalidDataException("Saved timed Limit phase lacks its registered native graphics callback.");
        }
    }

    private void ValidateNativeLimitCallbacks()
    {
        foreach (BattleAttackCommand command in QueuedActions)
        {
            ValidateNativeLimitCallback(command.ActorId, command.NativeLimitCallback, command.TargetId, command.NativeAction);
        }
        if (ActiveAction is { } active)
        {
            ValidateNativeLimitCallback(active.ActorId, active.NativeLimitCallback, active.TargetId, active.NativeAction);
        }
    }

    private void ValidateNativeLimitCallback(string actorId, NativeLimitCallback? callback, string targetId, BattleNativeAction? action)
    {
        if (callback is not NativeLimitCallback value)
        {
            return;
        }
        bool owned = value switch
        {
            NativeLimitCallback.ShotOpening or NativeLimitCallback.ShotEffect or NativeLimitCallback.ShotEnding => nativeShotProcesses.TryGetValue(actorId, out BattleNativeShotState? shot) &&
                (!UsesExpandedRoster || shot.ExpandedTargetId == targetId && action?.ActionId == shot.ActionId && action.Command == (value == NativeLimitCallback.ShotOpening ? 14 : value == NativeLimitCallback.ShotEffect ? 237 : 238)),
            NativeLimitCallback.DuelOpening or NativeLimitCallback.DuelEffect or NativeLimitCallback.DuelFinisher => nativeDuelProcesses.TryGetValue(actorId, out BattleNativeDuelState? duel) &&
                (!UsesExpandedRoster || ValidateExpandedDuelCallback(actorId, duel, value, targetId, action)),
            _ => false
        };
        if (!owned)
        {
            throw new InvalidDataException("Saved timed Limit callback lacks its native process owner.");
        }
    }
}
