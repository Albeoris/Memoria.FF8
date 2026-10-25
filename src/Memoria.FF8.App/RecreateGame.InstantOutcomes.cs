using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private bool CanCompleteInstantOutcome => world?.ActiveTripleTriad is { } cards ? cards.CanCompleteInstantly : CanCompleteBattleInstantly(ActiveBattle);

    internal static bool CanCompleteBattleInstantly(BattleSession? battle)
    {
        return battle is { Outcome: BattleOutcome.Active, PendingSlotsDeal: null } && battle.ReadyPlayerCommandOrder.Count > 0;
    }

    private void CompleteInstantOutcome(bool victory)
    {
        if (!CanCompleteInstantOutcome)
        {
            return;
        }
        if (world?.ActiveTripleTriad is { } cards)
        {
            cards.CompleteInstantly(victory);
        }
        else
        {
            ApplyBattleCommand(new BattleInput(victory ? BattleInputKind.InstantVictory : BattleInputKind.InstantDefeat));
        }
        gameMenus?.Close();
    }
}
