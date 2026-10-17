using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class NameInputModeTests
{
    [Fact]
    public void Keyboard_is_default_without_a_controller_and_shows_typing_instructions()
    {
        MenuController menus = new();
        menus.OpenNamePrompt(0, "", _ => { });
        Assert.True(menus.IsTypingName);
        Assert.Equal("Keyboard", menus.Entries[98].Label);
        UiView view = menus.BuildView();
        view.Resize(640, 448);
        Assert.Contains(Descendants(view.Root).OfType<UiLabel>(), label => label.Text.Contains("Use your keyboard", StringComparison.Ordinal));
        Assert.DoesNotContain(Descendants(view.Root).OfType<UiButton>(), button => button.Id == "letter-1");
        Assert.True(menus.EnterNameCharacter('X'));
        Assert.True(menus.EnterNameCharacter('Ч'));
        Assert.Equal("XЧ", menus.NameDraft);
    }

    [Theory]
    [InlineData('X', "English", 1, "A")]
    [InlineData('Ч', "English", 1, "A")]
    [InlineData('X', "Symbols", 2, " ")]
    [InlineData('Ч', "Symbols", 2, " ")]
    [InlineData('X', "Cyrillic", 3, "А")]
    [InlineData('Ч', "Cyrillic", 3, "А")]
    public void Mapped_confirm_selects_left_alphabet_without_typing_its_character(char character, string alphabet, int moves, string firstLetter)
    {
        MenuController menus = new();
        menus.OpenNamePrompt(0, "Squall", _ => Assert.Fail("Choosing an alphabet must not accept the name."));
        menus.Navigate(MenuInput.Left);
        for (int move = 0; move < moves; move++)
        {
            menus.Navigate(MenuInput.Down);
        }
        Assert.Equal(alphabet, menus.SelectedEntry!.Label);
        Assert.False(menus.IsTypingName);
        Assert.False(menus.EnterNameCharacter(character));
        KeyboardInput keyboard = new() { NamingNavigationEnabled = true };
        Assert.True(keyboard.Read(new KeyboardState(Keys.X), true).Confirm);
        menus.BuildView();
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("Squall", menus.NameDraft);
        Assert.Equal(firstLetter, menus.SelectedEntry!.Action!.Value);
        Assert.False(menus.EnterNameCharacter(character));
        menus.BuildView();
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("Squall" + firstLetter, menus.NameDraft);
        menus.Navigate(MenuInput.Left);
        Assert.Equal("Keyboard", menus.SelectedEntry!.Label);
        menus.BuildView();
        menus.Navigate(MenuInput.Confirm);
        Assert.True(menus.IsTypingName);
        Assert.True(menus.EnterNameCharacter(character));
        Assert.Equal("Squall" + firstLetter + character, menus.NameDraft);
    }

    [Fact]
    public void Connected_controller_defaults_to_the_screen_keyboard()
    {
        MenuController menus = new() { NamingControllerConnected = true };
        menus.OpenNamePrompt(0, "", _ => { });
        Assert.False(menus.IsTypingName);
        Assert.Equal("A", menus.SelectedEntry!.Label);
        menus.BuildView();
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("A", menus.NameDraft);
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
