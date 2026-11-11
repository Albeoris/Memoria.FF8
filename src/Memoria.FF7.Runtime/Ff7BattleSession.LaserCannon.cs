using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    private Ff7BattleActionState StartLaserCannon(Ff7BattleActionState action)
    {
        Ff7BattleQueuedCommand command = action.Command;
        state = state with { PrimaryEffects = Ff7BattlePrimaryEffects.ScheduleLaserCannon(EnsurePrimaryEffects(), command.ActorSlot, command.TargetMask) };
        return action with { LaserCannonStarted = true, PrimaryCallbacksRunning = true };
    }
}
