using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private sealed class GuardianAbilityEditorElement(MenuController menus) : UiElement
    {
        private readonly List<(UiRect Bounds, Action Click)> targets = [];

        public override bool Key(UiKey key) => key == UiKey.Confirm && menus.NavigateGuardianEditor(MenuInput.Confirm);

        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            targets.Clear();
            menus.guardianControls.Clear();
            theme.Panel(canvas, Bounds);
            if (menus.guardianDraft is null)
            {
                canvas.Text("Choose a GF editing target.", 20, Bounds, theme.Foreground);
                return;
            }

            float scale = menus.TextScale;
            float font = 17 * scale;
            float small = 14 * scale;
            float rowHeight = 26 * scale;
            float gap = 9 * scale;
            float x = Bounds.X + gap;
            float width = Math.Max(0, Bounds.Width - gap * 2);
            float top = Bounds.Y + 7 * scale;
            float buttonHeight = 27 * scale;
            bool previewing = menus.guardianPreview;
            GuardianForceEditorContext context = menus.readGuardianEditor!();
            GuardianForceAbilityComposition composition = menus.GuardianEditorComposition();
            var rows = composition.Templates[menus.guardianSubject];
            int[] catalog = menus.GuardianCatalog();

            float backWidth = (previewing ? 126 : 54) * scale;
            Button("back", previewing ? "Back to editor" : "Back", new(x, top, backWidth, buttonHeight), () => menus.NavigateGuardianEditor(MenuInput.Cancel));
            float actionWidth = 83 * scale;
            float primaryWidth = 112 * scale;
            float right = x + width;
            Button("actions", "Actions", new(right - actionWidth, top, actionWidth, buttonHeight), menus.GuardianOpenActions);
            float titleRight = right - actionWidth - gap;
            if (!previewing)
            {
                Button("save", menus.guardianLocal ? "Apply changes" : "Save mod", new(titleRight - primaryWidth, top, primaryWidth, buttonHeight), () => menus.GuardianTool(menus.guardianLocal ? 10 : 11), primary: true);
                titleRight -= primaryWidth + gap;
                Button("preview", "Preview", new(titleRight - actionWidth, top, actionWidth, buttonHeight), () => menus.GuardianTool(5));
                titleRight -= actionWidth + gap;
            }
            string title = previewing ? "Preview · Read-only" : menus.GuardianTargetTitle() + (menus.guardianDraft.Dirty ? " · Unsaved" : "");
            Text(title, new(x + backWidth + gap, top, Math.Max(0, titleRight - x - backWidth - gap), buttonHeight), theme.Foreground);
            top += buttonHeight + gap;

            float catalogWidth = previewing ? 0 : width * 0.30f;
            float gfX = x + catalogWidth + (previewing ? 0 : gap);
            float gfWidth = Math.Max(0, width - catalogWidth - (previewing ? 0 : gap));
            float paneHeaderHeight = 80 * scale;
            float footerHeight = 115 * scale;
            float bodyTop = top + paneHeaderHeight;
            float footerTop = Math.Max(bodyTop + rowHeight, Bounds.Bottom - footerHeight);
            int visible = Math.Max(1, (int)((footerTop - bodyTop) / rowHeight));
            menus.guardianRowsPerColumn = visible;
            menus.guardianColumns = gfWidth >= 700 * scale ? 2 : 1;
            menus.guardianCatalogCapacity = visible;
            int capacity = visible * menus.guardianColumns;
            menus.guardianCatalogCursor = Math.Clamp(menus.guardianCatalogCursor, 0, Math.Max(0, catalog.Length - 1));
            menus.guardianRowCursor = Math.Clamp(menus.guardianRowCursor, 0, Math.Max(0, rows.Length - 1));
            menus.guardianCatalogScroll = menus.guardianCatalogCursor / visible * visible;
            menus.guardianRowScroll = menus.guardianRowCursor / capacity * capacity;
            menus.guardianCatalogBounds = previewing ? default : new(x, bodyTop, Math.Max(0, catalogWidth - gap), visible * rowHeight);
            menus.guardianRowsBounds = new(gfX, bodyTop, gfWidth, visible * rowHeight);

            if (!previewing)
            {
                Text($"Available abilities · {catalog.Length} results", new(x, top, catalogWidth - gap, 23 * scale), PaneColor("catalog"));
                float categoryWidth = Math.Min(110 * scale, catalogWidth * 0.40f);
                float clearWidth = 43 * scale;
                Button("search", menus.guardianSearchEditing ? menus.guardianSearch.Insert(Math.Clamp(menus.guardianSearchCaret, 0, menus.guardianSearch.Length), "|") : menus.guardianSearch.Length == 0 ? "Search abilities…" : menus.guardianSearch,
                    new(x, top + 24 * scale, Math.Max(0, catalogWidth - categoryWidth - clearWidth - gap * 3), 26 * scale), menus.GuardianStartSearch);
                Button("clear-search", "Clear", new(x + catalogWidth - categoryWidth - clearWidth - gap * 2, top + 24 * scale, clearWidth, 26 * scale), menus.GuardianClearSearch, enabled: menus.guardianSearch.Length > 0);
                Button("category", GuardianCategories[menus.guardianCategory], new(x + catalogWidth - categoryWidth - gap, top + 24 * scale, categoryWidth, 26 * scale), () => menus.GuardianTool(9));
                canvas.Line(gfX - gap, top, gfX - gap, footerTop + 55 * scale, theme.BorderLight, 1);
            }
            else
            {
                string[] modes = ["Source", menus.guardianLocal ? "These local changes only" : "This mod only", "Current setup"];
                float modeWidth = Math.Min(200 * scale, gfWidth / 4);
                for (int mode = 0; mode < modes.Length; mode++)
                {
                    int selectedMode = mode;
                    Button("mode-" + mode, modes[mode], new(gfX + mode * (modeWidth + 4 * scale), top, modeWidth, 25 * scale), () =>
                    {
                        menus.guardianPreviewMode = selectedMode;
                        menus.guardianRowCursor = 0;
                        menus.guardianRowScroll = 0;
                        menus._view = null;
                    }, active: menus.guardianPreviewMode == mode);
                }
                if (menus.guardianPreviewMode == 2)
                {
                    float includeX = gfX + 3 * (modeWidth + 4 * scale);
                    Button("include", (menus.guardianWithoutLayer ? "[ ] " : "[x] ") + "Include selected layer", new(includeX, top, Math.Max(0, gfX + gfWidth - includeX), 25 * scale), () => menus.GuardianTool(7));
                }
                top += 28 * scale;
                bodyTop += 28 * scale;
                visible = Math.Max(1, (int)((footerTop - bodyTop) / rowHeight));
                menus.guardianRowsPerColumn = visible;
                capacity = visible * menus.guardianColumns;
                menus.guardianRowScroll = menus.guardianRowCursor / capacity * capacity;
                menus.guardianRowsBounds = new(gfX, bodyTop, gfWidth, visible * rowHeight);
            }

            float gfButton = 28 * scale;
            Button("previous-gf", "‹", new(gfX, top, gfButton, 25 * scale), () => menus.NavigateGuardianEditor(MenuInput.PageLeft));
            string acquired = context.Current is null ? "" : (context.Current.GuardianForces[menus.guardianSubject].Flags & 1) != 0 ? " · Acquired" : " · Not acquired";
            Button("gf", menus.guardianEditorName!(menus.guardianSubject) + acquired + $" · {rows.Length} entries", new(gfX + gfButton + 3 * scale, top, gfWidth - gfButton * 2 - 6 * scale, 25 * scale), () => menus.GuardianTool(15), active: menus.guardianFocus == "rows");
            Button("next-gf", "›", new(gfX + gfWidth - gfButton, top, gfButton, 25 * scale), () => menus.NavigateGuardianEditor(MenuInput.PageRight));

            if (previewing)
            {
                Text(menus.GuardianPreviewStatus(), new(gfX, top + 27 * scale, gfWidth, 22 * scale), theme.Muted, small);
            }
            float columnWidth = gfWidth / menus.guardianColumns;
            for (int column = 0; column < menus.guardianColumns; column++)
            {
                float columnX = gfX + column * columnWidth;
                Text("#", new(columnX + 3 * scale, bodyTop - 25 * scale, 28 * scale, 22 * scale), theme.Muted, small);
                Text("Ability", new(columnX + 34 * scale, bodyTop - 25 * scale, Math.Max(0, columnWidth - 138 * scale), 22 * scale), PaneColor("rows"), small);
                Text("Start learned", new(columnX + columnWidth - 100 * scale, bodyTop - 25 * scale, 98 * scale, 22 * scale), theme.Muted, small);
                if (column > 0)
                {
                    canvas.Line(columnX - 3 * scale, bodyTop - 25 * scale, columnX - 3 * scale, footerTop, theme.Muted, 0.5f);
                }
            }

            if (!previewing)
            {
                float catalogBodyWidth = Math.Max(0, catalogWidth - gap * 2);
                Text("Ability", new(x + 3 * scale, bodyTop - 25 * scale, Math.Max(0, catalogBodyWidth - 101 * scale), 22 * scale), PaneColor("catalog"), small);
                Text("AP", new(x + catalogBodyWidth - 101 * scale, bodyTop - 25 * scale, 47 * scale, 22 * scale), theme.Muted, small);
                Text("Present", new(x + catalogBodyWidth - 54 * scale, bodyTop - 25 * scale, 54 * scale, 22 * scale), theme.Muted, small);
                menus.guardianControls.Add(("catalog", () => menus.NavigateGuardianEditor(MenuInput.Confirm)));
                for (int slot = 0; slot < visible && menus.guardianCatalogScroll + slot < catalog.Length; slot++)
                {
                    int index = menus.guardianCatalogScroll + slot;
                    int ability = catalog[index];
                    bool present = menus.guardianDraft.Preview.Templates[menus.guardianSubject].Any(entry => entry.Ability == ability);
                    UiRect rect = new(x, bodyTop + slot * rowHeight, Math.Max(0, catalogWidth - gap * 2), rowHeight);
                    Row(rect, menus.guardianCatalogCursor == index, "catalog");
                    float apWidth = 47 * scale;
                    float presentWidth = 54 * scale;
                    Text(menus.guardianAbilityName!(ability), new(rect.X + 3 * scale, rect.Y, Math.Max(0, rect.Width - apWidth - presentWidth), rowHeight), present ? theme.Muted : theme.Foreground);
                    Text(context.Source.MenuDefinitions.AbilityPoints[ability] + " AP", new(rect.Right - apWidth - presentWidth, rect.Y, apWidth, rowHeight), theme.Muted, small);
                    Text(present ? "✓" : "", new(rect.Right - presentWidth, rect.Y, presentWidth, rowHeight), theme.Muted, small);
                    targets.Add((rect, () => menus.GuardianSelectCatalog(index)));
                }
                if (catalog.Length == 0)
                {
                    Text("No abilities match.", menus.guardianCatalogBounds, theme.Muted);
                }
                Pager(true, x, Math.Max(0, catalogWidth - gap * 2), catalog.Length, visible, menus.guardianCatalogScroll);
                float localY = footerTop + 28 * scale;
                float half = Math.Max(0, (catalogWidth - gap * 2 - 4 * scale) / 2);
                Button("add", "Add", new(x, localY, half, 25 * scale), () => menus.GuardianTool(0), enabled: menus.GuardianCatalogCanAdd && menus.guardianPending is null);
                Button("replace", "Replace", new(x + half + 4 * scale, localY, half, 25 * scale), () => menus.GuardianTool(1), enabled: menus.GuardianCatalogCanAdd && rows.Length > 0 && menus.guardianPending is null);
            }

            menus.guardianControls.Add(("rows", () => menus.NavigateGuardianEditor(MenuInput.Confirm)));
            for (int slot = 0; slot < capacity && menus.guardianRowScroll + slot < rows.Length; slot++)
            {
                int index = menus.guardianRowScroll + slot;
                GuardianForceAbilityTemplate entry = rows[index];
                int column = slot / visible;
                int row = slot % visible;
                UiRect rect = new(gfX + column * columnWidth, bodyTop + row * rowHeight, columnWidth - 5 * scale, rowHeight);
                Row(rect, menus.guardianRowCursor == index, "rows");
                bool dirty = !previewing && menus.guardianDraft.RowDirty(menus.guardianSubject, entry.Ability);
                bool anchor = menus.guardianPending?.StartsWith("swap", StringComparison.Ordinal) == true && menus.guardianPendingAbility == entry.Ability;
                UiColor color = dirty ? new(255, 125, 125) : theme.Foreground;
                Text((index + 1).ToString(), new(rect.X + 3 * scale, rect.Y, 28 * scale, rowHeight), theme.Muted, small);
                Text((anchor ? "↔ " : dirty ? "* " : "") + menus.guardianAbilityName!(entry.Ability), new(rect.X + 34 * scale, rect.Y, Math.Max(0, rect.Width - 139 * scale), rowHeight), color);
                UiRect checkbox = new(rect.Right - 83 * scale, rect.Y + 2 * scale, 24 * scale, rowHeight - 4 * scale);
                canvas.Fill(checkbox, new UiColor(30, 34, 43));
                canvas.Line(checkbox.X, checkbox.Y, checkbox.Right, checkbox.Y, theme.Muted, 1);
                Text(entry.InitialLearned ? "✓" : "", checkbox, color);
                if (!previewing && menus.guardianPending is null)
                {
                    targets.Add((checkbox, () => menus.GuardianToggleRow(index)));
                }
                targets.Add((rect, () => menus.GuardianSelectRow(index)));
            }
            if (rows.Length == 0)
            {
                Text("No abilities in this GF.", menus.guardianRowsBounds, theme.Muted);
            }
            Pager(false, gfX, gfWidth, rows.Length, capacity, menus.guardianRowScroll);
            if (!previewing)
            {
                float localY = footerTop + 28 * scale;
                if (menus.guardianPending is not null)
                {
                    float cancelWidth = 73 * scale;
                    float commitWidth = 120 * scale;
                    string operation = menus.guardianPending.StartsWith("swap", StringComparison.Ordinal) ? "Swap entries" : "Replace ability";
                    Text(menus.GuardianOperationText(), new(gfX, localY, Math.Max(0, gfWidth - commitWidth - cancelWidth - gap * 2), 25 * scale), theme.Foreground, small);
                    Button("operation-confirm", operation, new(gfX + gfWidth - commitWidth - cancelWidth - gap, localY, commitWidth, 25 * scale), menus.GuardianConfirmOperation, enabled: menus.GuardianCanCommitOperation);
                    Button("operation-cancel", "Cancel", new(gfX + gfWidth - cancelWidth, localY, cancelWidth, 25 * scale), menus.GuardianCancelOperation);
                }
                else
                {
                    Button("swap", "Swap", new(gfX, localY, 80 * scale, 25 * scale), () => menus.GuardianTool(2), enabled: rows.Length > 1);
                    Button("undo", menus.GuardianUndoText(), new(gfX + 84 * scale, localY, 130 * scale, 25 * scale), () => menus.GuardianTool(3), enabled: menus.GuardianSelectedRow is { } selected && menus.guardianDraft.RowDirty(menus.guardianSubject, selected.Ability));
                }
            }

            int selectedAbility = menus.guardianPane == 0 && !previewing ? catalog.ElementAtOrDefault(menus.guardianCatalogCursor) : rows.ElementAtOrDefault(menus.guardianRowCursor)?.Ability ?? 0;
            float detailsY = footerTop + 56 * scale;
            string detail = menus.guardianAbilityName!(selectedAbility) + " · " + menus.guardianAbilityDescription!(selectedAbility).Replace('\n', ' ');
            Text(detail, new(x, detailsY, Math.Max(0, width - 77 * scale), 22 * scale), theme.Foreground, small);
            string diagnostics = string.Join(" · ", composition.Outcomes.Where(outcome => outcome.GuardianForce == menus.guardianSubject && outcome.Status is not ("Applied" or "AlreadySatisfied")).Take(1).Select(outcome => outcome.Message));
            GuardianForceAbilityTemplate? selectedEntry = rows.FirstOrDefault(entry => entry.Ability == selectedAbility);
            NativeGuardianForceAbility? current = context.Current?.GuardianForceAbilities(menus.guardianSubject).FirstOrDefault(entry => entry.Ability == selectedAbility);
            string currentState = current is null ? "" : current.Learned ? " · Current: Learned" : $" · Current: {current.Progress}/{current.RequiredPoints} AP";
            string condition = selectedEntry is null ? "" : !selectedEntry.SupportedCondition ? " · Unsupported source condition: inspect Details" : selectedEntry.PrerequisiteAbility is int prerequisite ? " · Requires " + menus.guardianAbilityName!(prerequisite) : selectedEntry.RequiredLevel > 1 ? " · Requires GF level " + selectedEntry.RequiredLevel : "";
            string secondLine = "Start learned affects the authored starting state; current AP stays unchanged." + currentState + condition;
            if (menus.guardianPending is not null)
            {
                secondLine = menus.GuardianOperationText() + currentState;
            }
            else if (diagnostics.Length > 0)
            {
                secondLine = diagnostics;
            }
            else if (!string.IsNullOrWhiteSpace(menus.Message))
            {
                secondLine = menus.Message;
            }
            Text(secondLine, new(x, detailsY + 21 * scale, Math.Max(0, width - 77 * scale), 19 * scale), theme.Muted, small);
            Button("details", "Details", new(x + width - 73 * scale, detailsY, 73 * scale, 25 * scale), menus.GuardianDetails);
            string tabBinding = menus.guardianBinding!(ControlAction.UiTab);
            if (string.IsNullOrWhiteSpace(tabBinding) || tabBinding.Equals("Unbound", StringComparison.OrdinalIgnoreCase))
            {
                tabBinding = "Tab / Shift+Tab";
            }
            string confirmHint = menus.GuardianConfirmHint();
            if (confirmHint.StartsWith("Confirm: ", StringComparison.Ordinal))
            {
                confirmHint = confirmHint[9..];
            }
            string hints = menus.guardianBinding!(ControlAction.Confirm) + ": " + confirmHint + " · " + tabBinding + ": next control · " + menus.guardianBinding!(ControlAction.Menu) + ": Actions · ←/→: page";
            Text(hints, new(x, Bounds.Bottom - 22 * scale, width, 20 * scale), theme.Muted, 13 * scale);

            string[] controlOrder = previewing
                ? ["back", "actions", "mode-0", "mode-1", "mode-2", "include", "previous-gf", "gf", "next-gf", "rows", "gf-prev-page", "gf-next-page", "details"]
                : ["back", "preview", "save", "actions", "search", "clear-search", "category", "catalog", "catalog-previous", "catalog-next", "add", "replace", "previous-gf", "gf", "next-gf", "rows", "gf-prev-page", "gf-next-page", "swap", "undo", "operation-confirm", "operation-cancel", "details"];
            menus.guardianControls.Sort((left, right) => Array.IndexOf(controlOrder, left.Id).CompareTo(Array.IndexOf(controlOrder, right.Id)));

            UiColor PaneColor(string id) => menus.guardianFocus == id ? theme.Foreground : theme.Muted;
            void Text(string value, UiRect rect, UiColor color, float? size = null)
            {
                canvas.Text(value, size ?? font, rect, color, UiAlignment.Center, UiTextFlow.SingleLine);
            }
            void Button(string id, string value, UiRect rect, Action action, bool primary = false, bool active = false, bool enabled = true)
            {
                bool focused = menus.guardianFocus == id;
                canvas.Fill(rect, focused ? theme.Highlight : primary ? new UiColor(62, 78, 105) : active ? new UiColor(58, 62, 75) : new UiColor(30, 34, 43));
                if (focused)
                {
                    canvas.Line(rect.X, rect.Bottom - 1, rect.Right, rect.Bottom - 1, theme.Foreground, 2);
                }
                Text(value, rect.Inset(3 * scale), enabled ? theme.Foreground : theme.Muted, small);
                if (enabled)
                {
                    menus.guardianControls.Add((id, action));
                    targets.Add((rect, () =>
                    {
                        menus.GuardianFocus(id);
                        action();
                    }));
                }
            }
            void Row(UiRect rect, bool selected, string pane)
            {
                if (selected)
                {
                    canvas.Fill(rect, menus.guardianFocus == pane ? theme.Highlight : new UiColor(55, 58, 66));
                    if (menus.guardianFocus == pane)
                    {
                        canvas.Fill(new(rect.X, rect.Y + 2 * scale, 2 * scale, rect.Height - 4 * scale), theme.Foreground);
                    }
                }
                canvas.Line(rect.X, rect.Bottom - 1, rect.Right, rect.Bottom - 1, new UiColor(95, 95, 102), 0.5f);
            }
            void Pager(bool isCatalog, float left, float paneWidth, int count, int pageCapacity, int start)
            {
                float arrowWidth = 30 * scale;
                Button(isCatalog ? "catalog-previous" : "gf-prev-page", "‹", new(left, footerTop, arrowWidth, 25 * scale), () => menus.GuardianPage(isCatalog, -1), enabled: start > 0);
                string range = count == 0 ? "0 / 0" : $"{start + 1}–{Math.Min(count, start + pageCapacity)} / {count}";
                int pageCount = Math.Max(1, (count + pageCapacity - 1) / pageCapacity);
                Text(range + $" · Page {start / pageCapacity + 1}/{pageCount}", new(left + arrowWidth + 2 * scale, footerTop, Math.Max(0, paneWidth - arrowWidth * 2 - 4 * scale), 25 * scale), theme.Foreground, small);
                Button(isCatalog ? "catalog-next" : "gf-next-page", "›", new(left + paneWidth - arrowWidth, footerTop, arrowWidth, 25 * scale), () => menus.GuardianPage(isCatalog, 1), enabled: start + pageCapacity < count);
            }
        }

        public override void Pointer(float x, float y)
        {
            foreach (var target in targets)
            {
                if (target.Bounds.Contains(x, y))
                {
                    target.Click();
                    return;
                }
            }
        }
    }
}
