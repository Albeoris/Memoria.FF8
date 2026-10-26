using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private string createModsFocusedControlId = "mods-create-new";
    private sealed class CreateModsElement : UiElement
    {
        private readonly MenuController menus;
        private readonly UiButton installed;
        private readonly UiButton create;
        private readonly List<UiButton> actions = [];

        public CreateModsElement(MenuController menus)
        {
            this.menus = menus;
            installed = new UiButton { Id = "mods-installed-tab", Text = menus.SystemText("mods.installed", "Installed Mods"), DrawBackground = false, TextInset = 10, Click = menus.SwitchModTab };
            create = new UiButton { Id = "mods-create-tab", Text = menus.SystemText("mods.create", "Create Mods"), DrawBackground = false, TextInset = 10, Click = () => { } };
            Children.Add(installed);
            Children.Add(create);
            IReadOnlyList<MenuEntry> entries = menus.LocalizeSystemEntries(menus.CreateModEntries());
            for (int index = 1; index < entries.Count; index++)
            {
                int command = index;
                MenuEntry entry = entries[index];
                UiButton action = new() { Id = entry.Id, Text = entry.Label, Enabled = entry.Enabled, TextInset = 14, Click = () => menus.Activate(command) };
                actions.Add(action);
                Children.Add(action);
            }
            foreach (UiElement child in Children)
            {
                child.FocusChanged = focused =>
                {
                    if (focused)
                    {
                        menus.createModsFocusedControlId = child.Id!;
                    }
                };
            }
        }

        public override void Arrange(UiRect bounds)
        {
            Bounds = bounds;
            float unit = menus.TextScale;
            float x = bounds.X + 14;
            float top = bounds.Y + 10 + 36 * unit + 8;
            installed.FontSize = 17 * unit;
            create.FontSize = 17 * unit;
            installed.Arrange(new UiRect(x, top, 165 * unit, 36 * unit));
            create.Arrange(new UiRect(installed.Bounds.Right + 8, top, 140 * unit, 36 * unit));
            top += 130 * unit;
            foreach (UiButton action in actions)
            {
                action.FontSize = 19 * unit;
                action.Arrange(new UiRect(x, top, Math.Min(Math.Max(0, bounds.Width - 28), 560 * unit), 42 * unit));
                top += 52 * unit;
            }
        }

        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            canvas.Fill(Bounds, new UiColor(21, 31, 46));
            float unit = menus.TextScale;
            float x = Bounds.X + 14;
            float width = Math.Max(0, Bounds.Width - 28);
            canvas.Text(menus.SystemText("main.mods", "Mods"), 25 * unit, new UiRect(x, Bounds.Y + 10, width, 36 * unit), theme.Foreground);
            canvas.Fill(new UiRect(create.Bounds.X, create.Bounds.Bottom - 3, create.Bounds.Width, 3), theme.Foreground);
            float top = create.Bounds.Bottom + 24 * unit;
            canvas.Text(menus.SystemText("mods.authoringtitle", "Gameplay and localization"), 23 * unit, new UiRect(x, top, width, 32 * unit), theme.Foreground);
            canvas.Text(menus.SystemText("mods.authoringhelp", "Create GF ability contributions or translation projects."), 17 * unit, new UiRect(x, top + 38 * unit, width, 40 * unit), theme.Muted, UiAlignment.Start, UiTextFlow.Wrap);
            string availability = !menus.GuardianEditorEditionSupported ? "GF ability authoring requires the supported Classic edition." : menus.readGuardianEditor?.Invoke().Current is null ? "Edit this playthrough: load a game first." : "Package editing keeps activation unchanged. Local changes belong to this playthrough.";
            float bottom = actions[^1].Bounds.Bottom + 14 * unit;
            canvas.Text(availability, 17 * unit, new UiRect(x, bottom, width, Math.Max(0, Bounds.Bottom - bottom)), theme.Muted, UiAlignment.Start, UiTextFlow.Wrap);
        }
    }
}
