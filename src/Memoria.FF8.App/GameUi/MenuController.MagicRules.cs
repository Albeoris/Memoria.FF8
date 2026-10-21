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
            Title = MagicUiText.Get("ReviewTitle"),
            Help = MagicUiText.Get("ReviewHelp"),
            Entries = _ => [new MenuEntry("apply", MagicUiText.Get("Apply"), MagicUiText.Get("ReviewHelp"), Action: new MenuAction("magic-rules-apply")), new MenuEntry("cancel", MagicUiText.Get("Cancel"), MagicUiText.Get("ReviewHelp"), Action: new MenuAction("magic-rules-cancel"))],
            Details = _ => magicRulesReview
        });
        Push("mode-review");
    }

    internal IReadOnlyList<MenuEntry> CommandSlotEntries()
    {
        string mode = Character?.AttackMode == "SmartMagic" ? MagicUiText.Get("SmartMagic") : MagicUiText.Get("PhysicalOnly");
        MenuEntry attack = new("attack", (Character?.Commands.FirstOrDefault() ?? "Attack") + " · " + mode, Data.SmartAttack ? MagicUiText.Get("AttackChoiceHelp") : MagicUiText.Get("SmartLocked"), Action: new MenuAction("attack-mode", Value: Character?.AttackMode == "SmartMagic" ? "PhysicalOnly" : "SmartMagic"), Enabled: Data.SmartAttack);
        return new[] { attack }.Concat(Enumerable.Range(1, 3).Select(slot => new MenuEntry(slot.ToString(), Character?.Commands.ElementAtOrDefault(slot) ?? "--", MagicUiText.Get("CommandSlotHelp"), "junction-command-choice", new MenuAction("select-command-slot", slot.ToString())))).ToArray();
    }

    internal IReadOnlyList<MenuEntry> AutoJunctionEntries()
    {
        List<MenuEntry> entries = [];
        foreach (string priority in new[] { "Attack", "Magic", "Defense" })
        {
            entries.Add(new MenuEntry(priority, priority, MagicUiText.Get("ConservativeHelp"), Action: new MenuAction("junction-auto", Value: priority)));
            if (Data.SharedMagicStock)
            {
                entries.Add(new MenuEntry("party-" + priority, "Entire party · " + priority, MagicUiText.Get("PartyHelp"), Action: new MenuAction("junction-auto-party", Value: priority)));
            }
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
