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
        [ControlAction.MoveUp, ControlAction.MoveDown, ControlAction.MoveLeft, ControlAction.MoveRight, ControlAction.UiTab, ControlAction.UiPageUp, ControlAction.UiPageDown, ControlAction.GuardianSwitchColumn],
        [ControlAction.Confirm, ControlAction.Cancel, ControlAction.Menu, ControlAction.Card, ControlAction.Select, ControlAction.Pause],
        [ControlAction.CameraLeft, ControlAction.CameraRight, ControlAction.RotateLeft, ControlAction.RotateRight, ControlAction.BattleAdvanceTurn],
        [ControlAction.SpeedToggle, ControlAction.QuickSave, ControlAction.QuickLoad, ControlAction.SaveMenu, ControlAction.LoadMenu, ControlAction.BattleInstantVictory, ControlAction.BattleInstantDefeat]
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
        string advancedHelp = advancedBinding == "Unbound" ? SystemText("ui.buildcontrolspage.advancedhelp.card.details.is.unbound.assign.it.to.open", "Card / Details is unbound. Assign it to open advanced options.") : FormatSystemText("ui.buildcontrolspage.advancedhelp.press.for.advanced.options", "Press {0} for advanced options.", advancedBinding);
        advancedHelp = $"{(IsDemoControls ? "Preview / Fixture" : "Global preferences")}. {advancedHelp}";
        string help = IsControlsBindingList ? advancedHelp : ScreenId == "config-controls-conflict" && _pendingBindingsReset ? FormatSystemText("ui.buildcontrolspage.help.default.bindings.conflict.with.replace.only.after.reviewing", "Default bindings conflict with {0}. Replace only after reviewing this scope.", string.Join(", ", CurrentControls.FindResetConflicts(_controlsDevice, _developerControls).Select(ControlsConflictLabel))) : ScreenId == "config-controls-conflict" && _pendingChord is not null ? FormatSystemText("ui.buildcontrolspage.help.is.assigned.to", "{0} is assigned to {1}.", _pendingChord.FormatPrompt(CurrentControllerPresentation), string.Join(", ", CurrentControls.FindConflicts(_controlsAction, _controlsDevice, _pendingChord).Select(ControlsConflictLabel))) : SelectedEntry?.Help ?? CurrentScreen.Help;
        string title = ScreenId == "config-controls-conflict" && _pendingBindingsReset ? FormatSystemText("ui.buildcontrolspage.title.restore", "Restore {0} ({1})", ControlsOwnerName, ControlsDeviceName) : ScreenId.EndsWith("-choice", StringComparison.Ordinal) || ScreenId == "config-controls-conflict" ? $"{ControlsActionName} ({ControlsDeviceName})" : IsControlsBindingList && !_developerControls ? $"{CurrentScreen.Title} / {ControlsGroups[_controlsGroup]}" : CurrentScreen.Title;
        Place(page, SecondaryHeader(_developerControls ? SystemText("ui.buildcontrolspage.header.debug.room", "Debug Room") : SystemText("ui.buildcontrolspage.header.config", "Config"), title, help), 0);
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

    private string ControlsConflictLabel(ControlAction action) => $"{ControlsLabel(action)} ({(ControlsProfile.IsDeveloperAction(action) ? "Debug Room" : "Config")})";

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
            Entries = _ => [new("keep", SystemText("ui.registercontrolsscreens.entry.label.keep.controller.settings", "Keep controller settings"), SystemText("ui.registercontrolsscreens.entry.help.return.without.changes", "Return without changes."), Action: new("menu-back")), new("reset", SystemText("ui.registercontrolsscreens.entry.label.restore.controller.settings", "Restore controller settings"), SystemText("ui.registercontrolsscreens.entry.help.restore.the.five.controller.preferences.only", "Restore the five controller preferences only."), Action: new("controls-settings-reset"))]
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
            Details = c => [SystemText("ui.registercontrolsscreens.details.deadzones.remove.stick.drift", "Deadzones remove stick drift."), SystemText("ui.registercontrolsscreens.details.run.threshold.uses.the.adjusted.stick", "Run threshold uses the adjusted stick."), SystemText("ui.registercontrolsscreens.details.a.lighter.tilt.selects.native.walking", "A lighter tilt selects native walking."), SystemText("ui.registercontrolsscreens.details.right.stick.steers.throttles.the.car", "Right stick steers / throttles the car."), SystemText("ui.registercontrolsscreens.details.button.labels.follow.the.connected.pad", "Button labels follow the connected pad."), SystemText("ui.registercontrolsscreens.details.choose.labels.manually.for.virtual.pads", "Choose labels manually for virtual pads."), SystemText("ui.registercontrolsscreens.details.confirm.cycles.the.selected.setting", "Confirm cycles the selected setting.")]
        });
        foreach (string id in new[] { "config-keyboard-choice", "config-controller-choice" })
        {
            Register(new MenuScreenDefinition
            {
                Id = id,
                Title = SystemText("ui.registercontrolsscreens.title.change.binding", "Change binding"),
                Help = SystemText("ui.registercontrolsscreens.help.capture.a.button.or.combination.or.remove.the", "Capture a button or combination, or remove the current binding."),
                Entries = c => c.ControlsChoiceEntries(),
                Details = c => c.ControlsChoiceDetails()
            });
        }
        Register(new MenuScreenDefinition
        {
            Id = "config-controls-conflict",
            Title = "Binding in use",
            Help = "Replacing removes this same combination from the other actions.",
            Entries = _ => [new("keep", SystemText("ui.registercontrolsscreens.entry.label.keep.current.bindings", "Keep current bindings"), SystemText("ui.registercontrolsscreens.entry.help.discard.the.proposed.binding", "Discard the proposed binding."), Action: new("controls-keep")), new("replace", SystemText("ui.registercontrolsscreens.entry.label.replace.conflicting.bindings", "Replace conflicting bindings"), SystemText("ui.registercontrolsscreens.entry.help.assign.this.combination.and.remove.its.conflicting.bindings", "Assign this combination and remove its conflicting bindings."), Action: new("controls-replace"))],
            Details = c => c.ControlsConflictDetails()
        });
        Register(new MenuScreenDefinition
        {
            Id = "config-controls-reset",
            Title = "Restore defaults",
            Help = "Restore only the selected owner's bindings on this device. Conflicts require a separate review.",
            Entries = c => [new("keep", SystemText("ui.registercontrolsscreens.entry.label.keep.current.bindings", "Keep current bindings"), SystemText("ui.registercontrolsscreens.entry.help.return.without.changing.bindings", "Return without changing bindings."), Action: new("menu-back")), new("reset", c._developerControls ? SystemText("ui.registercontrolsscreens.entry.label.restore.debug.shortcuts", "Restore debug shortcuts") : SystemText("ui.registercontrolsscreens.entry.label.restore.player.bindings", "Restore player bindings"), SystemText("ui.registercontrolsscreens.entry.help.preserve.the.other.owner.the.other.device.and", "Preserve the other owner, the other device and controller settings."), Action: new("controls-reset-confirm"))],
            Details = c => [FormatSystemText("ui.registercontrolsscreens.details.device", "Device: {0}", c.ControlsDeviceName), FormatSystemText("ui.registercontrolsscreens.details.scope", "Scope: {0}", c.ControlsOwnerName), SystemText("ui.registercontrolsscreens.details.other.bindings.and.controller.settings.stay", "Other bindings and controller settings stay."), c.IsDemoControls ? SystemText("ui.registercontrolsscreens.details.changes.stay.in.this.menu.demo", "Changes stay in this menu demo.") : SystemText("ui.registercontrolsscreens.details.global.changes.save.automatically", "Global: changes save automatically.")]
        });
    }

    private IReadOnlyList<MenuEntry> ControlsEntries(ControlDevice device)
    {
        string destination = device == ControlDevice.Keyboard ? "config-keyboard-choice" : "config-controller-choice";
        IReadOnlyList<ControlAction> actions = _developerControls ? ControlsProfile.DeveloperActions : ControlsGroupActions[_controlsGroup];
        List<MenuEntry> result = [new("device", FormatSystemText("ui.controlsentries.entry.label.device", "Device: {0}", ControlsDeviceName), SystemText("ui.controlsentries.entry.help.switch.keyboard.controller.while.retaining.the.selected.group", "Switch keyboard/controller while retaining the selected group and action."), Action: new("controls-switch-device"))];
        if (!_developerControls)
        {
            result.Add(new("group", FormatSystemText("ui.controlsentries.entry.label.group", "Group: {0}", ControlsGroups[_controlsGroup]), SystemText("ui.controlsentries.entry.help.choose.navigation.interaction.camera.and.battle.or.shortcuts", "Choose Navigation, Interaction, Camera and battle, or Shortcuts."), Action: new("controls-group")));
        }
        result.AddRange(actions.Select(action => new MenuEntry(action.ToString(), ControlsLabel(action), FormatSystemText("ui.controlsentries.entry.help.global", "Global {0}: {1}", ControlsOwnerName, BindingText(action, device)), destination, new("controls-select", action.ToString(), Value: device.ToString()))));
        result.Add(new("reset", _developerControls ? SystemText("ui.controlsentries.entry.label.restore.debug.shortcuts", "Restore debug shortcuts...") : SystemText("ui.controlsentries.entry.label.restore.player.bindings", "Restore player bindings..."), SystemText("ui.controlsentries.entry.help.restore.this.owner.s.bindings.for.the.selected", "Restore this owner's bindings for the selected device only."), "config-controls-reset", new("controls-device", device.ToString())));
        return result;
    }

    private IReadOnlyList<string> ControlsListDetails(ControlDevice device)
    {
        if (SelectedEntry?.Id == "settings")
        {
            return [SystemText("ui.controlslistdetails.details.controller.settings", "Controller settings"), SystemText("ui.controlslistdetails.details.adjust.left.and.right.radial.deadzones", "Adjust left and right radial deadzones."), SystemText("ui.controlslistdetails.details.choose.the.tilt.needed.to.run", "Choose the tilt needed to run."), SystemText("ui.controlslistdetails.details.right.stick.camera.car.controls", "Right stick: camera / car controls."), SystemText("ui.controlslistdetails.details.choose.button.labels.for.virtual.pads", "Choose button labels for virtual pads."), SystemText("ui.controlslistdetails.details.enable.or.disable.controller.vibration", "Enable or disable controller vibration."), SystemText("ui.controlslistdetails.details.changes.save.automatically", "Changes save automatically.")];
        }
        ControlActionDefinition? selected = ControlsProfile.Definitions.FirstOrDefault(definition => definition.Action.ToString() == SelectedEntry?.Id);
        List<string> details = [selected is null ? SystemText("ui.controlslistdetails.details.restore.defaults", "Restore defaults") : ControlsLabel(selected.Action), selected?.Category ?? SystemText("ui.controlslistdetails.details.all.actions", "All actions"), selected is null ? "" : BindingText(selected.Action, device), SystemText("ui.controlslistdetails.details.key.button.combinations.supported", "Key / button combinations supported."), device == ControlDevice.Controller ? SystemText("ui.controlslistdetails.details.left.stick.movement", "Left stick: movement") : "", device == ControlDevice.Controller ? SystemText("ui.controlslistdetails.details.right.stick.camera.car.controls.alternate", "Right stick: camera / car controls") : "", device == ControlDevice.Controller ? SystemText("ui.controlslistdetails.details.stick.directions.cannot.bind.actions", "Stick directions cannot bind actions.") : "", IsDemoControls ? SystemText("ui.controlslistdetails.details.menu.demo.changes.stay.isolated", "Menu demo: changes stay isolated.") : CanWriteControls ? SystemText("ui.controlslistdetails.details.changes.save.automatically", "Changes save automatically.") : SystemText("ui.controlslistdetails.details.controls.are.not.connected.to.this.host", "Controls are not connected to this host.")];
        return details.Where(line => line.Length != 0).ToArray();
    }

    private IReadOnlyList<MenuEntry> ControlsChoiceEntries()
    {
        if (_captureControls)
        {
            return [new("cancel-capture", SystemText("ui.controlschoiceentries.entry.label.cancel.capture", "Cancel capture"), SystemText("ui.controlschoiceentries.entry.help.keep.the.current.binding", "Keep the current binding."), Action: new("controls-cancel"))];
        }
        return [new("capture", SystemText("ui.controlschoiceentries.entry.label.capture.key.button.combination", "Capture key / button combination"), SystemText("ui.controlschoiceentries.entry.help.release.controls.hold.the.new.combination.then.release", "Release controls, hold the new combination, then release to apply."), Action: new("controls-capture"), Enabled: CanWriteControls), new("clear", SystemText("ui.controlschoiceentries.entry.label.clear.binding", "Clear binding"), SystemText("ui.controlschoiceentries.entry.help.remove.this.action.s.binding.for.this.device", "Remove this action's binding for this device."), Action: new("controls-clear"), Enabled: CanWriteControls && (_controlsDevice == ControlDevice.Keyboard || _controlsAction is not ControlAction.Confirm and not ControlAction.Cancel)), new("back", SystemText("ui.controlschoiceentries.entry.label.back", "Back"), SystemText("ui.controlschoiceentries.entry.help.return.to.the.action.list", "Return to the action list."), Action: new("menu-back"))];
    }

    private IReadOnlyList<string> ControlsChoiceDetails()
    {
        if (_captureControls)
        {
            return [ControlsActionName, _captureArmed ? SystemText("ui.controlschoicedetails.details.press.the.new.key.buttons", "Press the new key / buttons.") : SystemText("ui.controlschoicedetails.details.release.all.keys.and.buttons.first", "Release all keys and buttons first."), SystemText("ui.controlschoicedetails.details.hold.combination.buttons.together", "Hold combination buttons together."), SystemText("ui.controlschoicedetails.details.release.all.to.finish.capture", "Release all to finish capture."), _capturedChord?.FormatPrompt(CurrentControllerPresentation) ?? SystemText("ui.controlschoicedetails.details.waiting.for.input", "Waiting for input..."), SystemText("ui.controlschoicedetails.details.gameplay.input.is.paused", "Gameplay input is paused.")];
        }
        return [ControlsActionName, FormatSystemText("ui.controlschoicedetails.details.device", "Device: {0}", ControlsDeviceName), FormatSystemText("ui.controlschoicedetails.details.current", "Current: {0}", ControlsMapping), SystemText("ui.controlschoicedetails.details.capture.replaces.this.action.s.bindings", "Capture replaces this action's bindings."), SystemText("ui.controlschoicedetails.details.hold.combination.buttons.together", "Hold combination buttons together."), SystemText("ui.controlschoicedetails.details.confirm.and.cancel.must.stay.bound", "Confirm and Cancel must stay bound."), IsDemoControls ? SystemText("ui.controlschoicedetails.details.changes.stay.in.this.menu.demo", "Changes stay in this menu demo.") : SystemText("ui.controlschoicedetails.details.changes.save.automatically", "Changes save automatically.")];
    }

    private IReadOnlyList<string> ControlsConflictDetails()
    {
        if (_pendingBindingsReset)
        {
            return [FormatSystemText("ui.controlsconflictdetails.details.restore", "Restore {0} ({1})", ControlsOwnerName, ControlsDeviceName), SystemText("ui.controlsconflictdetails.details.default.bindings.conflict.with", "Default bindings conflict with:"), .. CurrentControls.FindResetConflicts(_controlsDevice, _developerControls).Select(ControlsConflictLabel), SystemText("ui.controlsconflictdetails.details.replace.removes.only.conflicting.chords", "Replace removes only conflicting chords."), SystemText("ui.controlsconflictdetails.details.keep.current.bindings.to.cancel", "Keep current bindings to cancel.")];
        }
        return [ControlsActionName, FormatSystemText("ui.controlsconflictdetails.details.new", "New: {0}", _pendingChord?.FormatPrompt(CurrentControllerPresentation)), SystemText("ui.controlsconflictdetails.details.already.assigned.to", "Already assigned to:"), .. _pendingChord is null ? [] : CurrentControls.FindConflicts(_controlsAction, _controlsDevice, _pendingChord).Select(action => ControlsConflictLabel(action)), SystemText("ui.controlsconflictdetails.details.keep.current.bindings.to.cancel", "Keep current bindings to cancel.")];
    }

    private string BindingText(ControlAction action, ControlDevice device)
    {
        IReadOnlyList<InputChord> bindings = CurrentControls.GetBindings(action, device);
        return bindings.Count == 0 ? SystemText("ui.bindingtext.details.unbound", "Unbound") : string.Join(" / ", bindings.Select(binding => binding.FormatPrompt(CurrentControllerPresentation)));
    }

    private string ControlsBindingText(ControlAction action, ControlDevice device)
    {
        string[] bindings = CurrentControls.GetBindings(action, device).Concat(ControlsProfile.GetPermanentBindings(action, device)).Select(binding => binding.FormatPrompt(CurrentControllerPresentation)).Distinct().ToArray();
        return bindings.Length == 0 ? SystemText("ui.controlsbindingtext.details.unbound", "Unbound") : string.Join(" / ", bindings);
    }

    private string CurrentBindingText(ControlAction action)
    {
        ControlDevice device = CurrentControlDevice;
        IReadOnlyList<InputChord> bindings = CurrentControls.GetBindings(action, device);
        if (bindings.Count == 0)
        {
            bindings = ControlsProfile.GetPermanentBindings(action, device);
        }
        if (device == ControlDevice.Controller && bindings.Count == 0)
        {
            bindings = CurrentControls.GetBindings(action, ControlDevice.Keyboard);
            if (bindings.Count == 0)
            {
                bindings = ControlsProfile.GetPermanentBindings(action, ControlDevice.Keyboard);
            }
        }
        return bindings.Count == 0 ? SystemText("ui.currentbindingtext.details.unbound", "Unbound") : string.Join(" / ", bindings.Select(binding => binding.FormatPrompt(CurrentControllerPresentation)));
    }

    private string ControlsLabel(ControlAction action)
    {
        return action switch
        {
            ControlAction.DebugRoom => SystemText("ui.controlslabel.details.open.debug.room", "Open Debug Room"),
            ControlAction.BattleDebug => SystemText("ui.controlslabel.details.open.battle.laboratory", "Open battle laboratory"),
            ControlAction.BattleInstantVictory => SystemText("ui.controlslabel.details.instant.victory", "Instant victory"),
            ControlAction.BattleInstantDefeat => SystemText("ui.controlslabel.details.instant.defeat", "Instant defeat"),
            ControlAction.RotateLeft => SystemText("ui.controlslabel.details.rotate.left.previous.character", "Rotate left / Previous character"),
            ControlAction.RotateRight => SystemText("ui.controlslabel.details.rotate.right.gunblade.next", "Rotate right / Gunblade / Next"),
            ControlAction.CameraRight => SystemText("ui.controlslabel.details.camera.mode.escape.battle.r", "Camera mode / Escape battle (R)"),
            _ => SystemText("controls.action." + action.ToString(), ControlsProfile.Definitions.Single(definition => definition.Action == action).Label)
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
                    Message = "";
                }
                return true;
            case "controls-setting":
                WriteControls(() => CycleControllerSetting(action.SubjectId));
                return true;
            case "controls-select":
                ControlAction selected = Enum.Parse<ControlAction>(action.SubjectId);
                if (ControlsProfile.IsDeveloperAction(selected) != _developerControls)
                {
                    Message = SystemText("ui.executecontrolsaction.message.this.binding.belongs.to.the.other.controls.editor", "This binding belongs to the other controls editor.");
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
                    Message = SystemText("ui.executecontrolsaction.message.release.controls.then.press.your.new.combination", "Release controls, then press your new combination.");
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
                        Message = "";
                    }
                    return true;
                }
                if (_pendingChord is not null && WriteControls(() => CurrentControls.WithBinding(_controlsAction, _controlsDevice, _pendingChord, replaceConflicts: true)))
                {
                    _pendingChord = null;
                    Back();
                    Message = "";
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
                    Message = "";
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
            new("left-deadzone", FormatSystemText("ui.controllersettingsentries.entry.label.left.stick.deadzone", "Left stick deadzone: {0:P0}", settings.LeftStickDeadZone), SystemText("ui.controllersettingsentries.entry.help.adjust.by.5.from.0.to.50", "Adjust by 5%, from 0% to 50%."), Action: new("controls-setting", "left-deadzone"), Enabled: CanWriteControls),
            new("right-deadzone", FormatSystemText("ui.controllersettingsentries.entry.label.right.stick.deadzone", "Right stick deadzone: {0:P0}", settings.RightStickDeadZone), SystemText("ui.controllersettingsentries.entry.help.adjust.by.5.from.0.to.50", "Adjust by 5%, from 0% to 50%."), Action: new("controls-setting", "right-deadzone"), Enabled: CanWriteControls),
            new("run-threshold", FormatSystemText("ui.controllersettingsentries.entry.label.run.threshold", "Run threshold: {0:P0}", settings.WalkRunThreshold), SystemText("ui.controllersettingsentries.entry.help.adjust.by.5.from.10.to.95", "Adjust by 5%, from 10% to 95%."), Action: new("controls-setting", "run-threshold"), Enabled: CanWriteControls),
            new("presentation", FormatSystemText("ui.controllersettingsentries.entry.label.button.labels", "Button labels: {0}", settings.Presentation), SystemText("ui.controllersettingsentries.entry.help.cycle.automatic.generic.xbox.playstation.and.nintendo.labels", "Cycle automatic, generic, Xbox, PlayStation and Nintendo labels."), Action: new("controls-setting", "presentation"), Enabled: CanWriteControls),
            new("vibration", FormatSystemText("ui.controllersettingsentries.entry.label.vibration", "Vibration: {0}", (settings.VibrationEnabled ? SystemText("ui.controllersettingsentries.value.on", "On") : SystemText("ui.controllersettingsentries.value.off", "Off"))), SystemText("ui.controllersettingsentries.entry.help.enable.or.disable.controller.vibration", "Enable or disable controller vibration."), Action: new("controls-setting", "vibration"), Enabled: CanWriteControls),
            new("reset-settings", SystemText("ui.controllersettingsentries.entry.label.restore.controller.settings", "Restore controller settings..."), SystemText("ui.controllersettingsentries.entry.help.restore.only.these.five.preferences.preserve.all.bindings", "Restore only these five preferences; preserve all bindings."), "config-controller-settings-reset")
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
            Message = SystemText("ui.writecontrols.message.controls.are.not.connected.to.this.host", "Controls are not connected to this host.");
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
            Message = "";
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
        Message = "";
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
            Message = released ? SystemText("ui.updatecontrolscapture.message.press.a.key.or.combination.then.release.all", "Press a key or combination, then release all to apply.") : SystemText("ui.updatecontrolscapture.message.release.all.keys.and.buttons.first", "Release all keys and buttons first.");
            _view = null;
            return true;
        }
        InputChord held = _controlsDevice == ControlDevice.Keyboard ? new(heldKeys, []) : new([], heldButtons);
        int count = held.Keys.Length + held.Buttons.Length;
        if (count > (_capturedChord?.Keys.Length ?? 0) + (_capturedChord?.Buttons.Length ?? 0))
        {
            _capturedChord = held;
            Message = SystemText("ui.updatecontrolscapture.message.release.all.controls.to.apply.this.combination", "Release all controls to apply this combination.");
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
