using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

/// <summary>Shared host input sampler for gameplay, menus and shortcuts on both devices.</summary>
internal sealed class KeyboardInput
{
    private HashSet<ControlAction> previous = [];
    private readonly HashSet<Keys> consumedKeys = [];
    private readonly HashSet<Buttons> consumedButtons = [];
    private readonly HashSet<Keys> disarmedKeys = [];
    private readonly HashSet<Buttons> disarmedButtons = [];
    private Vector2 previousStick;
    private KeyboardState previousKeyboard;
    private bool previousConnected;
    private bool previousActive = true;

    private ControlsProfile profile = ControlsProfile.CreateDefault();
    private bool profileChanged;
    private bool hasSampledInput;

    public ControlsProfile Profile
    {
        get => profile;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            profileChanged |= hasSampledInput && profile != value;
            profile = value;
        }
    }
    public bool RecoveryNavigationEnabled { get; set; }
    public bool SpeedToggleRequested { get; private set; }
    public bool DebugRoomRequested { get; private set; }
    public bool GameMenuRequested { get; private set; }
    public bool PauseRequested { get; private set; }
    public bool UiTabRequested { get; private set; }
    public int UiCharacterPage { get; private set; }
    public bool UiDetailRequested { get; private set; }
    public bool UiConfirmRequested { get; private set; }
    public bool UiCancelRequested { get; private set; }
    public bool BattleDebugRequested { get; private set; }
    public bool BattleInstantVictoryRequested { get; private set; }
    public bool BattleAdvanceTurnRequested { get; private set; }
    public ushort HeldFieldButtons { get; private set; }
    public string BindingHint(ControlAction action) => string.Join(" / ", Profile.GetBindings(action, ControlDevice.Keyboard).Concat(Profile.GetBindings(action, ControlDevice.Controller)).Select(chord => chord.DisplayName));

    public InputFrame Read(bool active) => Read(Keyboard.GetState(), GamePad.GetState(PlayerIndex.One, GamePadDeadZone.None), active);
    internal InputFrame Read(KeyboardState current, bool active) => Read(current, default, active);

    internal InputFrame Read(KeyboardState current, GamePadState pad, bool active)
    {
        if (!pad.IsConnected)
        {
            pad = default;
        }
        consumedKeys.RemoveWhere(current.IsKeyUp);
        consumedButtons.RemoveWhere(pad.IsButtonUp);
        disarmedKeys.RemoveWhere(current.IsKeyUp);
        disarmedButtons.RemoveWhere(pad.IsButtonUp);
        if (!active || !previousActive || profileChanged)
        {
            disarmedKeys.UnionWith(current.GetPressedKeys());
        }
        if (!active || !previousActive || profileChanged || pad.IsConnected && !previousConnected)
        {
            disarmedButtons.UnionWith(ControlsProfile.BindableButtons.Where(pad.IsButtonDown));
        }
        IReadOnlyList<(ControlAction Action, ControlDevice Device, InputChord Chord)> matches = Profile.Match(current, pad);
        foreach (var match in matches.Where(match => match.Chord.Count > 1))
        {
            consumedKeys.UnionWith(match.Chord.Keys);
            consumedButtons.UnionWith(match.Chord.Buttons);
        }
        HashSet<ControlAction> effective = matches.Where(match => !match.Chord.Keys.Any(disarmedKeys.Contains) && !match.Chord.Buttons.Any(disarmedButtons.Contains) && (match.Chord.Count > 1 || !match.Chord.Keys.Any(consumedKeys.Contains) && !match.Chord.Buttons.Any(consumedButtons.Contains))).Select(match => match.Action).ToHashSet();
        bool Down(ControlAction action) => active && effective.Contains(action);
        bool Pressed(ControlAction action) => Down(action) && !previous.Contains(action) && (previousConnected || !pad.IsConnected || Profile.GetBindings(action, ControlDevice.Keyboard).Any(chord => chord.Matches(current, default)));
        bool RecoveryPressed(Keys key) => active && RecoveryNavigationEnabled && !disarmedKeys.Contains(key) && current.IsKeyDown(key) && previousKeyboard.IsKeyUp(key);
        Vector2 movement = ApplyRadialDeadZone(pad.ThumbSticks.Left);
        Vector2 camera = ApplyRadialDeadZone(pad.ThumbSticks.Right);
        float horizontal = Down(ControlAction.MoveRight) || Down(ControlAction.MoveLeft) ? (Down(ControlAction.MoveRight) ? 1 : 0) - (Down(ControlAction.MoveLeft) ? 1 : 0) : active ? movement.X : 0;
        float vertical = Down(ControlAction.MoveDown) || Down(ControlAction.MoveUp) ? (Down(ControlAction.MoveDown) ? 1 : 0) - (Down(ControlAction.MoveUp) ? 1 : 0) : active ? -movement.Y : 0;
        int stickHorizontal = active ? (movement.X >= 0.5f && previousStick.X < 0.5f ? 1 : 0) - (movement.X <= -0.5f && previousStick.X > -0.5f ? 1 : 0) : 0;
        int stickVertical = active ? (movement.Y <= -0.5f && previousStick.Y > -0.5f ? 1 : 0) - (movement.Y >= 0.5f && previousStick.Y < 0.5f ? 1 : 0) : 0;
        SpeedToggleRequested = Pressed(ControlAction.SpeedToggle);
        DebugRoomRequested = Pressed(ControlAction.DebugRoom);
        BattleDebugRequested = Pressed(ControlAction.BattleDebug);
        BattleInstantVictoryRequested = Pressed(ControlAction.BattleInstantVictory);
        BattleAdvanceTurnRequested = Pressed(ControlAction.BattleAdvanceTurn);
        GameMenuRequested = Pressed(ControlAction.Menu);
        PauseRequested = Pressed(ControlAction.Pause);
        UiTabRequested = Pressed(ControlAction.UiTab);
        UiCharacterPage = (Pressed(ControlAction.RotateRight) ? 1 : 0) - (Pressed(ControlAction.RotateLeft) ? 1 : 0);
        UiDetailRequested = Pressed(ControlAction.Card);
        UiConfirmRequested = Pressed(ControlAction.Confirm);
        UiCancelRequested = Pressed(ControlAction.Cancel);
        HeldFieldButtons = 0;
        foreach ((ControlAction action, ushort mask) in NativeButtons)
        {
            if (Down(action))
            {
                HeldFieldButtons |= mask;
            }
        }
        bool confirm = UiConfirmRequested || RecoveryPressed(Keys.Enter);
        bool cancel = UiCancelRequested || RecoveryPressed(Keys.Escape);
        InputFrame input = new(horizontal, vertical, confirm, cancel, Pressed(ControlAction.SkipMovie), Pressed(ControlAction.QuickSave), (Pressed(ControlAction.MoveDown) || RecoveryPressed(Keys.Down) ? 1 : 0) - (Pressed(ControlAction.MoveUp) || RecoveryPressed(Keys.Up) ? 1 : 0) + stickVertical, Pressed(ControlAction.QuickLoad), Pressed(ControlAction.SaveMenu), Pressed(ControlAction.LoadMenu), (Pressed(ControlAction.MoveRight) || RecoveryPressed(Keys.Right) ? 1 : 0) - (Pressed(ControlAction.MoveLeft) || RecoveryPressed(Keys.Left) ? 1 : 0) + stickHorizontal, (Pressed(ControlAction.UiPageDown) ? 1 : 0) - (Pressed(ControlAction.UiPageUp) ? 1 : 0), Pressed(ControlAction.RecreateSave), CameraHorizontal: active ? camera.X != 0 ? camera.X : (Down(ControlAction.RotateRight) ? 1 : 0) - (Down(ControlAction.RotateLeft) ? 1 : 0) : 0);
        previous = effective;
        previousStick = movement;
        previousKeyboard = current;
        previousConnected = pad.IsConnected;
        previousActive = active;
        hasSampledInput = true;
        profileChanged = false;
        return input with { MenuVertical = Math.Sign(input.MenuVertical), MenuHorizontal = Math.Sign(input.MenuHorizontal) };
    }

    internal static Vector2 ApplyRadialDeadZone(Vector2 value)
    {
        const float deadZone = 0.2f;
        float length = value.Length();
        if (!float.IsFinite(length) || length <= deadZone)
        {
            return Vector2.Zero;
        }
        return value / length * Math.Clamp((length - deadZone) / (1 - deadZone), 0, 1);
    }

    private static readonly (ControlAction Action, ushort Mask)[] NativeButtons =
    [
        (ControlAction.MoveUp, 0x1000),
        (ControlAction.MoveRight, 0x2000),
        (ControlAction.MoveDown, 0x4000),
        (ControlAction.MoveLeft, 0x8000),
        (ControlAction.Confirm, 0x40),
        (ControlAction.Cancel, 0x10),
        (ControlAction.Menu, 0x20),
        (ControlAction.Card, 0x80),
        (ControlAction.CameraLeft, 0x01),
        (ControlAction.CameraRight, 0x02),
        (ControlAction.RotateLeft, 0x04),
        (ControlAction.RotateRight, 0x08),
        (ControlAction.Select, 0x100),
        (ControlAction.Pause, 0x800)
    ];
}
