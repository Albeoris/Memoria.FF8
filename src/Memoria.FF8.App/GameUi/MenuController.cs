using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed class MenuController
{
    private readonly Func<MenuData>? _liveProvider;
    private readonly Action<MenuAction>? _liveAction;
    private readonly Dictionary<string, MenuScreenDefinition> _screens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _selections = new(StringComparer.Ordinal);
    private readonly Stack<string> _history = new();
    private MenuData? _demo;
    private MenuData? _liveData;
    private string _characterId = "";
    private string _gfId = "";
    private string _itemId = "";
    private string _magicId = "";
    private string _cardId = "";
    private string _recipeId = "";
    private string _exchangeId = "";
    private string _junctionSlot = "HP";
    private string _mappingId = "Confirm";
    private int _commandSlot = 1;
    private int _abilitySlot;
    private UiView? _view;
    private int _testQuestion;
    private int _testCorrect;

    public MenuController(Func<MenuData>? liveProvider = null, Action<MenuAction>? liveAction = null)
    {
        _liveProvider = liveProvider;
        _liveAction = liveAction;
        MenuCatalog.Register(this);
    }

    public bool IsOpen { get; private set; }
    public bool IsDemo { get; private set; }
    public string ScreenId { get; private set; } = "main";
    public string Message { get; private set; } = "";
    public MenuData Data => IsDemo ? _demo! : _liveData ??= _liveProvider?.Invoke() ?? new MenuData();
    public IReadOnlyCollection<MenuScreenDefinition> Definitions => _screens.Values;
    public IReadOnlyList<string> ScreenIds => _screens.Keys.ToArray();
    public MenuScreenDefinition CurrentScreen => _screens[ScreenId];
    public int SelectedIndex => _selections.GetValueOrDefault(ScreenId);
    public IReadOnlyList<MenuEntry> Entries => CurrentScreen.Entries(this);
    public MenuEntry? SelectedEntry => Entries.Count == 0 ? null : Entries[Math.Clamp(SelectedIndex, 0, Entries.Count - 1)];
    public MenuCharacter? Character => Data.Characters.FirstOrDefault(c => c.Id == _characterId) ?? Data.Characters.FirstOrDefault(c => c.InParty) ?? Data.Characters.FirstOrDefault();
    public MenuGuardianForce? GuardianForce => Data.GuardianForces.FirstOrDefault(g => g.Id == _gfId) ?? Data.GuardianForces.FirstOrDefault();
    public MenuItem? Item => Data.Items.FirstOrDefault(i => i.Id == _itemId) ?? Data.Items.FirstOrDefault();
    public MenuMagic? Magic => Data.Magic.FirstOrDefault(m => m.Id == _magicId && m.CharacterId == Character?.Id) ?? Data.Magic.FirstOrDefault(m => m.CharacterId == Character?.Id);
    public MenuCard? Card => Data.Cards.FirstOrDefault(c => c.Id == _cardId) ?? Data.Cards.FirstOrDefault();
    public MenuRecipe? Recipe => Data.Recipes.FirstOrDefault(r => r.Id == _recipeId) ?? Data.Recipes.FirstOrDefault();
    public string JunctionSlot => _junctionSlot;
    public string MappingId => _mappingId;
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
        if (!_screens.ContainsKey(screenId))
        {
            throw new ArgumentException($"Unknown menu screen '{screenId}'.", nameof(screenId));
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
        IsOpen = true;
        ScreenId = screenId;
        _history.Clear();
        Message = "";
        _view = null;
    }

    public void Close()
    {
        IsOpen = false;
        _view = null;
        _history.Clear();
    }

    public void Navigate(MenuInput input)
    {
        if (!IsOpen)
        {
            return;
        }
        var entries = Entries;
        var columns = Math.Max(1, CurrentScreen.Columns);
        switch (input)
        {
            case MenuInput.Up:
                Select(SelectedIndex - columns, entries.Count);
                break;
            case MenuInput.Down:
                Select(SelectedIndex + columns, entries.Count);
                break;
            case MenuInput.Left:
                if (ScreenId == "config")
                {
                    ChangeSetting(-1);
                }
                else
                {
                    Select(SelectedIndex - 1, entries.Count);
                }
                break;
            case MenuInput.Right:
                if (ScreenId == "config")
                {
                    ChangeSetting(1);
                }
                else
                {
                    Select(SelectedIndex + 1, entries.Count);
                }
                break;
            case MenuInput.PageLeft:
                ChangeCharacter(-1);
                break;
            case MenuInput.PageRight:
                ChangeCharacter(1);
                break;
            case MenuInput.Alternate:
                if (ScreenId.StartsWith("status", StringComparison.Ordinal))
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

    public void Push(string screenId)
    {
        if (!_screens.ContainsKey(screenId))
        {
            Message = $"Screen '{screenId}' is not registered.";
            return;
        }
        _history.Push(ScreenId);
        ScreenId = screenId;
        Message = "";
        _view = null;
    }

    public void Back()
    {
        if (_history.TryPop(out var previous))
        {
            ScreenId = previous;
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
        _selections[ScreenId] = count == 0 ? 0 : (index % count + count) % count;
        _view = null;
    }

    private void ChangeCharacter(int delta)
    {
        var characters = Data.Characters;
        if (characters.Count == 0)
        {
            Message = "Character data is unavailable.";
            return;
        }
        var index = characters.FindIndex(c => c.Id == Character?.Id);
        _characterId = characters[(index + delta + characters.Count) % characters.Count].Id;
        _view = null;
    }

    private void Activate(int index)
    {
        var entries = Entries;
        if (index < 0 || index >= entries.Count)
        {
            return;
        }
        _selections[ScreenId] = index;
        var entry = entries[index];
        if (!entry.Enabled)
        {
            Message = entry.Help;
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
        _view = null;
    }

    private void ChangeSetting(int direction)
    {
        var setting = MenuSettings.Definitions.FirstOrDefault(s => s.Id == SelectedEntry?.Id);
        if (setting == null)
        {
            return;
        }
        var current = Data.Settings.GetValueOrDefault(setting.Id);
        var index = Array.IndexOf(setting.Values, current);
        Execute(new MenuAction("config", setting.Id, Value: setting.Values[(index + direction + setting.Values.Length) % setting.Values.Length]));
    }

    public void Execute(MenuAction action)
    {
        _view = null;
        switch (action.Kind)
        {
            case "select-character":
                _characterId = action.SubjectId;
                return;
            case "select-gf":
                _gfId = action.SubjectId;
                return;
            case "select-item":
                _itemId = action.SubjectId;
                return;
            case "select-magic":
                _magicId = action.SubjectId;
                return;
            case "select-card":
                _cardId = action.SubjectId;
                return;
            case "select-recipe":
                _recipeId = action.SubjectId;
                return;
            case "select-exchange":
                _exchangeId = action.SubjectId;
                return;
            case "select-slot":
                _junctionSlot = action.SubjectId;
                return;
            case "select-mapping":
                _mappingId = action.SubjectId;
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
                    Message = "The original SeeD test bank and rank mutation contract are not available.";
                    return;
                }
                if (action.Value == (_testQuestion % 2 == 0 ? "Yes" : "No"))
                {
                    _testCorrect++;
                }
                _testQuestion++;
                Message = _testQuestion >= 10 ? $"Fixture completed: {_testCorrect}/10. No native SeeD rank changed." : $"Fixture answer recorded ({_testQuestion}/10).";
                return;
            case "test-reset":
                _testQuestion = 0;
                _testCorrect = 0;
                return;
            case "demo-reset":
                if (IsDemo)
                {
                    _demo = MenuDemoData.Create();
                    Message = "All synthetic fixture changes reset.";
                }
                return;
        }
        if (!IsDemo)
        {
            var supported = Data.SupportedActions.Contains(action.Kind) || Data.SupportedActions.Contains($"{action.Kind}:{action.SubjectId}");
            if (!supported || _liveAction == null)
            {
                Message = $"Unsupported native contract: {action.Kind}. This view remains read-only.";
                return;
            }
            _liveAction(action);
            _liveData = null;
            Message = "Request sent to the live owner.";
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
            case "limit-setting":
                var limitOwner = target ?? character;
                if (limitOwner != null)
                {
                    limitOwner.LimitSettings[action.SubjectId] = action.Value;
                    Message = $"Fixture {limitOwner.Name} {action.SubjectId}: {action.Value}.";
                }
                break;
            case "slot-do-over":
                var slotOwner = target ?? character;
                if (slotOwner is { SlotPreview.Count: > 0 })
                {
                    var preview = int.TryParse(slotOwner.LimitSettings.GetValueOrDefault("Preview index"), out var oldPreview) ? oldPreview : 0;
                    slotOwner.LimitSettings["Preview index"] = ((preview + 1) % slotOwner.SlotPreview.Count).ToString();
                    Message = "Fixture Slot preview advanced; original random outcomes are not simulated.";
                }
                break;
            case "slot-select":
                Message = "Fixture spell preview selected. No battle spell was cast.";
                break;
            case "config":
                data.Settings[action.SubjectId] = action.Value;
                Message = $"Fixture {action.SubjectId}: {action.Value}";
                break;
            case "item-use":
                if (Item is { Quantity: > 0 } item && target != null)
                {
                    if (item.DemoHealing > 0)
                    {
                        target.CurrentHp = Math.Min(target.MaximumHp ?? 0, (target.CurrentHp ?? 0) + item.DemoHealing);
                        item.Quantity--;
                        Message = $"Fixture: {item.Name} restored {target.Name}'s HP.";
                    }
                    else if (item.Name is "Antidote" or "Remedy")
                    {
                        target.Conditions.Clear();
                        item.Quantity--;
                        Message = $"Fixture: conditions cleared on {target.Name}.";
                    }
                    else
                    {
                        Message = "This fixture item has no simulated field effect.";
                    }
                }
                break;
            case "item-sort":
                data.Items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
                Message = "Fixture inventory sorted by name.";
                break;
            case "item-move":
                var sourceIndex = data.Items.FindIndex(i => i.Id == _itemId);
                var targetIndex = data.Items.FindIndex(i => i.Id == action.TargetId);
                if (sourceIndex >= 0 && targetIndex >= 0)
                {
                    (data.Items[sourceIndex], data.Items[targetIndex]) = (data.Items[targetIndex], data.Items[sourceIndex]);
                    Message = "Fixture item positions exchanged.";
                }
                break;
            case "magic-use":
                if (Magic is { Quantity: > 0, DemoHealing: > 0 } magic && target != null)
                {
                    target.CurrentHp = Math.Min(target.MaximumHp ?? 0, (target.CurrentHp ?? 0) + magic.DemoHealing);
                    magic.Quantity--;
                    Message = $"Fixture: cast {magic.Name} on {target.Name}.";
                }
                else
                {
                    Message = "This fixture spell has no simulated field effect.";
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
                        Message = $"Fixture: transferred {quantity} {stock.Name} to {target.Name}.";
                    }
                }
                break;
            case "magic-sort":
                data.Magic.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
                Message = "Fixture magic stocks sorted by name.";
                break;
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
                    Message = "Fixture GF ownership toggled. Native junction effects are not calculated.";
                }
                break;
            case "junction-magic":
                if (character != null)
                {
                    character.Junctions[_junctionSlot] = action.Value;
                    Message = $"Fixture {_junctionSlot} junction: {action.Value}. Native stat effects are not calculated.";
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
                    Message = "Fixture junctions removed.";
                }
                break;
            case "junction-auto":
                if (character != null)
                {
                    var slot = action.Value == "Magic" ? "Mag" : action.Value == "Defense" ? "Vit" : "Str";
                    character.Junctions[slot] = "Ultima";
                    Message = $"Fixture {slot} assigned Ultima; original Auto optimization is unsupported.";
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
                    Message = $"Fixture command slot {slot + 1}: {action.Value}.";
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
                    Message = $"Fixture ability slot {_abilitySlot + 1}: {action.Value}.";
                }
                break;
            case "gf-learn":
                if (GuardianForce != null)
                {
                    GuardianForce.Learning = action.Value;
                    Message = $"Fixture {GuardianForce.Name} is learning {action.Value}.";
                }
                break;
            case "party-switch":
                var source = data.Characters.FirstOrDefault(c => c.Id == _exchangeId);
                if (source != null && target != null)
                {
                    (source.InParty, target.InParty) = (target.InParty, source.InParty);
                    Message = $"Fixture party membership exchanged: {source.Name} / {target.Name}.";
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
                    Message = "Fixture GF, magic, command, ability and stat junctions exchanged.";
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
                            Message = $"Fixture recipe ×{count} produced {recipe.ResultQuantity * count} {recipe.ResultName}.";
                        }
                        else
                        {
                            Message = "Fixture recipe lacks ingredients or exceeds the 100 stock capacity.";
                        }
                    }
                    else
                    {
                        Message = "Fixture recipe lacks ingredients or exceeds the 100 stock capacity.";
                    }
                }
                break;
            case "save":
                var save = data.SaveSlots.FirstOrDefault(s => s.Id == action.SubjectId);
                if (save != null)
                {
                    save.Summary = $"DEMO {character?.Name} · {data.Location}";
                    Message = "Fixture slot updated in memory. No game save was written.";
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
                        Message = $"Fixture bought one {shopItem.Name}.";
                    }
                    else if (action.Kind == "shop-sell" && shopItem.Quantity > 0)
                    {
                        data.Currency += shopItem.Price / 2;
                        shopItem.Quantity--;
                        Message = $"Fixture sold one {shopItem.Name}.";
                    }
                }
                break;
            default:
                Message = $"Unsupported contract: {action.Kind}. No fixture mutation is defined.";
                break;
        }
    }

    public static string Number(int? value) => value?.ToString() ?? "?";
    public static string Hp(MenuCharacter character) => $"{character.HpDisplay ?? Number(character.CurrentHp)} / {Number(character.MaximumHp)}";

    public UiView BuildView()
    {
        if (_view != null)
        {
            return _view;
        }
        var screen = CurrentScreen;
        var root = new UiPanel { Id = "menu-root", Bounds = new UiRect(0, 0, 960, 672), DrawBackground = false };
        if (ScreenId != "junction")
        {
            AddPanel(root, new UiRect(20, 18, screen.Layout == MenuLayout.Main ? 662 : 920, 68), "HELP", [Message.Length > 0 ? Message : SelectedEntry?.Help ?? screen.Help], 18);
        }
        if (screen.BuildContent is not null)
        {
            screen.BuildContent(this, root);
        }
        else if (screen.Layout == MenuLayout.Main)
        {
            BuildMain(root);
        }
        else if (ScreenId == "junction")
        {
            BuildJunction(root);
        }
        else if (screen.Layout is MenuLayout.GuardianForceGrid or MenuLayout.CardGrid)
        {
            BuildGrid(root);
        }
        else
        {
            BuildStandard(root);
        }
        var data = Data;
        AddPanel(root, new UiRect(20, 610, 920, 44), null, [$"{(IsDemo ? "DEMO" : "")}  {data.Location}    H / G: character    X / Enter: confirm    C / Esc: back    F2: Debug Room"], 14);
        _view = new UiView(root) { DesignWidth = 960, DesignHeight = 672, Cancel = Back };
        var focusables = FocusableDescendants(root).ToArray();
        _view.Focus(focusables.OfType<UiList>().FirstOrDefault() ?? focusables.FirstOrDefault(element => element.Id == SelectedEntry?.Id) ?? focusables.FirstOrDefault());
        return _view;
    }

    private static IEnumerable<UiElement> FocusableDescendants(UiElement element)
    {
        if (element.Focusable)
        {
            yield return element;
        }
        foreach (UiElement child in element.Children)
        {
            foreach (UiElement descendant in FocusableDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private void BuildMain(UiPanel root)
    {
        var party = Data.Characters.Where(c => c.InParty).ToArray();
        for (var i = 0; i < 3; i++)
        {
            var character = i < party.Length ? party[i] : null;
            var bounds = new UiRect(38, 104 + i * 106, 644, 94);
            var panel = new UiPanel { Bounds = bounds, Caption = "STATUS" };
            root.Children.Add(panel);
            AddLabel(panel, new UiRect(bounds.X + 18, bounds.Y + 35, 240, 44), character?.Name ?? "--", 30);
            AddLabel(panel, new UiRect(bounds.X + 262, bounds.Y + 40, 130, 36), $"Lv  {Number(character?.Level)}", 23);
            AddLabel(panel, new UiRect(bounds.X + 390, bounds.Y + 40, 244, 36), "HP  " + (character is null ? "--" : Hp(character)), 23);
        }
        var reservePanel = new UiPanel { Bounds = new UiRect(38, 422, 644, 172) };
        root.Children.Add(reservePanel);
        var reserves = Data.Characters.Where(c => !c.InParty).ToArray();
        for (int index = 0; index < reserves.Length; index++)
        {
            MenuCharacter reserve = reserves[index];
            float x = 56 + index % 2 * 318;
            float y = 436 + index / 2 * 51;
            AddLabel(reservePanel, new UiRect(x, y, 298, 26), reserve.Name, 20);
            AddLabel(reservePanel, new UiRect(x, y + 26, 298, 24), $"Lv {Number(reserve.Level)}  HP {Hp(reserve)}", 17);
        }
        var commands = new UiPanel { Bounds = new UiRect(706, 18, 234, 482) };
        root.Children.Add(commands);
        AddList(commands, new UiRect(714, 29, 218, 460), 41, 27);
        var time = Data.PlayTime.HasValue ? $"{(int)Data.PlayTime.Value.TotalHours:00}:{Data.PlayTime.Value.Minutes:00}" : "--:--";
        AddPanel(root, new UiRect(706, 514, 234, 80), null, [$"Play     {time}", $"SeeD  {Number(Data.SeeDRank)}      {Number(Data.Currency)}G"], 19);
    }

    private void BuildJunction(UiPanel root)
    {
        var tabs = new UiPanel { Bounds = new UiRect(20, 18, 694, 56) };
        root.Children.Add(tabs);
        var entries = Entries;
        for (int index = 0; index < entries.Count; index++)
        {
            int selected = index;
            tabs.Children.Add(new UiButton { Id = entries[index].Id, Bounds = new UiRect(24 + index * 86, 26, 84, 40), Text = entries[index].Label, Click = () => Activate(selected) });
        }
        AddPanel(root, new UiRect(728, 18, 212, 56), null, ["Junction"], 28);
        AddPanel(root, new UiRect(20, 90, 920, 66), "HELP", [Message.Length > 0 ? Message : SelectedEntry?.Help ?? CurrentScreen.Help], 18);
        var character = Character;
        DrawPortrait(root, 32, 180, character?.Name ?? "?", 98, 144);
        AddLabel(root, new UiRect(150, 181, 400, 46), character?.Name ?? "--", 34);
        AddLabel(root, new UiRect(150, 239, 520, 34), $"Level                   {Number(character?.Level)}", 26);
        AddLabel(root, new UiRect(150, 285, 520, 34), $"HP                  {(character is null ? "--" : Hp(character))}", 26);
        AddLabel(root, new UiRect(32, 336, 640, 34), $"Current EXP  {Number(character?.Experience)}p     Next level  {Number(character?.NextLevelExperience)}p", 21);
        AddPanel(root, new UiRect(728, 180, 212, 188), "COMMAND", character?.Commands.Take(4).ToArray() ?? ["--"], 26);
        var stats = new UiPanel { Bounds = new UiRect(32, 390, 908, 204) };
        root.Children.Add(stats);
        string[] keys = ["HP", "Str", "Vit", "Mag", "Spr", "Spd", "Eva", "Hit", "Luck"];
        for (int index = 0; index < keys.Length; index++)
        {
            string key = keys[index];
            float x = index < 5 ? 54 : 514;
            float y = 406 + (index < 5 ? index : index - 4) * 35;
            AddLabel(stats, new UiRect(x, y, 74, 32), key, 24);
            AddLabel(stats, new UiRect(x + 84, y, 235, 32), character?.Junctions.GetValueOrDefault(key, "--") ?? "--", 24);
            string value = key == "HP" ? Number(character?.MaximumHp) : Number(character?.Stats.GetValueOrDefault(key));
            if (key != "HP" && character?.Stats.ContainsKey(key) != true)
            {
                value = "--";
            }
            AddLabel(stats, new UiRect(x + 325, y, 78, 32), value, 24);
        }
    }

    private void BuildStandard(UiPanel root)
    {
        var character = Character;
        if (CurrentScreen.Layout is MenuLayout.Character or MenuLayout.Junction)
        {
            AddPanel(root, new UiRect(20, 96, 920, 92), character?.Name ?? "Character unavailable", character == null ? [Data.UnavailableReason.Length == 0 ? "No character owner has been projected." : Data.UnavailableReason] : [$"Lv {Number(character.Level)}       HP {Hp(character)}       {character.Weapon}", $"Commands: {(character.Commands.Count == 0 ? "unavailable" : string.Join(" / ", character.Commands))}"], 17);
        }
        var top = CurrentScreen.Layout is MenuLayout.Character or MenuLayout.Junction ? 206 : 100;
        AddList(root, new UiRect(20, top, 414, 592 - top), 30);
        AddPanel(root, new UiRect(454, top, 486, 592 - top), ScreenId == "debug-room" ? "Selected preview" : "Details", CurrentScreen.Details(this), 17);
    }

    private void BuildGrid(UiPanel root)
    {
        var entries = Entries;
        var columns = Math.Max(1, CurrentScreen.Columns);
        var rows = Math.Max(1, (entries.Count + columns - 1) / columns);
        var visibleRows = CurrentScreen.Layout == MenuLayout.GuardianForceGrid ? 2 : 4;
        var page = SelectedIndex / (columns * visibleRows);
        var first = page * columns * visibleRows;
        var width = 904f / columns;
        var height = 344f / visibleRows;
        if (entries.Count == 0)
        {
            AddPanel(root, new UiRect(20, 100, 920, 344), "Unavailable", ["No authenticated collection data."], 18);
        }
        for (var i = first; i < Math.Min(entries.Count, first + columns * visibleRows); i++)
        {
            var index = i;
            var local = i - first;
            var x = 24 + local % columns * width;
            var y = 100 + local / columns * height;
            var entry = entries[i];
            var button = new UiButton { Id = entry.Id, Bounds = new UiRect(x, y, width - 5, height - 6), Text = "", Click = () => Activate(index) };
            root.Children.Add(button);
            AddLabel(button, new UiRect(x + 8, y + 12, width - 16, 30), entry.Label, 14);
            DrawPortrait(button, x + 12, y + 42, entry.Label, width - 30, height - 85);
            if (CurrentScreen.Layout == MenuLayout.GuardianForceGrid)
            {
                var guardianForce = Data.GuardianForces.FirstOrDefault(force => force.Id == entry.Id);
                AddLabel(button, new UiRect(x + 8, y + height - 33, width - 16, 27), "Lv  " + Number(guardianForce?.Level), 20);
            }
        }
        AddPanel(root, new UiRect(20, 456, 920, 136), $"Page {page + 1} / {Math.Max(1, (rows + visibleRows - 1) / visibleRows)}", CurrentScreen.Details(this), 16);
    }

    private void AddList(UiPanel root, UiRect bounds, int rowHeight, int fontSize = 19)
    {
        var entries = Entries;
        var list = new UiList { Id = "commands", Bounds = bounds, FontSize = fontSize, Items = entries.Select(e => e.Label).ToList(), SelectedIndex = Math.Clamp(SelectedIndex, 0, Math.Max(0, entries.Count - 1)), RowHeight = rowHeight, SelectionChanged = index => Select(index, Entries.Count), Confirmed = Activate };
        root.Children.Add(list);
        if (entries.Count == 0)
        {
            root.Children.Add(new UiLabel { Bounds = new UiRect(bounds.X + 16, bounds.Y + 20, bounds.Width - 32, 84), Text = "Unavailable\nNo authenticated entries", FontSize = 17 });
        }
    }

    private static void AddLabel(UiElement root, UiRect bounds, string value, int fontSize)
    {
        root.Children.Add(new UiLabel { Bounds = bounds, Text = value, FontSize = fontSize });
    }

    private static void AddPanel(UiPanel root, UiRect bounds, string? caption, IReadOnlyList<string> lines, int fontSize)
    {
        var panel = new UiPanel { Bounds = bounds, Caption = caption ?? "" };
        root.Children.Add(panel);
        var y = bounds.Y + (string.IsNullOrEmpty(caption) ? 12 : 36);
        foreach (var line in lines)
        {
            panel.Children.Add(new UiLabel { Bounds = new UiRect(bounds.X + 16, y, bounds.Width - 32, fontSize + 8), Text = line, FontSize = fontSize });
            y += fontSize + 12;
        }
    }

    private static void DrawPortrait(UiElement root, float x, float y, string seed, float width, float height)
    {
        var hash = 17;
        foreach (var character in seed)
        {
            hash = unchecked(hash * 31 + character);
        }
        root.Children.Add(new UiPortrait { Id = $"portrait-{seed}", Bounds = new UiRect(x, y, width, height), Seed = hash, Initials = seed.Length == 0 ? "?" : seed[..1] });
    }
}
