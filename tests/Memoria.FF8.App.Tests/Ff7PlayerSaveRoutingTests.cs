using System.Reflection;
using System.Runtime.CompilerServices;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7PlayerSaveRoutingTests
{
    [Theory]
    [InlineData(Keys.F5, "quick-save")]
    [InlineData(Keys.F6, "save-menu")]
    [InlineData(Keys.F9, "quick-load")]
    [InlineData(Keys.F10, "load-menu")]
    public void Shared_shortcuts_reach_the_host_once_without_activating_title_continue(Keys key, string expected)
    {
        Ff7Scene scene = CreateTitle(new CombinedSessionSave { ActiveGame = SessionGame.Ff7 });
        List<InputFrame> requests = [];
        int continued = 0;
        scene.PlayerSaveRequested += requests.Add;
        scene.ContinueRequested += () => continued++;
        Sample(scene);
        Sample(scene, Keys.Down);
        Sample(scene);
        Sample(scene, key, Keys.J);
        Sample(scene, key, Keys.J);
        InputFrame frame = Assert.Single(requests);
        Assert.Equal(expected == "quick-save", frame.Save);
        Assert.Equal(expected == "quick-load", frame.QuickLoad);
        Assert.Equal(expected == "save-menu", frame.SaveMenu);
        Assert.Equal(expected == "load-menu", frame.LoadMenu);
        Assert.Equal(0, continued);
    }

    [Fact]
    public void Native_load_keeps_dormant_world_and_identity_without_carrying_old_battle_state()
    {
        CombinedSessionSave current = new()
        {
            ActiveGame = SessionGame.Ff7,
            Ff8Payload = [1, 2, 3],
            Ff8PayloadFormat = "test",
            CharacterNameOverrides = new() { [0] = "Cloud" },
            Mappings = [new(SessionGame.Ff7, 0, SessionGame.Ff8, 0, "test")]
        };
        Ff7Scene scene = CreateTitle(current);
        CombinedSessionSave incoming = new() { ActiveGame = SessionGame.Ff7 };
        CombinedSessionSave retained = scene.RetainDormantWorld(incoming);
        Assert.Equal(SessionGame.Ff7, retained.ActiveGame);
        Assert.Same(current.Ff8Payload, retained.Ff8Payload);
        Assert.Equal(current.Ff8PayloadFormat, retained.Ff8PayloadFormat);
        Assert.Same(current.Mappings, retained.Mappings);
        Assert.Same(current.CharacterNameOverrides, retained.CharacterNameOverrides);
        Assert.Null(retained.MixedCombat);
        Assert.Null(retained.GuestBattleSettlements);
        CombinedSessionSave combined = incoming with { Ff8Payload = [4], Ff8PayloadFormat = "other" };
        Assert.Same(combined, scene.RetainDormantWorld(combined));
    }

    [Fact]
    public void Ff7_folder_selection_keeps_native_banks_separate_from_isolated_memoria_slots()
    {
        string temporary = Path.Combine(Path.GetTempPath(), "memoria-ff7-save-routing-" + Guid.NewGuid().ToString("N"));
        string original = Path.Combine(temporary, "native");
        Directory.CreateDirectory(original);
        try
        {
            PlayerSaveStore store = new(Path.Combine(temporary, "checkpoint.ffsave"), directory: Path.Combine(temporary, "player"), discoverOriginalSaves: false, game: SessionGame.Ff7);
            Assert.Null(store.OriginalDirectory);
            Assert.Empty(store.OriginalDirectoryCandidates);
            Assert.Throws<InvalidOperationException>(() => store.GetPath(101));
            store.SetOriginalDirectory(original);
            Assert.Equal(original, store.OriginalDirectory);
            Assert.Equal(Path.Combine(original, "save00.ff7"), store.GetPath(101));
            Assert.Equal(Path.Combine(original, "save04.ff7"), store.GetPath(175));
            Assert.Equal(Path.Combine(original, "save05.ff7"), store.GetPath(176));
            Assert.Equal(Path.Combine(original, "save09.ff7"), store.GetPath(250));
            Assert.Equal(Path.Combine(temporary, "player", "ff7"), Path.GetDirectoryName(store.GetPath(1)));
            Assert.Empty(Directory.EnumerateFiles(original));
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }
    private static void Sample(Ff7Scene scene, params Keys[] keys)
    {
        scene.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1d / 60)), new KeyboardState(keys));
    }

    private static Ff7Scene CreateTitle(CombinedSessionSave envelope)
    {
        Ff7Scene scene = (Ff7Scene)RuntimeHelpers.GetUninitializedObject(typeof(Ff7Scene));
        Set(scene, "sharedInput", new KeyboardInput { Profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.Confirm, ControlDevice.Keyboard, new([Keys.J], []), replaceConflicts: true) });
        Set(scene, "sharedClock", new GameplayClock());
        Set(scene, "soundChannels", new Ff7SoundChannels());
        Set(scene, "movieSkipGesture", new MovieSkipGesture());
        Set(scene, "envelope", envelope);
        return scene;
    }

    private static void Set(Ff7Scene scene, string name, object value)
    {
        typeof(Ff7Scene).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(scene, value);
    }
}
