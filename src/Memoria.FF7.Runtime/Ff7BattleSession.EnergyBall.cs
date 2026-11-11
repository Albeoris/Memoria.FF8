using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    private Ff7BattleActionState StartPrimaryEnergyBall(Ff7BattleActionState action)
    {
        state = state with { PrimaryEffects = Ff7BattlePrimaryEffects.ScheduleEnergyBall(EnsurePrimaryEffects(), action.Command.ActorSlot, action.Command.TargetMask) };
        return action with { EnergyBallStarted = true, PrimaryCallbacksRunning = true };
    }
}
