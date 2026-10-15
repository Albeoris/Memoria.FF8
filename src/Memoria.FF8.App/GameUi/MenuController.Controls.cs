using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private Func<ControlsProfile>? _readControls;
    private Action<ControlsProfile>? _writeControls;
    private Func<ControllerPresentation>? _readControllerPresentation;
    private Func<int>? _readControllerGeneration;
    private Func<ControlDevice>? _readControlDevice;
    private ControllerPresentation _viewControllerPresentation;
    private ControlDevice _viewControlDevice;
    private int _captureControllerGeneration;
    private bool _captureControllerConnected;
    private ControlsProfile _demoControls = ControlsProfile.CreateDefault();
    private ControlDevice _controlsDevice;
    private ControlAction _controlsAction = ControlAction.Confirm;
    private InputChord? _pendingChord;
    private bool _captureControls;
    private bool _captureArmed;
    private bool _drainControls;
    private InputChord? _capturedChord;

    public bool IsCapturingControls => _captureControls;
    public bool IsControlsScreen => ScreenId.StartsWith("config-controller", StringComparison.Ordinal) || ScreenId.StartsWith("config-keyboard", StringComparison.Ordinal) || ScreenId.StartsWith("config-controls", StringComparison.Ordinal);
    private bool IsDemoControls => IsDemo && !UseHostControls;
    private ControlsProfile CurrentControls => IsDemoControls ? _demoControls : _readControls?.Invoke() ?? _demoControls;
    private bool CanWriteControls => IsDemoControls || _writeControls is not null;
    private string ControlsDeviceName => _controlsDevice == ControlDevice.Keyboard ? "Keyboard" : "Controller";
    private string ControlsActionName => ControlsLabel(_controlsAction);
    private ControllerPresentation CurrentControllerPresentation => CurrentControls.Controller.Presentation == ControllerPresentation.Automatic ? _readControllerPresentation?.Invoke() ?? ControllerPresentation.Generic : CurrentControls.Controller.Presentation;
    private ControlDevice CurrentControlDevice => _readControlDevice?.Invoke() ?? (ScreenId.StartsWith("config-controller", StringComparison.Ordinal) ? ControlDevice.Controller : ControlDevice.Keyboard);
    private string ControlsMapping => BindingText(_controlsAction, _controlsDevice);

    internal void ConnectControls(Func<ControlsProfile> read, Action<ControlsProfile> write, Func<ControllerPresentation>? presentation = null, Func<int>? generation = null, Func<ControlDevice>? inputDevice = null)
    {
        _readControls = read;
        _writeControls = write;
        _readControllerPresentation = presentation;
        _readControllerGeneration = generation;
        _readControlDevice = inputDevice;
        _view = null;
    }

    internal void RegisterControlsScreens()
    {
        Register(new MenuScreenDefinition
        {
            Id = "config-keyboard",
            Title = "Keyboard",
            Help = "Select an action to change its key or key combination.",
            Entries = c => c.ControlsEntries(ControlDevice.Keyboard),
            Details = c => c.ControlsListDetails(ControlDevice.Keyboard)
        });
        Register(new MenuScreenDefinition
        {
            Id = "config-controller",
            Title = "Controller",
            Help = "Select an action to change its button or combination.",
            Entries = c => c.ControlsEntries(ControlDevice.Controller),
            Details = c => c.ControlsListDetails(ControlDevice.Controller)
        });
        Register(new MenuScreenDefinition
        {
            Id = "config-controller-settings",
            Title = "Controller settings",
            Help = "Confirm cycles each setting. Changes save automatically.",
            Entries = c => c.ControllerSettingsEntries(),
            Details = c => ["Deadzones remove stick drift.", "Run threshold uses the adjusted stick.", "A lighter tilt selects native walking.", "Right stick steers / throttles the car.", "Button labels follow the connected pad.", "Choose labels manually for virtual pads.", "Confirm cycles the selected setting."]
        });
        foreach (string id in new[] { "config-keyboard-choice", "config-controller-choice" })
        {
            Register(new MenuScreenDefinition
            {
                Id = id,
                Title = "Change binding",
                Help = "Capture a button or combination, or remove the current binding.",
                Entries = c => c.ControlsChoiceEntries(),
                Details = c => c.ControlsChoiceDetails()
            });
        }
        Register(new MenuScreenDefinition
        {
            Id = "config-controls-conflict",
            Title = "Binding in use",
            Help = "Replacing removes this same combination from the other actions.",
            Entries = _ => [new("keep", "Keep current bindings", "Discard the proposed binding.", Action: new("controls-keep")), new("replace", "Replace conflicting bindings", "Assign this combination and remove its conflicting bindings.", Action: new("controls-replace"))],
            Details = c => c.ControlsConflictDetails()
        });
        Register(new MenuScreenDefinition
        {
            Id = "config-controls-reset",
            Title = "Restore defaults",
            Help = "Restore every binding and host preference for the selected device.",
            Entries = _ => [new("keep", "Keep current bindings", "Return without changing bindings.", Action: new("menu-back")), new("reset", "Restore device defaults", "Restore every native and extended action for this device.", Action: new("controls-reset-confirm"))],
            Details = c => [$"Device: {c.ControlsDeviceName}", "Device bindings and preferences reset.", "The other device keeps its bindings.", c.IsDemoControls ? "Changes stay in this menu demo." : "Changes save automatically."]
        });
    }

    private IReadOnlyList<MenuEntry> ControlsEntries(ControlDevice device)
    {
        string destination = device == ControlDevice.Keyboard ? "config-keyboard-choice" : "config-controller-choice";
        return [.. device == ControlDevice.Controller ? new[] { new MenuEntry("settings", "Stick / device settings...", "Set deadzones, run threshold, button labels and vibration.", "config-controller-settings") } : [], .. ControlsProfile.Definitions.Select(definition => new MenuEntry(definition.Action.ToString(), ControlsLabel(definition.Action), $"{definition.Category}: {BindingText(definition.Action, device)}", destination, new("controls-select", definition.Action.ToString(), Value: device.ToString()))), new("reset", "Restore device defaults...", "Reset all native and extended actions for this device.", "config-controls-reset", new("controls-device", device.ToString()))];
    }

    private IReadOnlyList<string> ControlsListDetails(ControlDevice device)
    {
        if (SelectedEntry?.Id == "settings")
        {
            return ["Controller settings", "Adjust left and right radial deadzones.", "Choose the tilt needed to run.", "Right stick: camera / car controls.", "Choose button labels for virtual pads.", "Enable or disable controller vibration.", "Changes save automatically."];
        }
        ControlActionDefinition? selected = ControlsProfile.Definitions.FirstOrDefault(definition => definition.Action.ToString() == SelectedEntry?.Id);
        List<string> details = [selected is null ? "Restore defaults" : ControlsLabel(selected.Action), selected?.Category ?? "All actions", selected is null ? "" : BindingText(selected.Action, device), "Confirm: change binding", "Key / button combinations supported.", device == ControlDevice.Controller ? "Left stick: movement" : "", device == ControlDevice.Controller ? "Right stick: camera / car controls" : "", device == ControlDevice.Controller ? "Stick directions cannot bind actions." : "", IsDemoControls ? "Menu demo: changes stay isolated." : CanWriteControls ? "Changes save automatically." : "Controls are not connected to this host."];
        return details.Where(line => line.Length != 0).ToArray();
    }

    private IReadOnlyList<MenuEntry> ControlsChoiceEntries()
    {
        if (_captureControls)
        {
            return [new("cancel-capture", "Cancel capture", "Keep the current binding.", Action: new("controls-cancel"))];
        }
        return [new("capture", "Capture key / button combination", "Release controls, hold the new combination, then release to apply.", Action: new("controls-capture"), Enabled: CanWriteControls), .. _controlsDevice == ControlDevice.Keyboard ? new[] { new MenuEntry("escape", "Bind Escape", "Assign Escape; Escape cancels during capture.", Action: new("controls-escape"), Enabled: CanWriteControls) } : [], new("clear", "Clear binding", "Remove this action's binding for this device.", Action: new("controls-clear"), Enabled: CanWriteControls && _controlsAction is not ControlAction.Confirm and not ControlAction.Cancel), new("back", "Back", "Return to the action list.", Action: new("menu-back"))];
    }

    private IReadOnlyList<string> ControlsChoiceDetails()
    {
        if (_captureControls)
        {
            return [ControlsActionName, _captureArmed ? "Press the new key / buttons." : "Release all keys and buttons first.", "Hold combination buttons together.", "Release all to finish capture.", _capturedChord?.FormatDisplayName(CurrentControllerPresentation) ?? "Waiting for input...", "Escape or click Cancel to cancel.", "Gameplay input is paused."];
        }
        return [ControlsActionName, $"Device: {ControlsDeviceName}", $"Current: {ControlsMapping}", "Capture replaces this action's bindings.", "Hold combination buttons together.", "Confirm and Cancel must stay bound.", IsDemoControls ? "Changes stay in this menu demo." : "Changes save automatically."];
    }

    private IReadOnlyList<string> ControlsConflictDetails()
    {
        return [ControlsActionName, $"New: {_pendingChord?.FormatDisplayName(CurrentControllerPresentation)}", "Already assigned to:", .. _pendingChord is null ? [] : CurrentControls.FindConflicts(_controlsAction, _controlsDevice, _pendingChord).Select(action => ControlsProfile.Definitions.Single(definition => definition.Action == action).Label), "Keep current bindings to cancel."];
    }

    private string BindingText(ControlAction action, ControlDevice device)
    {
        IReadOnlyList<InputChord> bindings = CurrentControls.GetBindings(action, device);
        return bindings.Count == 0 ? "Unbound" : string.Join(" / ", bindings.Select(binding => binding.FormatDisplayName(CurrentControllerPresentation)));
    }

    private string CurrentBindingText(ControlAction action)
    {
        ControlDevice device = CurrentControls.GetBindings(action, CurrentControlDevice).Count != 0 ? CurrentControlDevice : CurrentControlDevice == ControlDevice.Keyboard ? ControlDevice.Controller : ControlDevice.Keyboard;
        return BindingText(action, device);
    }

    private static string ControlsLabel(ControlAction action)
    {
        return action switch
        {
            ControlAction.RotateLeft => "Rotate left / Previous character",
            ControlAction.RotateRight => "Rotate right / Gunblade / Next",
            ControlAction.CameraRight => "Camera mode / Escape battle (R)",
            _ => ControlsProfile.Definitions.Single(definition => definition.Action == action).Label
        };
    }

    private bool ExecuteControlsAction(MenuAction action)
    {
        if (_captureControls && action.Kind != "controls-cancel")
        {
            return true;
        }
        switch (action.Kind)
        {
            case "controls-setting":
                WriteControls(() => CycleControllerSetting(action.SubjectId));
                return true;
            case "controls-select":
                _controlsAction = Enum.Parse<ControlAction>(action.SubjectId);
                _controlsDevice = Enum.Parse<ControlDevice>(action.Value);
                return true;
            case "controls-device":
                _controlsDevice = Enum.Parse<ControlDevice>(action.SubjectId);
                _selections["config-controls-reset"] = 0;
                return true;
            case "controls-capture":
                if (CanWriteControls)
                {
                    _captureControls = true;
                    _captureArmed = false;
                    _drainControls = false;
                    _capturedChord = null;
                    _captureControllerConnected = false;
                    _captureControllerGeneration = _readControllerGeneration?.Invoke() ?? 0;
                    Message = "Release controls, then press your new combination.";
                }
                return true;
            case "controls-cancel":
                CancelControlsCapture();
                return true;
            case "controls-escape":
                ProposeControlsBinding(new([Keys.Escape], []));
                return true;
            case "controls-clear":
                WriteControls(() => CurrentControls.WithoutBinding(_controlsAction, _controlsDevice));
                return true;
            case "controls-keep":
                _pendingChord = null;
                Back();
                return true;
            case "controls-replace":
                if (_pendingChord is not null && WriteControls(() => CurrentControls.WithBinding(_controlsAction, _controlsDevice, _pendingChord, replaceConflicts: true)))
                {
                    _pendingChord = null;
                    Back();
                    Message = "Binding saved.";
                }
                return true;
            case "controls-reset-confirm":
                if (WriteControls(() => CurrentControls.Reset(_controlsDevice)))
                {
                    Back();
                    Message = $"{ControlsDeviceName} defaults restored.";
                }
                return true;
            default:
                return false;
        }
    }

    private IReadOnlyList<MenuEntry> ControllerSettingsEntries()
    {
        ControllerSettings settings = CurrentControls.Controller;
        return
        [
            new("left-deadzone", $"Left stick deadzone: {settings.LeftStickDeadZone:P0}", "Confirm increases by 5%; after 50% returns to 0%.", Action: new("controls-setting", "left-deadzone"), Enabled: CanWriteControls),
            new("right-deadzone", $"Right stick deadzone: {settings.RightStickDeadZone:P0}", "Confirm increases by 5%; after 50% returns to 0%.", Action: new("controls-setting", "right-deadzone"), Enabled: CanWriteControls),
            new("run-threshold", $"Run threshold: {settings.WalkRunThreshold:P0}", "Confirm increases by 5%; after 95% returns to 10%.", Action: new("controls-setting", "run-threshold"), Enabled: CanWriteControls),
            new("presentation", $"Button labels: {settings.Presentation}", "Cycle automatic, generic, Xbox, PlayStation and Nintendo labels.", Action: new("controls-setting", "presentation"), Enabled: CanWriteControls),
            new("vibration", $"Vibration: {(settings.VibrationEnabled ? "On" : "Off")}", "Enable or disable controller vibration.", Action: new("controls-setting", "vibration"), Enabled: CanWriteControls)
        ];
    }

    private ControlsProfile CycleControllerSetting(string setting)
    {
        ControllerSettings current = CurrentControls.Controller;
        float Next(float value, float minimum, float maximum) => value >= maximum - 0.001f ? minimum : MathF.Round(value + 0.05f, 2);
        return CurrentControls.WithControllerSettings(
            setting == "left-deadzone" ? Next(current.LeftStickDeadZone, 0, 0.5f) : current.LeftStickDeadZone,
            setting == "right-deadzone" ? Next(current.RightStickDeadZone, 0, 0.5f) : current.RightStickDeadZone,
            setting == "run-threshold" ? Next(current.WalkRunThreshold, 0.1f, 0.95f) : current.WalkRunThreshold,
            setting == "presentation" ? (ControllerPresentation)(((int)current.Presentation + 1) % Enum.GetValues<ControllerPresentation>().Length) : current.Presentation,
            setting == "vibration" ? !current.VibrationEnabled : current.VibrationEnabled);
    }

    private bool WriteControls(Func<ControlsProfile> update)
    {
        if (!CanWriteControls)
        {
            Message = "Controls are not connected to this host.";
            return false;
        }
        try
        {
            ControlsProfile next = update();
            if (IsDemoControls)
            {
                _demoControls = next;
            }
            else
            {
                _writeControls!(next);
            }
            Message = IsDemoControls ? "Demo binding updated." : "Binding saved.";
            _view = null;
            return true;
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            Message = error.Message;
            _view = null;
            return false;
        }
    }

    private void ProposeControlsBinding(InputChord chord)
    {
        try
        {
            if (CurrentControls.FindConflicts(_controlsAction, _controlsDevice, chord).Count != 0)
            {
                _pendingChord = chord;
                _selections["config-controls-conflict"] = 0;
                Push("config-controls-conflict");
                return;
            }
            WriteControls(() => CurrentControls.WithBinding(_controlsAction, _controlsDevice, chord));
        }
        catch (ArgumentException error)
        {
            Message = error.Message;
            _view = null;
        }
    }

    private void CancelControlsCapture()
    {
        if (!_captureControls)
        {
            return;
        }
        _captureControls = false;
        _capturedChord = null;
        _drainControls = true;
        Message = "Capture cancelled; binding unchanged.";
        _view = null;
    }

    internal bool UpdateControlsCapture(KeyboardState keys, GamePadState pad, bool active)
    {
        if (!_captureControls && !_drainControls)
        {
            return false;
        }
        Keys[] heldKeys = keys.GetPressedKeys();
        Buttons[] heldButtons = ControlsProfile.BindableButtons.Where(pad.IsButtonDown).ToArray();
        bool released = heldKeys.Length == 0 && heldButtons.Length == 0;
        if (_drainControls)
        {
            _drainControls = !released;
            return true;
        }
        bool deviceChanged = (_readControllerGeneration?.Invoke() ?? 0) != _captureControllerGeneration;
        if (!active || _controlsDevice == ControlDevice.Controller && (_captureControllerConnected && !pad.IsConnected || deviceChanged))
        {
            CancelControlsCapture();
            return true;
        }
        _captureControllerConnected |= pad.IsConnected;
        if (!_captureArmed)
        {
            _captureArmed = released;
            Message = released ? "Press a key or combination, then release all to apply." : "Release all keys and buttons first.";
            _view = null;
            return true;
        }
        if (keys.IsKeyDown(Keys.Escape))
        {
            CancelControlsCapture();
            return true;
        }
        InputChord held = _controlsDevice == ControlDevice.Keyboard ? new(heldKeys, []) : new([], heldButtons);
        int count = held.Keys.Length + held.Buttons.Length;
        if (count > (_capturedChord?.Keys.Length ?? 0) + (_capturedChord?.Buttons.Length ?? 0))
        {
            _capturedChord = held;
            Message = "Release all controls to apply this combination.";
            _view = null;
        }
        if (released && _capturedChord is not null)
        {
            InputChord candidate = _capturedChord;
            _captureControls = false;
            _capturedChord = null;
            _drainControls = true;
            ProposeControlsBinding(candidate);
            _view = null;
        }
        return true;
    }
}
