using System.Globalization;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private Func<GameplayTuning>? readGameplayTuning;
    private Action<GameplayTuning>? writeGameplayTuning;
    private Func<string, string?>? gameplayTuningLock;
    private Func<string>? gameplayTuningStatus;
    private GameplayTuning gameplayTuningDraft = new();
    private int gameplayTuningPage;
    private string gameplayNumberField = nameof(GameplayTuning.DrawBonus);
    private string gameplayNumberDraft = "";
    private bool gameplayNumberReplace;

    internal bool GameplayTuningAvailable => readGameplayTuning is not null && writeGameplayTuning is not null && !IsFixtureContext;

    internal void ConnectGameplayTuning(Func<GameplayTuning> read, Action<GameplayTuning> write, Func<string, string?>? unavailableReason = null, Func<string>? status = null)
    {
        readGameplayTuning = read;
        writeGameplayTuning = write;
        gameplayTuningLock = unavailableReason;
        gameplayTuningStatus = status;
    }

    private static readonly string[][] GameplayTuningFields =
    [
        [nameof(GameplayTuning.DrawBonus), nameof(GameplayTuning.ApMultiplier), nameof(GameplayTuning.ExperienceMultiplier), nameof(GameplayTuning.GuardianExperienceMultiplier)],
        [nameof(GameplayTuning.UnlimitedMagic), nameof(GameplayTuning.MaximumDamage)],
        ["First", "Second", "Third", "Fourth", nameof(GameplayTuning.UnrestrictedScanRotation)]
    ];

    private static string GameplayTuningLabel(string field) => field switch
    {
        "DrawBonus" => "Draw bonus",
        "ApMultiplier" => "AP multiplier",
        "ExperienceMultiplier" => "Character EXP multiplier",
        "GuardianExperienceMultiplier" => "GF EXP multiplier",
        "UnlimitedMagic" => "Infinite magic",
        "MaximumDamage" => "Maximum damage",
        "UnrestrictedScanRotation" => "Free Scan rotation",
        "First" => "Rare Item Ã‚Â· Slot 1",
        "Second" => "Rare Item Ã‚Â· Slot 2",
        "Third" => "Rare Item Ã‚Â· Slot 3",
        "Fourth" => "Rare Item Ã‚Â· Slot 4",
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };

    private static bool IsRareItemField(string field) => field is "First" or "Second" or "Third" or "Fourth";
    private static bool IsGameplayToggle(string field) => field is "UnlimitedMagic" or "UnrestrictedScanRotation";
    private string? GameplayTuningLock(string field) => gameplayTuningLock?.Invoke(IsRareItemField(field) ? nameof(GameplayTuning.RareItem) : field);
    private decimal RareItemTotal => gameplayTuningDraft.RareItem.First + gameplayTuningDraft.RareItem.Second + gameplayTuningDraft.RareItem.Third + gameplayTuningDraft.RareItem.Fourth;
    private decimal GameplayTuningValue(string field) => field switch
    {
        "DrawBonus" => gameplayTuningDraft.DrawBonus,
        "ApMultiplier" => gameplayTuningDraft.ApMultiplier,
        "ExperienceMultiplier" => gameplayTuningDraft.ExperienceMultiplier,
        "GuardianExperienceMultiplier" => gameplayTuningDraft.GuardianExperienceMultiplier,
        "MaximumDamage" => gameplayTuningDraft.MaximumDamage,
        "First" => gameplayTuningDraft.RareItem.First,
        "Second" => gameplayTuningDraft.RareItem.Second,
        "Third" => gameplayTuningDraft.RareItem.Third,
        "Fourth" => gameplayTuningDraft.RareItem.Fourth,
        _ => 0
    };

    private string GameplayTuningDisplay(string field)
    {
        if (IsGameplayToggle(field))
        {
            bool enabled = field == "UnlimitedMagic" ? gameplayTuningDraft.UnlimitedMagic : gameplayTuningDraft.UnrestrictedScanRotation;
            return enabled ? "On" : "Off";
        }
        return GameplayTuningValue(field).ToString("0.######", CultureInfo.InvariantCulture) + (IsRareItemField(field) ? "%" : field.EndsWith("Multiplier", StringComparison.Ordinal) ? "Ãƒâ€”" : "");
    }

    private void RegisterGameplayTuningScreens()
    {
        Register(new MenuScreenDefinition
        {
            Id = "debug-gameplay-rules",
            Title = "Gameplay rules",
            Help = "Edit a draft, then Apply. Save the game to retain these rules.",
            Entries = c => c.GameplayTuningEntries(),
            BuildContent = (c, root) => root.Children.Add(c.BuildGameplayTuning())
        });
        Register(new MenuScreenDefinition
        {
            Id = "debug-gameplay-number",
            Title = "Enter value",
            Help = "Type a number or use the keypad. Accept stages it; Cancel keeps the previous value.",
            Columns = 4,
            Entries = c => c.GameplayNumberEntries(),
            BuildContent = (c, root) => root.Children.Add(c.BuildGameplayNumber())
        });
    }

    private IReadOnlyList<MenuEntry> GameplayTuningEntries()
    {
        List<MenuEntry> rows = [];
        foreach (string field in GameplayTuningFields[gameplayTuningPage])
        {
            string? locked = GameplayTuningLock(field);
            string help = locked ?? (field == "UnlimitedMagic" ? "Cast without consuming magic stock or cooldown costs. Takes precedence over Limited magic." : IsRareItemField(field) ? "Percentage from 0 to 100. All four slots must total exactly 100%." : field == "UnrestrictedScanRotation" ? "Allow rotation of every scanned target." : field == "MaximumDamage" ? "Cap all positive HP damage, including attacks that break the normal limit: 0â€“100000. Healing and instant KO are unchanged." : "Whole number from 0 to 100. Zero disables this bonus or reward.");
            rows.Add(new MenuEntry(field, GameplayTuningLabel(field) + "   " + GameplayTuningDisplay(field) + (locked is null ? "" : " Ã‚Â· Locked"), help, Action: new MenuAction("gameplay-rule-edit", field), Enabled: GameplayTuningAvailable && locked is null));
        }
        rows.Add(new("previous", "Previous page", "Keep the draft and show the previous group.", Action: new("gameplay-rule-page", Value: "-1")));
        rows.Add(new("next", "Next page", "Keep the draft and show the next group.", Action: new("gameplay-rule-page", Value: "1")));
        rows.Add(new("apply", "Apply", RareItemTotal == 100m ? "Apply this draft to the current game. Save normally to retain it." : "Rare Item slots must total exactly 100% before applying.", Action: new("gameplay-rule-apply"), Enabled: GameplayTuningAvailable && RareItemTotal == 100m));
        rows.Add(new("cancel", "Cancel", "Discard the draft.", Action: new("menu-back")));
        return rows;
    }

    private UiElement BuildGameplayTuning()
    {
        UiGrid page = Grid([1.1f, 0.55f, 6.3f, 1.4f], [1], 6);
        Place(page, Header(), 0);
        string title = new[] { "1 / 3 Ã‚Â· Rewards & Draw", "2 / 3 Ã‚Â· Magic & damage", "3 / 3 Ã‚Â· Rare Item & Scan" }[gameplayTuningPage];
        Place(page, Panel(null, Label(title, 21)), 1);
        Place(page, Panel(null, Commands(36, 21)), 2);
        string total = "Rare Item total: " + RareItemTotal.ToString("0.######", CultureInfo.InvariantCulture) + "%" + (RareItemTotal == 100m ? "" : " Ã‚Â· Must equal 100%");
        Place(page, Panel(null, Label(total + "\n" + (gameplayTuningStatus?.Invoke() ?? "Current game only. Save normally to retain changes. Battle changes may wait until the next battle."), 17, UiTextFlow.Wrap)), 3);
        return page;
    }

    private IReadOnlyList<MenuEntry> GameplayNumberEntries()
    {
        string[] keys = ["7", "8", "9", "Clear", "4", "5", "6", "Delete", "1", "2", "3", ".", "0", "Accept", "Cancel"];
        return keys.Select(key => new MenuEntry(key, key, key == "Accept" ? "Stage this value in the draft." : "Enter value using the keyboard or keypad.", Action: key == "Cancel" ? new("menu-back") : new("gameplay-rule-number", Value: key), Enabled: key != "." || IsRareItemField(gameplayNumberField))).ToArray();
    }

    private UiElement BuildGameplayNumber()
    {
        UiGrid page = Grid([1.1f, 1, 5, 0.6f], [1], 8);
        Place(page, Header(), 0);
        Place(page, Panel(null, Label(GameplayTuningLabel(gameplayNumberField) + "   " + gameplayNumberDraft, 28)), 1);
        UiGrid keypad = Grid([1, 1, 1, 1], [1, 1, 1, 1], 8);
        for (int index = 0; index < Entries.Count; index++)
        {
            int selected = index;
            MenuEntry entry = Entries[index];
            UiButton button = new() { Id = entry.Id, Text = entry.Label, FontSize = 25 * TextScale, Enabled = entry.Enabled, FocusChanged = focused => SelectConfigRow(focused, selected), Click = () => Activate(selected) };
            Place(keypad, button, index / 4, index % 4);
        }
        Place(page, Panel(null, keypad), 2);
        Place(page, Panel(null, Label(IsRareItemField(gameplayNumberField) ? "0Ã¢â‚¬â€œ100%, up to six decimal places. Total must equal 100%." : gameplayNumberField == "MaximumDamage" ? "Whole number: 0Ã¢â‚¬â€œ100000" : "Whole number: 0Ã¢â‚¬â€œ100", 17)), 3);
        return page;
    }

    public bool EnterGameplayNumberCharacter(char character)
    {
        if (!IsOpen || ScreenId != "debug-gameplay-number")
        {
            return false;
        }
        if (character == '\b')
        {
            EditGameplayNumber("Delete");
        }
        else if (character is >= '0' and <= '9' || character == '.')
        {
            EditGameplayNumber(character.ToString());
        }
        return true;
    }

    private void EditGameplayNumber(string key)
    {
        if (key == "Clear")
        {
            gameplayNumberDraft = "";
        }
        else if (key == "Delete")
        {
            gameplayNumberDraft = gameplayNumberDraft.Length > 0 ? gameplayNumberDraft[..^1] : "";
        }
        else
        {
            string next = gameplayNumberReplace ? "" : gameplayNumberDraft;
            if (next.Length < 12 && (key != "." || !next.Contains('.')))
            {
                gameplayNumberDraft = next + key;
            }
        }
        gameplayNumberReplace = false;
        Message = "";
        _view = null;
    }

    private bool ExecuteGameplayTuningAction(MenuAction action)
    {
        if (!action.Kind.StartsWith("gameplay-rule-", StringComparison.Ordinal))
        {
            return false;
        }
        if (!GameplayTuningAvailable)
        {
            return true;
        }
        if (action.Kind == "gameplay-rule-open")
        {
            gameplayTuningDraft = readGameplayTuning!();
            gameplayTuningPage = 0;
            Push("debug-gameplay-rules");
        }
        else if (ScreenId == "debug-gameplay-rules")
        {
            if (action.Kind == "gameplay-rule-page")
            {
                gameplayTuningPage = (gameplayTuningPage + (action.Value == "-1" ? 2 : 1)) % 3;
                Select(0, Entries.Count);
            }
            else if (action.Kind == "gameplay-rule-edit" && GameplayTuningFields[gameplayTuningPage].Contains(action.SubjectId) && GameplayTuningLock(action.SubjectId) is null)
            {
                if (IsGameplayToggle(action.SubjectId))
                {
                    gameplayTuningDraft = action.SubjectId == "UnlimitedMagic" ? gameplayTuningDraft with { UnlimitedMagic = !gameplayTuningDraft.UnlimitedMagic } : gameplayTuningDraft with { UnrestrictedScanRotation = !gameplayTuningDraft.UnrestrictedScanRotation };
                }
                else
                {
                    gameplayNumberField = action.SubjectId;
                    gameplayNumberDraft = GameplayTuningValue(action.SubjectId).ToString("0.######", CultureInfo.InvariantCulture);
                    gameplayNumberReplace = true;
                    Push("debug-gameplay-number");
                }
            }
            else if (action.Kind == "gameplay-rule-apply")
            {
                try
                {
                    gameplayTuningDraft.Validate();
                    writeGameplayTuning!(gameplayTuningDraft);
                    Back();
                    _liveData = null;
                    Message = "Gameplay rules applied. Save the game to retain them.";
                }
                catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or ArgumentException)
                {
                    Message = exception.Message;
                }
            }
        }
        else if (ScreenId == "debug-gameplay-number" && action.Kind == "gameplay-rule-number")
        {
            if (action.Value == "Accept")
            {
                AcceptGameplayNumber();
            }
            else if (action.Value is "Clear" or "Delete" or "." || action.Value is { Length: 1 } digit && digit[0] is >= '0' and <= '9')
            {
                EditGameplayNumber(action.Value!);
            }
        }
        return true;
    }

    private void AcceptGameplayNumber()
    {
        bool rare = IsRareItemField(gameplayNumberField);
        if (GameplayTuningLock(gameplayNumberField) is string reason)
        {
            Message = reason;
            return;
        }
        int maximum = gameplayNumberField == "MaximumDamage" ? 100000 : 100;
        int decimalPlaces = gameplayNumberDraft.Contains('.') ? gameplayNumberDraft.Length - gameplayNumberDraft.IndexOf('.') - 1 : 0;
        if (!decimal.TryParse(gameplayNumberDraft, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value) || value < 0 || value > maximum || decimalPlaces > (rare ? 6 : 0))
        {
            Message = rare ? "Enter 0Ã¢â‚¬â€œ100 with up to six decimal places." : $"Enter a whole number from 0 to {maximum}.";
            return;
        }
        gameplayTuningDraft = gameplayNumberField switch
        {
            "DrawBonus" => gameplayTuningDraft with { DrawBonus = (int)value },
            "ApMultiplier" => gameplayTuningDraft with { ApMultiplier = (int)value },
            "ExperienceMultiplier" => gameplayTuningDraft with { ExperienceMultiplier = (int)value },
            "GuardianExperienceMultiplier" => gameplayTuningDraft with { GuardianExperienceMultiplier = (int)value },
            "MaximumDamage" => gameplayTuningDraft with { MaximumDamage = (int)value },
            "First" => gameplayTuningDraft with { RareItem = gameplayTuningDraft.RareItem with { First = value } },
            "Second" => gameplayTuningDraft with { RareItem = gameplayTuningDraft.RareItem with { Second = value } },
            "Third" => gameplayTuningDraft with { RareItem = gameplayTuningDraft.RareItem with { Third = value } },
            "Fourth" => gameplayTuningDraft with { RareItem = gameplayTuningDraft.RareItem with { Fourth = value } },
            _ => gameplayTuningDraft
        };
        Back();
    }
}

