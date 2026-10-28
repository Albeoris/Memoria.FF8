using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class ModsInputAndAuthoringTests
{
    [Fact]
    public void Search_owns_bound_menu_and_card_keys_and_drains_enter_on_return()
    {
        KeyboardInput input = new() { ModsManagerEnabled = true, TextEntryEnabled = true };
        input.Read(new KeyboardState(Keys.V, Keys.S, Keys.Q), true);
        Assert.False(input.GameMenuRequested);
        Assert.False(input.UiDetailRequested);
        Assert.Equal(0, input.UiCharacterPage);
        input.Read(default, true);
        Assert.True(input.Read(new KeyboardState(Keys.Enter), true).Confirm);
        input.SuppressHeldUiInput();
        input.TextEntryEnabled = false;
        Assert.False(input.Read(new KeyboardState(Keys.Enter), true).Confirm);
        Assert.False(input.UiConfirmRequested);
    }

    [Fact]
    public void Manager_reverse_tab_remains_distinct_from_editor_column_chord()
    {
        KeyboardInput input = new() { ModsManagerEnabled = true };
        input.Read(new KeyboardState(Keys.LeftShift, Keys.Tab), true);
        Assert.True(input.UiReverseTabRequested);
        Assert.False(input.UiTabRequested);
        Assert.False(input.UiSwitchColumnRequested);
        input.Read(default, true);
        input.Read(new KeyboardState(Keys.Tab), true);
        Assert.True(input.UiTabRequested);
        Assert.False(input.UiReverseTabRequested);
    }

    [Fact]
    public void Create_new_mod_opens_independent_draft_without_loaded_playthrough_or_file_write()
    {
        MenuController menu = AuthoringMenu();
        menu.Open("mods-create");
        Assert.False(menu.Entries.Single(entry => entry.Id == "mods-edit-playthrough").Enabled);
        Assert.Contains("Load a game", menu.Entries.Single(entry => entry.Id == "mods-edit-playthrough").Help);
        UiView view = menu.BuildView();
        UiElement[] buttons = Descendants(view.Root).OfType<UiButton>().ToArray();
        Assert.Equal(buttons.Length, buttons.Select(button => button.Id).Distinct(StringComparer.Ordinal).Count());
        foreach (string id in new[] { "mods-installed-tab", "mods-create-tab", "mods-create-new", "mods-edit-existing", "mods-edit-playthrough", "mods-localization-new", "mods-localization-convert" })
        {
            Assert.Contains(buttons, button => button.Id == id);
        }
        Assert.All(buttons, button => Assert.StartsWith("mods-", button.Id));
        menu.SelectEntry("mods-create", "mods-create-new");
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("gf-ability-editor", menu.ScreenId);
        menu.Navigate(MenuInput.Cancel);
        Assert.Equal("mods-create", menu.ScreenId);
    }

    [Fact]
    public void Search_enter_returns_to_manager_without_running_package_confirmation()
    {
        string root = Path.Combine(Path.GetTempPath(), "mods-input-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using ModRuntime runtime = new(root, watch: false);
            MenuController menu = new();
            menu.ConnectMods(runtime);
            menu.Open("mods");
            menu.Push("mods-search");
            Assert.True(menu.IsModSearchEditing);
            Assert.True(menu.EnterModSearchCharacter('V'));
            menu.Navigate(MenuInput.Confirm);
            Assert.Equal("mods", menu.ScreenId);
            Assert.False(menu.IsModSearchEditing);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static MenuController AuthoringMenu()
    {
        var source = GuardianForceAbilityEditorTests.Source();
        MenuController menu = new(() => new MenuData());
        menu.ConnectGuardianForceEditor(() => new(source, null, [], [], []), id => "GF" + id, id => "Ability" + id, id => "Description" + id, action => action.ToString(),
            (_, _, _) => throw new InvalidOperationException("No review is expected while opening a new draft."),
            _ => throw new InvalidOperationException("No application is expected while opening a new draft."),
            (_, _, _) => throw new InvalidOperationException("No file write is expected while opening a new draft."));
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
