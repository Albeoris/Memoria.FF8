using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    public PlayerActionResult ReadMagazinePage(PlayerAction action, NativeMagazinePage page)
    {
        if (action.Value is < 1 or > 198 || action.Quantity is null or < 0 or >= 69 || action.Frames != 1 || action.Horizontal != 0 || action.Vertical != 0 || action.CameraHorizontal != 0 || action.Text is not null || page.ItemId != action.Value || page.PageId != action.Quantity)
        {
            throw new ArgumentException("Magazine input requires its owned original item/page identity.", nameof(action));
        }
        if (HasPendingBattle?.Invoke() == true || CurrentDialogue is not null)
        {
            return new PlayerActionResult(false, new PlayerTrigger(HasPendingBattle?.Invoke() == true ? PlayerTriggerKind.Battle : PlayerTriggerKind.Dialogue, session.State.FieldId, State.Frame, CurrentDialogue?.MessageId, CurrentDialogue?.Text), 0, "The current world interaction suspends magazine viewing.");
        }
        var next = page.View(session.State.Economy ?? throw new NotSupportedException("Magazine viewing requires authentic saved inventory."), session.State.WeaponRecipes, session.State.LimitProgress);
        session.SetNativePartyMenuOwners(session.State.CharacterVitals!, session.State.Economy!, session.State.ShotAvailability!, session.State.SalaryPresentation!, weaponRecipes: next.Recipes, limitProgress: next.Limits);
        return new PlayerActionResult(true, new PlayerTrigger(PlayerTriggerKind.WorldMap, session.State.FieldId, State.Frame, null, null), 0);
    }
}
