using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

/// <summary>The reached4E3090 save-panel WORD16 and fade WORD70; save writes are not implemented.</summary>
public sealed record NativeSavePanelState([property: JsonRequired] int NativeState, [property: JsonRequired] short Fade70);

public sealed partial class EventEngine
{
    private void StepSaveMenuOpening(NativePartyMenuState current)
    {
        if (current.SavePanel is null)
        {
            var prepared = PreparePartyMenuEntry(NativeFieldMenuKind.Normal);
            PublishPartyMenuCallback(prepared.Vitals, prepared.Economy, prepared.Shots, prepared.Salary, clearControllerRequests: true);
            //4E6740 constructs the panel with fade0 and calls4E3090 state0, which selects state1.
            partyMenu = current with { Callbacks = checked(current.Callbacks + 1), SavePanel = new NativeSavePanelState(1, 0) };
            return;
        }
        NativeSavePanelState panel = current.SavePanel;
        short fade = panel.NativeState == 1 ? (short)Math.Min(4096, panel.Fade70 + 409) : panel.Fade70;
        int next = panel.NativeState == 11 ? 15 : fade == 4096 ? 11 : 1;
        PublishPartyMenuCallback(services.CharacterVitals!, services.Economy!, services.ShotAvailability!, services.SalaryPresentation!.Advance(1), advanceVibration: true);
        partyMenu = current with { Phase = next == 15 ? NativePartyMenuPhase.Active : NativePartyMenuPhase.Entering, Callbacks = checked(current.Callbacks + 1), SavePanel = new NativeSavePanelState(next, fade) };
    }

    private void StepSaveMenuClosing(NativePartyMenuState current)
    {
        NativeSavePanelState panel = current.SavePanel!;
        int fade = panel.Fade70 - 409;
        //State82 sets83 and falls through to the same decrement.4BDAC0 runs only below0.
        bool complete = fade < 0;
        PublishPartyMenuCallback(services.CharacterVitals!, services.Economy!, services.ShotAvailability!, services.SalaryPresentation!.Advance(1), advanceVibration: true);
        partyMenu = current with { Phase = complete ? NativePartyMenuPhase.Teardown : NativePartyMenuPhase.Closing, Callbacks = checked(current.Callbacks + 1), SavePanel = new NativeSavePanelState(83, (short)Math.Max(0, fade)) };
    }

    private static void ValidateSaveMenuCheckpoint(NativePartyMenuState menu)
    {
        if (menu.Kind != NativeFieldMenuKind.Save)
        {
            if (menu.SavePanel is not null)
            {
                throw new InvalidDataException("A non-save menu contains save-panel state.");
            }
            return;
        }
        NativeSavePanelState? panel = menu.SavePanel;
        bool valid = menu.Phase switch
        {
            NativePartyMenuPhase.Requested => panel is null,
            NativePartyMenuPhase.Entering when panel is null => menu.Callbacks == 1,
            NativePartyMenuPhase.Entering => panel is { NativeState: 1, Fade70: >= 0 and < 4096 } && panel.Fade70 % 409 == 0 && menu.Callbacks == 2 + panel.Fade70 / 409 || panel is { NativeState: 11, Fade70: 4096 } && menu.Callbacks == 13,
            NativePartyMenuPhase.Active => panel is { NativeState: 15, Fade70: 4096 } && menu.Callbacks >= 14,
            NativePartyMenuPhase.Closing => panel is { NativeState: 82, Fade70: 4096 } or { NativeState: 83, Fade70: > 0 and < 4096 } && (4096 - panel.Fade70) % 409 == 0,
            _ => panel is { NativeState: 83, Fade70: 0 }
        };
        if (!valid || menu.TemporaryCombatParty is not null || menu.TemporaryReserveParty is not null || menu.EligibleMask != 0 || menu.LockedMask != 0 || menu.ForcedFieldMask != 0)
        {
            throw new InvalidDataException("Saved native save panel changed its executed opening/Cancel state or fade counter.");
        }
    }
}
