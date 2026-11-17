using Memoria.FF8.Ui;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private Action<string>? lightingAuthoringAction;
    private Action<string>? createLightingPackage;
    private readonly UiMultilineEditor lightingPackageName = new() { FontSize = 22 };
    private readonly AuthoringKeyboardInput lightingNameKeyboard = new();
    private readonly LocalizationAuthoringClipboard lightingNameClipboard = new();
    private string lightingDestination = "";
    internal bool IsLightingNameEditing => IsOpen && ScreenId == "lighting-create";
    internal void ReportLightingMessage(string value)
    {
        Message = value;
        InvalidateLiveData();
    }

    internal void DisposeLightingAuthoring()
    {
        lightingNameClipboard.Dispose();
    }

    internal void ConnectLightingAuthoring(Func<IReadOnlyList<MenuEntry>> entries, Action<string> action, Action<string> create, string destination)
    {
        lightingAuthoringAction = action;
        createLightingPackage = create;
        lightingDestination = destination;
        lightingPackageName.SetText("My field lighting");
        lightingPackageName.Select(0, lightingPackageName.Text.Length);
        lightingPackageName.ReadClipboard = lightingNameClipboard.Paste;
        lightingPackageName.WriteClipboard = lightingNameClipboard.Copy;
        Register(new MenuScreenDefinition
        {
            Id = "lighting-authoring",
            Title = "Lighting authoring",
            Help = "Select a lighting mod once, then open each field's light editor during gameplay.",
            Entries = _ => entries(),
            GalleryEligible = false
        });
        Register(new MenuScreenDefinition
        {
            Id = "lighting-create",
            Title = "Create lighting mod",
            Help = "Enter a package name. Creation enables authoring preview; installed activation remains separate.",
            Entries = _ => [new("create", "Create lighting mod", "Create and select an empty lighting package.", Action: new("lighting-authoring", "create")), new("cancel", "Back", "Return without creating a package.", Destination: "lighting-authoring")],
            BuildContent = (menus, panel) => panel.Children.Add(new LightingPackageNameElement(menus)),
            GalleryEligible = false
        });
    }

    private bool ExecuteLightingAuthoringAction(MenuAction action)
    {
        if (action.Kind != "lighting-authoring")
        {
            return false;
        }
        if (action.SubjectId == "create")
        {
            createLightingPackage?.Invoke(lightingPackageName.Text.Trim());
        }
        else
        {
            lightingAuthoringAction?.Invoke(action.SubjectId);
        }
        return true;
    }

    internal void PollLightingPackageName(KeyboardState keys, TimeSpan elapsed)
    {
        foreach (AuthoringKeyCommand command in lightingNameKeyboard.Poll(keys, elapsed))
        {
            if (command.Command == UiEditCommand.Confirm)
            {
                createLightingPackage?.Invoke(lightingPackageName.Text.Trim());
                break;
            }
            if (command.Command == UiEditCommand.Cancel)
            {
                Open("lighting-authoring", true);
                break;
            }
            if (command.Command is not (UiEditCommand.LineBreak or UiEditCommand.PageBreak))
            {
                lightingPackageName.Command(command.Command, command.Shift, command.Control);
            }
        }
    }

    internal void EnterLightingPackageName(char character)
    {
        if (IsLightingNameEditing && !char.IsControl(character) && lightingPackageName.Text.Length < 120)
        {
            lightingPackageName.Insert(character.ToString());
        }
    }

    internal void ResetLightingPackageNameInput(KeyboardState keys)
    {
        lightingNameKeyboard.Reset(keys);
    }

    private sealed class LightingPackageNameElement : UiElement
    {
        private readonly MenuController menus;
        private readonly UiButton create;
        private readonly UiButton cancel;

        public LightingPackageNameElement(MenuController menus)
        {
            this.menus = menus;
            create = new UiButton { Text = "Create lighting mod", FontSize = 20, Click = () => menus.createLightingPackage?.Invoke(menus.lightingPackageName.Text.Trim()) };
            cancel = new UiButton { Text = "Back", FontSize = 20, Click = () => menus.Open("lighting-authoring", true) };
            Children.Add(menus.lightingPackageName);
            Children.Add(create);
            Children.Add(cancel);
        }

        public override void Arrange(UiRect bounds)
        {
            Bounds = bounds;
            menus.lightingPackageName.Arrange(new(bounds.X + 30, bounds.Y + 116, bounds.Width - 60, 60));
            create.Arrange(new(bounds.X + 30, bounds.Y + 330, 310, 46));
            cancel.Arrange(new(bounds.X + 360, bounds.Y + 330, 170, 46));
        }

        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            theme.Panel(canvas, Bounds);
            canvas.Text("Create lighting mod", 28, new(Bounds.X + 30, Bounds.Y + 25, Bounds.Width - 60, 45), theme.Foreground);
            canvas.Text("Mod name — type a name, then press Enter", 18, new(Bounds.X + 30, Bounds.Y + 78, Bounds.Width - 60, 32), theme.Muted);
            canvas.Text("Destination: " + menus.lightingDestination, 17, new(Bounds.X + 30, Bounds.Y + 196, Bounds.Width - 60, 54), theme.Muted, UiAlignment.Start, UiTextFlow.Wrap);
            canvas.Text("The package starts empty and disabled for normal play. Authoring preview starts immediately. No scene lights are created until you add them.", 18, new(Bounds.X + 30, Bounds.Y + 252, Bounds.Width - 60, 65), theme.Foreground, UiAlignment.Start, UiTextFlow.Wrap);
            canvas.Text(menus.Message, 17, new(Bounds.X + 30, Bounds.Y + 396, Bounds.Width - 60, 90), theme.Muted, UiAlignment.Start, UiTextFlow.Wrap);
        }
    }
}
