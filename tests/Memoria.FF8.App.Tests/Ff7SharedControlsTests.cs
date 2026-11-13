using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7SharedControlsTests
{
    [Fact]
    public void ActiveSceneUsesItsOwnSharedFrameLimitAndMenusKeepTheMenuLimit()
    {
        Ff7Scene scene = (Ff7Scene)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Ff7Scene));
        typeof(Ff7Scene).GetField("envelope", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(scene, new Memoria.FF7.Runtime.CombinedSessionSave());
        GameUi.UiPreferences preferences = new() { FieldFps = 30, WorldFps = 60, MenuFps = 120 };
        typeof(Ff7Scene).GetField("dialoguePreferences", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(scene, (Func<GameUi.UiPreferences>)(() => preferences));
        Assert.Equal(120, scene.PresentationFrameLimit);
        Memoria.FF7.Runtime.Ff7Session session = (Memoria.FF7.Runtime.Ff7Session)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Memoria.FF7.Runtime.Ff7Session));
        typeof(Ff7Scene).GetProperty(nameof(Ff7Scene.Session))!.SetValue(scene, session);
        Assert.Equal(30, scene.PresentationFrameLimit);
        Memoria.FF7.Runtime.Ff7WorldRuntime world = (Memoria.FF7.Runtime.Ff7WorldRuntime)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Memoria.FF7.Runtime.Ff7WorldRuntime));
        typeof(Memoria.FF7.Runtime.Ff7Session).GetProperty(nameof(Memoria.FF7.Runtime.Ff7Session.World))!.SetValue(session, world);
        Assert.Equal(60, scene.PresentationFrameLimit);
        preferences = preferences with { WorldFps = 30, FieldFps = 60 };
        Assert.Equal(30, scene.PresentationFrameLimit);
        typeof(Ff7Scene).GetField("debugMenu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(scene, true);
        Assert.Equal(120, scene.PresentationFrameLimit);
    }

    [Theory]
    [InlineData(0x40, 0x20)]
    [InlineData(0x10, 0x40)]
    [InlineData(0x20, 0x10)]
    [InlineData(0x80, 0x80)]
    [InlineData(0xF90F, 0xF90F)]
    public void Native_slots_preserve_shared_actions_and_prompt_inverse(int shared, int native)
    {
        Assert.Equal((ushort)native, Ff7ControlButtons.FromShared((ushort)shared));
        Assert.Equal((ushort)shared, Ff7ControlButtons.ToShared((ushort)native));
    }

    [Fact]
    public void Configured_confirm_cancel_and_movement_drive_both_semantics_and_native_packet()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.Confirm, ControlDevice.Keyboard, new([Keys.J], []), replaceConflicts: true).WithBinding(ControlAction.Cancel, ControlDevice.Keyboard, new([Keys.K], []), replaceConflicts: true);
        KeyboardInput input = new() { Profile = profile };
        input.Read(default, true);
        Runtime.InputFrame frame = input.Read(new KeyboardState(Keys.J, Keys.K, Keys.Right), true);
        Assert.True(frame.Confirm);
        Assert.True(input.CancelHeld);
        Assert.Equal(1, frame.Horizontal);
        Assert.Equal((ushort)0x2060, Ff7ControlButtons.FromShared(input.HeldFieldButtons));
        Assert.False(Ff7ControlButtons.Run(input.HeldFieldButtons));
        input.Read(default, true);
        frame = input.Read(new KeyboardState(Keys.Right), true);
        Assert.Equal(1, frame.Horizontal);
        Assert.True(Ff7ControlButtons.Run(input.HeldFieldButtons));
    }

    [Fact]
    public void Shared_analog_walk_threshold_and_cancel_binding_are_retained()
    {
        KeyboardInput input = new() { Profile = ControlsProfile.CreateDefault().WithControllerSettings(0, 0, 0.6f) };
        input.Read(default, Pad(Vector2.Zero), true);
        Runtime.InputFrame slow = input.Read(default, Pad(new Vector2(0.3f, 0)), true);
        Assert.True(slow.Horizontal > 0);
        Assert.False(Ff7ControlButtons.Run(input.HeldFieldButtons));
        Runtime.InputFrame fast = input.Read(default, Pad(new Vector2(0.9f, 0)), true);
        Assert.True(fast.Horizontal > slow.Horizontal);
        Assert.True(Ff7ControlButtons.Run(input.HeldFieldButtons));
    }

    [Fact]
    public void Movie_hold_skip_uses_remapped_cancel_and_real_elapsed_time()
    {
        KeyboardInput input = new() { Profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.Cancel, ControlDevice.Keyboard, new([Keys.K], []), replaceConflicts: true) };
        MovieSkipGesture gesture = new();
        input.Read(default, true);
        input.Read(new KeyboardState(Keys.K), true);
        Assert.False(gesture.Update(TimeSpan.FromMilliseconds(600), input.CancelHeld));
        input.Read(default, true);
        Assert.False(gesture.Update(TimeSpan.FromMilliseconds(10), input.CancelHeld));
        input.Read(new KeyboardState(Keys.K), true);
        Assert.False(gesture.Update(TimeSpan.FromMilliseconds(999), input.CancelHeld));
        Assert.True(gesture.Update(TimeSpan.FromMilliseconds(1), input.CancelHeld));
        Assert.False(gesture.Update(TimeSpan.FromSeconds(1), input.CancelHeld));
    }

    private static GamePadState Pad(Vector2 movement) => new(movement, Vector2.Zero, 0, 0, Array.Empty<Buttons>());

    [Fact]
    public void Sixty_fps_sample_blends_positions_and_native_loop_phase_without_mutating_progress()
    {
        Memoria.FF7.Runtime.Ff7Actor previous = new() { Slot = 0, Entity = 0, Model = 0, Visible = true, X4096 = 4096, Direction = 250, AnimationFrame16 = 152 };
        Memoria.FF7.Runtime.Ff7Actor current = previous with { X4096 = 8192, Direction = 6, AnimationFrame16 = 8 };
        Memoria.FF7.Runtime.Ff7Actor sample = Ff7ActorPresentation.Interpolate(previous, current, 0.5f, 10);
        Assert.Equal(6144, sample.X4096);
        Assert.Equal(0, sample.Direction);
        Assert.Equal(0, sample.AnimationFrame16);
        Assert.Equal(8192, current.X4096);
        Assert.Equal(8, current.AnimationFrame16);
        Memoria.FF7.Runtime.Ff7Actor replacement = current with { Model = 1 };
        Assert.Same(replacement, Ff7ActorPresentation.Interpolate(previous, replacement, 0.5f, 10));
        Memoria.FF7.Runtime.Ff7Actor beforeWrap = current with { X4096 = short.MaxValue * 4096 };
        Memoria.FF7.Runtime.Ff7Actor afterWrap = current with { X4096 = short.MinValue * 4096 };
        Assert.Same(afterWrap, Ff7ActorPresentation.Interpolate(beforeWrap, afterWrap, 0.5f, 10));
    }
}
