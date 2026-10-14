using Memoria.FF8.Events;
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
            IReadOnlyList<byte> candidates = events.PartyMenuCandidates;
            if (candidates.Count > 0)
            {
                partyMenuCandidate = Math.Clamp(partyMenuCandidate + input.MenuVertical, 0, candidates.Count - 1);
            }
            if (input.Confirm && events.PartyMenu.Kind == NativeFieldMenuKind.PartySelection)
            {
                bool full = events.PartyMenu.TemporaryCombatParty!.All(row => row != 255);
                world!.Apply(full ? new PlayerAction(PlayerActionKind.Confirm) : new PlayerAction(PlayerActionKind.SelectPartyMember, candidates.Count > 0 ? candidates[partyMenuCandidate] : -1));
                fieldSeconds = 0;
                return;
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
