using Albeoris.Games.FF8.TextEncoding.Tags;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class DebugNameMenuTests
{
    [Fact]
    public void Debug_names_edit_live_owner_even_when_gallery_is_demo_and_cancel_discards_draft()
    {
        Dictionary<int, string> names = new() { [0] = "Squall", [16] = "Quezacotl" };
        int writes = 0;
        MenuController menus = new();
        menus.ConnectNaming(() => names.Select(pair => new DebugNameTarget(pair.Key, pair.Value, "Character")).ToArray(), (subject, name) =>
        {
            names[subject] = name;
            writes++;
        });
        menus.Open("debug-room", true);
        Assert.True(menus.Entries.Single(entry => entry.Id == "debug-names").Enabled);
        menus.Push("debug-names");
        menus.Execute(new MenuAction("name-edit", "0"));
        menus.Execute(new MenuAction("name-clear"));
        foreach (char character in "Леон")
        {
            Assert.True(menus.EnterNameCharacter(character));
        }
        menus.Execute(new MenuAction("name-save"));
        Assert.Equal("Леон", names[0]);
        Assert.Equal(1, writes);
        Assert.Contains(menus.Entries, entry => entry.Label.Contains("Леон", StringComparison.Ordinal));
        menus.Execute(new MenuAction("name-edit", "16"));
        menus.Execute(new MenuAction("name-clear"));
        menus.Execute(new MenuAction("name-save"));
        Assert.True(menus.IsEditingName);
        Assert.Equal(1, writes);
        menus.EnterNameCharacter('A');
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal("Quezacotl", names[16]);
        Assert.False(menus.IsEditingName);
        menus.Close();
        Assert.False(menus.EnterNameCharacter('B'));
    }

    [Fact]
    public void Screen_keyboard_and_unicode_delete_edit_the_same_draft()
    {
        string name = "Squall";
        MenuController menus = new();
        menus.ConnectNaming(() => [new DebugNameTarget(0, name, "Character")], (_, value) => name = value);
        menus.Open("debug-names", true);
        menus.Navigate(MenuInput.Confirm);
        menus.Execute(new MenuAction("name-clear"));
        menus.BuildView();
        menus.Navigate(MenuInput.Right);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("B", menus.NameDraft);
        menus.Execute(new MenuAction("name-clear"));
        menus.Execute(new MenuAction("name-letter", Value: "😀"));
        menus.EnterNameCharacter('\b');
        Assert.Equal("", menus.NameDraft);
        menus.Execute(new MenuAction("name-letter", Value: "A"));
        menus.Execute(new MenuAction("name-save"));
        Assert.Equal("A", name);
    }

    [Fact]
    public void Typing_does_not_trigger_game_shortcuts_but_arrows_enter_and_escape_work()
    {
        KeyboardInput keyboard = new() { TextEntryEnabled = true };
        InputFrame text = keyboard.Read(new KeyboardState(Keys.X, Keys.C, Keys.V, Keys.H, Keys.G, Keys.F1, Keys.F4, Keys.F5), true);
        Assert.False(text.Confirm);
        Assert.False(text.Cancel);
        Assert.False(text.Save);
        Assert.False(keyboard.GameMenuRequested);
        Assert.False(keyboard.SpeedToggleRequested);
        Assert.False(keyboard.BattleDebugRequested);
        keyboard.Read(new KeyboardState(), true);
        InputFrame navigation = keyboard.Read(new KeyboardState(Keys.Enter, Keys.Escape, Keys.Right), true);
        Assert.True(navigation.Confirm);
        Assert.True(navigation.Cancel);
        Assert.Equal(1, navigation.MenuHorizontal);
    }

    [Fact]
    public void All_character_and_guardian_tags_use_their_matching_saved_name_subjects()
    {
        Dictionary<int, string> names = Enumerable.Range(0, 43).Where(GameNames.IsValidSubject).ToDictionary(subject => subject, subject => $"Name {subject}");
        for (int identity = 0; identity < 11; identity++)
        {
            TextTagCharacter character = (TextTagCharacter)((int)TextTagCharacter.Squall + identity);
            Assert.Equal(names[GameNames.CharacterSubject(identity)], DialogueTextFormatter.Format($"{{Char {character}}}", names));
        }
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        for (int identity = 0; identity < 16; identity++)
        {
            int subject = GameNames.GuardianForceSubject(identity);
            TextTagName guardian = (TextTagName)((int)TextTagName.Quetzacotl + identity);
            Assert.Equal(names[subject], DialogueTextFormatter.Format($"{{Name {guardian}}}", names));
            Assert.Equal(catalog.GuardianForceName(identity), GameNames.DefaultName(subject));
        }
    }
}
