using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

internal sealed partial class NativeTutorialPlayer
{
    private static readonly NativeWorldControlState SwitchLessonControls = NativeWorldControlState.CreateNewGame().WithPartyMenuEnabled(1);

    private NativeCharacterVitals PrepareSwitchLessonParty(NativeCharacterVitals source)
    {
        // The lesson installs combat slots independently of the roster flags. Enroll its displayed rows in the isolated normal-menu party.
        NativeCharacterVital[] records = source.Records.ToArray();
        foreach (int row in visibleRows)
        {
            records[row] = records[row] with { MemberFlags = (ushort)(records[row].MemberFlags | 1) };
        }
        return source with { Records = records };
    }

    private void InitializeSwitchLessonNavigation()
    {
        Menu.Open("main");
        Menu.SelectEntry("main", "switch");
        Menu.Navigate(MenuInput.Confirm);
        Menu.SelectEntry("switch", "switch-party");
        Menu.Navigate(MenuInput.Confirm);
    }

    private NativeCharacterVitals ApplySwitchLessonPartyAction(MenuAction action)
    {
        return NativePartySwitchInput.Apply(party, NativePartySwitchMenuProjection.PlayerAction(action), SwitchLessonControls.Menu209);
    }

    private string? SwitchLessonMessage() => textId switch
    {
        3 => Menu.SystemText("tutorial.switch.current.party", "Choose Party to change the active members."),
        16 => Menu.SystemText("tutorial.switch.current.exchange", "Junction Exchange also asks for a source character and a destination."),
        19 => Menu.SystemText("tutorial.switch.current.confirm", "Confirm the destination to exchange their junctions."),
        20 => Menu.SystemText("tutorial.switch.current.hp-warning", "If the exchange lowers maximum HP, the menu asks you to confirm."),
        21 => Menu.SystemText("tutorial.switch.current.hp-result", "The menu applies the new HP limit after confirmation."),
        _ => null
    };

    private void RestoreSwitchLessonNavigation(NativeTutorialSnapshot saved)
    {
        MenuNavigationState navigation = saved.Navigation;
        string? legacySource = saved.Settings.GetValueOrDefault("Exchange source");
        string? legacyTarget = saved.Settings.GetValueOrDefault("Exchange target");
        string[] history = navigation.ScreenId switch
        {
            "switch" => ["main"],
            "switch-party" or "switch-junction" => ["main", "switch"],
            "switch-party-target" => ["main", "switch", "switch-party"],
            "switch-junction-target" => ["main", "switch", "switch-junction"],
            "switch-junction-confirm" => ["main", "switch", "switch-junction", "switch-junction-target"],
            _ => navigation.History
        };
        if (navigation.History is null || navigation.Selections is null || !navigation.History.SequenceEqual(history.TakeLast(navigation.History.Length)))
        {
            throw new InvalidDataException("Saved Switch lesson menu history is inconsistent.");
        }
        if (navigation.ScreenId != "switch-junction-confirm")
        {
            if (navigation.Selections.ContainsKey("switch-junction-confirm"))
            {
                Menu.RegisterJunctionExchangeConfirmation();
            }
            Menu.RestoreNavigation(navigation with { History = history });
            return;
        }

        Dictionary<string, int> selections = new(navigation.Selections, StringComparer.Ordinal);
        bool hadChoice = selections.Remove("switch-junction-confirm", out int choice);
        if (choice is < 0 or > 1 || legacyTarget is not null && legacySource != navigation.ExchangeId)
        {
            throw new InvalidDataException("Saved Switch lesson exchange confirmation is inconsistent.");
        }
        Menu.RestoreNavigation(navigation with { ScreenId = "switch-junction-target", History = history[..^1], Selections = selections });
        string target = legacyTarget ?? Menu.SelectedEntry?.Id ?? throw new InvalidDataException("Saved Switch lesson exchange target is missing.");
        Menu.SelectEntry("switch-junction-target", target);
        if (!Menu.RestoreJunctionExchangeConfirmation(target))
        {
            throw new InvalidDataException("Saved Switch lesson exchange no longer requires a confirmation.");
        }
        bool yes = hadChoice && choice == (legacyTarget is null ? 1 : 0);
        Menu.SelectEntry("switch-junction-confirm", yes ? "yes" : "no");
    }
}
