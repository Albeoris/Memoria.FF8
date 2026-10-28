using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private readonly Func<MenuData>? _liveProvider;
    private readonly Action<MenuAction>? _liveAction;
    private readonly Dictionary<string, MenuScreenDefinition> _screens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _selections = new(StringComparer.Ordinal);
    private readonly Stack<string> _history = new();
    private MenuData? _demo;
    private MenuData? _liveData;
    private bool sortMagicByName;
    private string _characterId = "";
    private string _gfId = "";
    private string _itemId = "";
    private string _magicId = "";
    private string _cardId = "";
    private string _recipeId = "";
    private string _exchangeId = "";
    private string _junctionSlot = "HP";
    private string _saveSlotId = "";
    private string _battleTargetId = "";
    private string _battleCommand = "Attack";
    private int _commandSlot = 1;
    private int _abilitySlot;
    private UiView? _view;
    private int _testQuestion;
    private int _testCorrect;
    private string _characterSelectionDestination = "";
    private int _characterSelectionIndex;

    public MenuController(Func<MenuData>? liveProvider = null, Action<MenuAction>? liveAction = null)
    {
        _liveProvider = liveProvider;
        _liveAction = liveAction;
        MenuCatalog.Register(this);
        RegisterFontPickerScreen();
        RegisterQuickSaveRetentionScreen();
        RegisterGameplayTuningScreens();
        RegisterNamingScreens();
        RegisterChocoboWorldScreens();
        RegisterMagazineScreens();
    }

    public bool IsOpen { get; private set; }
    public event Action? BattleDebugRequested;
    public event Action<string>? ScreenOpened;
    public event Action<MenuSound>? SoundRequested;
    public bool BattleDebugAvailable => BattleDebugRequested is not null;
    public bool IsDemo { get; private set; }
    public bool IsFixtureContext { get; private set; }
    public string ScreenId { get; private set; } = "main";
    private string message = "";
    private Func<string>? systemMessage;
    public string Message
    {
        get => systemMessage?.Invoke() ?? message;
        private set
        {
            systemMessage = null;
            message = value;
        }
    }

    private void SetSystemMessage(Func<string> text)
    {
        systemMessage = text;
    }
    public MenuData Data
    {
        get
        {
            MenuData data = IsFixtureContext || IsDemo ? _demo! : _liveData ??= _liveProvider?.Invoke() ?? new MenuData();
            if (sortMagicByName)
            {
                data.Magic.Sort((left, right) =>
                {
                    int name = string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase);
                    return name != 0 ? name : string.Compare(left.Id, right.Id, StringComparison.Ordinal);
                });
            }
            return data;
        }
    }
    public IReadOnlyCollection<MenuScreenDefinition> Definitions => _screens.Values.Select(LocalizeSystemScreen).ToArray();
    public IReadOnlyList<string> ScreenIds => _screens.Keys.ToArray();
    public MenuScreenDefinition CurrentScreen => LocalizeSystemScreen(_screens[ScreenId]);
    public int SelectedIndex => _selections.GetValueOrDefault(ScreenId);
    public IReadOnlyList<MenuEntry> Entries => LocalizeSystemEntries(CurrentScreen.Entries(this));
    public MenuEntry? SelectedEntry => Entries.Count == 0 ? null : Entries[Math.Clamp(SelectedIndex, 0, Entries.Count - 1)];
    public MenuCharacter? Character => _characterId.Length > 0 ? Data.Characters.FirstOrDefault(c => c.Id == _characterId) : Data.Characters.FirstOrDefault(c => c.InParty) ?? Data.Characters.FirstOrDefault();
    public string ExchangeId => _exchangeId;
    public bool IsSelectingCharacter => _characterSelectionDestination.Length > 0;
    public string CharacterSelectionDestination => _characterSelectionDestination;
    public int CharacterSelectionIndex => _characterSelectionIndex;
    public IReadOnlyList<MenuCharacter> CharacterChoices => Data.Characters.Where(character => !string.IsNullOrWhiteSpace(character.Id)).OrderByDescending(character => character.InParty).ToArray();
    public MenuGuardianForce? GuardianForce => Data.GuardianForces.FirstOrDefault(g => g.Id == _gfId) ?? Data.GuardianForces.FirstOrDefault();
    public MenuItem? Item => _itemId.Length == 0 ? Data.Items.FirstOrDefault() : Data.Items.FirstOrDefault(i => i.Id == _itemId);
    public MenuMagic? Magic => _magicId.Length > 0 ? Data.Magic.FirstOrDefault(m => m.Id == _magicId && m.CharacterId == Character?.Id) : Data.Magic.FirstOrDefault(m => m.CharacterId == Character?.Id);
    public MenuCard? Card => Data.Cards.FirstOrDefault(c => c.Id == _cardId) ?? Data.Cards.FirstOrDefault();
    public MenuRecipe? Recipe => _recipeId.Length > 0 ? Data.Recipes.FirstOrDefault(r => r.Id == _recipeId) : Data.Recipes.FirstOrDefault();
    public MenuSaveSlot? SaveSlot => Data.SaveSlots.FirstOrDefault(s => s.Id == _saveSlotId) ?? Data.SaveSlots.FirstOrDefault();
    public MenuBattleTarget? BattleTarget => Data.BattleTargets.FirstOrDefault(t => t.Id == _battleTargetId) ?? Data.BattleTargets.FirstOrDefault();
    public string BattleCommand => _battleCommand;
    public string JunctionSlot => _junctionSlot;
    public int CommandSlot => _commandSlot;
    public int AbilitySlot => _abilitySlot;
    public int TestQuestion => _testQuestion;
    public int TestCorrect => _testCorrect;
    public UiView? View => _view;

    public void Register(MenuScreenDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            throw new ArgumentException("A menu screen requires a stable ID.", nameof(definition));
        }
        _screens[definition.Id] = definition;
        _view = null;
    }

    public void Open(string screenId = "main", bool demo = false)
    {
        screenId = ResolveSettingsAlias(screenId);
        IsFixtureContext = demo && !screenId.StartsWith("debug-", StringComparison.Ordinal);
        ClearBattleLearningReview();
        exitGame = null;
        pendingGuardianForceTransfer = null;
        CancelControlsCapture();
        _nameTarget = null;
        _acceptName = null;
        _nameHighSurrogate = null;
        if (!_screens.ContainsKey(screenId))
        {
            throw new ArgumentException($"Unknown menu screen '{screenId}'.", nameof(screenId));
        }
        if (IsDemo != demo)
        {
            _magicId = "";
            _recipeId = "";
            _refinementAbility = "";
        }
        IsDemo = demo;
        _liveData = null;
        if (demo)
        {
            _demo ??= MenuDemoData.Create();
            var family = MenuCatalog.LimitFamilyForScreen(screenId);
            var fixtureCharacter = _demo.Characters.FirstOrDefault(c => c.LimitBreak == family);
            if (fixtureCharacter != null)
            {
                _characterId = fixtureCharacter.Id;
            }
        }
        if (!Data.Characters.Any(character => character.Id == _characterId))
        {
            _characterId = "";
        }
        IsOpen = true;
        ScreenId = screenId;
        PrepareControlsScreen(screenId);
        InitializeScreenCursor(screenId);
        if (screenId.StartsWith("config-controller", StringComparison.Ordinal))
        {
            _controlsDevice = ControlDevice.Controller;
        }
        else if (screenId.StartsWith("config-keyboard", StringComparison.Ordinal))
        {
            _controlsDevice = ControlDevice.Keyboard;
        }
        _history.Clear();
        _characterSelectionDestination = "";
        if (demo && CurrentScreen.Layout == MenuLayout.BattleHud && Character?.InParty != true)
        {
            _characterId = Data.Characters.FirstOrDefault(character => character.InParty)?.Id ?? "";
        }
        Message = "";
        _view = null;
        ScreenOpened?.Invoke(ScreenId);
    }

    public void InvalidateLiveData()
    {
        string? selected = ScreenId is "magic-use" or "magic-stock" or "magic-exchange" or "junction-stock" ? SelectedEntry?.Id : null;
        _liveData = null;
        _view = null;
        if (selected is not null)
        {
            int index = Entries.ToList().FindIndex(entry => entry.Id == selected);
            if (index >= 0)
            {
                _selections[ScreenId] = index;
            }
        }
    }

    public void Close()
    {
        ClearBattleLearningReview();
        exitGame = null;
        if (ScreenId == "mode-review")
        {
            cancelMagicRules?.Invoke();
            cancelMagicRules = null;
            applyMagicRules = null;
            _liveData = null;
        }
        CancelControlsCapture();
        IsOpen = false;
        _nameTarget = null;
        _acceptName = null;
        _nameHighSurrogate = null;
        _view = null;
        _history.Clear();
        _characterSelectionDestination = "";
    }

    public void Navigate(MenuInput input)
    {
        if (!IsOpen || IsCapturingControls)
        {
            return;
        }
        if (NavigateModManager(input))
        {
            return;
        }
        if (NavigateGuardianEditor(input))
        {
            return;
        }
        if (NavigateNaming(input))
        {
            return;
        }
        if (TryNavigateMagazine(input))
        {
            return;
        }
        if (TryNavigateNativeTutorialBrowser(input))
        {
            return;
        }
        if (TryNavigateNativeTutorialExam(input))
        {
            return;
        }
        if (IsSelectingCharacter)
        {
            NavigateCharacterSelection(input);
            return;
        }
        if (ScreenId == "junction-gf-confirm" && input is MenuInput.PageLeft or MenuInput.PageRight)
        {
            return;
        }
        if (NavigateAbilityJunction(input))
        {
            return;
        }
        if (NavigateMagicJunction(input))
        {
            return;
        }
        if (NavigateNativeSwitch(input))
        {
            return;
        }
        var entries = Entries;
        if (IsConfigSettingsScreen && input is MenuInput.PageUp or MenuInput.PageDown)
        {
            Select(Math.Clamp(SelectedIndex + (input == MenuInput.PageUp ? -ConfigRowsPerPage : ConfigRowsPerPage), 0, Math.Max(0, entries.Count - 1)), entries.Count);
            return;
        }
        if (IsConfigSettingsScreen && input == MenuInput.Confirm && SelectedEntry?.Action?.Kind == "config" && MenuSettings.Definitions.FirstOrDefault(setting => setting.Id == SelectedEntry?.Id) is { } numericSetting && TryConfigNumbers(ResolveConfigSetting(numericSetting), out _))
        {
            return;
        }
        if (input is MenuInput.Left or MenuInput.Right && ChangeControlsSetting(input == MenuInput.Left ? -1 : 1))
        {
            return;
        }
        UiView view = BuildView();
        if (view.FocusedElement is UiList { Orientation: UiOrientation.Vertical, Paginated: true } list && !(ScreenId == "shop-junk" && input is MenuInput.Up or MenuInput.Down) &&
            (input is MenuInput.PageUp or MenuInput.PageDown || input is MenuInput.Left or MenuInput.Right && !IsConfigSettingsScreen && !IsHostSettingsScreen || list.Id != "commands" && input is MenuInput.Up or MenuInput.Down))
        {
            view.Key(input switch
            {
                MenuInput.Left => UiKey.Left,
                MenuInput.Right => UiKey.Right,
                MenuInput.Up => UiKey.Up,
                MenuInput.Down => UiKey.Down,
                MenuInput.PageUp => UiKey.PageUp,
                _ => UiKey.PageDown
            });
            return;
        }
        var columns = Math.Max(1, CurrentScreen.Columns);
        switch (input)
        {
            case MenuInput.Tab:
                BuildView().Key(UiKey.Tab);
                break;
            case MenuInput.PageUp:
            case MenuInput.PageDown:
                BuildView().Key(input == MenuInput.PageUp ? UiKey.PageUp : UiKey.PageDown);
                break;
            case MenuInput.Up:
                Select(SelectedIndex - columns, entries.Count);
                break;
            case MenuInput.Down:
                Select(SelectedIndex + columns, entries.Count);
                break;
            case MenuInput.Left:
                if (ChangeControlsSetting(-1))
                {
                }
                else if (IsConfigSettingsScreen || IsHostSettingsScreen || ScreenId == "quick-save-editor")
                {
                    ChangeSetting(-1);
                }
                else
                {
                    Select(SelectedIndex - 1, entries.Count);
                }
                break;
            case MenuInput.Right:
                if (ChangeControlsSetting(1))
                {
                }
                else if (IsConfigSettingsScreen || IsHostSettingsScreen || ScreenId == "quick-save-editor")
                {
                    ChangeSetting(1);
                }
                else
                {
                    Select(SelectedIndex + 1, entries.Count);
                }
                break;
            case MenuInput.PageLeft:
                ChangePageSubject(-1);
                break;
            case MenuInput.PageRight:
                ChangePageSubject(1);
                break;
            case MenuInput.Alternate:
                if (ScreenId is "gf-learn" or "gf-abilities" && SelectedEntry?.Action?.Kind == "gf-passive-toggle")
                {
                    Activate(SelectedIndex);
                }
                else if (IsControlsBindingList && SelectedEntry?.Action is { Kind: "controls-select" } controlsAction)
                {
                    Execute(controlsAction);
                    Push(ControlsChoiceScreen);
                    SoundRequested?.Invoke(MenuSound.Confirm);
                }
                else if (ScreenId.StartsWith("status", StringComparison.Ordinal))
                {
                    Push("status-limit");
                }
                break;
            case MenuInput.Confirm:
                if (_view?.Key(UiKey.Confirm) != true)
                {
                    Activate(SelectedIndex);
                }
                break;
            case MenuInput.Cancel:
                Back();
                break;
        }
    }

    private bool NavigateMagicJunction(MenuInput input)
    {
        if (ScreenId is not ("junction-magic" or "junction-element" or "junction-status") || input is not (MenuInput.Left or MenuInput.Right))
        {
            return false;
        }
        string destination = (ScreenId, input) switch
        {
            ("junction-magic", MenuInput.Left) => "junction-element",
            ("junction-element", MenuInput.Left) => "junction-status",
            ("junction-element", MenuInput.Right) => "junction-magic",
            ("junction-status", MenuInput.Right) => "junction-element",
            _ => ScreenId
        };
        if (destination != ScreenId)
        {
            ScreenId = destination;
            Message = "";
            _view = null;
            SoundRequested?.Invoke(MenuSound.Move);
            ScreenOpened?.Invoke(ScreenId);
        }
        return true;
    }

    public void Push(string screenId)
    {
        if (!_screens.ContainsKey(screenId))
        {
            SetSystemMessage(() => FormatSystemText("ui.push.message.screen.is.not.registered", "Screen \u0027{0}\u0027 is not registered.", screenId));
            return;
        }
        if (ScreenId == "debug-gallery")
        {
            if (!_screens[screenId].GalleryEligible)
            {
                SetSystemMessage(() => SystemText("ui.push.message.this.tool.has.no.isolated.fixture.adapter", "This tool has no isolated fixture adapter."));
                return;
            }
            IsFixtureContext = true;
            _demo ??= MenuDemoData.Create();
        }
        _history.Push(ScreenId);
        ScreenId = screenId;
        PrepareControlsScreen(screenId);
        InitializeScreenCursor(screenId);
        ScreenOpened?.Invoke(ScreenId);
        Message = "";
        _view = null;
    }

    public void Back()
    {
        if (ScreenId == "localization-review" && (localizationPreparation is not null || localizationWriting is not null))
        {
            localizationCancellation?.Cancel();
            localizationProgress = "Cancelling…";
            return;
        }
        if (ScreenId is "gf-ability-dirty" or "gf-ability-review")
        {
            guardianAfterDiscard = null;
        }
        if (BackFromBattleLearning())
        {
            return;
        }
        if (ScreenId == "junction-gf-confirm")
        {
            pendingGuardianForceTransfer = null;
        }
        if (ScreenId == "mode-review")
        {
            cancelMagicRules?.Invoke();
            cancelMagicRules = null;
            applyMagicRules = null;
            _liveData = null;
        }
        if (IsNamePrompt)
        {
            DeleteNameCharacter();
            return;
        }
        SoundRequested?.Invoke(MenuSound.Cancel);
        if (IsSelectingCharacter)
        {
            _characterSelectionDestination = "";
            Message = "";
            _view = null;
            return;
        }
        if (IsCapturingControls)
        {
            CancelControlsCapture();
            return;
        }
        if (_history.TryPop(out var previous))
        {
            ScreenId = previous;
            if (ScreenId == "debug-gallery")
            {
                IsFixtureContext = false;
            }
            PrepareControlsScreen(ScreenId);
            ScreenOpened?.Invoke(ScreenId);
            Message = "";
            _view = null;
        }
        else
        {
            Close();
        }
    }

    private void Select(int index, int count)
    {
        int next = count == 0 ? 0 : (index % count + count) % count;
        if (next != SelectedIndex)
        {
            SoundRequested?.Invoke(MenuSound.Move);
        }
        _selections[ScreenId] = next;
        RememberControlsSelection();
        if (ScreenId == "font-picker" && SelectedEntry?.Id.StartsWith("family:", StringComparison.Ordinal) == true)
        {
            selectedFontId = SelectedEntry.Id;
        }
        if (ScreenId == "mods" && SelectedEntry?.Action is { Kind: "mod-toggle" } modAction)
        {
            selectedModId = modAction.SubjectId;
            modSelectedOrdinal = Math.Max(0, Array.FindIndex(FilteredModViews(), view => view.Package.Id.Equals(selectedModId, StringComparison.OrdinalIgnoreCase)));
            modFocusedControlId = "commands";
        }
        else if (ScreenId == "mods" && SelectedEntry is { } modEntry)
        {
            modFocusedControlId = modEntry.Id;
        }
        _view = null;
    }

    private void BeginCharacterSelection(string destination)
    {
        IReadOnlyList<MenuCharacter> choices = CharacterChoices;
        if (choices.Count == 0)
        {
            SetSystemMessage(() => SystemText("ui.begincharacterselection.message.no.characters.are.available", "No characters are available."));
            return;
        }
        _characterSelectionDestination = destination;
        _characterSelectionIndex = Math.Max(0, Array.FindIndex(choices.ToArray(), character => character.Id == _characterId));
        SetSystemMessage(() => SystemText("ui.begincharacterselection.message.select.a.character", "Select a character."));
        _view = null;
    }

    private void NavigateCharacterSelection(MenuInput input)
    {
        IReadOnlyList<MenuCharacter> choices = CharacterChoices;
        if (input == MenuInput.Cancel)
        {
            Back();
            return;
        }
        if (choices.Count == 0)
        {
            SetSystemMessage(() => SystemText("ui.navigatecharacterselection.message.no.characters.are.available", "No characters are available."));
            return;
        }
        if (input == MenuInput.Confirm)
        {
            string? focusedId = _view?.FocusedElement?.Id;
            ChooseCharacter(choices.Any(character => character.Id == focusedId) ? focusedId! : choices[Math.Clamp(_characterSelectionIndex, 0, choices.Count - 1)].Id);
            return;
        }
        int direction = input switch
        {
            MenuInput.Up or MenuInput.Left or MenuInput.PageLeft => -1,
            MenuInput.Down or MenuInput.Right or MenuInput.PageRight or MenuInput.Tab => 1,
            _ => 0
        };
        if (direction == 0)
        {
            return;
        }
        int next = (_characterSelectionIndex + direction + choices.Count) % choices.Count;
        if (next != _characterSelectionIndex)
        {
            SoundRequested?.Invoke(MenuSound.Move);
        }
        _characterSelectionIndex = next;
        _view = null;
    }

    public void ChooseCharacter(string id)
    {
        if (!IsSelectingCharacter || !CharacterChoices.Any(character => character.Id == id))
        {
            return;
        }
        string destination = _characterSelectionDestination;
        _characterId = id;
        _magicId = "";
        _characterSelectionDestination = "";
        Push(destination);
        SoundRequested?.Invoke(MenuSound.Confirm);
    }

    private void ChangePageSubject(int delta)
    {
        if (ScreenId is "gf-learn" or "gf-abilities")
        {
            var guardianForces = Data.GuardianForces;
            if (guardianForces.Count == 0)
            {
                return;
            }
            int guardianForceIndex = guardianForces.FindIndex(force => force.Id == GuardianForce?.Id);
            _gfId = guardianForces[(guardianForceIndex + delta + guardianForces.Count) % guardianForces.Count].Id;
            _selections[ScreenId] = Math.Clamp(SelectedIndex, 0, Math.Max(0, Entries.Count - 1));
            FocusBattleLearnedAbility();
            _view = null;
            return;
        }
        var characters = Data.Characters;
        if (characters.Count == 0)
        {
            SetSystemMessage(() => SystemText("ui.changepagesubject.message.character.data.is.unavailable", "Character data is unavailable."));
            return;
        }
        var index = characters.FindIndex(c => c.Id == Character?.Id);
        _characterId = characters[(index + delta + characters.Count) % characters.Count].Id;
        _magicId = "";
        if (!IsDemo)
        {
            _abilitySlot = Math.Clamp(_abilitySlot, 0, Math.Max(0, (Character?.AbilitySlots ?? 2) - 1));
        }
        _view = null;
    }

    private void Activate(int index)
    {
        if (IsSelectingCharacter)
        {
            return;
        }
        var entries = Entries;
        if (index < 0 || index >= entries.Count)
        {
            return;
        }
        _selections[ScreenId] = index;
        var entry = entries[index];
        bool captureBinding = IsControlsBindingList && entry.Action?.Kind == "controls-select";
        if (!entry.Enabled)
        {
            Message = entry.Help;
            if (ScreenId.StartsWith("ability", StringComparison.Ordinal))
            {
                SoundRequested?.Invoke(MenuSound.Error);
            }
            _view = null;
            return;
        }
        if (ScreenId == "main" && (entry.Destination is "junction" or "status" || entry.Destination == "magic" && !Data.SharedMagicStock))
        {
            BeginCharacterSelection(entry.Destination);
            SoundRequested?.Invoke(MenuSound.Confirm);
            return;
        }
        if (entry.Action != null)
        {
            Execute(entry.Action);
        }
        if (entry.Destination != null)
        {
            Push(entry.Destination);
        }
        if (captureBinding)
        {
            Execute(new("controls-capture", Value: "return-to-list"));
        }
        if (entry.Action?.Kind is not ("menu-back" or "name-save"))
        {
            SoundRequested?.Invoke(MenuSound.Confirm);
        }
        _view = null;
    }

    private void ChangeSetting(int direction)
    {
        if (ScreenId == "quick-save-editor" && SelectedEntry?.Id == "quick-save-value")
        {
            ChangeQuickSaveDraft(direction);
            return;
        }
        if (SelectedEntry?.Enabled != true)
        {
            return;
        }
        var setting = MenuSettings.Definitions.FirstOrDefault(s => s.Id == SelectedEntry?.Id);
        if (setting == null)
        {
            return;
        }
        if (IsConfigSettingsScreen)
        {
            setting = ResolveConfigSetting(setting);
        }
        if (setting.Id is "UI font" or "Dialogue font" or "Quick saves")
        {
            return;
        }
        var current = IsHostSettingsScreen || setting.Id == "UI font" && (IsDemo || _readPreferences is not null) ? HostSettingValue(setting.Id) : Data.Settings.GetValueOrDefault(setting.Id);
        if (IsConfigSettingsScreen && TryConfigNumbers(setting, out float[] numbers))
        {
            float number = float.TryParse(current?.TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture, out float parsed) ? parsed : numbers[0];
            if (setting.Id is "Music volume" or "Effect volume" or "Voice volume")
            {
                SetConfigValue(setting, number + direction);
                return;
            }
            float next = direction > 0 ? numbers.FirstOrDefault(candidate => candidate > number, numbers[^1]) : numbers.LastOrDefault(candidate => candidate < number, numbers[0]);
            SetConfigValue(setting, next);
            SoundRequested?.Invoke(MenuSound.Move);
            return;
        }
        var index = Array.IndexOf(setting.Values, current);
        Execute(new MenuAction(IsHostSettingsScreen ? "host-config" : "config", setting.Id, Value: setting.Values[(index + direction + setting.Values.Length) % setting.Values.Length]));
        SoundRequested?.Invoke(MenuSound.Move);
    }

    internal string PendingAngeloMove { get; private set; } = "";

    public void Execute(MenuAction action)
    {
        _view = null;
        if (TryHandleLocalizationAction(action))
        {
            return;
        }
        if (action.Kind == "angelo-confirm")
        {
            PendingAngeloMove = action.SubjectId;
            Push("status-angelo-warning");
            SelectEntry("status-angelo-warning", "no");
            return;
        }
        if (action.Kind == "angelo-cancel")
        {
            Back();
            return;
        }
        if (ExecuteHardcoreConfirmation(ref action))
        {
            return;
        }
        if (ExecuteChocoboWorldAction(action))
        {
            return;
        }
        if (ExecuteFixtureAction(action) || ExecuteFontPickerAction(action) || ExecuteQuickSaveRetentionAction(action) || ExecuteGameplayTuningAction(action))
        {
            return;
        }
        if (ExecuteGuardianForceTransfer(action))
        {
            return;
        }
        if (ExecuteTitleExitAction(action))
        {
            return;
        }
        if (ExecuteMagicRulesAction(action))
        {
            return;
        }
        if (TryExecuteNativeTutorialBrowser(action))
        {
            return;
        }
        if (TryExecuteNativeTutorialExam(action))
        {
            return;
        }
        if (ExecuteGuardianEditorAction(action))
        {
            return;
        }
        if (ExecuteModAction(action))
        {
            return;
        }
        if (ExecuteNamingAction(action))
        {
            return;
        }
        if (ExecuteHostSettingsAction(action))
        {
            return;
        }
        if (ExecuteAutoPlayAction(action))
        {
            return;
        }
        if (ExecuteControlsAction(action))
        {
            return;
        }
        if (ExecuteModelBrowserAction(action) || ExecuteSceneInspectorAction(action))
        {
            return;
        }
        switch (action.Kind)
        {
            case "open-battle-debug":
                if (BattleDebugRequested is null)
                {
                    SetSystemMessage(() => SystemText("ui.execute.message.the.battle.laboratory.is.not.connected.to.this", "The battle laboratory is not connected to this menu host."));
                    return;
                }
                BattleDebugRequested.Invoke();
                return;
            case "menu-back":
                Back();
                return;
            case "select-character":
                _characterId = action.SubjectId;
                _magicId = "";
                return;
            case "select-gf":
                _gfId = action.SubjectId;
                return;
            case "select-item":
                _itemId = action.SubjectId;
                if (!IsDemo && ScreenId == "item-use" && Item?.NativeMagazinePages.Length > 0)
                {
                    ShowMagazinePage(0);
                }
                return;
            case "magazine-page":
                ShowMagazinePage(_magazineIndex + int.Parse(action.Value, System.Globalization.CultureInfo.InvariantCulture));
                return;
            case "select-magic":
                _magicId = action.SubjectId;
                return;
            case "select-card":
                _cardId = action.SubjectId;
                return;
            case "select-refinement-ability":
                _refinementAbility = action.SubjectId;
                _recipeId = Data.Recipes.FirstOrDefault(recipe => recipe.Ability == _refinementAbility)?.Id ?? "";
                return;
            case "select-recipe":
                _recipeId = action.SubjectId;
                _refinementAbility = Recipe?.Ability ?? "";
                return;
            case "select-exchange":
                _exchangeId = action.SubjectId;
                return;
            case "select-slot":
                _junctionSlot = action.SubjectId;
                return;
            case "select-save-slot":
                _saveSlotId = action.SubjectId;
                return;
            case "select-battle-command":
                _battleCommand = action.SubjectId;
                return;
            case "select-battle-gf":
                _battleCommand = "GF";
                _gfId = action.SubjectId;
                return;
            case "select-battle-target":
                _battleTargetId = action.SubjectId;
                return;
            case "select-command-slot":
                _commandSlot = int.Parse(action.SubjectId);
                return;
            case "select-ability-slot":
                _abilitySlot = int.Parse(action.SubjectId);
                return;
            case "test-answer":
                if (!IsDemo)
                {
                    SetSystemMessage(() => SystemText("ui.execute.message.the.original.seed.test.bank.and.rank.mutation", "The original SeeD test bank and rank mutation contract are not available."));
                    return;
                }
                if (action.Value == (_testQuestion % 2 == 0 ? "Yes" : "No"))
                {
                    _testCorrect++;
                }
                _testQuestion++;
                SetSystemMessage(() => _testQuestion >= 10 ? FormatSystemText("ui.execute.message.fixture.completed.10.no.native.seed.rank.changed", "Fixture completed: {0}/10. No native SeeD rank changed.", _testCorrect) : FormatSystemText("ui.execute.message.fixture.answer.recorded.10", "Fixture answer recorded ({0}/10).", _testQuestion));
                return;
            case "test-reset":
                _testQuestion = 0;
                _testCorrect = 0;
                return;
            case "demo-reset":
                if (IsDemo)
                {
                    _demo = MenuDemoData.Create();
                    SetSystemMessage(() => SystemText("ui.execute.message.all.synthetic.fixture.changes.reset", "All synthetic fixture changes reset."));
                }
                return;
        }
        if (!IsDemo)
        {
            if (action.Kind is "magic-use" or "magic-transfer")
            {
                if (Magic is not { Quantity: > 0 } selectedMagic)
                {
                    SetSystemMessage(() => SystemText("ui.execute.message.selected.magic.stock.is.no.longer.available.select", "Selected magic stock is no longer available. Select a spell again."));
                    return;
                }
                action = action with { SubjectId = selectedMagic.Id, TargetId = action.Kind == "magic-transfer" && action.TargetId.Length == 0 ? _exchangeId : action.TargetId };
                if (Character?.CurrentHp is null || Data.Characters.FirstOrDefault(character => character.Id == action.TargetId)?.CurrentHp is null)
                {
                    SetSystemMessage(() => SystemText("ui.execute.message.magic.requires.exact.saved.hp.values.for.the", "Magic requires exact saved HP values for the selected characters."));
                    return;
                }
            }
            var supported = Data.SupportedActions.Contains(action.Kind) || Data.SupportedActions.Contains($"{action.Kind}:{action.SubjectId}");
            if (!supported || _liveAction == null)
            {
                SetSystemMessage(() => FormatSystemText("ui.execute.message.unsupported.native.contract.this.view.remains.read.only", "Unsupported native contract: {0}. This view remains read-only.", action.Kind));
                return;
            }
            if (action.Kind is "junction-gf" or "junction-gf-reassign" or "junction-command" or "junction-ability" or "junction-magic" or "junction-off" or "junction-auto" && Character is null)
            {
                SetSystemMessage(() => SystemText("ui.execute.message.selected.character.data.is.unavailable", "Selected character data is unavailable."));
                return;
            }
            action = action.Kind switch
            {
                "party-switch" => action with { SubjectId = _exchangeId },
                "item-use" => action with { SubjectId = Item?.Id ?? "" },
                "junction-gf" => action with { TargetId = Character?.Id ?? "" },
                "junction-off" => action with { TargetId = Character?.Id ?? "" },
                "junction-auto" => action with { TargetId = Character?.Id ?? "" },
                "attack-mode" => action with { TargetId = Character?.Id ?? "" },
                "junction-command" => action with { TargetId = Character?.Id ?? "", Value = (_commandSlot - 1).ToString(System.Globalization.CultureInfo.InvariantCulture) },
                "junction-ability" => action with { TargetId = Character?.Id ?? "", Value = _abilitySlot.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                "junction-magic-reassign" => action with { TargetId = Character?.Id ?? "", Value = _junctionSlot },
                "junction-magic" => action with { TargetId = Character?.Id ?? "", Value = _junctionSlot },
                "gf-learn" or "gf-passive-toggle" => action with { TargetId = GuardianForce?.Id ?? "" },
                _ => action
            };
            _liveAction(action);
            _liveData = null;
            if (action.Kind == "angelo-learn" && ScreenId == "status-angelo-warning")
            {
                Back();
            }
            if (action.Kind == "native-refine" && ScreenId == "ability-recipes")
            {
                _selections[ScreenId] = Math.Clamp(SelectedIndex, 0, Math.Max(0, Entries.Count - 1));
            }
            if (action.Kind == "item-use" && !Data.Items.Any(item => item.Id == action.SubjectId && item.Quantity > 0))
            {
                while (_history.Count > 0 && _history.Peek() != "item-use")
                {
                    _history.Pop();
                }
                if (_history.Count > 0)
                {
                    _history.Pop();
                }
                ScreenId = "item-use";
                _itemId = "";
            }
            if (action.Kind == "party-switch")
            {
                ScreenId = _history.TryPop(out string? sourceScreen) ? sourceScreen : "switch-party";
                ScreenOpened?.Invoke(ScreenId);
            }
            if (action.Kind is "junction-magic" or "junction-auto")
            {
                ScreenId = _history.TryPop(out string? previous) ? previous : action.Kind == "junction-auto" ? "junction" : "junction-magic";
                ScreenOpened?.Invoke(ScreenId);
                _view = null;
            }
            Message = "";
            return;
        }
        ApplyDemo(action);
    }

    private void ApplyDemo(MenuAction action)
    {
        var data = Data;
        var character = Character;
        var targetId = action.Kind == "magic-transfer" && action.TargetId.Length == 0 ? _exchangeId : action.TargetId;
        var target = data.Characters.FirstOrDefault(c => c.Id == targetId);
        switch (action.Kind)
        {
            case "battle-preview":
                SetSystemMessage(() => FormatSystemText("ui.applydemo.message.fixture.target.preview.no.combat.effect.was.executed", "Fixture {0} target preview: {1}. No combat effect was executed.", action.Value, data.BattleTargets.FirstOrDefault(t => t.Id == action.TargetId)?.Name ?? target?.Name ?? action.TargetId));
                break;
            case "battle-draw":
                if (action.Value == "Stock" && Magic is { } drawn)
                {
                    var quantity = Math.Min(3, 100 - drawn.Quantity);
                    drawn.Quantity += quantity;
                    SetSystemMessage(() => FormatSystemText("ui.applydemo.message.fixture.stocked.native.draw.formulas.are.unsupported", "Fixture stocked {0} {1}; native Draw formulas are unsupported.", quantity, drawn.Name));
                }
                else
                {
                    SetSystemMessage(() => SystemText("ui.applydemo.message.fixture.cast.preview.selected.no.combat.spell.was", "Fixture Cast preview selected; no combat spell was executed."));
                }
                break;
            case "card-mod":
                if (Card is { Quantity: > 0, RefinementItemId: not null, RefinementQuantity: > 0 } card)
                {
                    var output = data.Items.FirstOrDefault(i => i.Id == card.RefinementItemId);
                    if (output != null)
                    {
                        card.Quantity--;
                        output.Quantity += card.RefinementQuantity.Value;
                        SetSystemMessage(() => FormatSystemText("ui.applydemo.message.fixture.card.mod.produced.original.card.recipes.are", "Fixture Card Mod produced {0} {1}; original card recipes are not inferred.", card.RefinementQuantity, output.Name));
                    }
                }
                break;
            case "load":
                SetSystemMessage(() => SystemText("ui.applydemo.message.fixture.load.confirmation.accepted.no.native.save.or", "Fixture load confirmation accepted. No native save or game state was loaded."));
                break;
            case "limit-setting":
                var limitOwner = target ?? character;
                if (limitOwner != null)
                {
                    limitOwner.LimitSettings[action.SubjectId] = action.Value;
                    if (action.SubjectId is "Auto" or "Indicator" or "Duel-Auto" or "Angel Wing")
                    {
                        limitOwner.LimitToggles[action.SubjectId] = action.Value == "On";
                    }
                    SetSystemMessage(() => FormatSystemText("ui.applydemo.message.fixture", "Fixture {0} {1}: {2}.", limitOwner.Name, action.SubjectId, action.Value));
                }
                break;
            case "slot-do-over":
                var slotOwner = target ?? character;
                if (slotOwner is { SlotPreview.Count: > 0 })
                {
                    var preview = int.TryParse(slotOwner.LimitSettings.GetValueOrDefault("Preview index"), out var oldPreview) ? oldPreview : 0;
                    slotOwner.LimitSettings["Preview index"] = ((preview + 1) % slotOwner.SlotPreview.Count).ToString();
                    SetSystemMessage(() => SystemText("ui.applydemo.message.fixture.slot.preview.advanced.original.random.outcomes.are", "Fixture Slot preview advanced; original random outcomes are not simulated."));
                }
                break;
            case "slot-select":
                SetSystemMessage(() => SystemText("ui.applydemo.message.fixture.spell.preview.selected.no.battle.spell.was", "Fixture spell preview selected. No battle spell was cast."));
                break;
            case "config":
                data.Settings[action.SubjectId] = action.Value;
                SetSystemMessage(() => FormatSystemText("ui.applydemo.message.fixture.alternate", "Fixture {0}: {1}", action.SubjectId, action.Value));
                break;
            case "item-use":
                if (Item is { Quantity: > 0 } item && target != null)
                {
                    if (item.DemoHealing > 0)
                    {
                        target.CurrentHp = Math.Min(target.MaximumHp ?? 0, (target.CurrentHp ?? 0) + item.DemoHealing);
                        item.Quantity--;
                        SetSystemMessage(() => FormatSystemText("ui.applydemo.message.fixture.restored.s.hp", "Fixture: {0} restored {1}\u0027s HP.", item.Name, target.Name));
                    }
                    else if (item.Name is "Antidote" or "Remedy")
                    {
                        target.Conditions.Clear();
                        item.Quantity--;
                        SetSystemMessage(() => FormatSystemText("ui.applydemo.message.fixture.conditions.cleared.on", "Fixture: conditions cleared on {0}.", target.Name));
                    }
                    else
                    {
                        SetSystemMessage(() => SystemText("ui.applydemo.message.this.fixture.item.has.no.simulated.field.effect", "This fixture item has no simulated field effect."));
                    }
                }
                break;
            case "item-sort":
                data.Items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
                SetSystemMessage(() => SystemText("ui.applydemo.message.fixture.inventory.sorted.by.name", "Fixture inventory sorted by name."));
                break;
            case "item-move":
                var sourceIndex = data.Items.FindIndex(i => i.Id == _itemId);
                var targetIndex = data.Items.FindIndex(i => i.Id == action.TargetId);
                if (sourceIndex >= 0 && targetIndex >= 0)
                {
                    (data.Items[sourceIndex], data.Items[targetIndex]) = (data.Items[targetIndex], data.Items[sourceIndex]);
                    SetSystemMessage(() => SystemText("ui.applydemo.message.fixture.item.positions.exchanged", "Fixture item positions exchanged."));
                }
                break;
            case "magic-use":
                if (Magic is { Quantity: > 0, DemoHealing: > 0 } magic && target != null)
                {
                    target.CurrentHp = Math.Min(target.MaximumHp ?? 0, (target.CurrentHp ?? 0) + magic.DemoHealing);
                    magic.Quantity--;
                    SetSystemMessage(() => FormatSystemText("ui.applydemo.message.fixture.cast.on", "Fixture: cast {0} on {1}.", magic.Name, target.Name));
                }
                else
                {
                    SetSystemMessage(() => SystemText("ui.applydemo.message.this.fixture.spell.has.no.simulated.field.effect", "This fixture spell has no simulated field effect."));
                }
                break;
            case "magic-transfer":
                if (Magic is { Quantity: > 0 } stock && target != null && target.Id != stock.CharacterId)
                {
                    var destination = data.Magic.FirstOrDefault(m => m.CharacterId == target.Id && m.Id == stock.Id);
                    if (destination != null)
                    {
                        var requested = int.TryParse(action.Value, out var amount) ? Math.Max(0, amount) : stock.Quantity;
                        var quantity = Math.Min(requested, Math.Min(stock.Quantity, 100 - destination.Quantity));
                        stock.Quantity -= quantity;
                        destination.Quantity += quantity;
                        SetSystemMessage(() => FormatSystemText("ui.applydemo.message.fixture.transferred.to", "Fixture: transferred {0} {1} to {2}.", quantity, stock.Name, target.Name));
                    }
                }
                break;
            case "magic-sort":
                data.Magic.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
                SetSystemMessage(() => SystemText("ui.applydemo.message.fixture.magic.stocks.sorted.by.name", "Fixture magic stocks sorted by name."));
                break;
            case "junction-gf-reassign":
            case "junction-gf":
                if (character != null)
                {
                    if (!character.GuardianForceIds.Remove(action.SubjectId))
                    {
                        foreach (var owner in data.Characters)
                        {
                            owner.GuardianForceIds.Remove(action.SubjectId);
                        }
                        character.GuardianForceIds.Add(action.SubjectId);
                    }
                    SetSystemMessage(() => SystemText("ui.applydemo.message.fixture.gf.ownership.toggled.native.junction.effects.are", "Fixture GF ownership toggled. Native junction effects are not calculated."));
                }
                break;
            case "junction-magic":
                if (character != null)
                {
                    character.Junctions[_junctionSlot] = action.Value;
                    SetSystemMessage(() => FormatSystemText("ui.applydemo.message.fixture.junction.native.stat.effects.are.not.calculated", "Fixture {0} junction: {1}. Native stat effects are not calculated.", _junctionSlot, action.Value));
                }
                break;
            case "junction-off":
                if (character != null)
                {
                    character.Junctions.Clear();
                    if (action.Value == "All")
                    {
                        character.GuardianForceIds.Clear();
                    }
                    SetSystemMessage(() => SystemText("ui.applydemo.message.fixture.junctions.removed", "Fixture junctions removed."));
                }
                break;
            case "junction-auto":
                if (character != null)
                {
                    var slot = action.Value == "Magic" ? "Mag" : action.Value == "Defense" ? "Vit" : "Str";
                    character.Junctions[slot] = "Ultima";
                    SetSystemMessage(() => FormatSystemText("ui.applydemo.message.fixture.assigned.ultima.original.auto.optimization.is.unsupported", "Fixture {0} assigned Ultima; original Auto optimization is unsupported.", slot));
                }
                break;
            case "junction-command":
                if (character != null)
                {
                    var slot = _commandSlot;
                    while (character.Commands.Count <= slot)
                    {
                        character.Commands.Add("Empty");
                    }
                    character.Commands[slot] = action.Value;
                    SetSystemMessage(() => FormatSystemText("ui.applydemo.message.fixture.command.slot", "Fixture command slot {0}: {1}.", slot + 1, action.Value));
                }
                break;
            case "junction-ability":
                if (character != null)
                {
                    while (character.Abilities.Count <= _abilitySlot)
                    {
                        character.Abilities.Add("Empty");
                    }
                    character.Abilities[_abilitySlot] = action.Value;
                    SetSystemMessage(() => FormatSystemText("ui.applydemo.message.fixture.ability.slot", "Fixture ability slot {0}: {1}.", _abilitySlot + 1, action.Value));
                }
                break;
            case "gf-learn":
                if (GuardianForce != null)
                {
                    GuardianForce.Learning = action.Value;
                    GuardianForce.LearningAbilityId = action.SubjectId;
                    SetSystemMessage(() => FormatSystemText("ui.applydemo.message.fixture.is.learning", "Fixture {0} is learning {1}.", GuardianForce.Name, action.Value));
                }
                break;
            case "party-switch":
                var source = data.Characters.FirstOrDefault(c => c.Id == _exchangeId);
                if (source != null && target != null)
                {
                    (source.InParty, target.InParty) = (target.InParty, source.InParty);
                    SetSystemMessage(() => FormatSystemText("ui.applydemo.message.fixture.party.membership.exchanged", "Fixture party membership exchanged: {0} / {1}.", source.Name, target.Name));
                }
                break;
            case "junction-exchange":
                var other = data.Characters.FirstOrDefault(c => c.Id == _exchangeId);
                if (other != null && target != null)
                {
                    var oldJunctions = other.Junctions.ToArray();
                    other.Junctions.Clear();
                    foreach (var pair in target.Junctions)
                    {
                        other.Junctions[pair.Key] = pair.Value;
                    }
                    target.Junctions.Clear();
                    foreach (var pair in oldJunctions)
                    {
                        target.Junctions[pair.Key] = pair.Value;
                    }
                    var oldGfs = other.GuardianForceIds.ToArray();
                    other.GuardianForceIds.Clear();
                    other.GuardianForceIds.AddRange(target.GuardianForceIds);
                    target.GuardianForceIds.Clear();
                    target.GuardianForceIds.AddRange(oldGfs);
                    var oldCommands = other.Commands.ToArray();
                    other.Commands.Clear();
                    other.Commands.AddRange(target.Commands);
                    target.Commands.Clear();
                    target.Commands.AddRange(oldCommands);
                    var oldAbilities = other.Abilities.ToArray();
                    other.Abilities.Clear();
                    other.Abilities.AddRange(target.Abilities);
                    target.Abilities.Clear();
                    target.Abilities.AddRange(oldAbilities);
                    foreach (var spell in data.Magic)
                    {
                        if (spell.CharacterId == other.Id)
                        {
                            spell.CharacterId = target.Id;
                        }
                        else if (spell.CharacterId == target.Id)
                        {
                            spell.CharacterId = other.Id;
                        }
                    }
                    SetSystemMessage(() => SystemText("ui.applydemo.message.fixture.gf.magic.command.ability.and.stat.junctions", "Fixture GF, magic, command, ability and stat junctions exchanged."));
                }
                break;
            case "refine":
                if (Recipe is { } recipe && character != null)
                {
                    var ingredient = data.Items.FirstOrDefault(i => i.Id == recipe.IngredientId);
                    var result = data.Magic.FirstOrDefault(m => m.Id == recipe.ResultId && m.CharacterId == character.Id);
                    if (ingredient != null && result != null)
                    {
                        var available = Math.Min(ingredient.Quantity / recipe.IngredientQuantity, (100 - result.Quantity) / recipe.ResultQuantity);
                        var requested = action.Value == "All" ? available : int.TryParse(action.Value, out var amount) ? Math.Max(0, amount) : 1;
                        var count = Math.Min(requested, available);
                        if (count > 0)
                        {
                            ingredient.Quantity -= recipe.IngredientQuantity * count;
                            result.Quantity += recipe.ResultQuantity * count;
                            SetSystemMessage(() => FormatSystemText("ui.applydemo.message.fixture.recipe.produced", "Fixture recipe \u00D7{0} produced {1} {2}.", count, recipe.ResultQuantity * count, recipe.ResultName));
                        }
                        else
                        {
                            SetSystemMessage(() => SystemText("ui.applydemo.message.fixture.recipe.lacks.ingredients.or.exceeds.the.100", "Fixture recipe lacks ingredients or exceeds the 100 stock capacity."));
                        }
                    }
                    else
                    {
                        SetSystemMessage(() => SystemText("ui.applydemo.message.fixture.recipe.lacks.ingredients.or.exceeds.the.100", "Fixture recipe lacks ingredients or exceeds the 100 stock capacity."));
                    }
                }
                break;
            case "save":
                var save = data.SaveSlots.FirstOrDefault(s => s.Id == action.SubjectId);
                if (save != null)
                {
                    save.Summary = $"DEMO {character?.Name} · {data.Location}";
                    SetSystemMessage(() => SystemText("ui.applydemo.message.fixture.slot.updated.in.memory.no.game.save", "Fixture slot updated in memory. No game save was written."));
                }
                break;
            case "shop-buy":
            case "shop-sell":
                var shopItem = data.Items.FirstOrDefault(i => i.Id == action.SubjectId);
                if (shopItem != null && shopItem.Price.HasValue && data.Currency.HasValue)
                {
                    if (action.Kind == "shop-buy" && data.Currency >= shopItem.Price)
                    {
                        data.Currency -= shopItem.Price;
                        shopItem.Quantity++;
                        SetSystemMessage(() => FormatSystemText("ui.applydemo.message.fixture.bought.one", "Fixture bought one {0}.", shopItem.Name));
                    }
                    else if (action.Kind == "shop-sell" && shopItem.Quantity > 0)
                    {
                        data.Currency += shopItem.Price / 2;
                        shopItem.Quantity--;
                        SetSystemMessage(() => FormatSystemText("ui.applydemo.message.fixture.sold.one", "Fixture sold one {0}.", shopItem.Name));
                    }
                }
                break;
            default:
                SetSystemMessage(() => FormatSystemText("ui.applydemo.message.unsupported.contract.no.fixture.mutation.is.defined", "Unsupported contract: {0}. No fixture mutation is defined.", action.Kind));
                break;
        }
    }

    public static string Number(int? value) => value?.ToString() ?? "?";
    public static string Hp(MenuCharacter character) => $"{character.HpDisplay ?? Number(character.CurrentHp)} / {Number(character.MaximumHp)}";

}
