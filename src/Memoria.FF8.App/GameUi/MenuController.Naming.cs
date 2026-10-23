using System.Globalization;
using System.Text;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed record DebugNameTarget(int Subject, string Name, string Category);

public sealed partial class MenuController
{
    private const int NameColumns = 14;
    private const int NameRows = 7;
    private const int NameCells = NameColumns * NameRows;
    private static readonly (string Title, string Characters)[] NamePages = CreateNamePages();
    private Func<IReadOnlyList<DebugNameTarget>>? _readNames;
    private Action<int, string>? _writeName;
    private Action<string>? _acceptName;
    private DebugNameTarget? _nameTarget;
    private string _nameDraft = "";
    private char? _nameHighSurrogate;
    private int _namePage;
    private bool _nameKeyboard;
    public bool IsNamingScreen => ScreenId is "debug-names" or "debug-name-input" or "name-input";
    public bool IsEditingName => IsOpen && ScreenId is "debug-name-input" or "name-input" && _nameTarget is not null;
    public bool IsNamePrompt => IsEditingName && ScreenId == "name-input";
    internal bool NamingControllerConnected { get; set; }
    internal bool IsTypingName => IsEditingName && _nameKeyboard && SelectedIndex < NameCells;
    internal bool NamingAvailable => _writeName is not null && _readNames?.Invoke().Count > 0;
    internal string NameDraft => _nameDraft;

    internal void ConnectNaming(Func<IReadOnlyList<DebugNameTarget>> read, Action<int, string> write)
    {
        _readNames = read;
        _writeName = write;
    }

    public void OpenNamePrompt(int subject, string name, Action<string> accept)
    {
        if (!GameNames.IsValidSubject(subject))
        {
            throw new ArgumentOutOfRangeException(nameof(subject));
        }
        Open("name-input");
        _acceptName = accept;
        BeginName(new DebugNameTarget(subject, name, subject is >= 16 and <= 31 ? "Guardian Force" : "Character"));
    }

    private void BeginName(DebugNameTarget target)
    {
        _nameTarget = target;
        _nameDraft = target.Name;
        _nameHighSurrogate = null;
        _namePage = 0;
        _nameKeyboard = !NamingControllerConnected;
        _selections[ScreenId] = 0;
        _view = null;
    }

    private void RegisterNamingScreens()
    {
        Register(new MenuScreenDefinition
        {
            Id = "item-name-confirm",
            Title = "Item · Name",
            Help = "Use this item to change the name?",
            Entries = c => [new MenuEntry("yes", "Yes", "Use one item and open name entry.", Action: new MenuAction("item-use", TargetId: "character-0")), new MenuEntry("no", "No", "Return without using the item.", Action: new MenuAction("menu-back"))],
            Details = c => [c.Item?.Name ?? "", "Change this name?", "The item is used when you choose Yes."],
            BuildContent = (c, panel) =>
            {
                UiGrid page = Grid([1.2f, 5], [1], 4);
                Place(page, c.NativeMenuHeader("Item"), 0);
                UiGrid confirmation = Grid([1, 2, 2, 1], [1, 2, 1]);
                Place(confirmation, c.Panel(null, c.Lines([c.Item?.Name ?? "", "Change the name?"], 24)), 1, 1);
                Place(confirmation, c.Panel(null, c.Commands(48, 26)), 2, 1);
                Place(page, confirmation, 1);
                panel.Children.Add(page);
            }
        });
        Register(new MenuScreenDefinition
        {
            Id = "debug-names",
            Title = "Names",
            Help = "Select a character or Guardian Force to rename.",
            Entries = c => c._readNames?.Invoke().Select(target => new MenuEntry(target.Subject.ToString(CultureInfo.InvariantCulture), $"{target.Category} · {target.Name}", "Edit this name in the current game.", Action: new MenuAction("name-edit", target.Subject.ToString(CultureInfo.InvariantCulture)))).ToArray() ?? [],
            Details = _ => ["Characters and Guardian Forces", "Select a name to edit it.", "Confirm applies to the current game.", "Cancel discards the draft.", "Save the game to keep your changes."]
        });
        foreach (string id in new[] { "debug-name-input", "name-input" })
        {
            Register(new MenuScreenDefinition
            {
                Id = id,
                Title = "Name Input",
                Help = "Name the character.",
                Columns = NameColumns,
                Entries = c => c.NameInputEntries(),
                BuildContent = (c, panel) => panel.Children.Add(c.BuildNameInput())
            });
        }
    }

    private static (string Title, string Characters)[] CreateNamePages()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding western = Encoding.GetEncoding(1252);
        string english = "ABCDEFGHIJKLMN" + "OPQRSTUVWXYZ".PadRight(NameColumns, '\0') + new string('\0', NameColumns) + "abcdefghijklmn" + "opqrstuvwxyz".PadRight(NameColumns, '\0') + ".,:?!_-()".PadRight(NameColumns, '\0') + "0123456789".PadRight(NameColumns, '\0');
        string symbols = new string(Enumerable.Range(32, 95).Select(value => (char)value).Where(character => !english.Contains(character)).ToArray());
        symbols += string.Concat(Enumerable.Range(128, 128).Where(value => value is not (129 or 141 or 143 or 144 or 157)).Select(value => western.GetString([(byte)value])));
        string cyrillic = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";
        cyrillic += new string(Enumerable.Range(0x400, 256).Select(value => (char)value).Where(character => !cyrillic.Contains(character)).ToArray());
        List<(string, string)> pages = [];
        foreach ((string title, string characters) in new[] { ("English", english), ("Symbols", symbols), ("Cyrillic", cyrillic) })
        {
            int count = (characters.Length + NameCells - 1) / NameCells;
            for (int page = 0; page < count; page++)
            {
                pages.Add((count == 1 ? title : $"{title} {page + 1}/{count}", characters.Substring(page * NameCells, Math.Min(NameCells, characters.Length - page * NameCells))));
            }
        }
        return pages.ToArray();
    }

    private IReadOnlyList<MenuEntry> NameInputEntries()
    {
        string characters = NamePages[_namePage].Characters;
        List<MenuEntry> entries = [];
        for (int index = 0; index < NameCells; index++)
        {
            if (_nameKeyboard)
            {
                entries.Add(new MenuEntry("letter-" + index, "", "Enter a name using your keyboard.", Action: new MenuAction("name-keyboard"), Enabled: index == 0));
                continue;
            }
            string letter = index < characters.Length && characters[index] != '\0' ? characters[index].ToString() : "";
            entries.Add(new MenuEntry("letter-" + index, letter == " " ? "␣" : letter, "Append this character.", Action: new MenuAction("name-letter", Value: letter), Enabled: letter.Length > 0));
        }
        entries.AddRange(new[] { ("Keyboard", "name-keyboard", ""), ("English", "name-page", "English"), ("Symbols", "name-page", "Symbols"), ("Cyrillic", "name-page", "Cyrillic"), ("Space", "name-letter", " "), ("Confirm", "name-save", ""), ("Delete", "name-delete", ""), ("Default", "name-default", ""), ("Clear", "name-clear", ""), ("Cancel", "menu-back", "") }.Select((entry, index) => new MenuEntry("name-command-" + index, entry.Item1, entry.Item1, Action: new MenuAction(entry.Item2, Value: entry.Item3), Enabled: index != 9 || !IsNamePrompt)));
        return entries;
    }

    private UiElement BuildNameInput()
    {
        UiGrid page = Grid([0.65f, 2.4f, 4.6f, 0.65f], [1, 3.25f], 5);
        UiPanel title = Panel(null, Label("Name Input", 27));
        title.HorizontalAlignment = UiAlignment.End;
        title.Width = 220;
        Place(page, title, 0, 1);
        int? portraitId = _nameTarget?.Subject switch
        {
            0 => 0,
            1 => 4,
            >= 33 and <= 42 => _nameTarget.Subject - 32,
            _ => null
        };
        UiImage portrait = _nameTarget?.Subject switch
        {
            2 => Artwork(UiImageKind.AngeloPortrait, 0),
            >= 16 and <= 31 => Artwork(UiImageKind.GuardianForcePortrait, _nameTarget.Subject - 16),
            _ => Artwork(UiImageKind.CharacterPortrait, portraitId)
        };
        Place(page, portrait, 1, 0);
        UiGrid identity = Grid([1, 1.4f], [1], 3);
        Place(identity, Panel("HELP", Label(_nameTarget?.Subject is >= 16 and <= 31 ? "Name the Guardian Force" : "Name the character", 24)), 0);
        UiGrid name = Grid([1, 0.45f], [1], 0);
        Place(name, Label(_nameDraft + "_", 28), 0);
        Place(name, Label($"{StringInfo.ParseCombiningCharacters(_nameDraft).Length}/{GameNames.MaximumLength}", 15), 1);
        Place(identity, Panel("NAME", name), 1);
        Place(page, identity, 1, 1);
        UiGrid commands = new() { Columns = 1, Rows = 10, Gap = 1 };
        UiGrid keys = new() { Columns = NameColumns, Rows = NameRows, Gap = 0 };
        IReadOnlyList<MenuEntry> entries = Entries;
        for (int index = 0; index < entries.Count; index++)
        {
            if (_nameKeyboard && index < NameCells)
            {
                continue;
            }
            int selected = index;
            UiButton button = new() { Id = entries[index].Id, DrawBackground = false, HighlightSelection = false, PointerSize = index < NameCells ? 12 : 18, Enabled = entries[index].Enabled, Click = () => Activate(selected) };
            button.Children.Add(new UiLabel { Text = entries[index].Label, TextFlow = UiTextFlow.SingleLine, Muted = !entries[index].Enabled, FontSize = (index < NameCells ? 24 : 22) * TextScale, HorizontalAlignment = index < NameCells ? UiAlignment.Center : UiAlignment.Start, VerticalAlignment = UiAlignment.Center });
            if (index < NameCells)
            {
                Place(keys, button, index / NameColumns, index % NameColumns);
            }
            else
            {
                Place(commands, button, index - NameCells);
            }
        }
        Place(page, Panel(null, commands), 2, 0);
        UiElement input = keys;
        if (_nameKeyboard)
        {
            UiButton keyboard = new() { Id = "letter-0", DrawBackground = false, HighlightSelection = false, PointerSize = 0, Click = () => Execute(new MenuAction("name-keyboard")) };
            keyboard.Children.Add(Lines(["Use your keyboard to enter a name.", "Or select an on-screen keyboard", "from the menu on the left."], 22));
            input = keyboard;
        }
        Place(page, Panel(_nameKeyboard ? "Keyboard" : NamePages[_namePage].Title, input), 2, 1);
        Place(page, Panel(null, Label(Message.Length > 0 ? Message : "20 characters", 16)), 3, 0, 1, 2);
        return page;
    }

    public bool EnterNameCharacter(char character)
    {
        if (!IsTypingName)
        {
            return false;
        }
        if (character == '\b')
        {
            DeleteNameCharacter();
        }
        else if (char.IsHighSurrogate(character))
        {
            _nameHighSurrogate = character;
        }
        else if (!char.IsControl(character))
        {
            string text = char.IsLowSurrogate(character) && _nameHighSurrogate is char high ? new string([high, character]) : character.ToString();
            _nameHighSurrogate = null;
            AppendNameText(text);
        }
        _view = null;
        return true;
    }

    public bool EnterNameText(string text)
    {
        if (!IsEditingName)
        {
            return false;
        }
        _nameHighSurrogate = null;
        AppendNameText(text);
        _view = null;
        return true;
    }

    public void SetNameDraft(string text)
    {
        if (!GameNames.IsValidDraft(text))
        {
            throw new ArgumentException("Use at most 20 characters without control characters.", nameof(text));
        }
        _nameDraft = text;
        _nameHighSurrogate = null;
        _view = null;
    }

    private void AppendNameText(string text)
    {
        if (!GameNames.IsValidDraft(_nameDraft + text))
        {
            Message = "Names can contain at most 20 characters.";
            return;
        }
        _nameDraft += text;
        Message = "";
    }

    private void DeleteNameCharacter()
    {
        if (_nameDraft.Length > 0)
        {
            _nameDraft = _nameDraft[..StringInfo.ParseCombiningCharacters(_nameDraft)[^1]];
        }
        _nameHighSurrogate = null;
        Message = "";
        _view = null;
    }

    private bool NavigateNaming(MenuInput input)
    {
        if (!IsEditingName)
        {
            return false;
        }
        int index = SelectedIndex;
        int commandCount = IsNamePrompt ? 9 : 10;
        switch (input)
        {
            case MenuInput.Confirm when IsTypingName:
                Execute(new MenuAction("name-save"));
                return true;
            case MenuInput.Tab:
                SelectNameCell(index >= NameCells ? 0 : NameCells, 1);
                break;
            case MenuInput.PageLeft:
            case MenuInput.PageUp:
                _nameKeyboard = false;
                _namePage = (_namePage + NamePages.Length - 1) % NamePages.Length;
                break;
            case MenuInput.PageRight:
            case MenuInput.PageDown:
                _nameKeyboard = false;
                _namePage = (_namePage + 1) % NamePages.Length;
                break;
            case MenuInput.Up:
                if (IsTypingName)
                {
                    return true;
                }
                SelectNameCell(index >= NameCells ? NameCells + (index - NameCells + commandCount - 1) % commandCount : (index + NameCells - NameColumns) % NameCells, -NameColumns);
                break;
            case MenuInput.Down:
                if (IsTypingName)
                {
                    return true;
                }
                SelectNameCell(index >= NameCells ? NameCells + (index - NameCells + 1) % commandCount : (index + NameColumns) % NameCells, NameColumns);
                break;
            case MenuInput.Left:
                if (index >= NameCells)
                {
                    return true;
                }
                int left = _nameKeyboard || index % NameColumns == 0 ? NameCells + Math.Min(index / NameColumns, commandCount - 1) : index - 1;
                SelectNameCell(left, -1);
                break;
            case MenuInput.Right:
                if (_nameKeyboard)
                {
                    SelectNameCell(0, 1);
                }
                else if (index >= NameCells)
                {
                    SelectNameCell(Math.Min(index - NameCells, NameRows - 1) * NameColumns, 1);
                }
                else
                {
                    SelectNameCell((index + 1) % NameColumns == 0 ? NameCells + Math.Min(index / NameColumns, commandCount - 1) : index + 1, 1);
                }
                break;
            case MenuInput.Cancel:
                if (IsNamePrompt)
                {
                    DeleteNameCharacter();
                }
                else
                {
                    Back();
                }
                break;
            default:
                return false;
        }
        if (SelectedIndex < NameCells && !Entries[SelectedIndex].Enabled)
        {
            SelectNameCell(SelectedIndex, 1);
        }
        _view = null;
        return true;
    }

    private void SelectNameCell(int index, int direction)
    {
        IReadOnlyList<MenuEntry> entries = Entries;
        for (int attempt = 0; index < NameCells && !entries[index].Enabled && attempt < NameCells; attempt++)
        {
            index = (index + direction + NameCells) % NameCells;
        }
        Select(index, entries.Count);
    }

    private bool ExecuteNamingAction(MenuAction action)
    {
        if (action.Kind == "name-edit")
        {
            DebugNameTarget? target = _readNames?.Invoke().SingleOrDefault(target => target.Subject.ToString(CultureInfo.InvariantCulture) == action.SubjectId);
            if (target is not null && _writeName is not null)
            {
                Push("debug-name-input");
                BeginName(target);
            }
            return true;
        }
        if (!action.Kind.StartsWith("name-", StringComparison.Ordinal))
        {
            return false;
        }
        if (!IsEditingName)
        {
            return true;
        }
        switch (action.Kind)
        {
            case "name-keyboard":
                _nameKeyboard = true;
                _nameHighSurrogate = null;
                _selections[ScreenId] = 0;
                break;
            case "name-page":
                int firstPage = Array.FindIndex(NamePages, page => page.Title.StartsWith(action.Value, StringComparison.Ordinal));
                if (firstPage < 0)
                {
                    throw new ArgumentException("Unknown naming alphabet.", nameof(action));
                }
                _namePage = NamePages[_namePage].Title.StartsWith(action.Value, StringComparison.Ordinal) && _namePage + 1 < NamePages.Length && NamePages[_namePage + 1].Title.StartsWith(action.Value, StringComparison.Ordinal) ? _namePage + 1 : firstPage;
                _nameKeyboard = false;
                _nameHighSurrogate = null;
                _selections[ScreenId] = 0;
                break;
            case "name-letter":
                AppendNameText(action.Value);
                break;
            case "name-delete":
                DeleteNameCharacter();
                break;
            case "name-clear":
                _nameDraft = "";
                Message = "";
                break;
            case "name-default":
                _nameDraft = GameNames.DefaultName(_nameTarget!.Subject);
                Message = "";
                break;
            case "name-save":
                try
                {
                    string name = GameNames.Normalize(_nameDraft);
                    if (IsNamePrompt)
                    {
                        _acceptName!(name);
                        Close();
                        _acceptName = null;
                    }
                    else
                    {
                        _writeName!(_nameTarget!.Subject, name);
                        Back();
                        _liveData = null;
                        Message = "";
                    }
                    SoundRequested?.Invoke(MenuSound.Confirm);
                }
                catch (ArgumentException)
                {
                    Message = "Use 1–20 characters.";
                }
                catch (NotSupportedException exception)
                {
                    Message = exception.Message;
                }
                break;
        }
        _view = null;
        return true;
    }
}
