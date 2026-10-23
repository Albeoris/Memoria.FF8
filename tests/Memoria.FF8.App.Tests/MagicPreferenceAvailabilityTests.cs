using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class MagicPreferenceAvailabilityTests
{
    [Theory]
    [InlineData("Shared magic stock")]
    [InlineData("Infinite magic")]
    [InlineData("Smart Attack")]
    public void Unavailable_magic_setting_is_greyed_explains_the_boundary_and_rejects_input(string setting)
    {
        const string reason = "Magic rules are unavailable while a tutorial is active.";
        MenuData data = new() { MagicPreferencesUnavailableReason = reason };
        data.Settings[setting] = "Off";
        data.SupportedActions.Add("config:" + setting);
        List<MenuAction> actions = [];
        MenuController menu = new(() => data, actions.Add);
        menu.Open("config-magic");

        MenuEntry entry = Assert.Single(menu.Entries);
        Assert.Equal(setting, entry.Id);
        Assert.False(entry.Enabled);
        Assert.Equal(reason, entry.Help);
        Assert.Equal(reason, menu.SelectedEntry!.Help);
        menu.Navigate(MenuInput.Right);
        menu.Navigate(MenuInput.Left);
        menu.Navigate(MenuInput.Confirm);
        Assert.Empty(actions);

        UiView view = menu.BuildView();
        Assert.Contains(Descendants(view.Root).OfType<UiLabel>(), label => label.Text == reason);
        UiChoiceRow row = Assert.Single(Descendants(view.Root).OfType<UiChoiceRow>());
        Assert.True(row.Enabled);
        Assert.True(row.ReadOnly);
        Assert.Same(row, view.FocusedElement);
        view.Pointer(row.ValueBounds.Right - 5, row.Bounds.Y + row.Bounds.Height / 2, true);
        Assert.Empty(actions);
        Assert.Equal("Off", data.Settings[setting]);
    }

    [Theory]
    [InlineData("config-magic", "Shared magic stock")]
    [InlineData("config-magic", "Infinite magic")]
    [InlineData("config-magic", "Smart Attack")]
    [InlineData("new-game-options", "Shared magic stock")]
    [InlineData("new-game-options", "Infinite magic")]
    [InlineData("new-game-options", "Smart Attack")]
    public void Available_magic_setting_remains_editable_in_config_and_title(string screen, string setting)
    {
        MenuData data = new();
        data.Settings[setting] = "Off";
        data.SupportedActions.Add("config:" + setting);
        List<MenuAction> actions = [];
        MenuController menu = new(() => data, actions.Add);
        menu.Open(screen);

        MenuEntry entry = Assert.Single(menu.Entries, entry => entry.Id == setting);
        Assert.True(entry.Enabled);
        menu.Navigate(MenuInput.Right);
        MenuAction action = Assert.Single(actions);
        Assert.Equal("config", action.Kind);
        Assert.Equal(setting, action.SubjectId);
        Assert.Equal("On", action.Value);
        UiView view = menu.BuildView();
        Assert.True(Assert.Single(Descendants(view.Root).OfType<UiChoiceRow>()).Enabled);
    }

    private static IEnumerable<UiElement> Descendants(UiElement root)
    {
        yield return root;
        foreach (UiElement child in root.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
