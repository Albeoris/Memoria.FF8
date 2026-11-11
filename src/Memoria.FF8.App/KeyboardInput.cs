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
    private bool leftStickDisarmed;
    private bool rightStickDisarmed;
    private int previousGeneration;
    private int menuHorizontal;
    private int menuVertical;
    private TimeSpan nextMenuRepeat;
    private ControllerPresentation connectedPresentation = ControllerPresentation.Generic;
    private KeyboardState previousKeyboard;
    private GamePadState previousPad;
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
    public int ControllerGeneration { get; set; }
    public ControllerPresentation ControllerPresentation
    {
        get => Profile.Controller.Presentation == ControllerPresentation.Automatic ? connectedPresentation : Profile.Controller.Presentation;
        set => connectedPresentation = value == ControllerPresentation.Automatic ? ControllerPresentation.Generic : value;
    }
    private bool nativeButtonsOwnInput;
    public bool NativeButtonsOwnInput
    {
        get => nativeButtonsOwnInput;
        set
        {
            if (nativeButtonsOwnInput && !value)
            {
                SuppressHeldUiInput();
            }
            nativeButtonsOwnInput = value;
        }
    }
    public bool RecoveryNavigationEnabled { get; set; }
    public bool NamingNavigationEnabled { get; set; }
    public bool TextEntryEnabled { get; set; }
    public bool GuardianEditorEnabled { get; set; }
    public bool ModsManagerEnabled { get; set; }
    public bool SpeedToggleRequested { get; private set; }
    public bool DebugRoomRequested { get; private set; }
    public bool GameMenuRequested { get; private set; }
    public bool PauseRequested { get; private set; }
    public bool UiTabRequested { get; private set; }
    public bool UiReverseTabRequested { get; private set; }
    public bool UiSwitchColumnRequested { get; private set; }
    public int UiPaneFocus { get; private set; }
    public int UiCharacterPage { get; private set; }
    public bool UiDetailRequested { get; private set; }
    public bool UiConfirmRequested { get; private set; }
    public bool UiCancelRequested { get; private set; }
    public bool CancelHeld { get; private set; }
    public string CancelBindingHint { get; private set; } = "";
    public bool BattleDebugRequested { get; private set; }
    public bool BattleInstantVictoryRequested { get; private set; }
    public bool BattleInstantDefeatRequested { get; private set; }
    public bool BattleAdvanceTurnRequested { get; private set; }
    public bool GunbladeTriggerRequested { get; private set; }
    public bool GuardianBoostRequested { get; private set; }
    public bool GuardianFormSkipRequested { get; private set; }
    public ushort HeldFieldButtons { get; private set; }
    public ushort HeldControlButtons { get; private set; }

    public string NativeButtonBindingHint(ushort buttons)
    {
        return string.Join("+", NativeButtons.Where(binding => (buttons & binding.Mask) != 0).Select(binding => BindingHint(binding.Action)));
    }

    public string NativeButtonBindingPrompt(ushort buttons)
    {
        return string.Join("+", NativeButtons.Where(binding => (buttons & binding.Mask) != 0).Select(binding => BindingPrompt(binding.Action)));
    }

    internal Func<ushort, string> CaptureNativeButtonBindingPrompt()
    {
        var bindings = NativeButtons.Select(binding => (binding.Mask, Text: BindingPrompt(binding.Action))).ToArray();
        return buttons => string.Join("+", bindings.Where(binding => (buttons & binding.Mask) != 0).Select(binding => binding.Text));
    }

    internal static ControlAction NativeButtonAction(ushort button)
    {
        return NativeButtons.Single(binding => binding.Mask == button).Action;
    }
    public ControlDevice LastInputDevice { get; private set; }
    internal string BindingPrompt(ControlAction action) => BindingHint(action, true);

    public string BindingHint(ControlAction action) => BindingHint(action, false);

    private string BindingHint(ControlAction action, bool prompt)
    {
        IReadOnlyList<InputChord> bindings = Profile.GetBindings(action, LastInputDevice);
        if (bindings.Count == 0)
        {
            bindings = ControlsProfile.GetPermanentBindings(action, LastInputDevice);
        }
        if (bindings.Count == 0 && LastInputDevice == ControlDevice.Controller)
        {
            bindings = Profile.GetBindings(action, ControlDevice.Keyboard);
            if (bindings.Count == 0)
            {
                bindings = ControlsProfile.GetPermanentBindings(action, ControlDevice.Keyboard);
            }
        }
        return bindings.Count == 0 ? "Unbound" : string.Join(" / ", bindings.Select(chord => prompt ? chord.FormatPrompt(ControllerPresentation) : chord.FormatDisplayName(ControllerPresentation)));
    }

    internal InputFrame Read(KeyboardState current, bool active) => Read(current, default, active);

    internal void SuppressHeldNameInput()
    {
        disarmedKeys.UnionWith(previousKeyboard.GetPressedKeys());
    }

    internal void SuppressHeldUiInput()
    {
        disarmedKeys.UnionWith(previousKeyboard.GetPressedKeys());
        disarmedButtons.UnionWith(ControlsProfile.BindableButtons.Where(previousPad.IsButtonDown));
        leftStickDisarmed = true;
        rightStickDisarmed = true;
    }

    internal bool AllowsNameText(Keys key) => !disarmedKeys.Contains(key) && !IsArrowKey(key);

    internal InputFrame Read(KeyboardState current, GamePadState pad, bool active)
        => Read(current, pad, active, System.Diagnostics.Stopwatch.GetElapsedTime(0));

    internal InputFrame Read(KeyboardState current, GamePadState pad, bool active, TimeSpan sampleTime)
    {
        if (!pad.IsConnected)
        {
            pad = default;
        }
        consumedKeys.RemoveWhere(current.IsKeyUp);
        consumedButtons.RemoveWhere(pad.IsButtonUp);
        disarmedKeys.RemoveWhere(current.IsKeyUp);
        disarmedButtons.RemoveWhere(pad.IsButtonUp);
        bool deviceChanged = ControllerGeneration != previousGeneration;
        if (!active || !previousActive || profileChanged)
        {
            disarmedKeys.UnionWith(current.GetPressedKeys());
        }
        if (!active || !previousActive || profileChanged || deviceChanged || pad.IsConnected && !previousConnected)
        {
            disarmedButtons.UnionWith(ControlsProfile.BindableButtons.Where(pad.IsButtonDown));
        }
        IReadOnlyList<(ControlAction Action, ControlDevice Device, InputChord Chord)> matches = Profile.Match(current, pad, GuardianEditorEnabled && !TextEntryEnabled);
        if (TextEntryEnabled)
        {
            matches = matches.Where(match => match.Device == ControlDevice.Controller).ToArray();
        }
        else if (NamingNavigationEnabled)
        {
            matches = matches.Where(match => match.Device == ControlDevice.Controller || IsNamingNavigationAction(match.Action) && !match.Chord.Keys.Any(IsArrowKey)).ToArray();
        }
        foreach (var match in matches.Where(match => match.Chord.Count > 1))
        {
            consumedKeys.UnionWith(match.Chord.Keys);
            consumedButtons.UnionWith(match.Chord.Buttons);
        }
        var effectiveMatches = matches.Where(match => !match.Chord.Keys.Any(disarmedKeys.Contains) && !match.Chord.Buttons.Any(disarmedButtons.Contains) && (match.Chord.Count > 1 || ControlsProfile.IsPermanentBinding(match.Action, match.Device, match.Chord) || !match.Chord.Keys.Any(consumedKeys.Contains) && !match.Chord.Buttons.Any(consumedButtons.Contains))).ToArray();
        HashSet<ControlAction> effective = effectiveMatches.Select(match => match.Action).ToHashSet();
        bool Down(ControlAction action) => active && effective.Contains(action);
        bool Pressed(ControlAction action) => !NativeButtonsOwnInput && Down(action) && !previous.Contains(action) && (previousConnected || !pad.IsConnected || effectiveMatches.Any(match => match.Action == action && match.Device == ControlDevice.Keyboard));
        bool RecoveryPressed(Keys key) => !NativeButtonsOwnInput && active && (RecoveryNavigationEnabled || NamingNavigationEnabled || TextEntryEnabled || ModsManagerEnabled) && !disarmedKeys.Contains(key) && current.IsKeyDown(key) && previousKeyboard.IsKeyUp(key);
        Vector2 movement = ApplyRadialDeadZone(pad.ThumbSticks.Left, Profile.LeftStickDeadZone);
        Vector2 camera = ApplyRadialDeadZone(pad.ThumbSticks.Right, Profile.RightStickDeadZone);
        if (!active || !previousActive || profileChanged || deviceChanged || hasSampledInput && pad.IsConnected && !previousConnected)
        {
            leftStickDisarmed |= movement != Vector2.Zero;
            rightStickDisarmed |= camera != Vector2.Zero;
        }
        leftStickDisarmed &= movement != Vector2.Zero;
        rightStickDisarmed &= camera != Vector2.Zero;
        if (leftStickDisarmed)
        {
            movement = Vector2.Zero;
        }
        if (rightStickDisarmed)
        {
            camera = Vector2.Zero;
        }
        bool leftStickEngaged = movement != Vector2.Zero && ApplyRadialDeadZone(previousPad.ThumbSticks.Left, Profile.LeftStickDeadZone) == Vector2.Zero;
        bool rightStickEngaged = camera != Vector2.Zero && ApplyRadialDeadZone(previousPad.ThumbSticks.Right, Profile.RightStickDeadZone) == Vector2.Zero;
        if (!pad.IsConnected)
        {
            LastInputDevice = ControlDevice.Keyboard;
        }
        if (active && current.GetPressedKeys().Any(key => previousKeyboard.IsKeyUp(key) && !disarmedKeys.Contains(key)))
        {
            LastInputDevice = ControlDevice.Keyboard;
        }
        else if (active && (leftStickEngaged || rightStickEngaged || ControlsProfile.BindableButtons.Any(button => pad.IsButtonDown(button) && previousPad.IsButtonUp(button) && !disarmedButtons.Contains(button))))
        {
            LastInputDevice = ControlDevice.Controller;
        }
        float horizontal = Down(ControlAction.MoveRight) || Down(ControlAction.MoveLeft) ? (Down(ControlAction.MoveRight) ? 1 : 0) - (Down(ControlAction.MoveLeft) ? 1 : 0) : active ? movement.X : 0;
        float vertical = Down(ControlAction.MoveDown) || Down(ControlAction.MoveUp) ? (Down(ControlAction.MoveDown) ? 1 : 0) - (Down(ControlAction.MoveUp) ? 1 : 0) : active ? -movement.Y : 0;
        (int stickHorizontal, int stickVertical) = ReadStickNavigation(movement, active, sampleTime);
        SpeedToggleRequested = Pressed(ControlAction.SpeedToggle);
        DebugRoomRequested = Pressed(ControlAction.DebugRoom);
        BattleDebugRequested = Pressed(ControlAction.BattleDebug);
        BattleInstantVictoryRequested = Pressed(ControlAction.BattleInstantVictory);
        BattleInstantDefeatRequested = Pressed(ControlAction.BattleInstantDefeat);
        BattleAdvanceTurnRequested = Pressed(ControlAction.BattleAdvanceTurn);
        GunbladeTriggerRequested = Pressed(ControlAction.RotateRight);
        GuardianBoostRequested = Pressed(ControlAction.Card);
        GuardianFormSkipRequested = Pressed(ControlAction.CameraLeft);
        GameMenuRequested = Pressed(ControlAction.Menu);
        PauseRequested = Pressed(ControlAction.Pause);
        UiSwitchColumnRequested = GuardianEditorEnabled && !TextEntryEnabled && Pressed(ControlAction.GuardianSwitchColumn);
        bool switchColumnHeld = GuardianEditorEnabled && !TextEntryEnabled && Down(ControlAction.GuardianSwitchColumn);
        bool tabRequested = !switchColumnHeld && (Pressed(ControlAction.UiTab) || RecoveryPressed(Keys.Tab));
        UiReverseTabRequested = (GuardianEditorEnabled || ModsManagerEnabled) && tabRequested && (current.IsKeyDown(Keys.LeftShift) || current.IsKeyDown(Keys.RightShift));
        UiTabRequested = tabRequested && !UiReverseTabRequested;
        UiPaneFocus = Pressed(ControlAction.CameraLeft) ? -1 : Pressed(ControlAction.CameraRight) ? 1 : 0;
        UiCharacterPage = (Pressed(ControlAction.RotateRight) ? 1 : 0) - (Pressed(ControlAction.RotateLeft) ? 1 : 0);
        UiDetailRequested = Pressed(ControlAction.Card);
        UiConfirmRequested = Pressed(ControlAction.Confirm);
        UiCancelRequested = Pressed(ControlAction.Cancel);
        CancelHeld = !NativeButtonsOwnInput && Down(ControlAction.Cancel);
        if (CancelHeld)
        {
            CancelBindingHint = effectiveMatches.First(match => match.Action == ControlAction.Cancel).Chord.FormatDisplayName(ControllerPresentation);
        }
        HeldFieldButtons = 0;
        foreach ((ControlAction action, ushort mask) in NativeButtons)
        {
            if (Down(action))
            {
                HeldFieldButtons |= mask;
            }
        }
        HeldControlButtons = HeldFieldButtons;
        bool digitalMovement = Down(ControlAction.MoveUp) || Down(ControlAction.MoveDown) || Down(ControlAction.MoveLeft) || Down(ControlAction.MoveRight);
        if (active && !digitalMovement && movement.LengthSquared() > 0 && movement.Length() < Profile.WalkRunThreshold)
        {
            HeldFieldButtons |= 0x10;
        }
        bool confirm = UiConfirmRequested || RecoveryPressed(Keys.Enter);
        bool cancel = UiCancelRequested || RecoveryPressed(Keys.Escape);
        InputFrame input = new(horizontal, vertical, confirm, cancel, false, Pressed(ControlAction.QuickSave), (Pressed(ControlAction.MoveDown) || RecoveryPressed(Keys.Down) ? 1 : 0) - (Pressed(ControlAction.MoveUp) || RecoveryPressed(Keys.Up) ? 1 : 0) + stickVertical, Pressed(ControlAction.QuickLoad), Pressed(ControlAction.SaveMenu), Pressed(ControlAction.LoadMenu), (Pressed(ControlAction.MoveRight) || RecoveryPressed(Keys.Right) ? 1 : 0) - (Pressed(ControlAction.MoveLeft) || RecoveryPressed(Keys.Left) ? 1 : 0) + stickHorizontal, (Pressed(ControlAction.UiPageDown) || RecoveryPressed(Keys.PageDown) ? 1 : 0) - (Pressed(ControlAction.UiPageUp) || RecoveryPressed(Keys.PageUp) ? 1 : 0), CameraHorizontal: active ? camera.X != 0 ? camera.X : (Down(ControlAction.RotateRight) ? 1 : 0) - (Down(ControlAction.RotateLeft) ? 1 : 0) : 0, SecondaryVertical: active ? -camera.Y : 0, SecondaryHorizontal: active ? camera.X : 0);
        previous = effective;
        previousGeneration = ControllerGeneration;
        previousKeyboard = current;
        previousPad = pad;
        previousConnected = pad.IsConnected;
        previousActive = active;
        hasSampledInput = true;
        profileChanged = false;
        return NativeButtonsOwnInput ? default : input with { MenuVertical = Math.Sign(input.MenuVertical), MenuHorizontal = Math.Sign(input.MenuHorizontal), CardChallenge = UiDetailRequested };
    }

    private static bool IsArrowKey(Keys key) => key is Keys.Up or Keys.Down or Keys.Left or Keys.Right;

    private static bool IsNamingNavigationAction(ControlAction action) => action is ControlAction.MoveUp or ControlAction.MoveDown or ControlAction.MoveLeft or ControlAction.MoveRight or ControlAction.Confirm or ControlAction.Cancel or ControlAction.RotateLeft or ControlAction.RotateRight or ControlAction.UiTab or ControlAction.UiPageUp or ControlAction.UiPageDown;

    private (int Horizontal, int Vertical) ReadStickNavigation(Vector2 movement, bool active, TimeSpan sampleTime)
    {
        int horizontal = 0;
        int vertical = 0;
        if (active)
        {
            float threshold = menuHorizontal != 0 || menuVertical != 0 ? 0.35f : 0.5f;
            bool keepHorizontal = menuHorizontal != 0 && Math.Abs(movement.X) >= threshold && Math.Abs(movement.X) + 0.15f >= Math.Abs(movement.Y);
            bool keepVertical = menuVertical != 0 && Math.Abs(movement.Y) >= threshold && Math.Abs(movement.Y) + 0.15f >= Math.Abs(movement.X);
            if (keepHorizontal || !keepVertical && Math.Abs(movement.X) > Math.Abs(movement.Y))
            {
                horizontal = Math.Abs(movement.X) >= threshold ? Math.Sign(movement.X) : 0;
            }
            else
            {
                vertical = Math.Abs(movement.Y) >= threshold ? -Math.Sign(movement.Y) : 0;
            }
        }
        bool changed = horizontal != menuHorizontal || vertical != menuVertical;
        menuHorizontal = horizontal;
        menuVertical = vertical;
        if (horizontal == 0 && vertical == 0)
        {
            nextMenuRepeat = TimeSpan.Zero;
            return (0, 0);
        }
        if (changed)
        {
            nextMenuRepeat = sampleTime + TimeSpan.FromMilliseconds(350);
            return (horizontal, vertical);
        }
        if (sampleTime >= nextMenuRepeat)
        {
            nextMenuRepeat = sampleTime + TimeSpan.FromMilliseconds(100);
            return (horizontal, vertical);
        }
        return (0, 0);
    }

    internal static Vector2 ApplyRadialDeadZone(Vector2 value, float deadZone = 0.2f)
    {
        if (!float.IsFinite(deadZone) || deadZone < 0 || deadZone > 0.5f)
        {
            throw new ArgumentOutOfRangeException(nameof(deadZone));
        }
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
