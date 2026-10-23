using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private sealed class GuardianAbilityEditorElement(MenuController menus) : UiElement
    {
        private readonly List<(UiRect Bounds, Action Click)> targets = [];
        private const float HeaderHeight = 34;
        public override bool Key(UiKey key) => key == UiKey.Confirm && menus.NavigateGuardianEditor(MenuInput.Confirm);

        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            targets.Clear();
            theme.Panel(canvas, Bounds);
            if (menus.guardianDraft is null)
            {
                canvas.Text("Choose a GF editing target.", 20, Bounds, theme.Foreground);
                return;
            }
            float scale = menus.TextScale;
            float font = 17 * scale;
            float rowHeight = 28 * scale;
            float x = Bounds.X + 12;
            float width = Bounds.Width - 24;
            float top = Bounds.Y + 6;
            GuardianForceEditorContext context = menus.readGuardianEditor!();
            string acquired = context.Current is null ? "No playthrough loaded" : (context.Current.GuardianForces[menus.guardianSubject].Flags & 1) != 0 ? "Acquired" : "Not acquired";
            string baseline = context.Current?.GuardianForceOpaqueBaseline == true ? " · Opaque imported baseline (prior attribution baked)" : "";
            string owner = menus.guardianLocal ? "Playthrough local layer" : menus.guardianPackage is not null ? Path.GetFileName(menus.guardianPackage.PackageDirectory) : "New mod draft";
            string view = new[] { "Source (read-only)", "This mod only", "Current setup" }[menus.guardianPreviewMode];
            Text($"GF ABILITIES · {owner} {(menus.guardianDraft.Dirty ? "[*] Unsaved" : "Saved / clean")}", new(x, top, width, HeaderHeight * scale), theme.Foreground);
            top += HeaderHeight * scale;
            UiRect previous = new(x, top, 48, rowHeight);
            UiRect next = new(x + width - 48, top, 48, rowHeight);
            Button("<", previous, () => menus.NavigateGuardianEditor(MenuInput.PageLeft));
            Button(">", next, () => menus.NavigateGuardianEditor(MenuInput.PageRight));
            canvas.Image(new UiImageSource(UiImageKind.GuardianForcePortrait, menus.guardianSubject), new(x + 50, top - 3, rowHeight, rowHeight));
            Text($"{menus.guardianEditorName!(menus.guardianSubject)} · {acquired} · {view}{baseline}", new(x + 56 + rowHeight, top, width - 110 - rowHeight, rowHeight), theme.Foreground);
            top += rowHeight + 3;
            int columns = 5;
            float cellWidth = width / columns;
            for (int tool = 0; tool < GuardianTools.Length; tool++)
            {
                int index = tool;
                string label = GuardianTools[tool];
                if (tool == 6)
                {
                    label = $"[{(menus.guardianPreviewEnabled ? "x" : " ")}] Preview enabled";
                }
                if (tool == 7)
                {
                    label = $"[{(menus.guardianWithoutLayer ? "x" : " ")}] Without layer";
                }
                if (tool == 9)
                {
                    label = GuardianCategories[menus.guardianCategory];
                }
                Button(label, new(x + tool % columns * cellWidth, top + tool / columns * rowHeight, cellWidth - 3, rowHeight - 2), () => menus.GuardianTool(index), menus.guardianPane == 2 && menus.guardianToolbarCursor == tool);
            }
            top += rowHeight * 3 + 8;
            float leftWidth = width * 0.42f;
            Text("AVAILABLE · " + (menus.guardianSearch.Length == 0 ? "all names" : menus.guardianSearch), new(x, top, leftWidth - 8, rowHeight), menus.guardianPane == 0 ? theme.Foreground : theme.Muted);
            GuardianForceAbilityComposition preview = menus.GuardianEditorComposition();
            var rows = preview.Templates[menus.guardianSubject];
            Text($"SELECTED GF · {rows.Length} entries · Initially / Current", new(x + leftWidth, top, width - leftWidth, rowHeight), menus.guardianPane == 1 ? theme.Foreground : theme.Muted);
            top += rowHeight;
            float footerTop = Bounds.Bottom - Math.Min(Bounds.Height * 0.32f, 184 * scale);
            int visible = Math.Max(1, (int)((footerTop - top) / rowHeight));
            int[] catalog = menus.GuardianCatalog();
            menus.guardianCatalogCursor = Math.Clamp(menus.guardianCatalogCursor, 0, Math.Max(0, catalog.Length - 1));
            menus.guardianRowCursor = Math.Clamp(menus.guardianRowCursor, 0, Math.Max(0, rows.Length - 1));
            Follow(ref menus.guardianCatalogScroll, menus.guardianCatalogCursor, catalog.Length, visible);
            Follow(ref menus.guardianRowScroll, menus.guardianRowCursor, rows.Length, visible);
            for (int offset = 0; offset < visible; offset++)
            {
                int catalogIndex = menus.guardianCatalogScroll + offset;
                if (catalogIndex < catalog.Length)
                {
                    int ability = catalog[catalogIndex];
                    bool present = menus.guardianDraft.Preview.Templates[menus.guardianSubject].Any(entry => entry.Ability == ability);
                    UiRect rect = new(x, top + offset * rowHeight, leftWidth - 10, rowHeight - 1);
                    Row($"{(present ? "✓ " : "+ ")}{menus.guardianAbilityName!(ability)} · {context.Source.MenuDefinitions.AbilityPoints[ability]} AP", rect, menus.guardianPane == 0 && menus.guardianCatalogCursor == catalogIndex, present ? theme.Muted : theme.Foreground, 26 * scale);
                    if (NativeAbilityMenuMetadata.Icon(ability) is { } icon)
                    {
                        canvas.Image(icon, new(rect.X + 2, rect.Y + 2, 20 * scale, rowHeight - 4));
                    }
                    int selected = catalogIndex;
                    targets.Add((rect, () => { menus.guardianPane = 0; menus.guardianCatalogCursor = selected; menus.NavigateGuardianEditor(MenuInput.Confirm); }
                    ));
                }
                int rowIndex = menus.guardianRowScroll + offset;
                if (rowIndex < rows.Length)
                {
                    GuardianForceAbilityTemplate entry = rows[rowIndex];
                    bool dirty = menus.guardianDraft.RowDirty(menus.guardianSubject, entry.Ability);
                    NativeGuardianForceAbility? current = context.Current?.GuardianForceAbilities(menus.guardianSubject).FirstOrDefault(ability => ability.Ability == entry.Ability);
                    string currentState = current is null ? "—" : current.Learned ? "Learned now" : $"{current.Progress}/{current.RequiredPoints} AP";
                    bool armed = menus.guardianPending == "swap" && menus.guardianPendingAbility == entry.Ability;
                    string condition = entry.PrerequisiteAbility is int prerequisite ? " ← " + menus.guardianAbilityName!(prerequisite) : entry.RequiredLevel > 1 ? " Lv" + entry.RequiredLevel : "";
                    string support = entry.SupportedCondition ? "" : " [unsupported source condition]";
                    string provenance = preview.Origins.LastOrDefault(origin => origin.GuardianForce == menus.guardianSubject && origin.Ability == entry.Ability)?.PackageId ?? "Source";
                    UiRect rect = new(x + leftWidth, top + offset * rowHeight, width - leftWidth, rowHeight - 1);
                    Row($"{(armed ? "↔ " : dirty ? "[*] " : "")}{menus.guardianAbilityName!(entry.Ability)} · {(entry.InitialLearned ? "Initially learned" : "Unlearned")}{condition}{support} · {currentState} · {provenance}", rect, menus.guardianPane == 1 && menus.guardianRowCursor == rowIndex, dirty ? new UiColor(255, 110, 110) : theme.Foreground);
                    int selected = rowIndex;
                    targets.Add((rect, () => { menus.guardianPane = 1; menus.guardianRowCursor = selected; menus.NavigateGuardianEditor(MenuInput.Confirm); }
                    ));
                }
            }
            int selectedAbility = menus.guardianPane == 0 ? catalog.ElementAtOrDefault(menus.guardianCatalogCursor) : rows.ElementAtOrDefault(menus.guardianRowCursor)?.Ability ?? 0;
            string description = menus.guardianAbilityDescription!(selectedAbility);
            string diagnostics = string.Join(" · ", preview.Outcomes.Where(outcome => outcome.GuardianForce == menus.guardianSubject && outcome.Status is not ("Applied" or "AlreadySatisfied")).Take(2).Select(outcome => outcome.Message));
            string binding(ControlAction action) => menus.guardianBinding!(action);
            string pending = menus.guardianPending is not null ? "Pending " + menus.guardianPending + ": " + menus.guardianAbilityName!(menus.guardianPendingAbility) + ". Cancel clears it." : "Confirm a [*] row: Undo latest edit affecting that row.";
            string origin = string.Join(" · ", preview.Origins.Where(value => value.GuardianForce == menus.guardianSubject && value.Ability == selectedAbility).Select(value => value.PackageId + " / " + value.Aspect).Distinct());
            string footer = $"{description}\nOrigin: {(origin.Length == 0 ? "Loaded source" : origin)}\n{pending}\n{binding(ControlAction.CameraLeft)} / {binding(ControlAction.CameraRight)}: panes · {binding(ControlAction.Card)}: starting state · {binding(ControlAction.RotateLeft)} / {binding(ControlAction.RotateRight)}: GF · {binding(ControlAction.UiTab)}: toolbar\n{diagnostics}\n{menus.Message}";
            Text(footer, new(x, footerTop, width, Bounds.Bottom - footerTop - 5), theme.Foreground, 14 * scale);

            void Text(string value, UiRect rect, UiColor color, float? size = null) => canvas.Text(value, size ?? font, rect, color);
            void Button(string value, UiRect rect, Action action, bool selected = false)
            {
                canvas.Fill(rect, selected ? theme.Highlight : new UiColor(30, 39, 58));
                Text(value, rect.Inset(3), theme.Foreground, 14 * scale);
                targets.Add((rect, action));
            }
            void Row(string value, UiRect rect, bool selected, UiColor color, float inset = 2)
            {
                if (selected)
                {
                    canvas.Fill(rect, theme.Highlight);
                }
                canvas.Text(value, font, new UiRect(rect.X + inset, rect.Y + 2, Math.Max(0, rect.Width - inset - 2), rect.Height - 4), color, UiAlignment.Center, UiTextFlow.SingleLine);
            }
        }

        private static void Follow(ref int scroll, int selection, int count, int visible)
        {
            scroll = Math.Clamp(scroll, Math.Max(0, selection - visible + 1), selection);
            scroll = Math.Clamp(scroll, 0, Math.Max(0, count - visible));
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
