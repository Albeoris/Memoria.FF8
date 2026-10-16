using System.Globalization;
using System.Text;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed record DebugNameTarget(int Subject, string Name, string Category);

public sealed partial class MenuController
{
    private Func<IReadOnlyList<DebugNameTarget>>? _readNames;
    private Action<int, string>? _writeName;
    private DebugNameTarget? _nameTarget;
    private string _nameDraft = "";
    public bool IsNamingScreen => ScreenId is "debug-names" or "debug-name-input";
    public bool IsEditingName => IsOpen && ScreenId == "debug-name-input" && _nameTarget is not null;
    internal bool NamingAvailable => _writeName is not null && _readNames?.Invoke().Count > 0;
    internal string NameDraft => _nameDraft;

    internal void ConnectNaming(Func<IReadOnlyList<DebugNameTarget>> read, Action<int, string> write)
    {
        _readNames = read;
        _writeName = write;
    }

    private void RegisterNamingScreens()
    {
        Register(new MenuScreenDefinition
        {
            Id = "debug-names",
            Title = "Names",
            Help = "Select a character or Guardian Force to rename.",
            Entries = c => c._readNames?.Invoke().Select(target => new MenuEntry(target.Subject.ToString(CultureInfo.InvariantCulture), $"{target.Category} · {target.Name}", "Edit this name in the current game.", Action: new MenuAction("name-edit", target.Subject.ToString(CultureInfo.InvariantCulture)))).ToArray() ?? [],
            Details = _ => ["Characters and Guardian Forces", "Select a name to edit it.", "Save applies to the current game.", "Cancel discards the draft.", "Save the game to keep your changes."]
        });
        Register(new MenuScreenDefinition
        {
            Id = "debug-name-input",
            Title = "Rename",
            Help = "Type a name or use the character buttons. Enter saves; Escape cancels.",
            Columns = 8,
            Entries = c => c.NameInputEntries(),
            BuildContent = (c, panel) => panel.Children.Add(c.BuildNameInput())
        });
    }

    private IReadOnlyList<MenuEntry> NameInputEntries()
    {
        List<MenuEntry> entries = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-'".Select(character => new MenuEntry("letter-" + (int)character, character.ToString(), "Append this character.", Action: new MenuAction("name-letter", Value: character.ToString()))).ToList();
        entries.AddRange(new[] { ("Save", "name-save", ""), ("Cancel", "menu-back", ""), ("Delete", "name-delete", ""), ("Clear", "name-clear", ""), ("Space", "name-letter", " "), (".", "name-letter", "."), ("!", "name-letter", "!"), ("?", "name-letter", "?") }.Select((entry, index) => new MenuEntry("name-command-" + index, entry.Item1, entry.Item1, Action: new MenuAction(entry.Item2, Value: entry.Item3))));
        return entries;
    }

    private UiElement BuildNameInput()
    {
        UiGrid page = Grid([0.8f, 1, 7, 0.8f], [1]);
        Place(page, Panel(null, Label("Rename · " + (_nameTarget?.Name ?? "Select a name first"), 24)), 0);
        Place(page, Panel("New name", Label(_nameDraft + "_", 28)), 1);
        UiGrid keys = new() { Columns = 8, Rows = 9, Gap = 5 };
        IReadOnlyList<MenuEntry> entries = Entries;
        for (int index = 0; index < entries.Count; index++)
        {
            int selected = index;
            UiButton button = new() { Id = entries[index].Id, Click = () => Activate(selected) };
            button.Children.Add(new UiLabel { Text = entries[index].Label, FontSize = 18 * TextScale, HorizontalAlignment = UiAlignment.Center, VerticalAlignment = UiAlignment.Center });
            Place(keys, button, index / 8, index % 8);
        }
        Place(page, keys, 2);
        Place(page, Panel(null, Label(Message.Length > 0 ? Message : "Type or select letters · Enter: save · Escape: cancel · 12 characters", 16)), 3);
        return page;
    }

    public bool EnterNameCharacter(char character)
    {
        if (!IsEditingName)
        {
            return false;
        }
        if (character == '\b')
        {
            DeleteNameCharacter();
        }
        else if (!char.IsControl(character))
        {
            AppendNameText(character.ToString());
        }
        _view = null;
        return true;
    }

    private void AppendNameText(string text)
    {
        if (text.Any(character => char.IsControl(character) || character is '{' or '}'))
        {
            Message = "Control characters and braces are unavailable in names.";
            return;
        }
        if ((_nameDraft + text).EnumerateRunes().Count() > 12)
        {
            Message = "Names can contain at most 12 characters.";
            return;
        }
        _nameDraft += text;
        Message = "";
    }

    private void DeleteNameCharacter()
    {
        if (_nameDraft.Length > 0)
        {
            int last = StringInfo.ParseCombiningCharacters(_nameDraft)[^1];
            _nameDraft = _nameDraft[..last];
        }
        Message = "";
    }

    private bool ExecuteNamingAction(MenuAction action)
    {
        if (action.Kind == "name-edit")
        {
            _nameTarget = _readNames?.Invoke().SingleOrDefault(target => target.Subject.ToString(CultureInfo.InvariantCulture) == action.SubjectId);
            if (_nameTarget is not null && _writeName is not null)
            {
                _nameDraft = _nameTarget.Name;
                _selections["debug-name-input"] = 64;
                Push("debug-name-input");
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
            case "name-save":
                try
                {
                    string name = GameNames.Normalize(_nameDraft);
                    _writeName!(_nameTarget!.Subject, name);
                    ScreenId = _history.TryPop(out string? previous) ? previous : "debug-names";
                    _liveData = null;
                    Message = "Name changed. Save the game to keep it.";
                    SoundRequested?.Invoke(MenuSound.Confirm);
                }
                catch (ArgumentException exception)
                {
                    Message = "Use 1–12 characters; braces and control characters are unavailable.";
                    Console.Error.WriteLine(exception);
                }
                catch (NotSupportedException exception)
                {
                    Message = exception.Message;
                    Console.Error.WriteLine(exception);
                }
                break;
        }
        return true;
    }
}
