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

    private void RemoveNativeUiReady(BattleCombatant actor)
    {
    }

    private NativeBattleGraphicsState? PrepareNativeUiReadyRemoval(BattleCombatant actor)
    {
        return ((NativeBattleGraphicsState?)null);
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



    private void DispatchNativeTriggerReceipt()
    {
        if (expandedPresentationProcess is { } process)
        {
            if (process.PendingTrigger is { } hostedTrigger && expandedHostedEvent?.Renzokuken is not null && CurrentExpandedHostedInvocation?.Header.Command == 5)
            {
                DispatchExpandedHostedRenzokukenTrigger(process, hostedTrigger);
            }
            else
            {
                DispatchExpandedGunbladeTrigger(process);
            }
            return;
        }
            return;
    }


}
