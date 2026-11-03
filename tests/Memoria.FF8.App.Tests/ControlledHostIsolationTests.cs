using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class ControlledHostIsolationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-controlled-host-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ExclusiveInputNeverPollsPhysicalDevicesAndKeepsPermanentKeysAndSticksNeutral()
    {
        HostPhysicalInput physical = new(false, () => throw new InvalidOperationException("keyboard polled"), () => throw new InvalidOperationException("controller polled"), () => throw new InvalidOperationException("mouse polled"));
        KeyboardInput input = new() { RecoveryNavigationEnabled = true, NativeButtonsOwnInput = true };
        InputFrame frame = input.Read(physical.ReadKeyboard(), physical.ReadController(), true);
        Assert.Equal(default(InputFrame), frame);
        Assert.Equal((ushort)0, input.HeldFieldButtons);
        Assert.Equal((ushort)0, input.HeldControlButtons);
        Assert.False(input.PauseRequested);
        Assert.Empty(physical.ReadKeyboard().GetPressedKeys());
        Assert.False(physical.ReadController().IsConnected);
        Assert.Equal(Vector2.Zero, physical.ReadController().ThumbSticks.Left);
        Assert.Equal(ButtonState.Released, physical.ReadMouse().LeftButton);
    }

    [Fact]
    public void OrdinaryHostStillReceivesKeyboardControllerAndMouseStates()
    {
        KeyboardState keys = new(Keys.F5, Keys.Enter, Keys.Up);
        GamePadState pad = new(new Vector2(1, -1), new Vector2(-1, 1), 1, 1, Buttons.A);
        MouseState mouse = new(12, 24, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        HostPhysicalInput physical = new(true, () => keys, () => pad, () => mouse);
        Assert.Equal(keys, physical.ReadKeyboard());
        Assert.Equal(pad, physical.ReadController());
        Assert.Equal(mouse, physical.ReadMouse());
        KeyboardInput input = new();
        InputFrame frame = input.Read(physical.ReadKeyboard(), physical.ReadController(), true);
        Assert.True(frame.Confirm);
        Assert.True(frame.Save);
        Assert.NotEqual((ushort)0, input.HeldFieldButtons);
    }

    [Fact]
    public void ControlledMenuAndQuickSavesUseOnlyTheOutputSaveBank()
    {
        LaunchOptions options = LaunchOptions.Parse(["--control-exclusive", "--control-dir", Path.Combine(root, "control"), "--settings-dir", Path.Combine(root, "settings"), "--save-state", Path.Combine(root, "save", "checkpoint.json")]);
        Directory.CreateDirectory(Path.GetDirectoryName(options.SavePath)!);
        File.WriteAllText(options.SavePath, "checkpoint sentinel");
        PlayerSaveStore store = new(options.SavePath, directory: options.IsolatedPlayerSaveDirectory, discoverOriginalSaves: false);
        GameSession session = new(new GameState { Scene = SceneKind.Field, X = 42 });
        store.Save(0, session);
        store.Save(1, session);
        string expected = Path.Combine(root, "save", "player-saves");
        Assert.Equal(expected, Path.GetDirectoryName(store.GetPath(0)));
        Assert.Equal(Path.Combine(expected, "slot-01.json"), store.GetPath(1));
        Assert.Equal(42, store.Load(0).X);
        Assert.Equal(42, store.Load(1).X);
        Assert.Equal("checkpoint sentinel", File.ReadAllText(options.SavePath));
        Assert.Empty(store.OriginalDirectoryCandidates);
        Assert.Null(store.OriginalDirectory);
    }

    [Fact]
    public void ExclusivePhysicalInputDoesNotBlockTheFileCommandQueue()
    {
        HostPhysicalInput physical = new(false, () => throw new InvalidOperationException(), () => throw new InvalidOperationException(), () => throw new InvalidOperationException());
        List<string> commands = [];
        ControlQueue queue = new(root, request =>
        {
            commands.Add(request.Command);
            return new { Accepted = true };
        });
        File.WriteAllText(Path.Combine(root, "requests", "001-ui.json"), "{\"Id\":\"001-ui\",\"Command\":\"ui-input\"}");
        File.WriteAllText(Path.Combine(root, "requests", "002-battle.json"), "{\"Id\":\"002-battle\",\"Command\":\"battle-input\"}");
        Assert.False(physical.ReadController().IsConnected);
        queue.Poll();
        queue.Poll();
        Assert.Equal(new[] { "ui-input", "battle-input" }, commands);
        Assert.True(File.Exists(Path.Combine(root, "responses", "001-ui.json")));
        Assert.True(File.Exists(Path.Combine(root, "responses", "002-battle.json")));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
