using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private sealed class ModListElement : UiElement
    {
        private readonly MenuController menus;
        private int firstPackage;
        private int visiblePackages = 1;
        private float rowHeight;
        private float packageTop;

        public ModListElement(MenuController menus)
        {
            this.menus = menus;
            Focusable = true;
            Id = "commands";
        }

        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            theme.Panel(canvas, Bounds);
            IReadOnlyList<MenuEntry> entries = menus.Entries;
            float scale = menus.TextScale;
            rowHeight = 34 * scale;
            float toolbarTop = Bounds.Y + 48 * scale;
            packageTop = toolbarTop + rowHeight * (ModToolbarCount - 1) + 12;
            float bottom = Bounds.Bottom - 80 * scale;
            visiblePackages = Math.Max(1, (int)((bottom - packageTop) / rowHeight));
            int packageCount = Math.Max(0, entries.Count - ModToolbarCount);
            int packageSelection = menus.SelectedIndex - ModToolbarCount;
            if (packageSelection >= 0)
            {
                firstPackage = Math.Clamp(firstPackage, Math.Max(0, packageSelection - visiblePackages + 1), packageSelection);
            }
            firstPackage = Math.Clamp(firstPackage, 0, Math.Max(0, packageCount - visiblePackages));
            canvas.Text("CONFIG · MODS", 24 * scale, new UiRect(Bounds.X + 14, Bounds.Y + 6, Bounds.Width - 28, 40 * scale), theme.Foreground);
            float listWidth = Bounds.Width * 0.66f;
            float tabWidth = (listWidth - 20) / 2;
            for (int index = 0; index < Math.Min(2, entries.Count); index++)
            {
                UiRect tab = new(Bounds.X + 14 + index * tabWidth, toolbarTop, tabWidth - 3, rowHeight - 2);
                canvas.Fill(tab, index == 0 || menus.SelectedIndex == index ? theme.Highlight : new UiColor(30, 39, 58));
                canvas.Text(entries[index].Label, 17 * scale, tab.Inset(4), theme.Foreground);
            }
            for (int index = 2; index < Math.Min(ModToolbarCount, entries.Count); index++)
            {
                DrawEntry(canvas, theme, entries[index], index, toolbarTop + (index - 1) * rowHeight, listWidth - 20);
            }
            for (int offset = 0; offset < visiblePackages && firstPackage + offset < packageCount; offset++)
            {
                int index = ModToolbarCount + firstPackage + offset;
                DrawEntry(canvas, theme, entries[index], index, packageTop + offset * rowHeight, listWidth - 20);
            }
            if (packageCount == 0)
            {
                canvas.Text("No matching packages.", 17 * scale, new UiRect(Bounds.X + 18, packageTop, listWidth - 20, rowHeight), theme.Muted);
            }
            canvas.Text(string.Join("\n", menus.ModDetails()), 16 * scale, new UiRect(Bounds.X + listWidth, toolbarTop, Bounds.Width - listWidth - 14, Math.Max(rowHeight, bottom - toolbarTop)), theme.Foreground);
            canvas.Text($"{packageCount} matching packages · Showing {(packageCount == 0 ? 0 : firstPackage + 1)}-{Math.Min(packageCount, firstPackage + visiblePackages)}\n" + menus.Message, 15 * scale, new UiRect(Bounds.X + 14, bottom + 6, Bounds.Width - 28, 74 * scale), theme.Foreground);
        }

        private void DrawEntry(IUiCanvas canvas, UiTheme theme, MenuEntry entry, int index, float top, float width)
        {
            UiRect row = new(Bounds.X + 14, top, width, rowHeight - 2);
            if (menus.SelectedIndex == index)
            {
                canvas.Fill(row, theme.Highlight);
                theme.Pointer(canvas, row.X + 4, top + rowHeight / 2 - 4);
            }
            canvas.Text(entry.Label, 17 * menus.TextScale, new UiRect(row.X + 22, top, width - 22, rowHeight), entry.Enabled ? theme.Foreground : theme.Muted, UiAlignment.Center, UiTextFlow.SingleLine);
        }

        public override bool Key(UiKey key)
        {
            if (key == UiKey.Confirm)
            {
                menus.Activate(menus.SelectedIndex);
                return true;
            }
            if (key is UiKey.PageUp or UiKey.PageDown)
            {
                int index = Math.Clamp(menus.SelectedIndex + (key == UiKey.PageUp ? -visiblePackages : visiblePackages), ModToolbarCount, Math.Max(ModToolbarCount, menus.Entries.Count - 1));
                menus.Select(index, menus.Entries.Count);
                return true;
            }
            return false;
        }

        public override void Pointer(float x, float y)
        {
            float toolbarTop = Bounds.Y + 48 * menus.TextScale;
            int index;
            if (y >= toolbarTop && y < toolbarTop + rowHeight && x >= Bounds.X + 14 && x < Bounds.X + Bounds.Width * 0.66f)
            {
                float tabWidth = (Bounds.Width * 0.66f - 20) / 2;
                index = Math.Min(1, (int)((x - Bounds.X - 14) / tabWidth));
            }
            else if (y >= toolbarTop + rowHeight && y < toolbarTop + rowHeight * (ModToolbarCount - 1) && x < Bounds.X + Bounds.Width * 0.66f)
            {
                index = 1 + (int)((y - toolbarTop) / rowHeight);
            }
            else if (y >= packageTop && y < packageTop + rowHeight * visiblePackages && x < Bounds.X + Bounds.Width * 0.66f)
            {
                index = ModToolbarCount + firstPackage + (int)((y - packageTop) / rowHeight);
            }
            else
            {
                return;
            }
            if (index < menus.Entries.Count)
            {
                menus.Activate(index);
            }
        }
    }
}
