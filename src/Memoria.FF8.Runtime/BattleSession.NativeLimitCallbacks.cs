namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void RequireNativeLimitCallback(string actorId, NativeLimitCallback? expected)
    {
        if (expected is not null && !actions.Any(command => command.ActorId == actorId && command.NativeLimitCallback == expected) && !(ActiveAction?.ActorId == actorId && ActiveAction.NativeLimitCallback == expected))
        {
            throw new InvalidDataException("Saved timed Limit phase lacks its registered native graphics callback.");
        }
    }

    private void ValidateNativeLimitCallbacks()
    {
        foreach (BattleAttackCommand command in actions)
        {
            ValidateNativeLimitCallback(command.ActorId, command.NativeLimitCallback);
        }
        if (ActiveAction is { } active)
        {
            ValidateNativeLimitCallback(active.ActorId, active.NativeLimitCallback);
        }
    }

    private void ValidateNativeLimitCallback(string actorId, NativeLimitCallback? callback)
    {
        if (callback is not NativeLimitCallback value)
        {
            return;
        }
        bool owned = value switch
        {
            NativeLimitCallback.ShotOpening or NativeLimitCallback.ShotEffect or NativeLimitCallback.ShotEnding => nativeShotProcesses.ContainsKey(actorId),
            NativeLimitCallback.DuelOpening or NativeLimitCallback.DuelEffect or NativeLimitCallback.DuelFinisher => nativeDuelProcesses.ContainsKey(actorId),
            _ => false
        };
        if (!owned)
        {
            throw new InvalidDataException("Saved timed Limit callback lacks its native process owner.");
        }
    }
}
