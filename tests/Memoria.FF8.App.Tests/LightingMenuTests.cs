using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class LightingMenuTests
{
    [Fact]
    public void Opening_conflicts_include_bare_gameplay_keys_and_either_modifier_side()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.Pause, ControlDevice.Keyboard, new([Keys.L], []));
        Assert.Contains("Pause", LightingHostInputPolicy.FindConflict(profile, LightingEditorCommand.OpenEditor, new(Keys.L, Control: true))!);
        profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.Pause, ControlDevice.Keyboard, new([Keys.RightControl, Keys.L], []));
        Assert.NotNull(LightingHostInputPolicy.FindConflict(profile, LightingEditorCommand.OpenEditor, new(Keys.L, Control: true)));
        Assert.Null(LightingHostInputPolicy.FindConflict(profile, LightingEditorCommand.OpenEditor, new(Keys.L)));
        Assert.Null(LightingHostInputPolicy.FindConflict(ControlsProfile.CreateDefault(), LightingEditorCommand.OpenEditor, new(Keys.L, Control: true)));
        Assert.Null(LightingHostInputPolicy.FindConflict(profile, LightingEditorCommand.Translate, new(Keys.L, Control: true)));
    }

    [Theory]
    [InlineData(LightingEditorCommand.OpenEditor)]
    [InlineData(LightingEditorCommand.ToggleVisibility)]
    [InlineData(LightingEditorCommand.Save)]
    public void Window_chord_is_reserved_for_every_editor_action(LightingEditorCommand command)
    {
        ControlsProfile profile = ControlsProfile.CreateDefault();
        Assert.Contains("Alt+Enter", LightingHostInputPolicy.FindConflict(profile, command, new(Keys.Enter, Alt: true))!);
        Assert.NotNull(LightingHostInputPolicy.FindConflict(profile, command, new(Keys.Enter, Control: true, Shift: true, Alt: true)));
        Assert.NotNull(LightingHostInputPolicy.FindConflict(profile, command, new(Keys.Enter, Alt: null)));
        Assert.Null(LightingHostInputPolicy.FindConflict(profile, command, new(Keys.F8)));
    }

    [Fact]
    public void Opening_detects_menu_presentation_key_even_with_extra_modifiers()
    {
        Assert.Contains("F7", LightingHostInputPolicy.FindConflict(ControlsProfile.CreateDefault(), LightingEditorCommand.OpenEditor, new(Keys.F7, Control: true))!);
        Assert.Null(LightingHostInputPolicy.FindConflict(ControlsProfile.CreateDefault(), LightingEditorCommand.ToggleVisibility, new(Keys.F7)));
    }

    [Fact]
    public void Name_entry_replaces_selected_default_and_held_enter_on_entry_cannot_create_a_package()
    {
        List<string> created = [];
        MenuController menu = Create(created.Add);
        try
        {
            menu.Open("lighting-create", true);
            Assert.True(menu.IsLightingNameEditing);
            menu.ResetLightingPackageNameInput(new(Keys.Enter));
            menu.PollLightingPackageName(new(Keys.Enter), TimeSpan.FromMilliseconds(16));
            menu.PollLightingPackageName(new(Keys.Enter), TimeSpan.FromMilliseconds(500));
            Assert.Empty(created);
            menu.PollLightingPackageName(new(), TimeSpan.FromMilliseconds(16));
            foreach (char character in "Garden illumination")
            {
                menu.EnterLightingPackageName(character);
            }
            menu.PollLightingPackageName(new(Keys.Enter), TimeSpan.FromMilliseconds(16));
            menu.PollLightingPackageName(new(Keys.Enter), TimeSpan.FromMilliseconds(1000));
            Assert.Equal("Garden illumination", Assert.Single(created));
            KeyboardInput gameplay = new();
            gameplay.Read(new(Keys.Enter), false);
            gameplay.SuppressHeldUiInput();
            Assert.False(gameplay.Read(new(Keys.Enter), true).Confirm);
        }
        finally
        {
            menu.DisposeLightingAuthoring();
        }
    }

    [Fact]
    public void Name_entry_cancel_returns_to_authoring_without_creating_and_ignores_control_characters()
    {
        List<string> created = [];
        MenuController menu = Create(created.Add);
        try
        {
            menu.Open("lighting-create", true);
            menu.ResetLightingPackageNameInput(new());
            menu.EnterLightingPackageName('\n');
            menu.EnterLightingPackageName('\r');
            menu.PollLightingPackageName(new(Keys.Escape), TimeSpan.FromMilliseconds(16));
            Assert.Empty(created);
            Assert.False(menu.IsLightingNameEditing);
            Assert.Equal("lighting-authoring", menu.ScreenId);
        }
        finally
        {
            menu.DisposeLightingAuthoring();
        }
    }

    [Fact]
    public void Create_page_pointer_and_semantic_selection_activate_the_same_lighting_command()
    {
        MenuController menu = Create(_ => { });
        try
        {
            menu.Open("mods-create", true);
            UiView view = menu.BuildView();
            UiElement button = Descendants(view.Root).Single(element => element.Id == "mods-lighting-create");
            view.Pointer(button.Bounds.X + button.Bounds.Width / 2, button.Bounds.Y + button.Bounds.Height / 2, true);
            Assert.Equal("lighting-create", menu.ScreenId);
            menu.Open("mods-create", true);
            menu.SelectEntry("mods-create", "mods-lighting-edit");
            Assert.Equal("mods-lighting-edit", menu.BuildView().FocusedElement!.Id);
            Assert.Equal("mods-lighting-edit", menu.SelectedEntry!.Id);
            menu.Navigate(MenuInput.Confirm);
            Assert.Equal("lighting-authoring", menu.ScreenId);
        }
        finally
        {
            menu.DisposeLightingAuthoring();
        }
    }

    [Theory]
    [InlineData(100, MenuPresentation.Classic)]
    [InlineData(150, MenuPresentation.Classic)]
    [InlineData(100, MenuPresentation.Vector)]
    [InlineData(150, MenuPresentation.Vector)]
    public void Shared_create_page_exposes_lighting_actions_and_keeps_each_action_inside_its_panel(int scale, MenuPresentation presentation)
    {
        MenuController menu = Create(_ => { });
        try
        {
            menu.Presentation = presentation;
            menu.Data.Settings["Text scale"] = scale + "%";
            menu.Open("mods-create", true);
            Assert.True(menu.Entries.Single(entry => entry.Id == "mods-lighting-create").Enabled);
            Assert.True(menu.Entries.Single(entry => entry.Id == "mods-lighting-edit").Enabled);
            UiView view = menu.BuildView();
            view.Resize(1280, 720);
            UiElement panel = Descendants(view.Root).Single(element => element.Children.Any(child => child.Id == "mods-lighting-create"));
            UiButton[] actions = panel.Children.OfType<UiButton>().Where(button => button.Id is not ("mods-installed-tab" or "mods-create-tab")).ToArray();
            Assert.All(actions, action =>
            {
                Assert.True(action.Bounds.X >= panel.Bounds.X && action.Bounds.Right <= panel.Bounds.Right, action.Id + " lies outside its panel horizontally.");
                Assert.True(action.Bounds.Y >= panel.Bounds.Y && action.Bounds.Bottom <= panel.Bounds.Bottom, action.Id + " lies outside its panel vertically.");
            });
            menu.SelectEntry("mods-create", "mods-lighting-create");
            menu.Navigate(MenuInput.Confirm);
            Assert.Equal("lighting-create", menu.ScreenId);
        }
        finally
        {
            menu.DisposeLightingAuthoring();
        }
    }

    private static MenuController Create(Action<string> create)
    {
        var source = GuardianForceAbilityEditorTests.Source();
        MenuController menu = new();
        menu.ConnectGuardianForceEditor(() => new(source, null, [], [], []), id => "GF" + id, id => "Ability" + id, id => "Description" + id, action => action.ToString(), (_, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException());
        menu.ConnectLightingAuthoring(() => [new("create", "Create lighting mod", "", Destination: "lighting-create")], _ => { }, create, "Mods");
        return menu;
    }

    private static IEnumerable<UiElement> Descendants(UiElement element)
    {
        yield return element;
        foreach (UiElement child in element.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
