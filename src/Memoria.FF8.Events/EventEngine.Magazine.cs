using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public Func<int, int, NativeMagazinePage>? ResolveNativeMagazinePage { get; set; }

    private bool ReadMagazinePage(PlayerAction action)
    {
        bool activeMenu = partyMenu is { Kind: NativeFieldMenuKind.Normal, Phase: NativePartyMenuPhase.Active };
        if (!activeMenu && (partyMenu is not null || !CanAcceptFieldMovement || CurrentDialogue is not null || !Scene.MenuEnabled))
        {
            return false;
        }
        NativeMagazinePage page = ResolveNativeMagazinePage?.Invoke(action.Value, action.Quantity!.Value) ?? throw new NotSupportedException("Magazine viewing requires the original item/page metadata.");
        var next = page.View(services.Economy!, services.WeaponRecipes, services.LimitProgress);
        if (activeMenu)
        {
            PublishPartyMenuCallback(services.CharacterVitals!, services.Economy!, services.ShotAvailability!, services.SalaryPresentation!.Advance(1), advanceVibration: true, weaponRecipes: next.Recipes, limitProgress: next.Limits);
            partyMenu = partyMenu! with { Callbacks = checked(partyMenu.Callbacks + 1) };
        }
        else
        {
            services.SetNativePartyMenuOwners(services.CharacterVitals!, services.Economy!, services.ShotAvailability!, services.SalaryPresentation!, weaponRecipes: next.Recipes, limitProgress: next.Limits);
        }
        return true;
    }
}
