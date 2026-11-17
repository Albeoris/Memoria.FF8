namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private BattleAttackCommand PrepareNativeGraphicsAction(BattleCombatant actor, BattleAttackCommand command)
    {
        if (command.NativeHeader is not null)
        {
            return command;
        }
            return command;
    }

    private void CompleteNativeGraphicsAction()
    {
    }
}
