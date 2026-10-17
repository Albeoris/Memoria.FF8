using System.Globalization;
using System.Text;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class NameInputWindowTests
{
    [Fact]
    public void Entity_misc_name_tags_use_their_distinct_literal_saved_names()
    {
        Dictionary<int, string> names = new() { [GameNames.BokoSubject] = "鳥{Page}", [GameNames.GrieverSubject] = "Лев" };
        Assert.Equal("鳥{Page} Лев", DialogueTextFormatter.Format("{Name Boko} {Name Griever}", names));
    }

    [Fact]
    public void Pet_nametag_confirmation_sends_no_item_action_until_yes()
    {
        MenuData data = new();
        data.Items.Add(new MenuItem { Id = "item-107", Name = "Pet Nametag", Quantity = 1, NativeFieldUseSupported = true, NativeFieldNameConfirmation = true, NativeFieldNameSubject = 2 });
        data.SupportedActions.Add("item-use");
        List<MenuAction> sent = [];
        MenuController menus = new(() => data, sent.Add);
        menus.Open("item-target");
        menus.Execute(new MenuAction("select-item", "item-107"));
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("item-name-confirm", menus.ScreenId);
        menus.Navigate(MenuInput.Cancel);
        Assert.Empty(sent);
        menus.Navigate(MenuInput.Confirm);
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Confirm);
        Assert.Empty(sent);
        menus.Navigate(MenuInput.Confirm);
        menus.Navigate(MenuInput.Up);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("item-use", Assert.Single(sent).Kind);
    }

    [Fact]
    public void Controller_pages_include_all_printable_windows1252_and_the_cyrillic_block()
    {
        MenuController menus = new() { NamingControllerConnected = true };
        menus.OpenNamePrompt(0, "Squall", _ => { });
        HashSet<string> characters = [];
        for (int page = 0; page < 6; page++)
        {
            foreach (MenuEntry entry in menus.Entries.Where(entry => entry.Enabled && entry.Action?.Kind == "name-letter"))
            {
                characters.Add(entry.Action!.Value);
            }
            menus.Navigate(MenuInput.PageRight);
        }
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding western = Encoding.GetEncoding(1252);
        foreach (int value in Enumerable.Range(32, 224).Where(value => value is < 127 or > 159 || value is 128 or 130 or 131 or 132 or 133 or 134 or 135 or 136 or 137 or 138 or 139 or 140 or 142 or 145 or 146 or 147 or 148 or 149 or 150 or 151 or 152 or 153 or 154 or 155 or 156 or 158 or 159))
        {
            Assert.Contains(western.GetString([(byte)value]), characters);
        }
        foreach (int value in Enumerable.Range(0x400, 256))
        {
            Assert.Contains(((char)value).ToString(), characters);
        }
    }

    [Fact]
    public void Keyboard_accepts_braces_cjk_and_surrogate_pairs_up_to_twenty_graphemes()
    {
        string? saved = null;
        MenuController menus = new();
        menus.OpenNamePrompt(0, "", name => saved = name);
        string name = "{Page}界" + string.Concat(Enumerable.Repeat("👩🏽‍🚀", 13));
        foreach (char character in name)
        {
            Assert.True(menus.EnterNameCharacter(character));
        }
        Assert.Equal(20, StringInfo.ParseCombiningCharacters(menus.NameDraft).Length);
        menus.EnterNameCharacter('X');
        Assert.Equal(name, menus.NameDraft);
        menus.EnterNameCharacter('\b');
        Assert.Equal(19, StringInfo.ParseCombiningCharacters(menus.NameDraft).Length);
        menus.EnterNameText("e\u0301");
        menus.Execute(new MenuAction("name-save"));
        Assert.Equal(name[..^7] + "e\u0301", saved);
        Assert.False(menus.IsOpen);
        Assert.False(menus.EnterNameText("hidden"));
    }

    [Fact]
    public void Native_cancel_deletes_and_default_restores_the_subject_without_accepting()
    {
        int accepted = 0;
        MenuController menus = new();
        menus.OpenNamePrompt(16, "Custom", _ => accepted++);
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal("Custo", menus.NameDraft);
        menus.Execute(new MenuAction("name-default"));
        Assert.Equal(GameNames.DefaultName(16), menus.NameDraft);
        menus.Execute(new MenuAction("name-clear"));
        menus.Execute(new MenuAction("name-save"));
        Assert.True(menus.IsNamePrompt);
        Assert.Equal(0, accepted);
        menus.Execute(new MenuAction("name-default"));
        menus.Execute(new MenuAction("name-save"));
        Assert.Equal(1, accepted);
    }

    [Fact]
    public void Controller_confirm_selects_letters_and_cancel_deletes_while_typing_mode_is_active()
    {
        KeyboardInput keyboard = new() { TextEntryEnabled = true };
        MenuController menus = new() { NamingControllerConnected = true };
        menus.OpenNamePrompt(0, "", _ => { });
        keyboard.Read(default, Pad(), true);
        InputFrame right = keyboard.Read(default, Pad(Buttons.DPadRight), true);
        Assert.Equal(1, right.MenuHorizontal);
        menus.Navigate(MenuInput.Right);
        keyboard.Read(default, Pad(), true);
        InputFrame confirm = keyboard.Read(default, Pad(Buttons.A), true);
        Assert.True(confirm.Confirm);
        menus.BuildView();
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("B", menus.NameDraft);
        keyboard.Read(default, Pad(), true);
        InputFrame cancel = keyboard.Read(default, Pad(Buttons.B), true);
        Assert.True(cancel.Cancel);
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal("", menus.NameDraft);
        Assert.True(menus.IsOpen);
    }

    [Theory]
    [InlineData(640, 448, 0)]
    [InlineData(1280, 896, 16)]
    [InlineData(1920, 1080, 43)]
    public void Name_window_uses_nonoverlapping_retained_controls_and_subject_artwork(int width, int height, int subject)
    {
        MenuController menus = new() { NamingControllerConnected = true };
        menus.OpenNamePrompt(subject, GameNames.DefaultName(subject), _ => { });
        UiView view = menus.BuildView();
        view.Resize(width, height);
        UiElement[] elements = Descendants(view.Root).ToArray();
        UiPanel help = Assert.Single(elements.OfType<UiPanel>(), panel => panel.Caption == "HELP");
        UiLabel helpText = Assert.Single(Descendants(help).OfType<UiLabel>());
        Assert.True(helpText.Bounds.Y >= help.Bounds.Y + help.Padding.Top + help.CaptionHeight);
        Assert.True(helpText.Bounds.Bottom <= help.Bounds.Bottom - help.Padding.Bottom);
        UiButton[] buttons = elements.OfType<UiButton>().Where(button => button.Enabled).ToArray();
        Assert.All(buttons, button => Assert.True(button.Bounds.Width > 0 && button.Bounds.Height > 0));
        UiLabel[] commands = buttons.Where(button => button.Id?.StartsWith("name-command-", StringComparison.Ordinal) == true).Select(button => Assert.Single(button.Children.OfType<UiLabel>())).ToArray();
        Assert.All(commands, command => Assert.Equal(commands[0].Bounds.X, command.Bounds.X));
        for (int first = 0; first < buttons.Length; first++)
        {
            for (int second = first + 1; second < buttons.Length; second++)
            {
                UiRect a = buttons[first].Bounds;
                UiRect b = buttons[second].Bounds;
                Assert.False(a.X < b.Right - 0.01f && b.X < a.Right - 0.01f && a.Y < b.Bottom - 0.01f && b.Y < a.Bottom - 0.01f, $"Overlapping controls: {buttons[first].Id} and {buttons[second].Id}");
            }
        }
        UiImage image = Assert.Single(elements.OfType<UiImage>());
        Assert.Equal(subject == 43 ? null : new UiImageSource(subject == 16 ? UiImageKind.GuardianForcePortrait : UiImageKind.CharacterPortrait, 0), image.Source);
        UiButton letter = buttons.Single(button => button.Id == "letter-0");
        string before = menus.NameDraft;
        float scale = Math.Min(width / view.DesignWidth, height / view.DesignHeight);
        float offsetX = (width - view.DesignWidth * scale) / 2;
        float offsetY = (height - view.DesignHeight * scale) / 2;
        view.Pointer(offsetX + (letter.Bounds.X + letter.Bounds.Width / 2) * scale, offsetY + (letter.Bounds.Y + letter.Bounds.Height / 2) * scale, true);
        Assert.Equal(before + "A", menus.NameDraft);
    }

    private static GamePadState Pad(params Buttons[] buttons) => new(Vector2.Zero, Vector2.Zero, 0, 0, buttons);

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
