using System.Reflection;
using System.Runtime.CompilerServices;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7TitleConfigTests
{
    [Fact]
    public void ConfigOpensWithoutStartingAContinuationAndCancelReturnsToItsTitleSelection()
    {
        Ff7Scene scene = CreateTitle(out _);
        int continues = 0;
        scene.ContinueRequested += () => continues++;
        Press(scene, Keys.Down);
        Press(scene, Keys.Down);
        Press(scene, Keys.J);
        Assert.True(scene.IsTitle);
        Assert.Null(scene.Session);
        MenuController menu = Assert.IsType<MenuController>(scene.ActivePlayerMenu);
        Assert.Equal("config", menu.ScreenId);
        Assert.Equal(new[] { "config-graphics", "config-audio", "config-controls", "config-interface", "config-speed" }, menu.CurrentScreen.Entries(menu).Select(entry => entry.Id));
        Press(scene, Keys.K);
        Assert.False(scene.IsPlayerMenuOpen);
        Press(scene, Keys.J);
        Assert.Equal("config", scene.ActivePlayerMenu?.ScreenId);
        Assert.Equal(0, continues);
        Assert.Null(scene.Error);
    }

    [Fact]
    public void AudioSettingPersistsWithoutGameplayAndCancelsThroughConfigToTitle()
    {
        Ff7Scene scene = CreateTitle(out Func<UiPreferences> preferences);
        Press(scene, Keys.Down);
        Press(scene, Keys.Down);
        Press(scene, Keys.J);
        Press(scene, Keys.Down);
        Press(scene, Keys.J);
        Assert.Equal("config-audio", scene.ActivePlayerMenu?.ScreenId);
        Press(scene, Keys.Left);
        Assert.Equal(99, preferences().MusicVolume);
        Assert.Equal("99%", scene.ActivePlayerMenu!.Data.Settings["Music volume"]);
        Assert.Null(scene.Session);
        Press(scene, Keys.K);
        Assert.Equal("config", scene.ActivePlayerMenu?.ScreenId);
        Press(scene, Keys.K);
        Assert.False(scene.IsPlayerMenuOpen);
        Assert.True(scene.IsTitle);
        Assert.Equal(99, preferences().MusicVolume);
        Assert.Null(scene.Error);
    }

    [Fact]
    public void TitlePreferenceWritesPreserveTheRetainedBattleModeAndOtherGlobalPreferences()
    {
        Ff7Scene scene = CreateTitle(out Func<UiPreferences> preferences);
        typeof(Ff7Scene).GetField("envelope", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(scene, new CombinedSessionSave { ActiveGame = SessionGame.Ff7, TurnBasedBattle = false });
        UiPreferences before = preferences();
        Assert.Equal(BattleAtbMode.TurnBased, before.AtbMode);
        Assert.Equal(BattleAtbMode.Normal, scene.PresentationPreferences.AtbMode);
        Press(scene, Keys.Down);
        Press(scene, Keys.Down);
        Press(scene, Keys.J);
        scene.ActivePlayerMenu!.Execute(new("config", "Music volume", Value: "75%"));
        Assert.Equal(before with { MusicVolume = 75 }, preferences());
        Assert.Equal(BattleAtbMode.Normal, scene.PresentationPreferences.AtbMode);
        Assert.Null(scene.Session);
    }

    [Fact]
    public void ContinueKeepsItsExistingTitlePosition()
    {
        Ff7Scene scene = CreateTitle(out _);
        int continues = 0;
        scene.ContinueRequested += () => continues++;
        Press(scene, Keys.Down);
        Press(scene, Keys.J);
        Assert.Equal(1, continues);
        Assert.False(scene.IsPlayerMenuOpen);
        Assert.Null(scene.Error);
    }

    private static void Press(Ff7Scene scene, Keys key)
    {
        scene.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1d / 60)), new KeyboardState());
        scene.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1d / 60)), new KeyboardState(key));
    }

    private static Ff7Scene CreateTitle(out Func<UiPreferences> readPreferences)
    {
        Ff7Scene scene = (Ff7Scene)RuntimeHelpers.GetUninitializedObject(typeof(Ff7Scene));
        UiPreferences preferences = new();
        KeyboardInput input = new() { Profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.Confirm, ControlDevice.Keyboard, new([Keys.J], []), replaceConflicts: true).WithBinding(ControlAction.Cancel, ControlDevice.Keyboard, new([Keys.K], []), replaceConflicts: true) };
        Set("sharedInput", input);
        Set("sharedClock", new GameplayClock());
        Set("soundChannels", new Ff7SoundChannels());
        Set("movieSkipGesture", new MovieSkipGesture());
        Set("noticeWindow", new NoticeWindow());
        Set("envelope", new CombinedSessionSave { ActiveGame = SessionGame.Ff7 });
        Set("dialoguePreferences", (Func<UiPreferences>)(() => preferences));
        Set("setDialoguePreferences", (Action<UiPreferences>)(updated => preferences = updated));
        readPreferences = () => preferences;
        return scene;

        void Set(string name, object value)
        {
            typeof(Ff7Scene).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(scene, value);
        }
    }
}
