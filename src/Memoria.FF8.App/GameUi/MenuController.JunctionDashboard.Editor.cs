using System.Diagnostics;
using System.Globalization;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private sealed record JunctionCandidate(string Id, string Name, string Detail, MenuAction Action);
    private string junctionSearch = "";
    private bool junctionSearchEditing;
    private string junctionSort = "benefit";
    private readonly Dictionary<string, int> junctionBenefits = new(StringComparer.Ordinal);
    internal bool IsJunctionSearchEditing => IsJunctionDashboard && junctionSearchEditing;
    private int JunctionCandidatePageSize => JunctionAccessible ? junctionViewportWidth < 850 ? 2 : 4 : 8;

    private JunctionCommandConfiguration JunctionConfiguration(MenuCharacter character) => new(character.Id, character.CommandIds.Cast<string?>().ToArray(), character.AvailableCommands.Select(choice => choice.Id).ToArray(), Data.SmartAttack && character.AttackMode == "SmartMagic");

    private void OpenJunctionEditor(string editor, string origin, string candidate = "")
    {
        if (junctionReview)
        {
            Message = J("resolve-review", "Apply or cancel the current review before editing another assignment.");
            _view = null;
            return;
        }
        junctionEditor = editor;
        junctionSearch = "";
        junctionSearchEditing = false;
        junctionOrigin = origin;
        junctionCandidate = "";
        junctionCandidatePage = 0;
        junctionImpactPage = 0;
        junctionPlan = null;
        junctionExitReview = false;
        if (origin.StartsWith("slot-", StringComparison.Ordinal))
        {
            _junctionSlot = origin[5..];
            junctionDashboard.SetDestination(_junctionSlot);
            if (Character?.AvailableJunctionSlots.Contains(_junctionSlot) != true)
            {
                junctionEditor = "";
                Message = J("slot-locked", "This slot requires a learned junction capability from an attached GF.");
            }
        }
        if (editor == "passive" && int.Parse(origin.AsSpan(8), CultureInfo.InvariantCulture) >= (Character?.AbilitySlots ?? 0))
        {
            junctionEditor = "";
            Message = J("passive-locked", "This passive position requires a learned capacity ability.");
        }
        if (candidate.Length == 0 && Character is { } selected)
        {
            candidate = editor switch
            {
                "magic" => selected.JunctionIds.GetValueOrDefault(_junctionSlot) ?? "0",
                "command" => selected.CommandIds.ElementAtOrDefault(int.Parse(origin.AsSpan(8), CultureInfo.InvariantCulture)) ?? "0",
                "passive" => selected.AbilityIds.ElementAtOrDefault(int.Parse(origin.AsSpan(8), CultureInfo.InvariantCulture)) ?? "0",
                _ => JunctionCandidates().FirstOrDefault()?.Id ?? ""
            };
        }
        if (candidate.Length > 0 && junctionEditor.Length > 0)
        {
            PreviewJunctionCandidate(candidate);
        }
        junctionFocus = "candidate-" + (candidate.Length > 0 ? candidate : JunctionCandidates().FirstOrDefault()?.Id);
        _view = null;
    }

    private IReadOnlyList<JunctionCandidate> JunctionCandidates()
    {
        if (Character is not { } actor)
        {
            return [];
        }
        List<JunctionCandidate> candidates = [];
        if (junctionEditor == "magic")
        {
            candidates.Add(new("0", J("none", "None"), _junctionSlot == "Elem-Atk" ? J("no-element", "No added element") : J("clear-slot", "Clear this slot"), new("junction-magic", "0", actor.Id, _junctionSlot)));
            foreach (MenuMagic spell in Data.Magic.Where(spell => spell.CharacterId == actor.Id && spell.Quantity > 0))
            {
                string detail = J("stock", "Stock") + $" ×{spell.Quantity}";
                if (spell.RemainingUses is not null || spell.UnlimitedUses)
                {
                    detail += " · " + J("casts", "Casts") + " " + (spell.UnlimitedUses ? "∞" : $"{spell.RemainingUses}/{spell.Capacity}");
                }
                if (spell.OwnerCharacterId is string owner && owner.Length > 0 && owner != actor.Id)
                {
                    detail += " · " + (Data.Characters.FirstOrDefault(character => character.Id == owner)?.Name ?? owner);
                }
                if (spell.BoundSlot is string bound && bound.Length > 0)
                {
                    detail += " · " + bound;
                }
                candidates.Add(new(spell.Id, spell.Name, detail, new(spell.JunctionAvailable ? "junction-magic" : "junction-magic-reassign", spell.Id, actor.Id, _junctionSlot)));
            }
        }
        else if (junctionEditor is "command" or "passive")
        {
            bool command = junctionEditor == "command";
            string action = command ? "junction-command" : "junction-ability";
            string slot = junctionOrigin[(command ? 8 : 8)..];
            candidates.Add(new("0", J("empty", "Empty"), J("clear-slot", "Clear this slot"), new(action, "0", actor.Id, slot)));
            foreach (MenuAbilityChoice choice in command ? actor.AvailableCommands : actor.AvailableAbilities)
            {
                candidates.Add(new(choice.Id, choice.Name, choice.Description, new(action, choice.Id, actor.Id, slot)));
            }
        }
        else if (junctionEditor == "gf")
        {
            foreach (MenuGuardianForce force in Data.GuardianForces)
            {
                string detail = force.Owner?.Name ?? J("unassigned", "Unassigned");
                if (force.Owner?.Id == actor.Id)
                {
                    detail += " · " + J("remove", "Remove");
                }
                MenuAction action = force.Owner is { } owner && owner.Id != actor.Id ? new("junction-gf-reassign", force.Id, actor.Id, owner.Id) : new("junction-gf", force.Id, actor.Id);
                candidates.Add(new(force.Id, force.Name, detail, action));
            }
        }
        if (junctionEditor == "magic")
        {
            string current = actor.JunctionIds.GetValueOrDefault(_junctionSlot) ?? "0";
            IEnumerable<JunctionCandidate> spells = candidates.Skip(1);
            spells = junctionSort switch
            {
                "name" => spells.OrderBy(value => value.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(value => value.Id, StringComparer.Ordinal),
                "stock" => spells.OrderByDescending(value => Data.Magic.FirstOrDefault(spell => spell.Id == value.Id)?.Quantity ?? 0).ThenBy(value => value.Id, StringComparer.Ordinal),
                _ => spells.OrderBy(value => value.Id == current ? 0 : Data.Magic.FirstOrDefault(spell => spell.Id == value.Id)?.BoundSlot is not (null or "") ? 2 : 1).ThenByDescending(value => JunctionBenefit(actor, value.Id)).ThenBy(value => value.Id, StringComparer.Ordinal)
            };
            candidates = [candidates[0], .. spells];
        }
        return junctionSearch.Length == 0 ? candidates : candidates.Where(value => value.Id == "0" || value.Name.Contains(junctionSearch, StringComparison.CurrentCultureIgnoreCase)).ToArray();
    }

    private int JunctionBenefit(MenuCharacter actor, string id)
    {
        string key = Data.JunctionPlanner?.SourceFingerprint + ":" + actor.Id + ":" + _junctionSlot + ":" + id;
        if (junctionBenefits.TryGetValue(key, out int cached))
        {
            return cached;
        }
        int benefit = 0;
        try
        {
            if (IsJunctionStat(_junctionSlot))
            {
                benefit = actor.JunctionPreview?.Invoke(_junctionSlot, id).GetValueOrDefault(_junctionSlot) ?? 0;
            }
            else if (actor.JunctionEffectsPreview?.Invoke(_junctionSlot, id) is { } effects)
            {
                var values = _junctionSlot.StartsWith("Elem-", StringComparison.Ordinal) ? effects.Elements : effects.Statuses;
                benefit = values.Sum(value => _junctionSlot.EndsWith("-Atk", StringComparison.Ordinal) ? value.Attack : value.Defense);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            benefit = int.MinValue;
        }
        junctionBenefits[key] = benefit;
        return benefit;
    }

    internal bool EnterJunctionSearchCharacter(char character)
    {
        if (!IsJunctionSearchEditing)
        {
            return false;
        }
        if (character == '\b' && junctionSearch.Length > 0)
        {
            junctionSearch = junctionSearch[..^1];
        }
        else if (!char.IsControl(character) && junctionSearch.Length < 80)
        {
            junctionSearch += character;
        }
        junctionCandidatePage = 0;
        _view = null;
        return true;
    }

    private void PreviewJunctionCandidate(string id)
    {
        JunctionCandidate? candidate = JunctionCandidates().FirstOrDefault(value => value.Id == id);
        junctionCandidate = id;
        junctionReview = false;
        junctionImpactPage = 0;
        if (candidate is not null && Data.JunctionPlanner is { } planner)
        {
            long start = Stopwatch.GetTimestamp();
            junctionPlan = planner.Plan(candidate.Action);
            JunctionPreviewMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            Message = junctionPlan.UnavailableReason ?? "";
        }
        else
        {
            junctionPlan = null;
            Message = J("rules-unavailable", "Junction previews require the original game rules and a live character state.");
        }
        _view = null;
    }

    public double JunctionPreviewMilliseconds { get; private set; }

    private UiElement BuildJunctionDock()
    {
        UiGrid dock = Grid([1], [3.1f, 1.25f], 12);
        UiElement content;
        if (junctionExitReview)
        {
            content = BuildJunctionExitReview();
        }
        else if (junctionReview)
        {
            content = BuildJunctionImpactList();
        }
        else if (junctionEditor == "optimize")
        {
            UiGrid priorities = JunctionRows([28, 34, 0], [1], 4);
            Place(priorities, JunctionLabel(J("optimize", "Optimize stats"), 19), 0);
            UiGrid choices = Grid([1], [1, 1, 1], 6);
            string[] values = ["Attack", "Magic", "Defense"];
            for (int index = 0; index < values.Length; index++)
            {
                string priority = values[index];
                Place(choices, JunctionButton("priority-" + priority, J("priority-" + priority.ToLowerInvariant(), priority), () => PlanJunctionOptimization(priority)), 0, index);
            }
            Place(priorities, choices, 1);
            Place(priorities, JunctionLabel(J("scope", "Stat magic only. GFs, commands, passives, elemental and status junctions stay as configured."), 13, true), 2);
            content = priorities;
        }
        else if (junctionEditor == "actions")
        {
            content = BuildJunctionActions();
        }
        else if (junctionEditor == "attack-mode")
        {
            content = BuildJunctionAttackModes();
        }
        else if (junctionEditor.Length > 0)
        {
            content = BuildJunctionCandidatePicker();
        }
        else
        {
            UiGrid help = JunctionRows([26, 0, 30], [1]);
            Place(help, JunctionLabel(Message.Length > 0 ? Message : J("select-slot", "Select an assignment to inspect or edit it."), 16), 0);
            Place(help, JunctionLabel(J("legend", "Elemental protection: 100% nullifies, 150% absorbs 50%. Status attack shows potency; actual success depends on the target."), 13, true), 1);
            Place(help, JunctionLabel(CurrentBindingText(ControlAction.Card) + "  " + J("actions-help", "Actions: optimize stats, clear junctions, fill commands, pin a stat."), 13, true), 2);
            content = help;
        }
        Place(dock, content, 0);
        Place(dock, BuildJunctionOperationSummary(), 0, 1);
        return JunctionPanel(dock);
    }

    private UiElement BuildJunctionCandidatePicker()
    {
        IReadOnlyList<JunctionCandidate> candidates = JunctionCandidates();
        int pageSize = JunctionCandidatePageSize;
        int start = Math.Clamp(junctionCandidatePage * pageSize, 0, Math.Max(0, candidates.Count - 1));
        UiGrid picker = JunctionRows([23, 0, 22], [1], 2);
        UiGrid heading = Grid([1], [3, 2, 1.7f, 0.5f, 0.5f], 4);
        Place(heading, JunctionLabel((Character?.Name ?? "") + " · " + (junctionEditor == "magic" ? _junctionSlot : J(junctionEditor, junctionEditor)) + " · " + J("preview", "Preview"), 14), 0);
        Place(heading, JunctionButton("junction-search", (junctionSearchEditing ? "▏" : "⌕ ") + (junctionSearch.Length == 0 ? J("search", "Search") : junctionSearch), () => { junctionSearchEditing = true; junctionFocus = "junction-search"; _view = null; }, size: 12), 0, 1);
        Place(heading, JunctionButton("junction-sort", J("sort-" + junctionSort, junctionSort), () => { junctionSort = junctionSort == "benefit" ? "name" : junctionSort == "name" ? "stock" : "benefit"; junctionCandidatePage = 0; _view = null; }, junctionEditor == "magic", 12), 0, 2);
        Place(heading, JunctionButton("candidate-previous", "‹", () => PageJunctionCandidates(-1), junctionCandidatePage > 0, 13), 0, 3);
        Place(heading, JunctionButton("candidate-next", "›", () => PageJunctionCandidates(1), start + pageSize < candidates.Count, 13), 0, 4);
        Place(picker, heading, 0);
        int columns = JunctionAccessible && junctionViewportWidth < 850 ? 1 : 2;
        int rowCount = pageSize / columns;
        UiGrid rows = Grid(Enumerable.Repeat(1f, rowCount).ToArray(), Enumerable.Repeat(1f, columns).ToArray(), 5);
        for (int index = 0; index < Math.Min(pageSize, candidates.Count - start); index++)
        {
            JunctionCandidate candidate = candidates[start + index];
            UiButton row = JunctionButton("candidate-" + candidate.Id, "", () => PreviewJunctionCandidate(candidate.Id));
            UiGrid text = Grid([1], [1.1f, 1.8f], 5);
            Place(text, JunctionLabel((candidate.Id == junctionCandidate ? "◆ " : "") + candidate.Name, 16), 0);
            Place(text, JunctionLabel(candidate.Detail, 12, true), 0, 1);
            row.Children.Add(text);
            row.FocusChanged = focused =>
            {
                if (focused)
                {
                    junctionFocus = row.Id!;
                    if (junctionCandidate != candidate.Id)
                    {
                        PreviewJunctionCandidate(candidate.Id);
                    }
                }
            };
            Place(rows, row, index % rowCount, index / rowCount);
        }
        Place(picker, rows, 1);
        string range = $"{(candidates.Count == 0 ? 0 : start + 1)}–{Math.Min(start + pageSize, candidates.Count)} / {candidates.Count}";
        Place(picker, JunctionLabel(J("candidates", "Candidates") + " · " + range + (junctionEditor == "magic" ? " · " + J("stock-owner", "Selected character's stock and current ownership") : ""), 12, true), 2);
        return picker;
    }

    private UiElement BuildJunctionImpactList()
    {
        UiGrid content = JunctionRows([24, 0, 22], [1]);
        Place(content, JunctionLabel(junctionEditor == "optimize" ? J("optimize", "Optimize stats") + " · " + J("priority-" + junctionPriority.ToLowerInvariant(), junctionPriority) : J("review", "Review consequences"), 18), 0);
        var changes = junctionPlan?.Changes ?? [];
        int start = Math.Min(junctionImpactPage * 5, Math.Max(0, changes.Count - 1));
        UiGrid rows = Grid([1, 1, 1, 1, 1], [1]);
        for (int index = 0; index < Math.Min(5, changes.Count - start); index++)
        {
            MenuJunctionChange change = changes[start + index];
            Place(rows, JunctionLabel($"{change.CharacterName} · {change.Slot}: {change.Before} → {change.After}", 14), index);
        }
        Place(content, rows, 1);
        UiGrid paging = Grid([1], [4, 1, 1], 4);
        Place(paging, JunctionLabel(J("changes", "Changes") + $" {Math.Min(changes.Count, start + 1)}–{Math.Min(changes.Count, start + 5)} / {changes.Count}", 12, true), 0);
        Place(paging, JunctionButton("impact-previous", "‹", () => { junctionImpactPage = Math.Max(0, junctionImpactPage - 1); _view = null; }, start > 0, 13), 0, 1);
        Place(paging, JunctionButton("impact-next", "›", () => { junctionImpactPage++; _view = null; }, start + 5 < changes.Count, 13), 0, 2);
        Place(content, paging, 2);
        return content;
    }

    private UiElement BuildJunctionOperationSummary()
    {
        UiGrid summary = JunctionRows([25, 0, 32], [1], 3);
        JunctionCandidate? candidate = JunctionCandidates().FirstOrDefault(value => value.Id == junctionCandidate);
        string title = junctionExitReview ? J("command-attention", "Command attention") : candidate?.Name ?? (junctionEditor == "optimize" ? J("stats-only", "Stats only") : J("preview", "Preview"));
        Place(summary, JunctionLabel(title, 19), 0);
        string text = junctionPlan?.UnavailableReason ?? Message;
        if (junctionPlan is { HasChanges: true } plan)
        {
            // Keep critical losses visible even when the complete list is on another page.
            text = string.Join("\n", plan.Changes.Where(change => change.Category is "HP" or "Command" or "Capacity" || change.Category == "Stat" && change.Slot == "HP").Select(change => $"{change.CharacterName} · {change.Slot}: {change.Before} → {change.After}"));
            if (!junctionReview)
            {
                text = string.Join("\n", plan.Changes.Select(change => $"{change.CharacterName} · {change.Slot}: {change.Before} → {change.After}"));
            }
            if (text.Length == 0)
            {
                text = J("review-changes", "Review all proposed changes before applying.");
            }
        }
        if (junctionEditor == "magic" && _junctionSlot == "Elem-Atk")
        {
            text = J("element-warning", "Attacks using this element can heal enemies that absorb it.") + "\n" + text;
        }
        UiLabel explanation = JunctionLabel(text, 12, true);
        explanation.TextFlow = UiTextFlow.Wrap;
        Place(summary, explanation, 1);
        UiGrid buttons = Grid([1], [1, 1.7f], 7);
        Place(buttons, JunctionButton("cancel-junction-preview", J("cancel", "Cancel"), CancelJunctionEditor), 0);
        Place(buttons, JunctionButton("apply-junction-preview", JunctionOperationLabel(), ConfirmJunctionPlan, junctionPlan?.HasChanges == true, 13), 0, 1);
        Place(summary, buttons, 2);
        return summary;
    }

    private string JunctionOperationLabel()
    {
        JunctionCandidate? candidate = JunctionCandidates().FirstOrDefault(value => value.Id == junctionCandidate);
        if (junctionEditor == "optimize")
        {
            return J("apply-stats", "Apply stat changes");
        }
        if (junctionEditor == "fill")
        {
            return J("fill-commands", "Fill empty commands");
        }
        if (junctionEditor == "gf" && candidate is not null)
        {
            MenuGuardianForce? force = Data.GuardianForces.FirstOrDefault(value => value.Id == candidate.Id);
            string verb = force?.Owner?.Id == Character?.Id ? J("remove", "Remove") : force?.Owner is null ? J("attach", "Attach") : J("transfer", "Transfer");
            return verb + " " + candidate.Name;
        }
        if (candidate is not null)
        {
            MenuMagic? spell = Data.Magic.FirstOrDefault(value => value.Id == candidate.Id);
            return (spell?.BoundSlot is not (null or "") && spell.BoundSlot != _junctionSlot ? J("move", "Move") : J("assign", "Assign")) + " " + candidate.Name;
        }
        return J("apply", "Apply changes");
    }

    private UiElement BuildJunctionActions()
    {
        UiGrid rows = Grid([1, 1, 1, 1], [1, 1], 5);
        Place(rows, JunctionButton("optimize", J("optimize", "Optimize stats"), OpenJunctionOptimizer), 0);
        Place(rows, JunctionButton("fill-commands", J("fill-commands", "Fill empty commands"), PlanJunctionCommands), 1);
        Place(rows, JunctionButton("pin-stat", J("pin", "Pin / Unpin stat"), ToggleJunctionPin, junctionOrigin.StartsWith("slot-", StringComparison.Ordinal) && IsJunctionStat(junctionOrigin[5..])), 2);
        Place(rows, JunctionButton("actions-cancel", J("cancel", "Cancel"), CancelJunctionEditor), 3);
        Place(rows, JunctionButton("clear-magic", J("clear-magic", "Clear this character's magic junctions"), () => ReviewJunctionAction(new("junction-off", TargetId: Character?.Id ?? "", Value: "Magic"))), 0, 1);
        Place(rows, JunctionButton("remove-junctions", J("remove-junctions", "Remove this character's junctions"), () => ReviewJunctionAction(new("junction-off", TargetId: Character?.Id ?? "", Value: "All"))), 1, 1);
        Place(rows, JunctionButton("clear-party-magic", J("clear-party-magic", "Clear all characters' magic junctions"), () => ReviewJunctionAction(new("junction-magic-off-all"))), 2, 1);
        return rows;
    }

    private void OpenJunctionActions()
    {
        if (junctionReview)
        {
            Message = J("resolve-review", "Apply or cancel the current review before editing another assignment.");
            _view = null;
            return;
        }
        if (junctionFocus.StartsWith("slot-", StringComparison.Ordinal))
        {
            junctionOrigin = junctionFocus;
        }
        junctionPlan = null;
        junctionEditor = "actions";
        junctionSearchEditing = false;
        junctionFocus = "optimize";
        _view = null;
    }

    private void ReviewJunctionAction(MenuAction action)
    {
        junctionPlan = Data.JunctionPlanner?.Plan(action);
        junctionReview = true;
        junctionImpactPage = 0;
        junctionFocus = "cancel-junction-preview";
        Message = junctionPlan?.UnavailableReason ?? "";
        _view = null;
    }

    private void OpenJunctionOptimizer()
    {
        if (junctionReview)
        {
            Message = J("resolve-review", "Apply or cancel the current review before editing another assignment.");
        }
        else
        {
            junctionEditor = "optimize";
            junctionPlan = null;
            junctionFocus = "priority-" + junctionPriority;
        }
        _view = null;
    }

    private void PlanJunctionOptimization(string priority)
    {
        junctionPriority = priority;
        junctionPlan = Character is { } actor ? Data.JunctionPlanner?.Optimize(actor.Id, priority, junctionDashboard.GetPinnedSlots(actor.Id)) : null;
        junctionReview = junctionPlan?.HasChanges == true;
        junctionImpactPage = 0;
        junctionFocus = junctionReview ? "cancel-junction-preview" : "priority-" + priority;
        Message = junctionPlan?.UnavailableReason ?? (junctionReview ? J("scope", "Stat magic only. GFs, commands, passives, elemental and status junctions stay as configured.") : J("no-stat-changes", "No stat changes suggested"));
        _view = null;
    }

    private void PlanJunctionCommands()
    {
        junctionEditor = "fill";
        junctionPlan = Character is { } actor ? Data.JunctionPlanner?.FillEmptyCommands(actor.Id) : null;
        junctionReview = true;
        junctionFocus = "cancel-junction-preview";
        Message = junctionPlan?.UnavailableReason ?? (junctionPlan?.HasChanges == true ? "" : J("no-commands", "No additional learned commands are available to fill these positions."));
        _view = null;
    }

    private void ConfirmJunctionPlan()
    {
        if (junctionPlan is not { HasChanges: true } plan || Data.JunctionPlanner is not { } planner)
        {
            return;
        }
        if (!planner.IsCurrent(plan))
        {
            junctionPlan = null;
            Message = J("stale", "The source changed. Select the proposal again to review its current effects.");
            _view = null;
            return;
        }
        if (!junctionReview && (plan.RequiresReview || plan.Changes.Count > 5))
        {
            junctionReview = true;
            junctionFocus = "cancel-junction-preview";
            _view = null;
            return;
        }
        bool firstCommands = Character is { } before && before.CommandIds.All(id => id == "0") && before.AvailableCommands.Count == 0;
        if (_liveAction is null || !Data.SupportedActions.Contains(plan.Action.Kind))
        {
            Message = J("mutation-unavailable", "The current host does not support applying this junction proposal.");
            _view = null;
            return;
        }
        try
        {
            _liveAction(plan.Action);
            _liveData = null;
            MenuJunctionPlanner? refreshed = Data.JunctionPlanner;
            if (refreshed?.SourceFingerprint != plan.AfterSourceFingerprint)
            {
                junctionPlan = null;
                junctionReview = false;
                Message = J("apply-rejected", "The owner rejected the change or its source changed. Select it again to review.");
                _view = null;
                return;
            }
            junctionUndo = refreshed.Reverse(plan);
            foreach (string id in plan.Changes.Select(change => change.CharacterId).Distinct())
            {
                junctionVisited.Add(id);
                junctionAudit.MarkAffected(id);
            }
            junctionPlan = null;
            junctionReview = false;
            junctionEditor = "";
            junctionFocus = junctionOrigin;
            Message = J("applied", "Junction changes applied.");
            if (firstCommands && Character is { AvailableCommands.Count: > 0 } after && after.CommandIds.All(id => id == "0"))
            {
                OpenJunctionEditor("command", "command-0");
                Message = J("first-gf", "The GF unlocked commands. Choose which commands to equip.");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or InvalidDataException or NotSupportedException)
        {
            _liveData = null;
            junctionPlan = null;
            junctionReview = false;
            Message = exception.Message;
        }
        _view = null;
    }

    private void UndoJunction()
    {
        if (junctionPlan is not null || junctionUndo is not { HasChanges: true } undo)
        {
            return;
        }
        if (Data.JunctionPlanner?.IsCurrent(undo) != true)
        {
            junctionUndo = null;
            Message = J("undo-stale", "Undo is unavailable because the relevant game state changed.");
            _view = null;
            return;
        }
        junctionPlan = undo;
        junctionReview = true;
        ConfirmJunctionPlan();
        junctionUndo = null;
    }

    private void CancelJunctionEditor()
    {
        junctionPlan = null;
        junctionSearchEditing = false;
        junctionReview = false;
        junctionExitReview = false;
        junctionEditor = "";
        junctionFocus = junctionOrigin;
        Message = "";
        _view = null;
    }

    private void PageJunctionCandidates(int delta)
    {
        int maximum = Math.Max(0, (JunctionCandidates().Count - 1) / JunctionCandidatePageSize);
        junctionCandidatePage = Math.Clamp(junctionCandidatePage + delta, 0, maximum);
        junctionFocus = "candidate-" + JunctionCandidates().ElementAtOrDefault(junctionCandidatePage * JunctionCandidatePageSize)?.Id;
        _view = null;
    }

    private static bool IsJunctionStat(string slot) => slot is "HP" or "Str" or "Vit" or "Mag" or "Spr" or "Spd" or "Eva" or "Hit" or "Luck";

    private void ToggleJunctionPin()
    {
        string origin = junctionFocus.StartsWith("slot-", StringComparison.Ordinal) ? junctionFocus : junctionOrigin;
        if (Character is { } actor && origin.StartsWith("slot-", StringComparison.Ordinal) && IsJunctionStat(origin[5..]))
        {
            junctionDashboard.SetPinned(actor.Id, origin[5..], !junctionDashboard.IsPinned(actor.Id, origin[5..]));
            Message = J("pin-help", "A pinned stat keeps its assignment during optimization. Manual editing remains available.");
        }
        _view = null;
    }

    private void OpenJunctionAttackMode()
    {
        junctionOrigin = "basic-action";
        if (!Data.SmartAttack)
        {
            Message = J("physical-attack", "This is the effective basic action. Smart Attack is disabled.");
            _view = null;
            return;
        }
        junctionEditor = "attack-mode";
        junctionFocus = "physical-mode";
        _view = null;
    }

    private void SwitchJunctionCharacter(int delta)
    {
        if (junctionReview)
        {
            Message = J("resolve-review", "Apply or cancel the current review before editing another assignment.");
            _view = null;
            return;
        }
        CancelJunctionEditor();
        ChangePageSubject(delta);
        _view = null;
    }

    private UiElement BuildJunctionExitReview()
    {
        var actors = JunctionAttentionCharacters();
        UiGrid rows = Grid([1, 1, 1], [1]);
        Place(rows, JunctionLabel(J("exit-review", "Some available command positions are empty, or Smart magic lacks Magic / Draw."), 15), 0);
        UiGrid links = Grid([1], Enumerable.Repeat(1f, Math.Max(1, actors.Count)).ToArray(), 5);
        for (int index = 0; index < actors.Count; index++)
        {
            MenuCharacter actor = actors[index];
            Place(links, JunctionButton("configure-" + actor.Id, actor.Name + " · " + J("configure", "Configure commands"), () =>
            {
                _characterId = actor.Id;
                junctionExitReview = false;
                OpenJunctionEditor("command", "command-" + (JunctionCommandAudit.Inspect(JunctionConfiguration(actor)).ActionableEmptySlots.FirstOrDefault()));
            }), 0, index);
        }
        Place(rows, links, 1);
        Place(rows, JunctionButton("leave-configured", J("leave", "Leave as configured"), () =>
        {
            foreach (MenuCharacter actor in actors)
            {
                junctionAudit.Acknowledge(JunctionConfiguration(actor));
            }
            junctionExitReview = false;
            junctionLeaving = true;
            Back();
            junctionLeaving = false;
        }), 2);
        return rows;
    }

    private List<MenuCharacter> JunctionAttentionCharacters() => Data.Characters.Where(actor => actor.Id == Character?.Id || junctionVisited.Contains(actor.Id)).Where(actor => actor.CommandIds.Count == 3 && junctionAudit.RequiresReview(JunctionConfiguration(actor))).ToList();

    private bool BackFromJunctionDashboard()
    {
        if (!IsJunctionDashboard || junctionLeaving)
        {
            return false;
        }
        if (junctionEditor.Length > 0 || junctionPlan is not null || junctionExitReview)
        {
            CancelJunctionEditor();
            return true;
        }
        if (JunctionAttentionCharacters().Count > 0)
        {
            junctionExitReview = true;
            junctionFocus = "leave-configured";
            _view = null;
            return true;
        }
        return false;
    }

    private bool NavigateJunctionDashboard(MenuInput input)
    {
        if (!IsJunctionDashboard)
        {
            return false;
        }
        if (IsJunctionSearchEditing)
        {
            if (input == MenuInput.Cancel && junctionSearch.Length > 0)
            {
                junctionSearch = "";
                junctionCandidatePage = 0;
            }
            else if (input is MenuInput.Cancel or MenuInput.Confirm or MenuInput.Tab)
            {
                junctionSearchEditing = false;
                junctionFocus = "junction-search";
            }
            _view = null;
            return true;
        }
        if (input == MenuInput.Cancel)
        {
            Back();
            return true;
        }
        if (input is MenuInput.PageLeft or MenuInput.PageRight)
        {
            SwitchJunctionCharacter(input == MenuInput.PageLeft ? -1 : 1);
            return true;
        }
        if (input is MenuInput.PageUp or MenuInput.PageDown)
        {
            if (junctionReview)
            {
                int maximum = Math.Max(0, ((junctionPlan?.Changes.Count ?? 0) - 1) / 5);
                junctionImpactPage = Math.Clamp(junctionImpactPage + (input == MenuInput.PageUp ? -1 : 1), 0, maximum);
                _view = null;
            }
            else
            {
                PageJunctionCandidates(input == MenuInput.PageUp ? -1 : 1);
            }
            return true;
        }
        if (input is MenuInput.Actions or MenuInput.Alternate)
        {
            OpenJunctionActions();
            return true;
        }
        UiView view = BuildView();
        if (input == MenuInput.Confirm)
        {
            if (view.FocusedElement?.Id?.StartsWith("candidate-", StringComparison.Ordinal) == true && junctionPlan is not null)
            {
                ConfirmJunctionPlan();
            }
            else
            {
                view.Key(UiKey.Confirm);
            }
            return true;
        }
        if (input is MenuInput.Tab or MenuInput.ReverseTab or MenuInput.FocusLeft or MenuInput.FocusRight)
        {
            string[] groups = ["manage-gfs", "slot-HP", "slot-Elem-Atk", "slot-ST-Atk", "elemental-attack", "status-attack", "command-0", "passive-0", "candidate-" + (JunctionCandidates().FirstOrDefault()?.Id ?? "0"), "junction-undo", "optimize", "junction-back"];
            UiElement[] controls = FocusableDescendants(view.Root).Where(element => groups.Contains(element.Id)).ToArray();
            int index = Array.FindIndex(controls, element => element.Id == junctionFocus);
            int delta = input is MenuInput.ReverseTab or MenuInput.FocusLeft ? -1 : 1;
            if (controls.Length > 0)
            {
                view.Focus(controls[(index + delta + controls.Length) % controls.Length]);
            }
            return true;
        }
        if (input is MenuInput.Up or MenuInput.Down or MenuInput.Left or MenuInput.Right)
        {
            MoveJunctionSpatialFocus(view, input);
            return true;
        }
        return true;
    }

    private static void MoveJunctionSpatialFocus(UiView view, MenuInput input)
    {
        if (view.FocusedElement is not { } focused)
        {
            return;
        }
        float x = focused.Bounds.X + focused.Bounds.Width / 2;
        float y = focused.Bounds.Y + focused.Bounds.Height / 2;
        bool horizontal = input is MenuInput.Left or MenuInput.Right;
        bool positive = input is MenuInput.Right or MenuInput.Down;
        UiElement? best = null;
        float distance = float.PositiveInfinity;
        foreach (UiElement target in FocusableDescendants(view.Root).Where(element => element.Enabled && element.Visible && !element.Bounds.IsEmpty && element != focused))
        {
            float dx = target.Bounds.X + target.Bounds.Width / 2 - x;
            float dy = target.Bounds.Y + target.Bounds.Height / 2 - y;
            float primary = horizontal ? dx : dy;
            float cross = Math.Abs(horizontal ? dy : dx);
            if (positive ? primary <= 1 : primary >= -1)
            {
                continue;
            }
            float score = Math.Abs(primary) + cross * 3;
            if (score < distance)
            {
                distance = score;
                best = target;
            }
        }
        if (best is not null)
        {
            view.Focus(best);
        }
    }
}
