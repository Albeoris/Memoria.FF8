using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private bool NavigateAbilityJunction(MenuInput input)
    {
        if (ScreenId is not ("junction-command" or "junction-ability") || input is not (MenuInput.Up or MenuInput.Down))
        {
            return false;
        }
        int next = SelectedIndex + (input == MenuInput.Up ? -1 : 1);
        if (next >= 0 && next < Entries.Count)
        {
            return false;
        }
        string destination = ScreenId == "junction-command" ? "junction-ability" : "junction-command";
        SwitchAbilitySlots(destination, input == MenuInput.Down ? 0 : _screens[destination].Entries(this).Count - 1);
        SoundRequested?.Invoke(MenuSound.Move);
        return true;
    }

    private void SwitchAbilitySlots(string screen, int index)
    {
        ScreenId = screen;
        _selections[screen] = index;
        Message = "";
        _view = null;
        ScreenOpened?.Invoke(ScreenId);
    }

    private UiElement BuildAbilityJunction()
    {
        bool choosing = ScreenId.EndsWith("-choice", StringComparison.Ordinal);
        bool command = ScreenId is "junction-command" or "junction-command-choice";
        var page = Grid([1.45f, 4.5f], [1], 4);
        var tabs = new UiGrid { Columns = 4, Gap = 0 };
        string[] names = ["Junction", "Off", "Auto", "Ability"];
        string[] destinations = ["junction-options", "junction-off", "junction-auto", "junction-command"];
        for (int index = 0; index < names.Length; index++)
        {
            string destination = destinations[index];
            tabs.Children.Add(new UiButton { Text = names[index], FontSize = 24 * TextScale, DrawBackground = false, HighlightSelection = false, Click = () => Push(destination) });
        }
        Place(page, NativeMenuHeader("Junction", tabs), 0);
        var body = Grid([1], [1.08f, 1], 4);
        Place(page, body, 1);
        var slots = Grid([0.6f, 2.4f, 2.4f], [1], 4);
        Place(body, slots, 0);
        var identity = Grid([1], [0.6f, 3], 10);
        Place(identity, Portrait(Character), 0);
        Place(identity, Label(Character?.Name ?? "--", 28), 0, 1);
        Place(slots, identity, 0);
        UiElement commands;
        if (Data.SmartAttack)
        {
            commands = AbilitySlots("junction-command", choosing);
        }
        else
        {
            var nativeCommands = Grid([1, 3], [1], 0);
            nativeCommands.RowDefinitions[0] = UiGridLength.Pixel(31 * TextScale);
            UiLabel attack = Label(Character?.Commands.FirstOrDefault() ?? "Attack", 25);
            attack.Margin = new UiThickness(35 * TextScale, 0, 0, 0);
            Place(nativeCommands, attack, 0);
            Place(nativeCommands, AbilitySlots("junction-command", choosing), 1);
            commands = nativeCommands;
        }
        UiPanel commandPanel = Panel("COMMAND", commands);
        commandPanel.Id = "junction-command-slots";
        Place(slots, commandPanel, 1);
        UiPanel passivePanel = Panel("ABILITY", AbilitySlots("junction-ability", choosing));
        passivePanel.Id = "junction-passive-slots";
        Place(slots, passivePanel, 2);
        UiList candidates;
        if (choosing)
        {
            candidates = Commands(31, 25);
        }
        else
        {
            IReadOnlyList<MenuEntry> entries = _screens[command ? "junction-command-choice" : "junction-ability-choice"].Entries(this);
            candidates = new UiList { TextFlow = UiTextFlow.SingleLine, Items = entries.Select(entry => entry.Label).ToList(), Images = entries.Select(EntryImage).ToList(), EnabledItems = entries.Select(entry => entry.Enabled).ToList(), RowHeight = 31 * TextScale, FontSize = 25 * TextScale, TextInset = 35 * TextScale, Paginated = true, ShowSelection = false, Focusable = false };
        }
        UiPanel choices = Panel("ABILITY", candidates);
        choices.Id = "junction-ability-choices";
        Place(body, choices, 0, 1);
        return page;
    }

    private UiList AbilitySlots(string screen, bool choosing)
    {
        if (!choosing && ScreenId == screen)
        {
            return Commands(31, 25);
        }
        IReadOnlyList<MenuEntry> entries = _screens[screen].Entries(this);
        return new UiList
        {
            TextFlow = UiTextFlow.SingleLine,
            Id = screen + "-preview",
            Items = entries.Select(entry => entry.Label).ToList(),
            Images = entries.Select(EntryImage).ToList(),
            RowHeight = 31 * TextScale,
            FontSize = 25 * TextScale,
            TextInset = 35 * TextScale,
            ShowSelection = false,
            Focusable = !choosing,
            Confirmed = index =>
            {
                SwitchAbilitySlots(screen, index);
                Activate(index);
            }
        };
    }
}
