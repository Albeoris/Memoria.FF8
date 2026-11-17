namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{

    private bool WaitingForTurnBasedDecision => !UsesRealTimeBattleClock && combatants.Any(actor => actor.IsPlayer && actor.IsReady);

    internal static NativeBattleUiServices ApplyNativeReadyMenuSelection(NativeBattleUiServices services, bool selectMenu, bool slotsPending)
    {
        if (selectMenu && !slotsPending)
        {
            //4BB840 resets the command widget;4ADDB0's mismatched-owner path calls4A7160(0).
            return services.SetFlags(0, 0x10) with { Menu957 = 4 };
        }
        return services;
    }

    public void OpenNativeActionMenu()
    {
        Presentation?.SetActionMenu(true);
    }

    public void CloseNativeActionMenu()
    {
        RequireNoPendingSlotsDeal();
        Presentation?.SetActionMenu(false);
    }

}
