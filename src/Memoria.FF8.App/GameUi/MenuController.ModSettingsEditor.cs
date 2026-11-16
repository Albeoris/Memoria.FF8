using Memoria.FF8.Mods;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private sealed class ModSettingsEditor : UiElement
    {
        private readonly MenuController menus;
        private readonly UiGrid options;
        private readonly UiLabel title;
        private readonly UiLabel help;
        private readonly List<UiElement> navigationControls = [];

        internal IEnumerable<UiElement> NavigationControls => navigationControls;
        internal UiScrollPanel Scroll { get; }

        public ModSettingsEditor(MenuController menus)
        {
            this.menus = menus;
            float scale = menus.TextScale;
            IReadOnlyList<ModSettingsRow> rows = menus.ModSettingsRows();
            title = menus.Label(menus.CurrentScreen.Title, 25, UiTextFlow.SingleLine);
            options = new UiGrid { Columns = 1, Gap = 4, Padding = new UiThickness(8) };
            Scroll = new UiScrollPanel
            {
                Id = "mod-settings-options",
                ScrollbarWidth = 12,
                OffsetChanged = offset => menus.modSettingsScrollOffset = offset
            };
            Scroll.Children.Add(options);
            Children.Add(title);
            Children.Add(Scroll);
            for (int index = 0; index < rows.Count; index++)
            {
                ModSettingsRow row = rows[index];
                UiElement control;
                UiElement item;
                if (row.SettingIndex is int settingIndex)
                {
                    control = SettingControl(row, settingIndex);
                    item = SettingRow(row, control);
                }
                else
                {
                    string chevron = row.Entry.Action?.Kind == "mod-settings-category" ? menus.modSettingsExpanded.Contains(row.Entry.Action.SubjectId) ? "▾ " : "▸ " : "";
                    control = ActionButton(row.Entry, index, chevron + row.Entry.Label);
                    item = row.Entry.Action?.Kind is "official-setting" or "official-batch" ? SettingRow(row, control) : control;
                }
                TrackControl(control, index);
                Place(options, item, index);
                options.RowDefinitions.Add(!ReferenceEquals(item, control) ? UiGridLength.Auto : UiGridLength.Pixel(32 * scale));
            }
            string helpText = menus.Message.Length > 0 ? menus.Message : menus.CurrentScreen.Help;
            if (menus.Message.Length == 0 && menus.modSettings.All(setting => menus.ModSettingLockReason(setting) is not null))
            {
                helpText = menus.modSettings.Select(menus.ModSettingLockReason).FirstOrDefault() ?? helpText;
            }
            help = menus.Label(helpText, 15, UiTextFlow.Wrap);
            help.Id = "mod-settings-help";
            help.Muted = true;
            Children.Add(help);
        }

        private UiButton ActionButton(MenuEntry entry, int index, string text)
        {
            return new UiButton
            {
                Id = entry.Id,
                Text = text,
                Enabled = entry.Enabled,
                TextInset = 16,
                FontSize = 17 * menus.TextScale,
                Click = () => menus.Activate(index)
            };
        }

        private void TrackControl(UiElement element, int index)
        {
            element.FocusChanged = focused => menus.FocusModSettingsRow(focused, index);
            navigationControls.Add(element);
        }

        private UiElement SettingControl(ModSettingsRow row, int index)
        {
            MenuEntry entry = row.Entry;
            BehaviorModSetting setting = menus.modSettings[index];
            float scale = menus.TextScale;
            if (setting.Control == "toggle")
            {
                return new UiToggle
                {
                    Id = entry.Id,
                    Text = row.Expanded switch
                    {
                        true => "▾ " + entry.Label,
                        false => "▸ " + entry.Label,
                        null => entry.Label
                    },
                    OnText = menus.SystemText("ui.moddetails.details.on", "On"),
                    OffText = menus.SystemText("ui.moddetails.details.off", "Off"),
                    FontSize = 18 * scale,
                    ValueWidth = 72 * scale,
                    Value = setting.Value != 0,
                    Enabled = entry.Enabled || row.Expanded.HasValue,
                    ReadOnly = !entry.Enabled,
                    DrawBackground = row.Expanded.HasValue,
                    ValueChanged = value => menus.ApplyModSetting(index, value ? 1 : 0, row.Expanded.HasValue)
                };
            }
            return new UiNumericSlider
            {
                Id = entry.Id,
                Text = entry.Label,
                Max = setting.Max,
                Min = setting.Min,
                Step = setting.Step,
                Value = setting.Value,
                ValueFormat = "0'" + setting.Unit.Replace("'", "''", StringComparison.Ordinal) + "'",
                Enabled = entry.Enabled,
                ReadOnly = !entry.Enabled,
                FontSize = 18 * scale,
                PointerSize = 12 * scale,
                ValueWidth = 72 * scale,
                LabelWidthRatio = 0.52f,
                ValueChanged = value => menus.ApplyModSetting(index, (int)value)
            };
        }

        private UiElement SettingRow(ModSettingsRow row, UiElement control)
        {
            float scale = menus.TextScale;
            UiGrid grid = new()
            {
                Columns = 1,
                Gap = 0,
                Padding = new UiThickness(row.Depth * 16 * scale, 0, 8, 5)
            };
            grid.RowDefinitions.Add(UiGridLength.Pixel(32 * scale));
            Place(grid, control, 0);
            if (!string.IsNullOrWhiteSpace(row.Entry.Help))
            {
                UiLabel description = menus.Label(row.Entry.Help, 14, UiTextFlow.Wrap);
                description.Id = row.Entry.Id.StartsWith("mod-setting-", StringComparison.Ordinal) ? "mod-setting-description-" + row.Entry.Id["mod-setting-".Length..] : row.Entry.Id + "-description";
                description.Margin = new UiThickness(22, 0, 0, 0);
                description.Muted = true;
                grid.RowDefinitions.Add(UiGridLength.Auto);
                Place(grid, description, 1);
            }
            return grid;
        }

        public override void Arrange(UiRect bounds)
        {
            Bounds = bounds;
            float scale = menus.TextScale;
            float x = bounds.X + 14;
            float width = Math.Max(0, bounds.Width - 28);
            float top = bounds.Y + 10;
            float gap = 8;
            float titleHeight = 36 * scale;
            title.Arrange(new UiRect(x, top, width, titleHeight));
            top += titleHeight + gap;

            float helpHeight = help.Measure(new UiSize(width, bounds.Height / 4)).Height;
            float helpTop = Math.Max(top, bounds.Bottom - 10 - helpHeight);
            help.Arrange(new UiRect(x, helpTop, width, helpHeight));
            float viewportHeight = Math.Max(0, helpTop - gap - top);
            float offset = menus.modSettingsScrollOffset;
            float contentHeight = options.Measure(new UiSize(width, float.MaxValue)).Height;
            if (contentHeight > viewportHeight)
            {
                contentHeight = options.Measure(new UiSize(Math.Max(0, width - Scroll.ScrollbarWidth), float.MaxValue)).Height;
            }
            Scroll.ContentHeight = contentHeight;
            Scroll.Arrange(new UiRect(x, top, width, viewportHeight));
            Scroll.Offset = offset;
        }

        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            canvas.Fill(Bounds, new UiColor(21, 31, 46));
            canvas.Fill(Scroll.Bounds, new UiColor(26, 39, 56));
        }

        internal void Page(UiView view, int direction)
        {
            if (Scroll.MaximumOffset <= 0)
            {
                return;
            }
            UiElement? focused = view.FocusedElement;
            float targetY = focused is not null && Scroll.Bounds.Intersect(focused.Bounds).Height > 0 ? focused.Bounds.Y : Scroll.Bounds.Y;
            Scroll.Key(direction < 0 ? UiKey.PageUp : UiKey.PageDown);
            UiElement? target = navigationControls.Where(control => control.Enabled && !Scroll.Bounds.Intersect(control.Bounds).IsEmpty).MinBy(control => Math.Abs(control.Bounds.Y - targetY));
            if (target is not null)
            {
                view.Focus(target);
            }
        }
    }
}
