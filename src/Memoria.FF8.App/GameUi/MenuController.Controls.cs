using Microsoft.Xna.Framework.Input;
using Memoria.FF8.Ui;

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
    private bool _developerControls;
    private int _controlsGroup;
    private bool _pendingBindingsReset;
    private static readonly string[] ControlsGroups = ["Navigation", "Interaction", "Camera and battle", "Shortcuts"];
    private static readonly ControlAction[][] ControlsGroupActions =
    [
        [ControlAction.MoveUp, ControlAction.MoveDown, ControlAction.MoveLeft, ControlAction.MoveRight, ControlAction.UiTab, ControlAction.UiPageUp, ControlAction.UiPageDown],
        [ControlAction.Confirm, ControlAction.Cancel, ControlAction.Menu, ControlAction.Card, ControlAction.Select, ControlAction.Pause],
        [ControlAction.CameraLeft, ControlAction.CameraRight, ControlAction.RotateLeft, ControlAction.RotateRight, ControlAction.BattleAdvanceTurn],
        [ControlAction.SpeedToggle, ControlAction.QuickSave, ControlAction.QuickLoad, ControlAction.SaveMenu, ControlAction.LoadMenu]
    ];
    private readonly Dictionary<int, string> _controlsGroupSelection = [];
    private string ControlsOwnerName => _developerControls ? "Debug shortcuts" : "Player bindings";
    private InputChord? _pendingChord;
    private bool _captureControls;
    private bool _returnToControlsList;
    private bool _captureArmed;
    private bool _drainControls;
    private InputChord? _capturedChord;

    public bool IsCapturingControls => _captureControls;
    public bool IsControlsScreen => ScreenId == "debug-shortcuts" || ScreenId.StartsWith("config-controller", StringComparison.Ordinal) || ScreenId.StartsWith("config-keyboard", StringComparison.Ordinal) || ScreenId.StartsWith("config-controls", StringComparison.Ordinal);
    private bool IsDemoControls => IsFixtureContext;
    private ControlsProfile CurrentControls => IsDemoControls ? _demoControls : _readControls?.Invoke() ?? _demoControls;
    private bool CanWriteControls => IsDemoControls || _writeControls is not null;
    private string ControlsDeviceName => _controlsDevice == ControlDevice.Keyboard ? "Keyboard" : "Controller";
    private string ControlsActionName => ControlsLabel(_controlsAction);
    private ControllerPresentation CurrentControllerPresentation => CurrentControls.Controller.Presentation == ControllerPresentation.Automatic ? _readControllerPresentation?.Invoke() ?? ControllerPresentation.Generic : CurrentControls.Controller.Presentation;
    private ControlDevice CurrentControlDevice => _readControlDevice?.Invoke() ?? (ScreenId.StartsWith("config-controller", StringComparison.Ordinal) ? ControlDevice.Controller : ControlDevice.Keyboard);
    private string ControlsMapping => BindingText(_controlsAction, _controlsDevice);
    private bool IsControlsBindingList => ScreenId is "config-keyboard" or "config-controller" or "debug-shortcuts";
    private string ControlsChoiceScreen => _controlsDevice == ControlDevice.Keyboard ? "config-keyboard-choice" : "config-controller-choice";

    private UiElement BuildControlsPage()
    {
        var page = Grid([1.8f * Math.Max(1, TextScale), 5.8f], [1], 5);
        ControlDevice device = _controlsDevice;
        string advancedBinding = ControlsBindingText(ControlAction.Card, device);
        string advancedHelp = advancedBinding == "Unbound" ? "Card / Details is unbound. Assign it to open advanced options." : $"Press {advancedBinding} for advanced options.";
        advancedHelp = $"{(IsDemoControls ? "Preview / Fixture" : "Global preferences")}. {advancedHelp}";
        string help = IsControlsBindingList ? advancedHelp : ScreenId == "config-controls-conflict" && _pendingBindingsReset ? $"Default bindings conflict with {string.Join(", ", CurrentControls.FindResetConflicts(_controlsDevice, _developerControls).Select(ControlsConflictLabel))}. Replace only after reviewing this scope." : ScreenId == "config-controls-conflict" && _pendingChord is not null ? $"{_pendingChord.FormatPrompt(CurrentControllerPresentation)} is assigned to {string.Join(", ", CurrentControls.FindConflicts(_controlsAction, _controlsDevice, _pendingChord).Select(ControlsConflictLabel))}." : SelectedEntry?.Help ?? CurrentScreen.Help;
        string title = ScreenId == "config-controls-conflict" && _pendingBindingsReset ? $"Restore {ControlsOwnerName} ({ControlsDeviceName})" : ScreenId.EndsWith("-choice", StringComparison.Ordinal) || ScreenId == "config-controls-conflict" ? $"{ControlsActionName} ({ControlsDeviceName})" : IsControlsBindingList && !_developerControls ? $"{CurrentScreen.Title} / {ControlsGroups[_controlsGroup]}" : CurrentScreen.Title;
        Place(page, SecondaryHeader(_developerControls ? "Debug Room" : "Config", title, help), 0);
        UiList commands = SecondaryCommands();
        commands.TextFlow = UiTextFlow.Wrap;
        commands.AutoRowHeight = true;
        if (IsControlsBindingList)
        {
            commands.SecondaryItems = Entries.Select(entry => Enum.TryParse(entry.Id, out ControlAction action) ? ControlsBindingText(action, device) : "").ToList();
            commands.SecondaryWidth = 0.3f;
            commands.SecondaryAlignment = UiAlignment.End;
        }
        Place(page, Panel(null, commands), 1);
        return page;
    }

    internal void ConnectControls(Func<ControlsProfile> read, Action<ControlsProfile> write, Func<ControllerPresentation>? presentation = null, Func<int>? generation = null, Func<ControlDevice>? inputDevice = null)
    {
        _readControls = read;
        _writeControls = write;
        _readControllerPresentation = presentation;
        _readControllerGeneration = generation;
        _readControlDevice = inputDevice;
        _view = null;
    }

    internal void ConnectReadOnlyTutorialControls(Func<ControlsProfile> read, Func<ControllerPresentation> presentation, Func<ControlDevice> inputDevice)
    {
        _readControls = read;
        _writeControls = null;
        _readControllerPresentation = presentation;
        _readControlDevice = inputDevice;
        _view = null;
    }

    private void PrepareControlsScreen(string screenId)
    {
        if (screenId == "config-controller-settings")
        {
            _developerControls = false;
            _controlsDevice = ControlDevice.Controller;
        }
        if (screenId is "config-keyboard" or "config-controller" or "debug-shortcuts")
        {
            _developerControls = screenId == "debug-shortcuts";
            if (!_developerControls)
            {
                _controlsDevice = screenId == "config-controller" ? ControlDevice.Controller : ControlDevice.Keyboard;
            }
        }
    }

    private void RememberControlsSelection()
    {
        if (IsControlsBindingList && !_developerControls && SelectedEntry?.Action is { Kind: "controls-select" } action)
        {
            _controlsGroupSelection[_controlsGroup] = action.SubjectId;
        }
    }

    private void PrepareControlsSelection(string screenId, string entryId)
    {
        if (screenId is "config-keyboard" or "config-controller" && Enum.TryParse(entryId, out ControlAction action) && !ControlsProfile.IsDeveloperAction(action))
        {
            int group = Array.FindIndex(ControlsGroupActions, values => values.Contains(action));
            if (group >= 0)
            {
                _controlsGroup = group;
                _controlsGroupSelection[group] = entryId;
            }
        }
    }

    private static string ControlsConflictLabel(ControlAction action) => $"{ControlsLabel(action)} ({(ControlsProfile.IsDeveloperAction(action) ? "Debug Room" : "Config")})";

    internal void RegisterControlsScreens()
    {
        Register(new MenuScreenDefinition
        {
            Id = "debug-shortcuts",
            Title = "Debug shortcuts",
            Help = "Global developer shortcuts. Select a device, then capture an action.",
            Entries = c => c.ControlsEntries(c._controlsDevice),
            Details = c => c.ControlsListDetails(c._controlsDevice)
        });
        Register(new MenuScreenDefinition
        {
            Id = "config-controller-settings-reset",
            Title = "Restore controller settings",
            Help = "Restore both deadzones, run threshold, button labels and vibration. Bindings remain unchanged.",
            Entries = _ => [new("keep", "Keep controller settings", "Return without changes.", Action: new("menu-back")), new("reset", "Restore controller settings", "Restore the five controller preferences only.", Action: new("controls-settings-reset"))]
        });
        Register(new MenuScreenDefinition
        {
            Id = "config-keyboard",
            GalleryEligible = true,
            Title = "Keyboard",
            Help = "Select an action to change its key or key combination.",
            Entries = c => c.ControlsEntries(ControlDevice.Keyboard),
            Details = c => c.ControlsListDetails(ControlDevice.Keyboard)
        });
        Register(new MenuScreenDefinition
        {
            Id = "config-controller",
            GalleryEligible = true,
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
            Help = "Restore only the selected owner's bindings on this device. Conflicts require a separate review.",
            Entries = c => [new("keep", "Keep current bindings", "Return without changing bindings.", Action: new("menu-back")), new("reset", c._developerControls ? "Restore debug shortcuts" : "Restore player bindings", "Preserve the other owner, the other device and controller settings.", Action: new("controls-reset-confirm"))],
            Details = c => [$"Device: {c.ControlsDeviceName}", $"Scope: {c.ControlsOwnerName}", "Other bindings and controller settings stay.", c.IsDemoControls ? "Changes stay in this menu demo." : "Global: changes save automatically."]
        });
    }

    private IReadOnlyList<MenuEntry> ControlsEntries(ControlDevice device)
    {
        string destination = device == ControlDevice.Keyboard ? "config-keyboard-choice" : "config-controller-choice";
        IReadOnlyList<ControlAction> actions = _developerControls ? ControlsProfile.DeveloperActions : ControlsGroupActions[_controlsGroup];
        List<MenuEntry> result = [new("device", $"Device: {ControlsDeviceName}", "Switch keyboard/controller while retaining the selected group and action.", Action: new("controls-switch-device"))];
        if (!_developerControls)
        {
            result.Add(new("group", $"Group: {ControlsGroups[_controlsGroup]}", "Choose Navigation, Interaction, Camera and battle, or Shortcuts.", Action: new("controls-group")));
        }
        result.AddRange(actions.Select(action => new MenuEntry(action.ToString(), ControlsLabel(action), $"Global {ControlsOwnerName}: {BindingText(action, device)}", destination, new("controls-select", action.ToString(), Value: device.ToString()))));
        result.Add(new("reset", _developerControls ? "Restore debug shortcuts..." : "Restore player bindings...", "Restore this owner's bindings for the selected device only.", "config-controls-reset", new("controls-device", device.ToString())));
        return result;
    }

    private IReadOnlyList<string> ControlsListDetails(ControlDevice device)
    {
        if (SelectedEntry?.Id == "settings")
        {
            return ["Controller settings", "Adjust left and right radial deadzones.", "Choose the tilt needed to run.", "Right stick: camera / car controls.", "Choose button labels for virtual pads.", "Enable or disable controller vibration.", "Changes save automatically."];
        }
        ControlActionDefinition? selected = ControlsProfile.Definitions.FirstOrDefault(definition => definition.Action.ToString() == SelectedEntry?.Id);
        List<string> details = [selected is null ? "Restore defaults" : ControlsLabel(selected.Action), selected?.Category ?? "All actions", selected is null ? "" : BindingText(selected.Action, device), "Key / button combinations supported.", device == ControlDevice.Controller ? "Left stick: movement" : "", device == ControlDevice.Controller ? "Right stick: camera / car controls" : "", device == ControlDevice.Controller ? "Stick directions cannot bind actions." : "", IsDemoControls ? "Menu demo: changes stay isolated." : CanWriteControls ? "Changes save automatically." : "Controls are not connected to this host."];
        return details.Where(line => line.Length != 0).ToArray();
    }

    private IReadOnlyList<MenuEntry> ControlsChoiceEntries()
    {
        if (_captureControls)
        {
            return [new("cancel-capture", "Cancel capture", "Keep the current binding.", Action: new("controls-cancel"))];
        }
        return [new("capture", "Capture key / button combination", "Release controls, hold the new combination, then release to apply.", Action: new("controls-capture"), Enabled: CanWriteControls), new("clear", "Clear binding", "Remove this action's binding for this device.", Action: new("controls-clear"), Enabled: CanWriteControls && (_controlsDevice == ControlDevice.Keyboard || _controlsAction is not ControlAction.Confirm and not ControlAction.Cancel)), new("back", "Back", "Return to the action list.", Action: new("menu-back"))];
    }

    private IReadOnlyList<string> ControlsChoiceDetails()
    {
        if (_captureControls)
        {
            return [ControlsActionName, _captureArmed ? "Press the new key / buttons." : "Release all keys and buttons first.", "Hold combination buttons together.", "Release all to finish capture.", _capturedChord?.FormatPrompt(CurrentControllerPresentation) ?? "Waiting for input...", "Gameplay input is paused."];
        }
        return [ControlsActionName, $"Device: {ControlsDeviceName}", $"Current: {ControlsMapping}", "Capture replaces this action's bindings.", "Hold combination buttons together.", "Confirm and Cancel must stay bound.", IsDemoControls ? "Changes stay in this menu demo." : "Changes save automatically."];
    }

    private IReadOnlyList<string> ControlsConflictDetails()
    {
        if (_pendingBindingsReset)
        {
            return [$"Restore {ControlsOwnerName} ({ControlsDeviceName})", "Default bindings conflict with:", .. CurrentControls.FindResetConflicts(_controlsDevice, _developerControls).Select(ControlsConflictLabel), "Replace removes only conflicting chords.", "Keep current bindings to cancel."];
        }
        return [ControlsActionName, $"New: {_pendingChord?.FormatPrompt(CurrentControllerPresentation)}", "Already assigned to:", .. _pendingChord is null ? [] : CurrentControls.FindConflicts(_controlsAction, _controlsDevice, _pendingChord).Select(action => ControlsConflictLabel(action)), "Keep current bindings to cancel."];
    }

    private string BindingText(ControlAction action, ControlDevice device)
    {
        IReadOnlyList<InputChord> bindings = CurrentControls.GetBindings(action, device);
        return bindings.Count == 0 ? "Unbound" : string.Join(" / ", bindings.Select(binding => binding.FormatPrompt(CurrentControllerPresentation)));
    }

    private string ControlsBindingText(ControlAction action, ControlDevice device)
    {
        string[] bindings = CurrentControls.GetBindings(action, device).Concat(ControlsProfile.GetPermanentBindings(action, device)).Select(binding => binding.FormatPrompt(CurrentControllerPresentation)).Distinct().ToArray();
        return bindings.Length == 0 ? "Unbound" : string.Join(" / ", bindings);
    }

    private string CurrentBindingText(ControlAction action)
    {
        ControlDevice device = CurrentControls.GetBindings(action, CurrentControlDevice).Count != 0 ? CurrentControlDevice : CurrentControlDevice == ControlDevice.Keyboard ? ControlDevice.Controller : ControlDevice.Keyboard;
        return ButtonPromptText.PlainText(BindingText(action, device));
    }

    private static string ControlsLabel(ControlAction action)
    {
        return action switch
        {
            ControlAction.DebugRoom => "Open Debug Room",
            ControlAction.BattleDebug => "Open battle laboratory",
            ControlAction.BattleInstantVictory => "Win current battle",
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
            case "controls-switch-device":
                string? selectedAction = SelectedEntry?.Id;
                _controlsDevice = _controlsDevice == ControlDevice.Keyboard ? ControlDevice.Controller : ControlDevice.Keyboard;
                if (!_developerControls)
                {
                    ScreenId = _controlsDevice == ControlDevice.Keyboard ? "config-keyboard" : "config-controller";
                    string restore = _controlsGroupSelection.GetValueOrDefault(_controlsGroup, selectedAction ?? "device");
                    int selectedIndex = Entries.ToList().FindIndex(entry => entry.Id == restore);
                    _selections[ScreenId] = Math.Max(0, selectedIndex);
                }
                _view = null;
                return true;
            case "controls-group":
                _controlsGroup = (_controlsGroup + 1) % ControlsGroups.Length;
                _selections[ScreenId] = 1;
                _view = null;
                return true;
            case "controls-settings-reset":
                if (WriteControls(() => CurrentControls.ResetControllerSettings()))
                {
                    Back();
                    Message = "Controller settings restored.";
                }
                return true;
            case "controls-setting":
                WriteControls(() => CycleControllerSetting(action.SubjectId));
                return true;
            case "controls-select":
                ControlAction selected = Enum.Parse<ControlAction>(action.SubjectId);
                if (ControlsProfile.IsDeveloperAction(selected) != _developerControls)
                {
                    Message = "This binding belongs to the other controls editor.";
                    return true;
                }
                _controlsAction = selected;
                if (!_developerControls)
                {
                    _controlsGroupSelection[_controlsGroup] = selected.ToString();
                }
                _controlsDevice = Enum.Parse<ControlDevice>(action.Value);
                return true;
            case "controls-device":
                _controlsDevice = Enum.Parse<ControlDevice>(action.SubjectId);
                _selections["config-controls-reset"] = 0;
                return true;
            case "controls-capture":
                if (CanWriteControls)
                {
                    _returnToControlsList = action.Value == "return-to-list";
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
            case "controls-clear":
                WriteControls(() => CurrentControls.WithoutBinding(_controlsAction, _controlsDevice));
                return true;
            case "controls-keep":
                _pendingBindingsReset = false;
                _pendingChord = null;
                Back();
                ReturnFromControlsCapture();
                return true;
            case "controls-replace":
                if (_pendingBindingsReset)
                {
                    if (WriteControls(() => CurrentControls.ResetBindings(_controlsDevice, _developerControls, replaceConflicts: true)))
                    {
                        _pendingBindingsReset = false;
                        Back();
                        Back();
                        Message = $"{ControlsOwnerName} restored.";
                    }
                    return true;
                }
                if (_pendingChord is not null && WriteControls(() => CurrentControls.WithBinding(_controlsAction, _controlsDevice, _pendingChord, replaceConflicts: true)))
                {
                    _pendingChord = null;
                    Back();
                    Message = "Binding saved.";
                    ReturnFromControlsCapture();
                }
                return true;
            case "controls-reset-confirm":
                if (CurrentControls.FindResetConflicts(_controlsDevice, _developerControls).Count != 0)
                {
                    _pendingBindingsReset = true;
                    _selections["config-controls-conflict"] = 0;
                    Push("config-controls-conflict");
                    return true;
                }
                if (WriteControls(() => CurrentControls.ResetBindings(_controlsDevice, _developerControls)))
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
            new("left-deadzone", $"Left stick deadzone: {settings.LeftStickDeadZone:P0}", "Adjust by 5%, from 0% to 50%.", Action: new("controls-setting", "left-deadzone"), Enabled: CanWriteControls),
            new("right-deadzone", $"Right stick deadzone: {settings.RightStickDeadZone:P0}", "Adjust by 5%, from 0% to 50%.", Action: new("controls-setting", "right-deadzone"), Enabled: CanWriteControls),
            new("run-threshold", $"Run threshold: {settings.WalkRunThreshold:P0}", "Adjust by 5%, from 10% to 95%.", Action: new("controls-setting", "run-threshold"), Enabled: CanWriteControls),
            new("presentation", $"Button labels: {settings.Presentation}", "Cycle automatic, generic, Xbox, PlayStation and Nintendo labels.", Action: new("controls-setting", "presentation"), Enabled: CanWriteControls),
            new("vibration", $"Vibration: {(settings.VibrationEnabled ? "On" : "Off")}", "Enable or disable controller vibration.", Action: new("controls-setting", "vibration"), Enabled: CanWriteControls),
            new("reset-settings", "Restore controller settings...", "Restore only these five preferences; preserve all bindings.", "config-controller-settings-reset")
        ];
    }

    private bool ChangeControlsSetting(int direction)
    {
        if (SelectedEntry?.Id == "group" && IsControlsBindingList)
        {
            _controlsGroup = (_controlsGroup + direction + ControlsGroups.Length) % ControlsGroups.Length;
            _selections[ScreenId] = 1;
            _view = null;
            return true;
        }
        if (SelectedEntry?.Id == "device" && IsControlsBindingList)
        {
            ExecuteControlsAction(new("controls-switch-device"));
            return true;
        }
        if (ScreenId != "config-controller-settings" || SelectedEntry?.Action is not { Kind: "controls-setting" } action)
        {
            return false;
        }
        WriteControls(() => CycleControllerSetting(action.SubjectId, direction));
        return true;
    }

    private ControlsProfile CycleControllerSetting(string setting, int direction = 1)
    {
        ControllerSettings current = CurrentControls.Controller;
        float Next(float value, float minimum, float maximum) => Math.Clamp(MathF.Round(value + direction * 0.05f, 2), minimum, maximum);
        return CurrentControls.WithControllerSettings(
            setting == "left-deadzone" ? Next(current.LeftStickDeadZone, 0, 0.5f) : current.LeftStickDeadZone,
            setting == "right-deadzone" ? Next(current.RightStickDeadZone, 0, 0.5f) : current.RightStickDeadZone,
            setting == "run-threshold" ? Next(current.WalkRunThreshold, 0.1f, 0.95f) : current.WalkRunThreshold,
            setting == "presentation" ? (ControllerPresentation)(((int)current.Presentation + direction + Enum.GetValues<ControllerPresentation>().Length) % Enum.GetValues<ControllerPresentation>().Length) : current.Presentation,
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
            Console.Error.WriteLine(error);
            _view = null;
            return false;
        }
    }

    private void ProposeControlsBinding(InputChord chord)
    {
        _pendingBindingsReset = false;
        try
        {
            if (CurrentControls.FindConflicts(_controlsAction, _controlsDevice, chord).Count != 0)
            {
                _pendingChord = chord;
                _selections["config-controls-conflict"] = 0;
                Push("config-controls-conflict");
                return;
            }
            if (WriteControls(() => CurrentControls.WithBinding(_controlsAction, _controlsDevice, chord)))
            {
                ReturnFromControlsCapture();
            }
        }
        catch (ArgumentException error)
        {
            Message = error.Message;
            Console.Error.WriteLine(error);
            _view = null;
        }
    }

    private void ReturnFromControlsCapture()
    {
        if (_returnToControlsList && ScreenId == ControlsChoiceScreen)
        {
            _returnToControlsList = false;
            string message = Message;
            Back();
            Message = message;
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
        ReturnFromControlsCapture();
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
        bool released = (_controlsDevice == ControlDevice.Keyboard ? heldKeys.Length == 0 : heldButtons.Length == 0) && keys.IsKeyUp(Keys.Escape);
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
        if (keys.IsKeyDown(Keys.Escape))
        {
            CancelControlsCapture();
            return true;
        }
        if (!_captureArmed)
        {
            _captureArmed = released;
            Message = released ? "Press a key or combination, then release all to apply." : "Release all keys and buttons first.";
            _view = null;
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
