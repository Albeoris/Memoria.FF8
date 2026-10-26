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
            entries.Add(new("package-" + option.Id, (index < 0 ? "[ ] " : "[x] " + (index + 1) + ". ") + option.Name + " · " + option.TargetId, option.BaseProjectId is null ? "Independent translation. Confirm toggles selection." : "Patch of " + option.BaseProjectId + ". Select its base explicitly.", Action: new("localization-stack-toggle", option.Id)));
        }
        entries.Add(new("lower", "Move selected package toward TOP (lower priority)", "Reorder the last selected package.", Action: new("localization-stack-move", Value: "-1"), Enabled: localizationStackSelected is not null && localizationStackDraft.IndexOf(localizationStackSelected) > 0));
        entries.Add(new("higher", "Move selected package toward BOTTOM (higher priority)", "Reorder the last selected package.", Action: new("localization-stack-move", Value: "1"), Enabled: localizationStackSelected is not null && localizationStackDraft.IndexOf(localizationStackSelected) >= 0 && localizationStackDraft.IndexOf(localizationStackSelected) < localizationStackDraft.Count - 1));
        entries.Add(new("primary", "Primary target: " + (localizationPrimaryTarget ?? "Original text"), "Cycle the primary Target ID among selected translations. This choice is saved with the stack.", Action: new("localization-stack-primary"), Enabled: LocalizationDraftTargets(view).Count > 1));
        entries.Add(new("apply", "Apply selected stack", "Persist this explicit package order. Empty selection chooses original text.", Action: new("localization-stack-apply")));
        entries.Add(new("cancel", "Cancel", "Keep the saved stack unchanged.", Action: new("menu-back")));
        return entries;
    }

    private IReadOnlyList<string> LocalizationStackDetails()
    {
        LocalizationStackView view = readLocalizationStack!();
        return [view.PendingChoice ? "Several independent translations are enabled. Choose the active stack explicitly." : "Choose any subset of the available translation packages.", "LOW priority at TOP → HIGH priority at BOTTOM", "Primary target: " + (localizationPrimaryTarget ?? "Original text"), .. localizationStackDraft.Select((id, index) => (index + 1) + ". " + (view.Options.FirstOrDefault(option => option.Id == id)?.Name ?? id)), "Original text fills missing or empty targets. No selection uses original text.", Message];
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
                Message = "Localization stack saved.";
                Back();
                return true;
            default:
                return false;
        }
    }
}
