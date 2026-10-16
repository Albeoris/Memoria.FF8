using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private PendingEntityName? queuedEntityName;
    private bool returnToItemMenuAfterNaming;

    private bool SynchronizeQueuedEntityName()
    {
        if (gameMenus is null || world is null)
        {
            return false;
        }
        if (world.PendingEntityName is not { } pending)
        {
            if (returnToItemMenuAfterNaming)
            {
                returnToItemMenuAfterNaming = false;
                gameMenus.Open("item-use", false);
            }
            return false;
        }
        if (queuedEntityName?.Subject == pending.Subject && queuedEntityName.Source == pending.Source && gameMenus.IsNamePrompt)
        {
            return false;
        }
        queuedEntityName = pending;
        suppressNativeNameInput = true;
        gameMenus.OpenNamePrompt(pending.Subject, pending.Draft ?? pending.DefaultName, name =>
        {
            world.CompleteEntityName(name);
            returnToItemMenuAfterNaming |= pending.Source.StartsWith("item:", StringComparison.Ordinal);
            queuedEntityName = null;
            fieldSeconds = 0;
            drainUiButtons = true;
        });
        fieldSeconds = 0;
        return true;
    }
}
