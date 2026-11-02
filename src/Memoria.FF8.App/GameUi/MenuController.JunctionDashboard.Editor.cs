using System.Diagnostics;
using System.Globalization;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private sealed record JunctionCandidate(string Id, string Name, string Detail, MenuAction Action, UiImageSource? Icon = null);
    private string junctionSort = "benefit";
    private readonly Dictionary<string, int> junctionBenefits = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MenuJunctionPlan> junctionPreviewCache = new(StringComparer.Ordinal);
    private string junctionCacheSource = "";
    private MenuAction? junctionReviewedAction;


    private void OpenJunctionEditor(string editor, string origin, string candidate = "")
    {
        if (junctionReview)
        {
            Message = J("resolve-review", "Apply or cancel the current review before editing another assignment.");
            _view = null;
            return;
        }
        junctionEditor = editor;
        junctionOrigin = origin;
        junctionCandidate = "";
        junctionCandidatePage = 0;
        junctionImpactPage = 0;
        junctionPlan = null;
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
                "magic" => selected.JunctionIds.GetValueOrDefault(_junctionSlot) is { } spell && (spell != "0" || selected.Junctions.ContainsKey(_junctionSlot) && JunctionMagicInventory.Any(value => value.CharacterId == selected.Id && value.Id == spell)) ? spell : "none",
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
            candidates.Add(new("none", "---", _junctionSlot == "Elem-Atk" ? J("no-element", "No added element") : J("clear-slot", "Clear this slot"), new("junction-magic", "0", actor.Id, _junctionSlot)));
            foreach (MenuMagic spell in JunctionMagicInventory.Where(spell => spell.CharacterId == actor.Id && spell.Quantity > 0 && (Data.JunctionPlanner is not { } planner || spell.SpellId is int spellId && planner.IsMagicCandidate(actor.Id, spellId, _junctionSlot))))
            {
                string detail = J("stock", "Stock") + $" ×{spell.Quantity}";
                if (spell.RemainingUses is not null || spell.UnlimitedUses)
                {
                    detail += " · " + J("casts", "Casts") + " " + (spell.UnlimitedUses ? "∞" : $"{spell.RemainingUses}/{spell.Capacity}");
                }
                if (spell.OwnerCharacterId is string owner && owner.Length > 0 && owner != actor.Id)
                {
                    detail += " · " + ((Data.JunctionPlanner?.Snapshot ?? Data).Characters.FirstOrDefault(character => character.Id == owner)?.Name ?? owner);
                }
                if (spell.BoundSlot is string bound && bound.Length > 0)
                {
                    detail += " · " + J("used-by", "Used by") + " " + bound;
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
                candidates.Add(new(choice.Id, choice.Name, choice.Description, new(action, choice.Id, actor.Id, slot), choice.Icon));
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
                "stock" => spells.OrderByDescending(value => JunctionMagicInventory.FirstOrDefault(spell => spell.Id == value.Id)?.Quantity ?? 0).ThenBy(value => value.Id, StringComparer.Ordinal),
                _ => spells.OrderBy(value => value.Id == current ? 0 : JunctionMagicInventory.FirstOrDefault(spell => spell.Id == value.Id)?.BoundSlot is not (null or "") ? 2 : 1).ThenByDescending(value => JunctionBenefit(actor, value.Id)).ThenBy(value => value.Id, StringComparer.Ordinal)
            };
            candidates = [candidates[0], .. spells];
        }
        return candidates;
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

    private void PreviewJunctionCandidate(string id)
    {
        if (junctionCandidate == id && junctionPlan is { } existing && Data.JunctionPlanner?.IsCurrent(existing) == true)
        {
            return;
        }
        JunctionCandidate? candidate = JunctionCandidates().FirstOrDefault(value => value.Id == id);
        junctionCandidate = id;
        junctionReview = false;
        junctionImpactPage = 0;
        if (candidate is not null && Data.JunctionPlanner is { } planner)
        {
            long start = Stopwatch.GetTimestamp();
            if (junctionCacheSource != planner.SourceFingerprint)
            {
                junctionCacheSource = planner.SourceFingerprint;
                junctionPreviewCache.Clear();
                junctionBenefits.Clear();
            }
            string key = candidate.Action.Kind + ":" + candidate.Action.TargetId + ":" + candidate.Action.Value + ":" + candidate.Id;
            if (!junctionPreviewCache.TryGetValue(key, out MenuJunctionPlan? cached))
            {
                cached = planner.Plan(candidate.Action);
                if (junctionPreviewCache.Count >= 16)
                {
                    junctionPreviewCache.Clear();
                }
                junctionPreviewCache.Add(key, cached);
            }
            junctionPlan = cached;
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
        UiGrid dock = JunctionRows([0, 20], [1]);
        UiElement content;
        if (junctionReview && !junctionTransferModal)
        {
            content = junctionEditor == "optimize" ? BuildJunctionOptimizationConfirmation() : BuildJunctionImpactList();
        }
        else if (junctionEditor == "optimize")
        {
            content = BuildJunctionActionColumns("junction-priority-columns", [
                ("priority-Attack", J("priority-attack", "Attack"), () => PlanJunctionOptimization("Attack")),
                ("priority-Magic", J("priority-magic", "Magic"), () => PlanJunctionOptimization("Magic")),
                ("priority-Defense", J("priority-defense", "Defense"), () => PlanJunctionOptimization("Defense"))
            ]);
        }
        else if (junctionEditor == "actions")
        {
            content = BuildJunctionActions();
        }
        else if (junctionEditor == "attack-mode")
        {
            content = BuildJunctionAttackModes();
        }
        else if (junctionEditor == "gf")
        {
            content = BuildJunctionGuardianCapabilities();
        }
        else if (junctionEditor is "magic" or "command" or "passive")
        {
            content = BuildJunctionCandidatePicker();
        }
        else
        {
            UiGrid help = JunctionRows([28, 0], [1], 4);
            string description = junctionEditor == "gf" && Data.GuardianForces.FirstOrDefault(force => force.Id == junctionCandidate) is { } gf ? gf.Name + " · " + J("learning", "Learning") + ": " + gf.Learning : JunctionFocusedDescription();
            Place(help, JunctionLabel(Message.Length > 0 ? Message : description, 16), 0);
            UiLabel impact = JunctionLabel(JunctionImpactDescription(), 13, true);
            impact.TextFlow = UiTextFlow.Wrap;
            Place(help, impact, 1);
            content = help;
        }
        Place(dock, content, 0);
        Place(dock, BuildJunctionBindingHints(), 1);
        return JunctionPanel(dock);
    }

    private UiElement BuildJunctionCandidatePicker()
    {
        return BuildJunctionCandidateColumns(JunctionCandidates());
    }

    private UiElement BuildJunctionOptimizationConfirmation()
    {
        UiLabel prompt = JunctionLabel(J("apply-question", "Apply changes?"), 18);
        prompt.Id = "junction-review";
        prompt.Focusable = true;
        prompt.VerticalTextAlignment = UiAlignment.Start;
        return prompt;
    }

    private UiElement BuildJunctionImpactList()
    {
        UiGrid content = JunctionRows([24, 0, 22], [1]);
        content.Id = "junction-review";
        content.Focusable = true;
        Place(content, JunctionLabel(junctionEditor == "optimize" ? J("optimize", "Optimize stats") + " · " + J("priority-" + junctionPriority.ToLowerInvariant(), junctionPriority) : J("review", "Review consequences"), 18), 0);
        IReadOnlyList<MenuJunctionChange> changes = junctionPlan?.Changes ?? [];
        int start = Math.Min(junctionImpactPage * 5, Math.Max(0, changes.Count - 1));
        UiGrid rows = Grid([1, 1, 1, 1, 1], [1]);
        for (int index = 0; index < Math.Min(5, changes.Count - start); index++)
        {
            MenuJunctionChange change = changes[start + index];
            UiLabel line = JunctionLabel(JunctionImpactLine(change), 14);
            if (change.Category is "Stat" or "HP" && int.TryParse(change.Before, NumberStyles.Integer, CultureInfo.InvariantCulture, out int before) && int.TryParse(change.After, NumberStyles.Integer, CultureInfo.InvariantCulture, out int after) && before != after)
            {
                line.Color = after > before ? JunctionIncreaseColor : JunctionDecreaseColor;
            }
            Place(rows, line, index);
        }
        Place(content, rows, 1);
        UiGrid paging = Grid([1], [4, 1, 1], 4);
        Place(paging, JunctionLabel(J("changes", "Changes") + $" {Math.Min(changes.Count, start + 1)}–{Math.Min(changes.Count, start + 5)} / {changes.Count}", 12, true), 0);
        Place(paging, JunctionButton("impact-previous", "‹", () =>
        {
            junctionImpactPage = Math.Max(0, junctionImpactPage - 1);
            _view = null;
        }, start > 0, 13), 0, 1);
        Place(paging, JunctionButton("impact-next", "›", () =>
        {
            junctionImpactPage++;
            _view = null;
        }, start + 5 < changes.Count, 13), 0, 2);
        Place(content, paging, 2);
        return content;
    }

    private string JunctionImpactLine(MenuJunctionChange change)
    {
        if (change.Category == "Capability" && junctionPlan is { } plan)
        {
            bool before = plan.Before.Characters.FirstOrDefault(actor => actor.Id == change.CharacterId)?.AvailableJunctionSlots.Contains(change.Slot) == true;
            bool after = plan.After.Characters.FirstOrDefault(actor => actor.Id == change.CharacterId)?.AvailableJunctionSlots.Contains(change.Slot) == true;
            string Availability(bool available) => available ? J("available", "Available") : J("unavailable", "Unavailable");
            return $"{change.CharacterName} · {change.Slot}: {Availability(before)} → {Availability(after)}";
        }
        if (change.Category == "Magic")
        {
            string none = SystemText("ui.junctiondashboard.none", "None");
            string before = change.Before == none ? "---" : change.Before;
            string after = change.After == none ? "---" : change.After;
            return $"{change.CharacterName} · {change.Slot}: {before} → {after}";
        }
        return $"{change.CharacterName} · {change.Slot}: {change.Before} → {change.After}";
    }

    private string JunctionImpactDescription()
    {
        if (junctionPlan is not { } plan)
        {
            return Message;
        }
        if (plan.UnavailableReason is { } unavailable)
        {
            return unavailable;
        }
        return string.Join("\n", plan.Changes.Where(change => change.Category is "HP" or "Command" or "Capacity" || change.Category == "Stat" && change.Slot == "HP").Select(change => $"{change.CharacterName} · {change.Slot}: {change.Before} → {change.After}"));
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
        if (junctionEditor == "actions" && junctionPlan is { } actionPlan)
        {
            return actionPlan.Label == "junction-magic-off-all" ? J("clear-party-magic", "Clear all characters' magic junctions") : J("clear-reviewed", "Apply reviewed removal");
        }
        if (junctionEditor == "gf" && candidate is not null)
        {
            MenuGuardianForce? force = Data.GuardianForces.FirstOrDefault(value => value.Id == candidate.Id);
            string verb = force?.Owner?.Id == Character?.Id ? J("remove", "Remove") : force?.Owner is null ? J("attach", "Attach") : J("transfer", "Transfer");
            return verb + " " + candidate.Name + (force?.Owner?.Id == Character?.Id ? "" : " → " + Character?.Name);
        }
        if (candidate is not null)
        {
            if (candidate.Id == "none" || candidate.Id == "0" && junctionEditor != "magic")
            {
                return J("clear-slot", "Clear this slot");
            }
            MenuMagic? spell = Data.Magic.FirstOrDefault(value => value.Id == candidate.Id);
            return (spell?.BoundSlot is not (null or "") && spell.BoundSlot != _junctionSlot ? J("move", "Move") : J("assign", "Assign")) + " " + candidate.Name;
        }
        return J("apply", "Apply changes");
    }

    private UiElement BuildJunctionActions()
    {
        return BuildJunctionActionColumns("junction-action-columns", [
            ("optimize", J("optimize", "Optimize stats"), OpenJunctionOptimizer),
            ("clear-magic", J("clear-magic", "Clear this character's magic junctions"), () => ReviewJunctionAction(new("junction-off", TargetId: Character?.Id ?? "", Value: "Magic"))),
            ("clear-party-magic", J("clear-party-magic", "Clear all characters' magic junctions"), () => ReviewJunctionAction(new("junction-magic-off-all")))
        ]);
    }

    private UiElement BuildJunctionActionColumns(string id, IReadOnlyList<(string Id, string Label, Action Activate)> actions)
    {
        UiColumnPicker columns = new()
        {
            Id = id,
            RowHeight = 26 * TextScale,
            NameQuantityGap = 0,
            ColumnGap = 16 * TextScale,
            HorizontalPadding = 8 * TextScale,
            InitialIndex = Math.Max(0, actions.ToList().FindIndex(action => action.Id == junctionFocus))
        };
        junctionCandidateColumns = columns;
        foreach (var action in actions)
        {
            UiButton row = JunctionButton(action.Id, "", action.Activate);
            row.DrawBackground = false;
            columns.Add(row, JunctionLabel(action.Label, 16), JunctionLabel("", 16));
        }
        return columns;
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
        junctionFocus = "optimize";
        _view = null;
    }

    private void ReviewJunctionAction(MenuAction action)
    {
        junctionReviewedAction = action;
        junctionPlan = Data.JunctionPlanner?.Plan(action);
        junctionReview = true;
        junctionImpactPage = 0;
        junctionFocus = "junction-review";
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
        junctionFocus = junctionReview ? "junction-review" : "priority-" + priority;
        Message = junctionPlan?.UnavailableReason ?? (junctionReview ? J("scope", "Stat magic only. GFs, commands, passives, elemental and status junctions stay as configured.") : J("no-stat-changes", "No stat changes suggested"));
        _view = null;
    }

    private void PlanJunctionCommands()
    {
        junctionEditor = "fill";
        junctionPlan = Character is { } actor ? Data.JunctionPlanner?.FillEmptyCommands(actor.Id) : null;
        junctionReview = true;
        junctionFocus = "junction-review";
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
        string operationLabel = JunctionOperationLabel();
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
            junctionAttackUndo = null;
            junctionUndoLabel = operationLabel;
            foreach (string id in plan.Changes.Select(change => change.CharacterId).Distinct())
            {
                junctionVisited.Add(id);
            }
            junctionPlan = null;
            junctionTransferModal = false;
            junctionReview = false;
            junctionEditor = "";
            junctionFocus = junctionOrigin;
            Message = J("applied", "Junction changes applied.");
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
        if (junctionPlan is null && junctionAttackUndo is { } attack)
        {
            if (Data.JunctionPlanner?.SourceFingerprint == attack.Fingerprint && _liveAction is not null && Data.SupportedActions.Contains("attack-mode"))
            {
                _liveAction(new MenuAction("attack-mode", TargetId: attack.CharacterId, Value: attack.Mode));
                _liveData = null;
            }
            else
            {
                Message = J("undo-stale", "Undo is unavailable because the relevant game state changed.");
            }
            junctionAttackUndo = null;
            _view = null;
            return;
        }
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
        junctionTransferModal = false;
        junctionReview = false;
        junctionEditor = "";
        junctionFocus = junctionOrigin;
        Message = "";
        _view = null;
    }

    private void PageJunctionCandidates(int delta)
    {
        BuildView().MoveFocus(delta < 0 ? UiKey.Left : UiKey.Right, JunctionControlIsActive);
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
        if (junctionReview)
        {
            Message = J("resolve-review", "Apply or cancel the current review before editing another assignment.");
            _view = null;
            return;
        }
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
        if (JunctionAdjacentCharacter(delta) is not { } next)
        {
            return;
        }
        string focus = junctionEditor.Length > 0 ? junctionOrigin : junctionFocus;
        CancelJunctionEditor();
        junctionFocus = focus;
        _characterId = next.Id;
        _magicId = "";
        _abilitySlot = Math.Clamp(_abilitySlot, 0, Math.Max(0, next.AbilitySlots - 1));
        _view = null;
    }

    private string JunctionFocusedDescription()
    {
        if (Character is not { } actor)
        {
            return J("no-character", "Character data is unavailable.");
        }
        if (junctionFocus.StartsWith("slot-", StringComparison.Ordinal))
        {
            string slot = junctionFocus[5..];
            string assigned = actor.Junctions.GetValueOrDefault(slot) ?? "---";
            return actor.Name + " · " + StatText(slot) + " · " + assigned + (junctionDashboard.IsPinned(actor.Id, slot) ? " · " + J("pinned", "Pinned") : "");
        }
        if (junctionFocus.StartsWith("gf-portrait-", StringComparison.Ordinal) && Data.GuardianForces.FirstOrDefault(force => "gf-portrait-" + force.Id == junctionFocus) is { } gf)
        {
            return gf.Name + " · " + J("learning", "Learning") + ": " + gf.Learning;
        }
        if (junctionFocus.StartsWith("command-", StringComparison.Ordinal) && int.TryParse(junctionFocus.AsSpan(8), out int command))
        {
            return actor.Name + " · " + J("command", "Command") + " " + (command + 1) + " · " + actor.Commands.ElementAtOrDefault(command + 1);
        }
        if (junctionFocus.StartsWith("passive-", StringComparison.Ordinal) && int.TryParse(junctionFocus.AsSpan(8), out int passive))
        {
            return actor.Name + " · " + J("passive", "Passive") + " " + (passive + 1) + " · " + actor.Abilities.ElementAtOrDefault(passive);
        }
        return J("select-slot", "Select an assignment to inspect or edit it.");
    }

    private bool BackFromJunctionDashboard()
    {
        if (!IsJunctionDashboard)
        {
            return false;
        }
        if (junctionEditor == "gf" && !junctionTransferModal)
        {
            junctionEditor = "";
            junctionCandidate = "";
            junctionPlan = null;
        }
        if (junctionEditor.Length > 0 || junctionPlan is not null)
        {
            CancelJunctionEditor();
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
        if (input == MenuInput.Cancel)
        {
            Back();
            return true;
        }
        if (junctionEditor == "gf" && BuildView().FocusedElement is UiList capability && input is MenuInput.Up or MenuInput.Down or MenuInput.PageUp or MenuInput.PageDown)
        {
            capability.Key(input switch
            {
                MenuInput.Up => UiKey.Up,
                MenuInput.Down => UiKey.Down,
                MenuInput.PageUp => UiKey.PageUp,
                _ => UiKey.PageDown
            });
            return true;
        }
        if (input is MenuInput.PageLeft or MenuInput.PageRight)
        {
            if (!JunctionHasActiveScope)
            {
                SwitchJunctionCharacter(input == MenuInput.PageLeft ? -1 : 1);
            }
            return true;
        }
        if (input is MenuInput.PageUp or MenuInput.PageDown)
        {
            if (junctionTransferModal)
            {
                junctionTransferColumns?.ScrollColumns(input == MenuInput.PageUp ? -1 : 1);
            }
            else if (junctionReview && junctionEditor != "optimize")
            {
                int maximum = Math.Max(0, ((junctionPlan?.Changes.Count ?? 0) - 1) / 5);
                junctionImpactPage = Math.Clamp(junctionImpactPage + (input == MenuInput.PageUp ? -1 : 1), 0, maximum);
                _view = null;
            }
            else if (!junctionReview && junctionEditor is "magic" or "command" or "passive" or "actions" or "optimize")
            {
                PageJunctionCandidates(input == MenuInput.PageUp ? -1 : 1);
            }
            return true;
        }
        if (input is MenuInput.Actions or MenuInput.Alternate)
        {
            if (!JunctionHasActiveScope)
            {
                OpenJunctionActions();
            }
            return true;
        }
        UiView view = BuildView();
        if (input == MenuInput.Confirm)
        {
            if (junctionReview && !junctionTransferModal)
            {
                if (view.FocusedElement?.Id == "junction-cancel")
                {
                    CancelJunctionEditor();
                }
                else
                {
                    ConfirmJunctionPlan();
                }
            }
            else
            {
                view.Key(UiKey.Confirm);
            }
            return true;
        }
        if (input is MenuInput.Tab or MenuInput.ReverseTab or MenuInput.FocusLeft or MenuInput.FocusRight)
        {
            UiElement[] controls = FocusableDescendants(view.Root).Where(JunctionControlIsActive).Where(element => element.Enabled && element.Visible).ToArray();
            int index = Array.FindIndex(controls, element => element.Id == junctionFocus);
            int delta = input is MenuInput.ReverseTab or MenuInput.FocusLeft ? -1 : 1;
            if (controls.Length > 0)
            {
                view.Focus(controls[Math.Clamp(index + delta, 0, controls.Length - 1)]);
            }
            return true;
        }
        if (input is MenuInput.Up or MenuInput.Down or MenuInput.Left or MenuInput.Right)
        {
            MoveJunctionSpatialFocus(view, input);
        }
        return true;
    }

    private void MoveJunctionSpatialFocus(UiView view, MenuInput input)
    {
        view.MoveFocus(input switch
        {
            MenuInput.Left => UiKey.Left,
            MenuInput.Right => UiKey.Right,
            MenuInput.Up => UiKey.Up,
            _ => UiKey.Down
        }, JunctionControlIsActive);
    }
}
