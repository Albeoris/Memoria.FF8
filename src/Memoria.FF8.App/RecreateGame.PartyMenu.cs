using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private int partyMenuCandidate;

    private void UpdatePartyMenu(TimeSpan elapsed, InputFrame input)
    {
        if (events!.PartyMenu!.Phase == NativePartyMenuPhase.Active)
        {
            if (events.PartyMenu.Kind is NativeFieldMenuKind.Normal or NativeFieldMenuKind.Shop or NativeFieldMenuKind.WeaponShop && input.Cancel)
            {
                world!.Apply(new PlayerAction(PlayerActionKind.Cancel));
                fieldSeconds = 0;
                return;
            }
            if (events.PartyMenu.Kind == NativeFieldMenuKind.PartySelection)
            {
                if (input.Cancel && events.PartyMenu.SelectedPartySlot is not null)
                {
                    world!.Apply(new PlayerAction(PlayerActionKind.Cancel));
                    partyMenuCandidate = 0;
                    fieldSeconds = 0;
                    return;
                }
                IReadOnlyList<PlayerAction> choices = NativePartyMenuProjection.Inputs(events.PartyMenu);
                if (choices.Count > 0)
                {
                    partyMenuCandidate = Math.Clamp(partyMenuCandidate + input.MenuVertical, 0, choices.Count - 1);
                }
                if (input.Confirm && choices.Count > 0)
                {
                    world!.Apply(choices[partyMenuCandidate]);
                    partyMenuCandidate = 0;
                    fieldSeconds = 0;
                    return;
                }
            }
        }
        fieldSeconds += options.ControlStep ? 0 : elapsed.TotalSeconds;
        while (fieldSeconds + 1e-9 >= 1d / EventEngine.SimulationFps && events.PartyMenu is not null)
        {
            fieldSeconds -= 1d / EventEngine.SimulationFps;
            world!.Apply(new PlayerAction(PlayerActionKind.Wait));
        }
        if (events.PartyMenu is null)
        {
            fieldSeconds = 0;
            partyMenuCandidate = 0;
        }
    }

    private void DrawPartyMenu(Rectangle bounds)
    {
        DrawNativePartyUi(bounds);
    }
}
