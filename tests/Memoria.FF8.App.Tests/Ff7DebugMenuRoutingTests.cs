using System.Reflection;
using System.Runtime.CompilerServices;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7DebugMenuRoutingTests
{
    [Fact]
    public void TitleDebugNavigationNeverActivatesContinueUnderTheOverlay()
    {
        Ff7Scene scene = CreateTitle();
        int continued = 0;
        scene.ContinueRequested += () => continued++;
        Sample(scene);
        Sample(scene, Keys.F2);
        Assert.True(scene.IsDebugMenuOpen);
        Sample(scene);
        Sample(scene, Keys.Down);
        Sample(scene);
        Sample(scene, Keys.J);
        Assert.Equal(0, continued);
        Assert.True(scene.IsTitle);
        Assert.True(scene.IsDebugMenuOpen);
        Sample(scene);
        Sample(scene, Keys.F2);
        Assert.False(scene.IsDebugMenuOpen);
    }

    [Fact]
    public void HostDebugRequestOwnsTheOpeningCallbackAndDrainsConfirm()
    {
        Ff7Scene scene = CreateTitle();
        scene.UseHostDebugMenu = true;
        int opened = 0;
        int continued = 0;
        scene.DebugMenuRequested += () => opened++;
        scene.ContinueRequested += () => continued++;
        Sample(scene);
        Sample(scene, Keys.Down);
        Sample(scene);
        Sample(scene, Keys.F2, Keys.J);
        Assert.Equal(1, opened);
        Assert.Equal(0, continued);
        Assert.False(scene.IsDebugMenuOpen);
        Sample(scene, Keys.J);
        Assert.Equal(0, continued);
    }

    private static void Sample(Ff7Scene scene, params Keys[] keys)
    {
        scene.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1d / 60)), new KeyboardState(keys));
    }

    private static Ff7Scene CreateTitle()
    {
        Ff7Scene scene = (Ff7Scene)RuntimeHelpers.GetUninitializedObject(typeof(Ff7Scene));
        KeyboardInput input = new() { Profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.Confirm, ControlDevice.Keyboard, new([Keys.J], []), replaceConflicts: true) };
        Set(scene, "sharedInput", input);
        Set(scene, "sharedClock", new GameplayClock());
        Set(scene, "soundChannels", new Ff7SoundChannels());
        Set(scene, "movieSkipGesture", new MovieSkipGesture());
        Set(scene, "envelope", new CombinedSessionSave { ActiveGame = SessionGame.Ff7 });
        return scene;
    }

    private static void Set(Ff7Scene scene, string name, object value)
    {
        typeof(Ff7Scene).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(scene, value);
    }
}
