using System.Globalization;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    internal bool IsJunctionDashboard => ScreenId == "junction-dashboard";
    private readonly JunctionDashboardState junctionDashboard = new();
    private MenuJunctionPlan? junctionPlan;
    private MenuJunctionPlan? junctionUndo;
    private string junctionFocus = "slot-HP";
    private string junctionOrigin = "slot-HP";
    private string junctionEditor = "";
    private string junctionCandidate = "";
    private int junctionCandidatePage;
    private int junctionImpactPage;
    private string junctionPriority = "Attack";
    private bool junctionReview;
    private bool junctionExitReview;
    private bool junctionLeaving;
    private readonly HashSet<string> junctionVisited = new(StringComparer.Ordinal);
    private readonly JunctionCommandAudit junctionAudit = new();
    private string junctionSession = "";
    private float junctionViewportWidth = 1280;
    private float junctionViewportHeight = 720;
    private float junctionScrollOffset;
    private UiScrollPanel? junctionScroll;
    internal bool JunctionAccessible => TextScale > 1.05f || junctionViewportWidth < 1100 || junctionViewportHeight < 600;

    public void PrepareViewport(float width, float height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }
        bool changed = junctionViewportWidth != width || junctionViewportHeight != height;
        junctionViewportWidth = width;
        junctionViewportHeight = height;
        if (IsJunctionDashboard && changed)
        {
            _view = null;
        }
    }

    private string J(string key, string fallback) => SystemText("junction.dashboard." + key, fallback);

    private void RegisterJunctionDashboard()
    {
        Register(new MenuScreenDefinition
        {
            Id = "junction-dashboard",
            Title = "Junction",
            Help = "Inspect and edit this character's junctions. Confirm a proposal to apply it.",
            Layout = MenuLayout.Junction,
            GalleryEligible = true,
            Entries = controller => controller.JunctionDashboardEntries()
        });
    }

    private string ResolveJunctionDashboard(string screen)
    {
        if (!screen.StartsWith("junction", StringComparison.Ordinal) || screen == "junction-dashboard")
        {
            return screen;
        }
        junctionEditor = "";
        junctionPlan = null;
        junctionReview = false;
        junctionExitReview = false;
        junctionVisited.Clear();
        junctionFocus = screen switch
        {
            "junction-gf" => "manage-gfs",
            "junction-command" or "junction-command-choice" => "command-0",
            "junction-ability" or "junction-ability-choice" => "passive-0",
            "junction-element" => "slot-Elem-Atk",
            "junction-status" => "slot-ST-Atk",
            _ => "slot-HP"
        };
        return "junction-dashboard";
    }

    private void FocusJunctionDashboard(UiView view)
    {
        if (junctionSession != Data.SessionId)
        {
            junctionSession = Data.SessionId;
            junctionUndo = null;
            junctionDashboard.SetSession(junctionSession);
            junctionAudit.SetSession(junctionSession);
        }
        var elements = FocusableDescendants(view.Root).ToArray();
        view.Focus(elements.FirstOrDefault(element => element.Id == junctionFocus) ?? elements.FirstOrDefault());
    }

    private UiButton JunctionButton(string id, string text, Action click, bool enabled = true, float size = 15)
    {
        return new UiButton
        {
            Id = id,
            Text = text,
            FontSize = size * TextScale,
            TextInset = 10,
            PointerSize = 7,
            Enabled = enabled,
            Click = click,
            FocusChanged = focused =>
            {
                if (focused)
                {
                    junctionFocus = id;
                }
            }
        };
    }

    private UiLabel JunctionLabel(string text, float size = 15, bool muted = false) => new()
    {
        Text = text,
        FontSize = size * TextScale,
        Muted = muted,
        VerticalTextAlignment = UiAlignment.Center,
        TextFlow = UiTextFlow.SingleLine
    };

    private static UiGrid JunctionRows(float[] heights, float[] widths, float gap = 0)
    {
        UiGrid grid = new() { Gap = gap };
        foreach (float height in heights)
        {
            grid.RowDefinitions.Add(height == 0 ? UiGridLength.Star() : UiGridLength.Pixel(height));
        }
        foreach (float width in widths)
        {
            grid.ColumnDefinitions.Add(UiGridLength.Star(width));
        }
        return grid;
    }

    private static UiPanel JunctionPanel(UiElement content)
    {
        UiPanel panel = new() { CaptionHeight = 0, Padding = 5 };
        panel.Children.Add(content);
        return panel;
    }

    private MenuCharacter? JunctionPreviewCharacter => junctionPlan?.After.Characters.FirstOrDefault(character => character.Id == Character?.Id);

    private UiElement BuildJunctionDashboard()
    {
        if (junctionPlan is not null && Data.JunctionPlanner?.IsCurrent(junctionPlan) != true)
        {
            junctionPlan = null;
            junctionReview = false;
            Message = J("stale", "The source changed. Select the proposal again to review its current effects.");
        }
        bool accessible = JunctionAccessible;
        float dockHeight = accessible ? Math.Min(junctionViewportHeight * 0.35f, 160 * TextScale) : 160;
        UiGrid page = JunctionRows(accessible ? [44 * TextScale, 0, dockHeight, 32 * TextScale] : [44, 76, 0, 160, 32], [1], 8);
        page.Id = "junction-dashboard";
        Place(page, BuildJunctionHeader(), 0);
        if (!accessible)
        {
            Place(page, BuildJunctionGfs(), 1);
        }
        UiGrid dashboard = accessible ? Grid(junctionViewportWidth < 850 ? [1, 1, 1, 1] : [1, 1], junctionViewportWidth < 850 ? [1] : [1, 1], 12) : Grid([1], [300, 294, 366, 252], 12);
        Place(dashboard, BuildJunctionStats(), 0);
        UiGrid assignments = Grid([1, 1], [1], 8);
        Place(assignments, BuildJunctionBindings(true), 0);
        Place(assignments, BuildJunctionBindings(false), 1);
        Place(dashboard, assignments, accessible && junctionViewportWidth < 850 ? 1 : 0, accessible && junctionViewportWidth < 850 ? 0 : 1);
        UiGrid effects = Grid([1, 1], [1], 8);
        Place(effects, BuildJunctionPercentageTable(true), 0);
        Place(effects, BuildJunctionPercentageTable(false), 1);
        Place(dashboard, effects, accessible ? junctionViewportWidth < 850 ? 2 : 1 : 0, accessible ? 0 : 2);
        Place(dashboard, BuildJunctionAbilities(), accessible ? junctionViewportWidth < 850 ? 3 : 1 : 0, accessible ? junctionViewportWidth < 850 ? 0 : 1 : 3);
        if (accessible)
        {
            junctionScroll = new UiScrollPanel
            {
                Id = "junction-scroll",
                ContentHeight = (junctionViewportWidth < 850 ? 1580 : 850) * TextScale,
                OffsetChanged = offset => junctionScrollOffset = offset
            };
            UiGrid scrollContent = JunctionRows([junctionViewportWidth < 850 ? 116 * TextScale : 76 * TextScale, 0], [1], 8);
            Place(scrollContent, BuildJunctionGfs(), 0);
            Place(scrollContent, dashboard, 1);
            junctionScroll.Children.Add(scrollContent);
            Place(page, junctionScroll, 1);
        }
        else
        {
            junctionScroll = null;
            Place(page, dashboard, 2);
        }
        Place(page, BuildJunctionDock(), accessible ? 2 : 3);
        UiGrid footer = Grid([1], [3, 1, 1.5f, 1], 8);
        string hints = CurrentBindingText(ControlAction.CameraLeft) + " / " + CurrentBindingText(ControlAction.CameraRight);
        Place(footer, JunctionLabel(hints + "  " + J("character", "Character")), 0);
        Place(footer, JunctionButton("junction-undo", J("undo", "Undo") + (junctionUndo is null ? "" : ": " + J("last-edit", "last edit")), UndoJunction, junctionUndo is not null && junctionPlan is null), 0, 1);
        Place(footer, JunctionButton("junction-actions", CurrentBindingText(ControlAction.Card) + "  " + J("actions", "Actions"), OpenJunctionActions), 0, 2);
        Place(footer, JunctionButton("junction-back", J("back", "Back"), Back), 0, 3);
        Place(page, footer, accessible ? 3 : 4);
        return page;
    }

    private UiElement BuildJunctionHeader()
    {
        UiGrid header = Grid([1], [44, 250, 44, 380, 250], 8);
        Place(header, JunctionButton("previous-character", "‹", () => SwitchJunctionCharacter(-1), !junctionReview), 0);
        Place(header, JunctionLabel($"{Character?.Name}   {J("level", "Lv")} {Number(Character?.Level)}", 23), 0, 1);
        Place(header, JunctionButton("next-character", "›", () => SwitchJunctionCharacter(1), !junctionReview), 0, 2);
        Place(header, JunctionLabel("HP  " + (Character is null ? "—" : Hp(Character)), 22), 0, 3);
        int count = Character?.CommandIds.Count(id => id != "0") ?? 0;
        Place(header, JunctionButton("command-attention", J("commands", "Commands") + $" {count} / 3", () => OpenJunctionEditor("command", "command-0")), 0, 4);
        return header;
    }

    private UiElement BuildJunctionGfs()
    {
        UiGrid body = JunctionRows([20, 0], [1]);
        UiGrid heading = Grid([1], [3, 1]);
        Place(heading, JunctionLabel(J("guardian-forces", "Guardian Forces") + $" · {Character?.GuardianForceIds.Count ?? 0}", 13, true), 0);
        Place(heading, JunctionButton("manage-gfs", J("manage-gfs", "Manage GFs"), () => OpenJunctionEditor("gf", "manage-gfs"), size: 13), 0, 1);
        Place(body, heading, 0);
        int columns = JunctionAccessible && junctionViewportWidth < 850 ? 4 : 8;
        UiGrid chips = Grid(Enumerable.Repeat(1f, 16 / columns).ToArray(), Enumerable.Repeat(1f, columns).ToArray(), 3);
        string[] ids = Character?.GuardianForceIds.ToArray() ?? [];
        for (int index = 0; index < ids.Length; index++)
        {
            string id = ids[index];
            string name = Data.GuardianForces.FirstOrDefault(force => force.Id == id)?.Name ?? id;
            Place(chips, JunctionButton("assigned-" + id, name, () => OpenJunctionEditor("gf", "assigned-" + id, id), size: 15), index / columns, index % columns);
        }
        Place(body, chips, 1);
        return JunctionPanel(body);
    }

    private UiElement BuildJunctionStats()
    {
        UiGrid body = JunctionRows([24, 20, 0], [1]);
        Place(body, JunctionLabel(J("stats", "Stats"), 15, true), 0);
        UiGrid legend = Grid([1], [42, 112, 45, 87], 0);
        string[] labels = [J("stat", "Stat"), J("magic", "Magic"), J("stock", "Stock"), J("value", "Value")];
        for (int index = 0; index < labels.Length; index++)
        {
            Place(legend, JunctionLabel(labels[index], 11, true), 0, index);
        }
        Place(body, legend, 1);
        UiGrid rows = Grid(Enumerable.Repeat(1f, 9).ToArray(), [1], 0);
        for (int index = 0; index < 9; index++)
        {
            string slot = LiveMenuData.JunctionSlotName(index);
            Place(rows, BuildJunctionBindingRow(slot, true), index);
        }
        Place(body, rows, 2);
        return JunctionPanel(body);
    }

    private UiElement BuildJunctionBindings(bool elemental)
    {
        UiGrid body = JunctionRows([24, 0], [1]);
        Place(body, JunctionLabel(elemental ? J("elemental-junctions", "Elemental junctions") : J("status-junctions", "Status junctions"), 14, true), 0);
        UiGrid rows = Grid([1, 1, 1, 1, 1], [1], 0);
        int[] slots = elemental ? [9, 11, 12, 13, 14] : [10, 15, 16, 17, 18];
        for (int index = 0; index < slots.Length; index++)
        {
            Place(rows, BuildJunctionBindingRow(LiveMenuData.JunctionSlotName(slots[index]), false), index);
        }
        Place(body, rows, 1);
        return JunctionPanel(body);
    }

    private UiElement BuildJunctionBindingRow(string slot, bool stat)
    {
        bool available = Character?.AvailableJunctionSlots.Contains(slot) == true;
        string currentId = Character?.JunctionIds.GetValueOrDefault(slot) ?? "0";
        MenuMagic? magic = Data.Magic.FirstOrDefault(spell => spell.Id == currentId);
        string assigned = Character?.Junctions.GetValueOrDefault(slot) ?? (available ? J("none", "None") : J("locked", "Locked"));
        string name = slot.StartsWith("Elem-", StringComparison.Ordinal) ? slot[5..] : slot.StartsWith("ST-", StringComparison.Ordinal) ? slot[3..] : slot;
        bool pinned = junctionDashboard.IsPinned(Character?.Id ?? "", slot);
        UiGrid content = Grid([1], stat ? [42, 112, 45, 87] : [55, 178, 45], 0);
        Place(content, JunctionLabel((pinned ? "◆ " : "") + name, 12, true), 0);
        Place(content, JunctionLabel(assigned, 16, !available), 0, 1);
        Place(content, JunctionLabel(magic?.Quantity.ToString(CultureInfo.InvariantCulture) ?? "—", 14, true), 0, 2);
        if (stat)
        {
            int? current = Character?.Stats.GetValueOrDefault(slot);
            int? next = JunctionPreviewCharacter?.Stats.GetValueOrDefault(slot);
            string value = current?.ToString(CultureInfo.InvariantCulture) ?? "—";
            if (next is not null && next != current)
            {
                value += "→" + next;
            }
            if (slot is "Eva" or "Hit")
            {
                value += "%";
            }
            Place(content, JunctionLabel(value, 16), 0, 3);
        }
        string id = "slot-" + slot;
        UiButton row = JunctionButton(id, "", () => OpenJunctionEditor("magic", id));
        row.DrawBackground = false;
        row.Children.Add(content);
        row.FocusChanged = focused =>
        {
            if (focused)
            {
                junctionFocus = id;
                if (junctionDashboard.SetDestination(slot))
                {
                    _view = null;
                }
            }
        };
        return row;
    }

    private UiElement BuildJunctionPercentageTable(bool elemental)
    {
        JunctionEffectFamily family = elemental ? JunctionEffectFamily.Elemental : JunctionEffectFamily.Status;
        bool attack = (elemental ? junctionDashboard.ElementalDirection : junctionDashboard.StatusDirection) == JunctionEffectDirection.Attack;
        string prefix = elemental ? "elemental" : "status";
        IReadOnlyList<MenuJunctionPercentage>? current = elemental ? Character?.JunctionEffects?.Elements : Character?.JunctionEffects?.Statuses;
        IReadOnlyList<MenuJunctionPercentage>? next = elemental ? JunctionPreviewCharacter?.JunctionEffects?.Elements : JunctionPreviewCharacter?.JunctionEffects?.Statuses;
        UiGrid body = JunctionRows([24, 0], [1]);
        UiGrid heading = Grid([1], [1.5f, 1, 1], 3);
        Place(heading, JunctionLabel(elemental ? J("elements", "Elements") : J("statuses", "Statuses"), 13, true), 0);
        foreach (bool direction in new[] { true, false })
        {
            bool changed = current is not null && next is not null && current.Where((value, index) => index < next.Count && (direction ? value.Attack != next[index].Attack : value.Defense != next[index].Defense)).Any();
            string title = direction ? J("attack", "Attack") : J("defense", "Defense");
            Place(heading, JunctionButton(prefix + (direction ? "-attack" : "-defense"), (direction == attack ? "● " : "") + title + (changed ? " *" : ""), () =>
            {
                junctionDashboard.SetDirection(family, direction ? JunctionEffectDirection.Attack : JunctionEffectDirection.Defense);
                _view = null;
            }, size: 12), 0, direction ? 1 : 2);
        }
        Place(body, heading, 0);
        int count = elemental ? 8 : 13;
        int rowCount = elemental ? 4 : 7;
        UiGrid rows = Grid(Enumerable.Repeat(1f, rowCount).ToArray(), [1, 1], 2);
        for (int index = 0; index < count; index++)
        {
            int position = index;
            MenuJunctionPercentage? value = current?.ElementAtOrDefault(index);
            MenuJunctionPercentage? preview = next?.ElementAtOrDefault(index);
            string[] elements = ["Fire", "Ice", "Thunder", "Earth", "Poison", "Wind", "Water", "Holy"];
            string[] statuses = ["Death", "Poison", "Petrify", "Darkness", "Silence", "Berserk", "Zombie", "Sleep", "Slow", "Stop", "Curse", "Confuse", "Drain"];
            string label = value?.Name ?? J(prefix + "-" + index, (elemental ? elements : statuses)[index]);
            string percentage = value is null ? "—" : (attack ? value.Attack : value.Defense).ToString(CultureInfo.InvariantCulture);
            if (value is not null && preview is not null && (attack ? value.Attack != preview.Attack : value.Defense != preview.Defense))
            {
                percentage += "→" + (attack ? preview.Attack : preview.Defense);
            }
            if (value is not null)
            {
                percentage += "%";
            }
            UiGrid readout = Grid([1], [1, 1.1f], 2);
            readout.Id = prefix + "-readout-" + position;
            Place(readout, JunctionLabel(label, elemental ? 15 : 14), 0);
            Place(readout, JunctionLabel(percentage, elemental ? 16 : 15, value is null), 0, 1);
            Place(rows, readout, index % rowCount, index / rowCount);
        }
        Place(body, rows, 1);
        return JunctionPanel(body);
    }

    private UiElement BuildJunctionAbilities()
    {
        UiGrid body = JunctionRows([24, 0, 24, 0], [1]);
        int count = Character?.CommandIds.Count(id => id != "0") ?? 0;
        Place(body, JunctionLabel(J("commands", "Commands") + $" {count}/3", 15, true), 0);
        UiGrid commands = Grid([1, 1, 1, 1], [1], 0);
        Place(commands, JunctionButton("basic-action", Character?.Commands.FirstOrDefault() ?? "—", OpenJunctionAttackMode), 0);
        for (int index = 0; index < 3; index++)
        {
            int slot = index;
            bool empty = Character?.CommandIds.ElementAtOrDefault(index) is null or "0";
            string name = Character?.Commands.ElementAtOrDefault(index + 1) ?? "—";
            bool actionable = empty && Character is { } actor && actor.AvailableCommands.Any(choice => !actor.CommandIds.Contains(choice.Id));
            Place(commands, JunctionButton("command-" + index, $"{index + 1}  " + (empty ? (actionable ? "! " : "") + J("empty", "Empty") : name), () => OpenJunctionEditor("command", "command-" + slot)), index + 1);
        }
        Place(body, commands, 1);
        Place(body, JunctionLabel(J("passives", "Passive abilities") + $" · {Character?.AbilitySlots ?? 0}/4", 14, true), 2);
        UiGrid passives = Grid([1, 1, 1, 1], [1], 0);
        for (int index = 0; index < 4; index++)
        {
            int slot = index;
            string name = index >= (Character?.AbilitySlots ?? 0) ? J("locked", "Locked") : Character?.Abilities.ElementAtOrDefault(index) is not (null or "--") and var assigned ? assigned : J("empty", "Empty");
            Place(passives, JunctionButton("passive-" + index, $"{index + 1}  {name}", () => OpenJunctionEditor("passive", "passive-" + slot)), index);
        }
        Place(body, passives, 3);
        return JunctionPanel(body);
    }
}
