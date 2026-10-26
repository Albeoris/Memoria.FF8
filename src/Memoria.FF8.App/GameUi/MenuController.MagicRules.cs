namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private IReadOnlyList<string> magicRulesReview = [];
    private Action? applyMagicRules;
    private Action? cancelMagicRules;

    public void ShowMagicRulesReview(IReadOnlyList<string> lines, Action apply, Action? cancel = null)
    {
        magicRulesReview = lines.ToArray();
        applyMagicRules = apply;
        cancelMagicRules = cancel;
        Register(new MenuScreenDefinition
        {
            Id = "mode-review",
            Title = MagicText("ReviewTitle"),
            Help = MagicText("ReviewHelp"),
            Entries = _ => [new MenuEntry("apply", MagicText("Apply"), MagicText("ReviewHelp"), Action: new MenuAction("magic-rules-apply")), new MenuEntry("cancel", MagicText("Cancel"), MagicText("ReviewHelp"), Action: new MenuAction("magic-rules-cancel"))],
            Details = _ => magicRulesReview
        });
        Push("mode-review");
    }

    internal IReadOnlyList<MenuEntry> CommandSlotEntries()
    {
        string mode = Character?.AttackMode == "SmartMagic" ? MagicText("SmartMagic") : MagicText("PhysicalOnly");
        MenuEntry attack = new("attack", (Character?.Commands.FirstOrDefault() ?? SystemText("ui.commandslotentries.entry.label.attack", "Attack")) + " · " + mode, Data.SmartAttack ? MagicText("AttackChoiceHelp") : MagicText("SmartLocked"), Action: new MenuAction("attack-mode", Value: Character?.AttackMode == "SmartMagic" ? "PhysicalOnly" : "SmartMagic"), Enabled: Data.SmartAttack);
        return (Data.SmartAttack ? new[] { attack } : Array.Empty<MenuEntry>()).Concat(Enumerable.Range(1, 3).Select(slot => new MenuEntry(slot.ToString(), Character?.Commands.ElementAtOrDefault(slot) ?? "--", MagicText("CommandSlotHelp"), "junction-command-choice", new MenuAction("select-command-slot", slot.ToString())))).ToArray();
    }

    internal IReadOnlyList<MenuEntry> AutoJunctionEntries()
    {
        List<MenuEntry> entries = [];
        foreach (string priority in new[] { "Attack", "Magic", "Defense" })
        {
            entries.Add(new MenuEntry(priority, priority, MagicText("ConservativeHelp"), Action: new MenuAction("junction-auto", Value: priority)));
        }
        return entries;
    }

    private bool ExecuteMagicRulesAction(MenuAction action)
    {
        if (action.Kind == "magic-sort")
        {
            string? selected = SelectedEntry?.Id;
            sortMagicByName = true;
            int index = Entries.ToList().FindIndex(entry => entry.Id == selected);
            if (index >= 0)
            {
                _selections[ScreenId] = index;
            }
            _view = null;
            return true;
        }
        if (action.Kind is not ("magic-rules-apply" or "magic-rules-cancel"))
        {
            return false;
        }
        Action? apply = applyMagicRules;
        Action? cancel = cancelMagicRules;
        applyMagicRules = null;
        cancelMagicRules = null;
        if (action.Kind == "magic-rules-apply")
        {
            (apply ?? throw new InvalidOperationException("The magic migration preview is no longer active."))();
        }
        else
        {
            cancel?.Invoke();
        }
        _liveData = null;
        Back();
        return true;
    }
}
