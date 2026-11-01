using System.Globalization;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    internal bool IsJunctionDashboard => ScreenId == "junction-dashboard";
    private readonly JunctionDashboardState junctionDashboard = new();
    private MenuJunctionPlan? junctionPlan;
    private MenuJunctionPlan? junctionUndo;
    private string junctionUndoLabel = "";
    private (string CharacterId, string Mode, string Fingerprint)? junctionAttackUndo;
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
            "junction-gf" => "junction-gfs",
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
        var elements = FocusableDescendants(view.Root).Where(JunctionControlIsActive).Where(element => element.Enabled).ToArray();
        junctionRestoringFocus = true;
        try
        {
            view.Focus(elements.FirstOrDefault(element => element.Id == junctionFocus) ?? elements.FirstOrDefault());
        }
        finally
        {
            junctionRestoringFocus = false;
        }
    }

    private void EnsureJunctionSession()
    {
        if (junctionSession != Data.SessionId)
        {
            junctionSession = Data.SessionId;
            junctionUndo = null;
            junctionAttackUndo = null;
            junctionDashboard.SetSession(junctionSession);
            junctionAudit.SetSession(junctionSession);
        }
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
            Hover = () => LeaveGuardianPreview(id),
            FocusChanged = focused =>
            {
                if (focused)
                {
                    LeaveGuardianPreview(id);
                    bool changed = junctionFocus != id;
                    junctionFocus = id;
                    if (changed && junctionEditor.Length == 0)
                    {
                        _view = null;
                    }
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
    private IReadOnlyList<MenuMagic> JunctionMagicInventory => Data.Characters.Any(character => character.Id == Character?.Id) ? Data.Magic : Data.JunctionPlanner?.Snapshot.Magic ?? Data.Magic;

    private UiElement BuildJunctionDashboard()
    {
        EnsureJunctionSession();
        if (junctionFocus is "junction-gfs" or "manage-gfs")
        {
            junctionFocus = JunctionGfFocusId;
        }
        if (junctionEditor.Length == 0 && junctionFocus.StartsWith("gf-portrait-", StringComparison.Ordinal))
        {
            PreviewGuardian(junctionFocus[12..]);
        }
        if (junctionFocus.StartsWith("slot-", StringComparison.Ordinal))
        {
            junctionDashboard.SetDestination(junctionFocus[5..]);
        }
        if (junctionEditor == "attack-mode" && !Data.SmartAttack)
        {
            junctionEditor = "";
            junctionFocus = "basic-action";
            Message = J("physical-attack", "This is the effective basic action. Smart Attack is disabled.");
        }
        if (junctionPlan is not null && Data.JunctionPlanner?.IsCurrent(junctionPlan) != true)
        {
            junctionPlan = null;
            junctionReview = false;
            Message = J("stale", "The source changed. Select the proposal again to review its current effects.");
        }
        bool accessible = JunctionAccessible;
        float dockHeight = accessible ? Math.Min(junctionViewportHeight * 0.35f, 160 * TextScale) : 160;
        UiGrid page = JunctionRows(accessible ? [44 * TextScale, 0, dockHeight] : [44, 76, 0, 160], [1], 8);
        page.Id = "junction-dashboard";
        Place(page, BuildJunctionHeader(), 0);
        if (!accessible)
        {
            Place(page, BuildJunctionGfs(), 1);
        }
        UiGrid dashboard = accessible ? Grid(junctionViewportWidth < 850 ? [1, 1, 1, 1] : [1, 1], junctionViewportWidth < 850 ? [1] : [1, 1], 12) : Grid([1], [252, 300, 294, 366], 12);
        Place(dashboard, BuildJunctionAbilities(), 0);
        Place(dashboard, BuildJunctionStats(), accessible && junctionViewportWidth < 850 ? 1 : 0, accessible && junctionViewportWidth < 850 ? 0 : 1);
        UiGrid assignments = Grid([1, 1], [1], 8);
        Place(assignments, BuildJunctionBindings(true), 0);
        Place(assignments, BuildJunctionBindings(false), 1);
        Place(dashboard, assignments, accessible ? junctionViewportWidth < 850 ? 2 : 1 : 0, accessible ? 0 : 2);
        UiGrid effects = Grid([1, 1], [1], 8);
        Place(effects, BuildJunctionPercentageTable(true), 0);
        Place(effects, BuildJunctionPercentageTable(false), 1);
        Place(dashboard, effects, accessible ? junctionViewportWidth < 850 ? 3 : 1 : 0, accessible ? junctionViewportWidth < 850 ? 0 : 1 : 3);
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
        return page;
    }

    private UiElement BuildJunctionHeader()
    {
        UiGrid header = new() { Gap = 8 };
        header.RowDefinitions.Add(UiGridLength.Star());
        header.ColumnDefinitions.Add(UiGridLength.Pixel(44));
        header.ColumnDefinitions.Add(UiGridLength.Star(250));
        header.ColumnDefinitions.Add(UiGridLength.Star(380));
        header.ColumnDefinitions.Add(UiGridLength.Pixel(JunctionAccessible && junctionViewportWidth < 850 ? 164 : 250));
        UiImage face = Portrait(Character);
        face.Id = "junction-character-portrait";
        Place(header, face, 0);
        UiGrid identity = JunctionRows([14, 0], [1]);
        Place(identity, JunctionLabel(J("title", "Junction"), 11, true), 0);
        Place(identity, JunctionLabel($"{Character?.Name}   {J("level", "Lv")} {Number(Character?.Level)}", 23), 1);
        Place(header, identity, 0, 1);
        UiGrid hp = JunctionRows([0, 5], [1], 4);
        Place(hp, JunctionLabel("HP  " + (Character is null ? "—" : Hp(Character)), 22), 0);
        Place(hp, new UiProgressBar { Min = 0, Max = Math.Max(1, Character?.MaximumHp ?? 1), Value = Character?.CurrentHp ?? 0 }, 1);
        Place(header, hp, 0, 2);
        UiGrid navigation = new() { Gap = JunctionAccessible && junctionViewportWidth < 850 ? 4 : 8 };
        navigation.RowDefinitions.Add(UiGridLength.Star());
        navigation.ColumnDefinitions.Add(UiGridLength.Star());
        int column = 1;
        foreach (int direction in new[] { -1, 1 })
        {
            if (JunctionAdjacentCharacter(direction) is not { } neighbor)
            {
                continue;
            }
            string id = direction < 0 ? "previous-character" : "next-character";
            if (direction < 0)
            {
                navigation.ColumnDefinitions.Add(UiGridLength.Pixel(30));
                Place(navigation, JunctionLabel("⟵", 19), 0, column++);
            }
            navigation.ColumnDefinitions.Add(UiGridLength.Pixel(44));
            UiButton button = JunctionButton(id, "", () => SwitchJunctionCharacter(direction), !junctionReview);
            button.DrawBackground = false;
            UiImage portrait = Portrait(neighbor);
            portrait.Id = id + "-portrait";
            button.Children.Add(portrait);
            Place(navigation, button, 0, column++);
            if (direction > 0)
            {
                navigation.ColumnDefinitions.Add(UiGridLength.Pixel(30));
                Place(navigation, JunctionLabel("⟶", 19), 0, column++);
            }
        }
        Place(header, navigation, 0, 3);
        return header;
    }

    private UiElement BuildJunctionGfs()
    {
        JunctionGuardianPortraitRow portraits = new() { Id = "junction-gf-portraits" };
        foreach (MenuGuardianForce force in Data.GuardianForces.Take(16))
        {
            string id = force.Id;
            bool assigned = Character?.GuardianForceIds.Contains(id) == true;
            UiGrid content = JunctionRows([20, 0], [1]);
            UiImage ownerPortrait = new()
            {
                Id = "gf-owner-" + id,
                Source = force.Owner?.NativeId is int ownerId ? new UiImageSource(UiImageKind.CharacterPortrait, ownerId) : null,
                Visible = force.Owner is not null
            };
            Place(content, ownerPortrait, 0);
            Place(content, new UiImage
            {
                Id = "gf-image-" + id,
                Source = force.NativeId is int nativeId ? new UiImageSource(UiImageKind.GuardianForcePortrait, nativeId, Opacity: assigned ? (byte)255 : (byte)128) : null
            }, 1);
            UiButton button = JunctionButton("gf-portrait-" + id, "", () => ActivateGuardian(id));
            button.DrawBackground = false;
            button.SuppressSelection = true;
            button.Hover = () =>
            {
                junctionFocus = button.Id!;
                PreviewGuardian(id);
            };
            button.Children.Add(content);
            button.FocusChanged = focused =>
            {
                if (focused && !junctionRestoringFocus)
                {
                    junctionFocus = button.Id!;
                    PreviewGuardian(id);
                }
            };
            portraits.Children.Add(button);
        }
        return JunctionPanel(portraits);
    }

    private UiElement BuildJunctionStats()
    {
        UiGrid body = JunctionRows([24 * TextScale, 0], [1]);
        UiGrid legend = Grid([1], [42, 112, 45, 87], 0);
        legend.Margin = new UiThickness(8, 0, 0, 0);
        string[] labels = [J("stat", "Stat"), J("magic", "Magic"), J("stock", "Stock"), J("value", "Value")];
        for (int index = 0; index < labels.Length; index++)
        {
            Place(legend, JunctionLabel(labels[index], 15, true), 0, index);
        }
        Place(body, legend, 0);
        UiGrid rows = Grid(Enumerable.Repeat(1f, 9).ToArray(), [1], 0);
        for (int index = 0; index < 9; index++)
        {
            string slot = LiveMenuData.JunctionSlotName(index);
            Place(rows, BuildJunctionBindingRow(slot, true), index);
        }
        Place(body, rows, 1);
        return JunctionPanel(body);
    }

    private UiElement BuildJunctionBindings(bool elemental)
    {
        UiGrid body = JunctionRows([24, 20, 0], [1]);
        Place(body, JunctionLabel(elemental ? J("elemental-junctions", "Elemental junctions") : J("status-junctions", "Status junctions"), 14, true), 0);
        UiGrid legend = Grid([1], [55, 178, 45], 0);
        Place(legend, JunctionLabel(J("magic", "Magic"), 11, true), 0, 1);
        Place(legend, JunctionLabel(J("stock", "Stock"), 11, true), 0, 2);
        Place(body, legend, 1);
        UiGrid rows = Grid([1, 1, 1, 1, 1], [1], 0);
        int[] slots = elemental ? [9, 11, 12, 13, 14] : [10, 15, 16, 17, 18];
        for (int index = 0; index < slots.Length; index++)
        {
            Place(rows, BuildJunctionBindingRow(LiveMenuData.JunctionSlotName(slots[index]), false), index);
        }
        Place(body, rows, 2);
        return JunctionPanel(body);
    }

    private UiElement BuildJunctionBindingRow(string slot, bool stat)
    {
        bool available = Character?.AvailableJunctionSlots.Contains(slot) == true;
        bool projectedAvailable = JunctionPreviewCharacter?.AvailableJunctionSlots.Contains(slot) ?? available;
        bool availabilityChanged = available != projectedAvailable;
        string currentId = Character?.JunctionIds.GetValueOrDefault(slot) ?? "0";
        string actorId = Character?.Id ?? "";
        MenuMagic? magic = Character?.Junctions.ContainsKey(slot) == true ? JunctionMagicInventory.FirstOrDefault(spell => spell.CharacterId == actorId && spell.Id == currentId) : null;
        string assigned = Character?.Junctions.GetValueOrDefault(slot) ?? "---";
        string name = StatText(slot);
        if (slot.StartsWith("Elem-", StringComparison.Ordinal))
        {
            name = slot == "Elem-Atk" ? J("attack-short", "Atk") : J("defense-short", "Def") + " " + slot[^1];
        }
        else if (slot.StartsWith("ST-", StringComparison.Ordinal))
        {
            name = slot == "ST-Atk" ? J("attack-short", "Atk") : J("defense-short", "Def") + " " + slot[^1];
        }
        UiGrid content = Grid([1], stat ? [42, 112, 45, 87] : [55, 178, 45], 0);
        content.Margin = new UiThickness(8, 0, 0, 0);
        UiLabel slotLabel = JunctionLabel(name, stat ? 15 : 12, true);
        if (availabilityChanged)
        {
            slotLabel.Color = projectedAvailable ? JunctionIncreaseColor : JunctionDecreaseColor;
        }
        Place(content, slotLabel, 0);
        if (availabilityChanged)
        {
            UiGrid availability = Grid([1], projectedAvailable ? [1] : [22, 90], 0);
            if (!projectedAvailable)
            {
                Place(availability, new JunctionLockIcon { Id = "junction-lock-" + slot }, 0);
            }
            UiLabel change = JunctionLabel(projectedAvailable ? "+ " + J("slot-available", "Available") : "− " + J("slot-unavailable", "Locked"), 13);
            change.Id = "junction-slot-availability-" + slot;
            change.Color = projectedAvailable ? JunctionIncreaseColor : JunctionDecreaseColor;
            Place(availability, change, 0, projectedAvailable ? 0 : 1);
            Place(content, availability, 0, 1);
        }
        else
        {
            Place(content, available ? JunctionLabel(assigned, 16) : new JunctionLockIcon { Id = "junction-lock-" + slot }, 0, 1);
        }
        Place(content, JunctionLabel(magic?.Quantity.ToString(CultureInfo.InvariantCulture) ?? "—", stat ? 15 : 14, true), 0, 2);
        if (stat)
        {
            int? current = Character?.Stats.TryGetValue(slot, out int existing) == true ? existing : null;
            int? next = JunctionPreviewCharacter?.Stats.TryGetValue(slot, out int proposed) == true ? proposed : null;
            string value = current?.ToString(CultureInfo.InvariantCulture) ?? "—";
            if (next is not null && next != current)
            {
                value += "→" + next;
            }
            if (slot is "Eva" or "Hit")
            {
                value += "%";
            }
            UiLabel readout = JunctionLabel(value, 16);
            if (next is not null && next != current)
            {
                readout.Color = next > current ? JunctionIncreaseColor : JunctionDecreaseColor;
            }
            Place(content, readout, 0, 3);
        }
        string id = "slot-" + slot;
        UiButton row = JunctionButton(id, "", () => OpenJunctionEditor("magic", id));
        row.DrawBackground = false;
        if (availabilityChanged)
        {
            row.Children.Add(new JunctionSlotAvailabilityRow { Gained = projectedAvailable });
        }
        else if (junctionPlan?.Changes.Any(change => change.CharacterId == Character?.Id && change.Category == "Magic" && change.Slot == slot) == true)
        {
            row.Children.Add(new JunctionChangedRow());
        }
        row.Children.Add(content);
        row.FocusChanged = focused =>
        {
            if (focused)
            {
                LeaveGuardianPreview(id);
                bool changed = junctionFocus != id;
                junctionFocus = id;
                if (junctionDashboard.SetDestination(slot) || changed && junctionEditor.Length == 0)
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
        UiGrid body = JunctionRows([junctionPlan?.HasChanges == true ? 26 : 24, 0], [1]);
        UiGrid heading = Grid([1], [1.5f, 1, 1], 3);
        string familyTitle = (elemental ? J("elements", "Elements") : J("statuses", "Statuses")) + " (%)";
        if (junctionPlan?.HasChanges == true)
        {
            UiGrid caption = Grid([1, 1], [1], 0);
            Place(caption, JunctionLabel(familyTitle, 11, true), 0);
            Place(caption, JunctionLabel(J("current-preview", "Current → Preview"), 10, true), 1);
            Place(heading, caption, 0);
        }
        else
        {
            Place(heading, JunctionLabel(familyTitle, 13, true), 0);
        }
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
        UiGrid rows = Grid(Enumerable.Repeat(1f, rowCount).ToArray(), [1, 1], elemental ? 2 : 0);
        for (int index = 0; index < count; index++)
        {
            int position = index;
            MenuJunctionPercentage? value = current?.ElementAtOrDefault(index);
            MenuJunctionPercentage? preview = next?.ElementAtOrDefault(index);
            string[] elements = ["Fire", "Ice", "Thunder", "Earth", "Poison", "Wind", "Water", "Holy"];
            string[] statuses = ["Death", "Poison", "Petrify", "Darkness", "Silence", "Berserk", "Zombie", "Sleep", "Slow", "Stop", "Curse", "Confuse", "Drain"];
            string label = J(prefix + "-" + index, value?.Name ?? (elemental ? elements : statuses)[index]);
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
            Place(readout, JunctionEffectLabel(elemental, index, label, elemental ? 15 : 14), 0);
            UiLabel number = JunctionLabel(percentage, elemental ? 16 : 15, value is null);
            if (value is not null && preview is not null && (attack ? value.Attack != preview.Attack : value.Defense != preview.Defense))
            {
                number.Color = (attack ? preview.Attack > value.Attack : preview.Defense > value.Defense) ? JunctionIncreaseColor : JunctionDecreaseColor;
            }
            Place(readout, number, 0, 1);
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
        string basic = Character?.Commands.FirstOrDefault() ?? "—";
        if (Data.SmartAttack)
        {
            basic += " · " + MagicText(Character?.AttackMode == "SmartMagic" ? "SmartMagic" : "PhysicalOnly");
        }
        Place(commands, JunctionButton("basic-action", basic, OpenJunctionAttackMode), 0);
        for (int index = 0; index < 3; index++)
        {
            int slot = index;
            bool empty = Character?.CommandIds.ElementAtOrDefault(index) is null or "0";
            string name = Character?.Commands.ElementAtOrDefault(index + 1) ?? "—";
            bool actionable = empty && Character is { } actor && actor.AvailableCommands.Any(choice => !actor.CommandIds.Contains(choice.Id));
            UiButton command = JunctionButton("command-" + index, actionable ? "" : $"{index + 1}  " + (empty ? "---" : name), () => OpenJunctionEditor("command", "command-" + slot));
            if (actionable)
            {
                command.Children.Add(new JunctionAttentionRow());
                UiLabel attention = JunctionLabel($"{index + 1}  ! " + J("empty", "Empty"), 16);
                attention.Color = new UiColor(232, 184, 99);
                attention.Margin = new UiThickness(10, 0, 0, 0);
                command.Children.Add(attention);
            }
            Place(commands, command, index + 1);
        }
        Place(body, commands, 1);
        Place(body, JunctionLabel(J("passives", "Passive abilities") + $" · {Character?.AbilitySlots ?? 0}/4", 14, true), 2);
        UiGrid passives = Grid([1, 1, 1, 1], [1], 0);
        for (int index = 0; index < 4; index++)
        {
            int slot = index;
            bool locked = index >= (Character?.AbilitySlots ?? 0);
            bool empty = Character?.AbilityIds.ElementAtOrDefault(index) is null or "0";
            bool actionable = !locked && empty && Character is { } actor && actor.AvailableAbilities.Any(choice => choice.Id != "0" && !actor.AbilityIds.Contains(choice.Id));
            string name = Character?.Abilities.ElementAtOrDefault(index) ?? "---";
            UiButton passive = JunctionButton("passive-" + index, locked ? "" : $"{index + 1}  " + (empty ? actionable ? "! " + J("empty", "Empty") : "---" : name), () => OpenJunctionEditor("passive", "passive-" + slot));
            if (locked)
            {
                passive.Children.Add(new JunctionLockIcon { Id = "junction-lock-passive-" + index, Margin = new UiThickness(28, 0, 0, 0) });
            }
            else if (actionable)
            {
                passive.Text = "";
                UiLabel attention = JunctionLabel($"{index + 1}  ! " + J("empty", "Empty"), 16);
                attention.Color = new UiColor(232, 184, 99);
                attention.Margin = new UiThickness(10, 0, 0, 0);
                passive.Children.Add(new JunctionAttentionRow());
                passive.Children.Add(attention);
            }
            Place(passives, passive, index);
        }
        Place(body, passives, 3);
        return JunctionPanel(body);
    }

    private sealed class JunctionSlotAvailabilityRow : UiElement
    {
        public bool Gained { get; init; }

        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            UiColor color = Gained ? JunctionIncreaseColor : JunctionDecreaseColor;
            canvas.Fill(Bounds, new UiColor(color.R, color.G, color.B, 35));
            canvas.Fill(new UiRect(Bounds.X, Bounds.Y, 3, Bounds.Height), color);
        }
    }

    private sealed class JunctionChangedRow : UiElement
    {
        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            canvas.Fill(Bounds, new UiColor(80, 133, 169, 70));
            canvas.Fill(new UiRect(Bounds.X, Bounds.Y, 3, Bounds.Height), new UiColor(158, 211, 235));
        }
    }

    private sealed class JunctionAttentionRow : UiElement
    {
        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            canvas.Fill(Bounds, new UiColor(125, 86, 35, 85));
            canvas.Fill(new UiRect(Bounds.X, Bounds.Y, 3, Bounds.Height), new UiColor(232, 184, 99));
        }
    }
}
