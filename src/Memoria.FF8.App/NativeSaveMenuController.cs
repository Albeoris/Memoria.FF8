using Memoria.FF8.Events;

namespace Memoria.FF8;

internal sealed class NativeSaveMenuController
{
    private GameWorld? ownerWorld;
    private (int Field, long Sequence, int Operation)? owner;
    private bool presented;
    private bool completed;

    public bool Synchronize(GameWorld? world, PlayerSaveMenu panel, Action open)
    {
        NativePartyMenuState? menu = world?.Events.PartyMenu;
        (int Field, long Sequence, int Operation)? current = menu is { Kind: NativeFieldMenuKind.Save } ? (menu.FieldId, menu.OwnerSequence, menu.Operation) : null;
        bool abandoned = false;
        if (!ReferenceEquals(ownerWorld, world) || owner != current)
        {
            if (presented && panel.IsOpen && panel.Mode == PlayerSaveMenuMode.Save)
            {
                panel.Close();
                abandoned = true;
            }
            ownerWorld = world;
            owner = current;
            presented = false;
            completed = false;
        }
        if (menu is not { Kind: NativeFieldMenuKind.Save, Phase: NativePartyMenuPhase.Active } || completed)
        {
            return abandoned;
        }
        if (!presented)
        {
            if (panel.IsOpen)
            {
                return abandoned;
            }
            open();
            presented = panel.IsOpen && panel.Mode == PlayerSaveMenuMode.Save;
            return presented || abandoned;
        }
        if (panel.IsOpen)
        {
            return false;
        }
        PlayerActionResult result = world!.Apply(new PlayerAction(PlayerActionKind.Cancel));
        if (!result.Accepted)
        {
            throw new NotSupportedException(result.Detail);
        }
        completed = true;
        return true;
    }
}

internal sealed partial class RecreateGame
{
    private readonly NativeSaveMenuController nativeSaveMenu = new();

    private bool SynchronizeNativeSaveMenu()
    {
        if (!nativeSaveMenu.Synchronize(world, saveMenu, () => OpenPlayerSaveMenu(PlayerSaveMenuMode.Save)))
        {
            return false;
        }
        fieldSeconds = 0;
        drainUiButtons = true;
        return true;
    }
}
