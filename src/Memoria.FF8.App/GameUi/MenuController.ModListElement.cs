using Memoria.FF8.Mods;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private sealed class ModListElement : UiElement
    {
        private readonly MenuController menus;
        private readonly ModServiceView snapshot;
        private readonly ModPackageView[] packages;
        private readonly Dictionary<string, UiElement> controls = new();
        private readonly ModTable table;
        private UiRect inspector;
        private UiRect banner;
        private bool wide;
        private float unit;
        private float rowHeight;
        private int first;
        private int selected;
        private string? notice;

        public ModListElement(MenuController menus)
        {
            this.menus = menus;
            snapshot = menus.modService!.View;
            packages = menus.FilteredModViews();
            if (!packages.Any(view => view.Package.Id.Equals(menus.selectedModId, StringComparison.OrdinalIgnoreCase)))
            {
                menus.selectedModId = packages.FirstOrDefault()?.Package.Id;
            }
            selected = Math.Max(0, Array.FindIndex(packages, view => view.Package.Id.Equals(menus.selectedModId, StringComparison.OrdinalIgnoreCase)));
            if (packages.Length > 0)
            {
                menus.SelectMod(packages[selected].Package.Id);
            }
            Button("installed-tab", "Installed Mods", () => { });
            Button("create-tab", "Create Mods", () => menus.Push("mods-create"));
            Button("mod-refresh", "Refresh", () => menus.Execute(new("mod-refresh")));
            controls["mod-refresh"].Enabled = snapshot.Outcome != ModUpdateOutcome.Updating;
            Button("mod-actions", "Actions", () => menus.Push("mods-actions"));
            Button("mod-search", menus.modSearch.Length == 0 ? "Search mods…" : menus.modSearch, () => menus.Push("mods-search"));
            Choice("mod-filter", ModFilters, menus.modFilter, value =>
            {
                menus.modFilter = value;
                menus.RestoreModSelection(true);
            });
            Choice("mod-format", ModFormats, menus.modFormat, value =>
            {
                menus.modFormat = value;
                menus.RestoreModSelection(true);
            });
            Button("mod-clear", "Clear", () => menus.Execute(new("mod-clear")));
            controls["mod-clear"].Visible = menus.modSearch.Length != 0 || menus.modFilter != 0 || menus.modFormat != 0;
            notice = snapshot.Outcome switch
            {
                ModUpdateOutcome.Updating => "Updating mods…",
                ModUpdateOutcome.ReviewRequired => "GF changes require review. Current resources remain in use.",
                ModUpdateOutcome.Blocked => "Changes blocked. Current resources remain in use.",
                ModUpdateOutcome.Failed => "Changes could not be applied. Current resources remain in use.",
                ModUpdateOutcome.PublishedWithWarnings => snapshot.Diagnostics.Any(diagnostic => diagnostic.Code == "resource-rejected") ? "Mods applied. Some replacement resources were rejected." : "Mods applied. A follow-up action reported a problem.",
                _ => snapshot.Diagnostics.Count > 0 ? "Mod diagnostics are available." : null
            };
            Button("mod-review", menus.HasModReview ? "Review changes" : "Review details", () => menus.Push(menus.HasModReview ? "gf-mod-review" : "mods-diagnostics"));
            controls["mod-review"].Visible = notice is not null;
            table = new ModTable(this) { Id = "commands", Focusable = true };
            TrackFocus(table);
            controls.Add("commands", table);
            Children.Add(table);
            Button("mod-inspector", "Details", () =>
            {
                menus.modInspectorOpen = !menus.modInspectorOpen;
                menus._view = null;
            });
            Button("mod-selected-toggle", "Enable / Disable", () => menus.Execute(new("mod-selected-toggle")));
            Button("mod-diagnostics", "View diagnostics", () => menus.Push("mods-diagnostics"));
            Button("mod-prev", "← Previous", () => menus.MoveModSelection(-1, true));
            Button("mod-next", "Next →", () => menus.MoveModSelection(1, true));
            Button("mod-auto", (snapshot.AutoEnableNewMods ? "☑ " : "☐ ") + "Automatically enable new mods", () => menus.Execute(new("mod-auto")));
        }

        private void TrackFocus(UiElement element)
        {
            element.FocusChanged = focused =>
            {
                if (focused)
                {
                    menus.modFocusedControlId = element.Id!;
                }
            };
        }

        private void Button(string id, string text, Action action)
        {
            UiButton button = new() { Id = id, Text = text, Click = action, TextInset = 10, DrawBackground = true, FontSize = 17 * menus.TextScale };
            TrackFocus(button);
            controls.Add(id, button);
            Children.Add(button);
        }

        private void Choice(string id, string[] items, int selectedIndex, Action<int> changed)
        {
            UiChoiceRow choice = new() { Id = id, Items = items, SelectedIndex = selectedIndex, LabelWidthRatio = 0, TextInset = 0, RightInset = 0, MaxVisibleItems = 1, FontSize = 17 * menus.TextScale, SelectionChanged = changed };
            TrackFocus(choice);
            controls.Add(id, choice);
            Children.Add(choice);
        }

        private void Place(string id, float x, float y, float width, float height)
        {
            controls[id].Arrange(new UiRect(x, y, Math.Max(0, width), Math.Max(0, height)));
        }

        public override void Arrange(UiRect bounds)
        {
            Bounds = bounds;
            unit = menus.TextScale;
            float gap = 8;
            float x = bounds.X + 14;
            float right = bounds.Right - 14;
            float width = Math.Max(0, right - x);
            float control = 32 * unit;
            bool condensed = bounds.Height < 350 * unit || width < 600 * unit;
            float top = bounds.Y + 10;
            wide = width >= 1000 * unit;
            float headerButton = 100 * unit;
            Place("mod-refresh", right - headerButton * 2 - gap, top, headerButton, control);
            Place("mod-actions", right - headerButton, top, headerButton, control);
            top += control + gap;
            float installedWidth = Math.Min(165 * unit, width * 0.45f);
            float createWidth = Math.Min(140 * unit, width * 0.4f);
            Place("installed-tab", x, top, installedWidth, control);
            Place("create-tab", x + installedWidth + gap, top, createWidth, control);
            top += control + gap;
            float statusWidth = Math.Min(width * 0.27f, 220 * unit);
            float formatWidth = Math.Min(width * 0.21f, 155 * unit);
            float clearWidth = controls["mod-clear"].Visible ? 65 * unit + gap : 0;
            Place("mod-search", x, top, width - statusWidth - formatWidth - clearWidth - gap * 2, control);
            Place("mod-filter", right - formatWidth - statusWidth - clearWidth - gap, top, statusWidth, control);
            Place("mod-format", right - formatWidth - clearWidth, top, formatWidth, control);
            Place("mod-clear", right - clearWidth + gap, top, clearWidth - gap, control);
            top += control + gap;
            foreach (string id in new[] { "mod-search", "mod-filter", "mod-format", "mod-clear" })
            {
                controls[id].Visible = !condensed && (id != "mod-clear" || menus.modSearch.Length != 0 || menus.modFilter != 0 || menus.modFormat != 0);
            }
            if (condensed)
            {
                top -= control + gap;
            }
            banner = new UiRect(x, top, width, notice is null ? 0 : control + 12);
            if (notice is not null)
            {
                Place("mod-review", right - 160 * unit, top + 6, 154 * unit, control);
                top += banner.Height + gap;
            }
            float footer = control * (condensed ? 1 : 2) + gap * 3;
            float bottom = Math.Max(top, bounds.Bottom - footer);
            float tableWidth = wide ? width * 0.66f : width;
            inspector = new UiRect(x + tableWidth + gap, top, Math.Max(0, width - tableWidth - gap), Math.Max(0, bottom - top));
            rowHeight = (tableWidth < 800 * unit ? 48 : 34) * unit;
            menus.modPageSize = Math.Max(1, (int)((bottom - top - 28 * unit) / rowHeight));
            first = selected / menus.modPageSize * menus.modPageSize;
            menus.modTableBounds = new UiRect(x, top, tableWidth, Math.Max(0, bottom - top));
            table.Arrange(menus.modTableBounds);
            float pagerY = bottom + gap;
            Place("mod-inspector", x, pagerY, 85 * unit, control);
            Place("mod-prev", right - 285 * unit, pagerY, 110 * unit, control);
            Place("mod-next", right - 100 * unit, pagerY, 100 * unit, control);
            controls["mod-auto"].Visible = !condensed;
            controls["mod-prev"].Enabled = first > 0;
            controls["mod-next"].Enabled = first + menus.modPageSize < packages.Length;
            Place("mod-auto", x, pagerY + control + gap, Math.Min(width, 330 * unit), control);
            bool showInspector = wide || menus.modInspectorOpen;
            controls["commands"].Visible = wide || !showInspector;
            if (!wide && showInspector)
            {
                inspector = menus.modTableBounds;
                controls["commands"].Visible = false;
            }
            controls["mod-selected-toggle"].Visible = showInspector && packages.Length > 0;
            controls["mod-diagnostics"].Visible = showInspector;
            controls["mod-selected-toggle"].Enabled = packages.ElementAtOrDefault(selected)?.CanToggle == true;
            if (packages.ElementAtOrDefault(selected) is { } package)
            {
                ((UiButton)controls["mod-selected-toggle"]).Text = package.Package.RequestedEnabled ? "Disable" : "Enable";
            }
            float actionWidth = Math.Min(130 * unit, inspector.Width);
            Place("mod-selected-toggle", inspector.X, inspector.Bottom - control * 2 - gap, actionWidth, control);
            Place("mod-diagnostics", inspector.X, inspector.Bottom - control, inspector.Width, control);
        }

        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            canvas.Fill(Bounds, new UiColor(21, 31, 46));
            canvas.Text("Mods", 25 * unit, new UiRect(Bounds.X + 14, Bounds.Y + 10, Math.Max(0, Bounds.Width - 240 * unit), 36 * unit), theme.Foreground);
            UiRect tab = controls["installed-tab"].Bounds;
            canvas.Fill(new UiRect(tab.X, tab.Bottom - 3, tab.Width, 3), theme.Foreground);
            if (notice is not null)
            {
                canvas.Fill(banner, new UiColor(60, 52, 36));
                canvas.Fill(new UiRect(banner.X, banner.Y, 3, banner.Height), new UiColor(230, 198, 135));
                canvas.Text(notice, 16 * unit, new UiRect(banner.X + 12, banner.Y + 4, Math.Max(0, banner.Width - 180 * unit), banner.Height - 8), theme.Foreground, UiAlignment.Center, UiTextFlow.Wrap);
            }
            if (wide || menus.modInspectorOpen)
            {
                canvas.PushClip(inspector);
                try
                {
                    canvas.Fill(inspector, new UiColor(26, 39, 56));
                    float actionHeight = 80 * unit + 16;
                    canvas.Text(string.Join("\n", menus.ModDetails()), 17 * unit, new UiRect(inspector.X + 10, inspector.Y + 8, Math.Max(0, inspector.Width - 20), Math.Max(0, inspector.Height - actionHeight)), theme.Foreground, UiAlignment.Start, UiTextFlow.Wrap);
                }
                finally
                {
                    canvas.PopClip();
                }
            }
            float y = controls["mod-prev"].Bounds.Y;
            string count = packages.Length == 0 ? "0 packages" : $"{first + 1}–{Math.Min(packages.Length, first + menus.modPageSize)} of {packages.Length}";
            canvas.Text(count, 15 * unit, new UiRect(Bounds.X + 110 * unit, y, Math.Max(0, Bounds.Width - 430 * unit), 36 * unit), theme.Muted, UiAlignment.Center, UiTextFlow.SingleLine);
            canvas.Text($"{first / menus.modPageSize + 1}/{Math.Max(1, (packages.Length + menus.modPageSize - 1) / menus.modPageSize)}", 15 * unit, new UiRect(controls["mod-prev"].Bounds.Right + 6, y, 60 * unit, 36 * unit), theme.Foreground, UiAlignment.Center, UiTextFlow.SingleLine);
            float hintX = controls["mod-auto"].Bounds.Right + 8;
            if (controls["mod-auto"].Visible)
            {
                canvas.Text("Explicit choices are kept · " + menus.CurrentBindingText(ControlAction.Confirm) + " " + (SelectedRequestedEnabled ? "Disable" : "Enable") + " · " + menus.CurrentBindingText(ControlAction.Card) + " Details", 14 * unit, new UiRect(hintX, controls["mod-auto"].Bounds.Y, Math.Max(0, Bounds.Right - hintX - 14), 36 * unit), theme.Muted, UiAlignment.Center, UiTextFlow.SingleLine);
            }
        }

        private bool SelectedRequestedEnabled => packages.ElementAtOrDefault(selected)?.Package.RequestedEnabled == true;

        private sealed class ModTable(ModListElement owner) : UiElement
        {
            protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
            {
                float scale = owner.unit;
                float check = 38 * scale;
                bool compact = Bounds.Width < 800 * scale;
                float statusWidth = Math.Min(Bounds.Width * 0.32f, 210 * scale);
                float formatWidth = compact ? 0 : 115 * scale;
                float nameWidth = Math.Max(0, Bounds.Width - check - statusWidth - formatWidth);
                float top = Bounds.Y + 28 * scale;
                canvas.Fill(Bounds, new UiColor(30, 43, 61));
                Text("On", Bounds.X + 5, Bounds.Y, check, 28 * scale, theme.Muted, 14);
                Text("Mod name", Bounds.X + check, Bounds.Y, nameWidth, 28 * scale, theme.Muted, 14);
                if (!compact)
                {
                    Text("Format", Bounds.Right - statusWidth - formatWidth, Bounds.Y, formatWidth, 28 * scale, theme.Muted, 14);
                }
                Text("Runtime status", Bounds.Right - statusWidth, Bounds.Y, statusWidth, 28 * scale, theme.Muted, 14);
                for (int offset = 0; offset < owner.menus.modPageSize && owner.first + offset < owner.packages.Length; offset++)
                {
                    ModPackageView view = owner.packages[owner.first + offset];
                    UiRect row = new(Bounds.X, top + offset * owner.rowHeight, Bounds.Width, owner.rowHeight);
                    bool selected = owner.first + offset == owner.selected;
                    if (selected)
                    {
                        canvas.Fill(row, IsFocused ? new UiColor(79, 107, 139) : new UiColor(48, 66, 88));
                    }
                    canvas.Fill(new UiRect(row.X, row.Bottom - 1, row.Width, 1), new UiColor(52, 69, 91));
                    Text(view.Package.RequestedEnabled ? "☑" : "☐", row.X + 8, row.Y, check - 8, row.Height, view.CanToggle ? theme.Foreground : theme.Muted, 20);
                    Text(view.Package.Name, row.X + check, row.Y, nameWidth - 8, compact ? row.Height * 0.6f : row.Height, theme.Foreground, 17);
                    if (compact)
                    {
                        Text(view.Package.Format.ToString(), row.X + check, row.Y + row.Height * 0.6f, nameWidth - 8, row.Height * 0.4f, theme.Muted, 12);
                    }
                    else
                    {
                        Text(view.Package.Format.ToString(), Bounds.Right - statusWidth - formatWidth, row.Y, formatWidth - 8, row.Height, theme.Muted, 14);
                    }
                    Text(view.StatusText, Bounds.Right - statusWidth, row.Y, statusWidth - 6, row.Height, view.NeedsAttention ? new UiColor(239, 204, 144) : theme.Foreground, 14);
                }
                if (owner.packages.Length == 0)
                {
                    string empty = owner.snapshot.Outcome == ModUpdateOutcome.Updating ? "Loading installed mods…" : owner.snapshot.Packages.Count == 0 ? "No mods found" : "No matching mods · Clear filters";
                    Text(empty, Bounds.X + 12, top, Bounds.Width - 24, owner.rowHeight, theme.Muted, 17);
                }

                void Text(string value, float x, float y, float width, float height, UiColor color, float size)
                {
                    canvas.Text(value, size * scale, new UiRect(x, y, Math.Max(0, width), height), color, UiAlignment.Center, UiTextFlow.SingleLine);
                }
            }

            public override bool Key(UiKey key)
            {
                if (key is UiKey.Left or UiKey.Right or UiKey.PageUp or UiKey.PageDown)
                {
                    owner.menus.MoveModSelection(key is UiKey.Left or UiKey.PageUp ? -1 : 1, true);
                    return true;
                }
                if (key is UiKey.Up or UiKey.Down)
                {
                    owner.menus.MoveModSelection(key == UiKey.Up ? -1 : 1, false);
                    return true;
                }
                if (key == UiKey.Confirm)
                {
                    owner.menus.Execute(new("mod-selected-toggle"));
                    return true;
                }
                return false;
            }

            public override void Pointer(float x, float y)
            {
                float rowTop = Bounds.Y + 28 * owner.unit;
                if (!Bounds.Contains(x, y) || y < rowTop)
                {
                    return;
                }
                int slot = (int)((y - rowTop) / owner.rowHeight);
                int index = owner.first + slot;
                if (slot >= owner.menus.modPageSize || index >= owner.packages.Length)
                {
                    return;
                }
                owner.menus.SelectMod(owner.packages[index].Package.Id);
                owner.menus.modFocusedControlId = "commands";
                if (x < Bounds.X + 38 * owner.unit)
                {
                    owner.menus.Execute(new("mod-selected-toggle"));
                }
                owner.menus._view = null;
            }
        }
    }
}
