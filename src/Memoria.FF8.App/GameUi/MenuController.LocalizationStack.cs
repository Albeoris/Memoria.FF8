namespace Memoria.FF8.GameUi;

internal sealed record LocalizationStackOption(string Id, string Name, string TargetId, string? BaseProjectId = null);
internal sealed record LocalizationStackView(IReadOnlyList<LocalizationStackOption> Options, IReadOnlyList<string> OrderedIds, bool PendingChoice = false, string? PrimaryTargetId = null);

public sealed partial class MenuController
{
    private Func<LocalizationStackView>? readLocalizationStack;
    private Action<IReadOnlyList<string>, string?>? applyLocalizationStack;
    private readonly List<string> localizationStackDraft = [];
    private string? localizationStackSelected;
    private string? localizationPrimaryTarget;

    internal void ConnectLocalizationStack(Func<LocalizationStackView> read, Action<IReadOnlyList<string>> apply)
    {
        ConnectLocalizationStack(read, (order, _) => apply(order));
    }

    internal void ConnectLocalizationStack(Func<LocalizationStackView> read, Action<IReadOnlyList<string>, string?> apply)
    {
        readLocalizationStack = read;
        applyLocalizationStack = apply;
        Register(new()
        {
            Id = "localization-stack",
            Title = "Mods · Localization stack",
            Help = "Select several translation packages. The list runs from LOW priority at the TOP to HIGH priority at the BOTTOM. Changes apply only after confirmation.",
            Entries = _ => LocalizationStackEntries(),
            Details = _ => LocalizationStackDetails()
        });
    }

    public void PromptLocalizationSelection()
    {
        if (readLocalizationStack is null || ScreenId == "localization-stack")
        {
            return;
        }
        if (!IsOpen)
        {
            Open("mods-create");
        }
        HandleLocalizationStackAction(new("localization-stack-open"));
        _view = null;
    }

    private IReadOnlyList<MenuEntry> LocalizationStackEntries()
    {
        LocalizationStackView view = readLocalizationStack!();
        List<MenuEntry> entries = [];
        foreach (LocalizationStackOption option in view.Options.OrderBy(option =>
        {
            int index = localizationStackDraft.IndexOf(option.Id);
            return index >= 0 ? index : int.MaxValue;
        }))
        {
            int index = localizationStackDraft.IndexOf(option.Id);
            entries.Add(new("package-" + option.Id, (index < 0 ? "[ ] " : SystemText("ui.localizationstackentries.entry.label.x", "[x] ") + (index + 1) + ". ") + option.Name + " · " + option.TargetId, option.BaseProjectId is null ? SystemText("ui.localizationstackentries.entry.help.independent.translation.confirm.toggles.selection", "Independent translation. Confirm toggles selection.") : SystemText("ui.localizationstackentries.entry.help.patch.of", "Patch of ") + option.BaseProjectId + SystemText("ui.localizationstackentries.entry.help.select.its.base.explicitly", ". Select its base explicitly."), Action: new("localization-stack-toggle", option.Id)));
        }
        entries.Add(new("lower", SystemText("ui.localizationstackentries.entry.label.move.selected.package.toward.top.lower.priority", "Move selected package toward TOP (lower priority)"), SystemText("ui.localizationstackentries.entry.help.reorder.the.last.selected.package", "Reorder the last selected package."), Action: new("localization-stack-move", Value: "-1"), Enabled: localizationStackSelected is not null && localizationStackDraft.IndexOf(localizationStackSelected) > 0));
        entries.Add(new("higher", SystemText("ui.localizationstackentries.entry.label.move.selected.package.toward.bottom.higher.priority", "Move selected package toward BOTTOM (higher priority)"), SystemText("ui.localizationstackentries.entry.help.reorder.the.last.selected.package", "Reorder the last selected package."), Action: new("localization-stack-move", Value: "1"), Enabled: localizationStackSelected is not null && localizationStackDraft.IndexOf(localizationStackSelected) >= 0 && localizationStackDraft.IndexOf(localizationStackSelected) < localizationStackDraft.Count - 1));
        entries.Add(new("primary", SystemText("ui.localizationstackentries.entry.label.primary.target", "Primary target: ") + (localizationPrimaryTarget ?? SystemText("ui.localizationstackentries.entry.label.original.text", "Original text")), SystemText("ui.localizationstackentries.entry.help.cycle.the.primary.target.id.among.selected.translations", "Cycle the primary Target ID among selected translations. This choice is saved with the stack."), Action: new("localization-stack-primary"), Enabled: LocalizationDraftTargets(view).Count > 1));
        entries.Add(new("apply", SystemText("ui.localizationstackentries.entry.label.apply.selected.stack", "Apply selected stack"), SystemText("ui.localizationstackentries.entry.help.persist.this.explicit.package.order.empty.selection.chooses", "Persist this explicit package order. Empty selection chooses original text."), Action: new("localization-stack-apply")));
        entries.Add(new("cancel", SystemText("ui.localizationstackentries.entry.label.cancel", "Cancel"), SystemText("ui.localizationstackentries.entry.help.keep.the.saved.stack.unchanged", "Keep the saved stack unchanged."), Action: new("menu-back")));
        return entries;
    }

    private IReadOnlyList<string> LocalizationStackDetails()
    {
        LocalizationStackView view = readLocalizationStack!();
        return [view.PendingChoice ? SystemText("ui.localizationstackdetails.details.several.independent.translations.are.enabled.choose.the.active", "Several independent translations are enabled. Choose the active stack explicitly.") : SystemText("ui.localizationstackdetails.details.choose.any.subset.of.the.available.translation.packages", "Choose any subset of the available translation packages."), SystemText("ui.localizationstackdetails.details.low.priority.at.top.high.priority.at.bottom", "LOW priority at TOP → HIGH priority at BOTTOM"), SystemText("ui.localizationstackdetails.details.primary.target", "Primary target: ") + (localizationPrimaryTarget ?? SystemText("ui.localizationstackdetails.details.original.text", "Original text")), .. localizationStackDraft.Select((id, index) => (index + 1) + ". " + (view.Options.FirstOrDefault(option => option.Id == id)?.Name ?? id)), SystemText("ui.localizationstackdetails.details.original.text.fills.missing.or.empty.targets.no", "Original text fills missing or empty targets. No selection uses original text."), Message];
    }

    private IReadOnlyList<string> LocalizationDraftTargets(LocalizationStackView view) => localizationStackDraft.Select(id => view.Options.FirstOrDefault(option => option.Id == id)?.TargetId).Where(target => target is not null).Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private bool HandleLocalizationStackAction(MenuAction action)
    {
        switch (action.Kind)
        {
            case "localization-stack-open":
                localizationStackDraft.Clear();
                LocalizationStackView view = readLocalizationStack!();
                localizationStackDraft.AddRange(view.OrderedIds);
                localizationStackSelected = localizationStackDraft.LastOrDefault();
                localizationPrimaryTarget = view.PrimaryTargetId ?? LocalizationDraftTargets(view).FirstOrDefault();
                Push("localization-stack");
                return true;
            case "localization-stack-toggle":
                localizationStackSelected = action.SubjectId;
                if (!localizationStackDraft.Remove(action.SubjectId))
                {
                    localizationStackDraft.Add(action.SubjectId);
                }
                IReadOnlyList<string> selectedTargets = LocalizationDraftTargets(readLocalizationStack!());
                if (localizationPrimaryTarget is null || !selectedTargets.Contains(localizationPrimaryTarget, StringComparer.OrdinalIgnoreCase))
                {
                    localizationPrimaryTarget = selectedTargets.FirstOrDefault();
                }
                return true;
            case "localization-stack-primary":
                string[] targets = LocalizationDraftTargets(readLocalizationStack!()).ToArray();
                if (targets.Length > 0)
                {
                    int selected = Array.FindIndex(targets, target => string.Equals(target, localizationPrimaryTarget, StringComparison.OrdinalIgnoreCase));
                    localizationPrimaryTarget = targets[(selected + 1) % targets.Length];
                }
                return true;
            case "localization-stack-move":
                int index = localizationStackSelected is null ? -1 : localizationStackDraft.IndexOf(localizationStackSelected);
                int next = index + int.Parse(action.Value, System.Globalization.CultureInfo.InvariantCulture);
                if (index >= 0 && next >= 0 && next < localizationStackDraft.Count)
                {
                    (localizationStackDraft[index], localizationStackDraft[next]) = (localizationStackDraft[next], localizationStackDraft[index]);
                }
                return true;
            case "localization-stack-apply":
                applyLocalizationStack!(localizationStackDraft.ToArray(), localizationPrimaryTarget);
                Message = SystemText("ui.handlelocalizationstackaction.message.localization.stack.saved", "Localization stack saved.");
                Back();
                return true;
            default:
                return false;
        }
    }
}
