using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public Func<int, NativeFieldItem?>? ResolveNativeFieldItem { get; set; }
    public Func<NativeCharacterVitals, int, BattleNativePlayerStatRules>? ResolveNativeFieldItemStatRules { get; set; }

    private bool ApplyFieldItemAction(PlayerAction action)
    {
        bool activeMenu = partyMenu is { Kind: NativeFieldMenuKind.Normal, Phase: NativePartyMenuPhase.Active };
        if (!activeMenu && (partyMenu is not null || !CanAcceptFieldMovement || CurrentDialogue is not null || !Scene.MenuEnabled))
        {
            return false;
        }
        NativeCharacterVitals vitals = services.CharacterVitals ?? throw new NotSupportedException("Field item use requires authentic saved characters.");
        NativeEconomy economy = services.Economy ?? throw new NotSupportedException("Field item use requires authentic saved inventory.");
        NativeFieldItem item = ResolveNativeFieldItem?.Invoke(action.Value) ?? throw new NotSupportedException("Field item use requires original installed item metadata.");
        NativeInputConfiguration input = services.InputConfiguration ?? throw new NotSupportedException("Field items require the actual saved menu context flags.");
        bool restAllowed = (((activeMenu ? partyMenu!.NativeEntrySelection : services.WorldControls?.Menu209) ?? 0) & 1) != 0;
        NativeFieldItemContext context = new(restAllowed, (byte)(input.Options >> 24));
        BattleNativePlayerStatRules? rules = item.Type == 20 ? ResolveNativeFieldItemStatRules?.Invoke(vitals, action.Quantity!.Value) : null;
        NativeLimitProgress? progress = null;
        (NativeCharacterVitals Vitals, NativeEconomy Economy, bool Changed) next;
        if (item.Type == 19)
        {
            var learned = item.LearnBlueMagic(vitals, economy, services.LimitProgress ?? throw new NotSupportedException("Blue Magic items require saved Limit progression."), action.Value, action.Quantity!.Value);
            next = (vitals, learned.Economy, learned.Changed);
            progress = learned.Progress;
        }
        else
        {
            if (action.Text is not null && item.Type != 18)
            {
                throw new ArgumentException("Only Amnesia Greens accept an ability-selection operand.");
            }
            next = item.Use(vitals, economy, action.Value, action.Quantity!.Value, rules, action.Text is null ? null : int.Parse(action.Text, System.Globalization.CultureInfo.InvariantCulture), context);
        }
        if (activeMenu)
        {
            PublishPartyMenuCallback(next.Vitals, next.Economy, services.ShotAvailability!, services.SalaryPresentation!.Advance(1), advanceVibration: true, limitProgress: progress);
            partyMenu = partyMenu! with { Callbacks = checked(partyMenu.Callbacks + 1) };
        }
        else
        {
            services.SetNativePartyMenuOwners(next.Vitals, next.Economy, services.ShotAvailability!, services.SalaryPresentation!, limitProgress: progress);
        }
        return true;
    }
}
