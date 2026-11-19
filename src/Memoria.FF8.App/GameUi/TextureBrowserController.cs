using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed record TextureBrowserRow(string Id, string Name, string Group, string Detail, bool Supported = true)
{
    public bool GroupNode { get; init; }
    public int Depth { get; init; }
    public bool Expandable { get; init; }
}

/// <summary>Shared texture hierarchy, independent of decoded images, with bounded displayed rows.</summary>
public sealed class TextureBrowserController
{
    private sealed class Group(string id, string name, int depth)
    {
        public string Id { get; } = id;
        public string Name { get; } = name;
        public int Depth { get; } = depth;
        public SortedDictionary<string, Group> Children { get; } = new(StringComparer.Ordinal);
        public List<TextureBrowserRow> Leaves { get; } = [];
        public List<TextureBrowserRow> Descendants { get; } = [];
    }

    public const int RowsPerPage = 9;
    public const int PreviewImageId = 93001;
    public const int ImportedPreviewImageId = 93002;
    private readonly Action close;
    private readonly Action<int> command;
    private readonly UiView view;
    private readonly Dictionary<string, Group> groups = new(StringComparer.Ordinal);
    private readonly HashSet<string> collapsed = new(StringComparer.Ordinal);
    private Group root = new("", "", -1);
    private readonly Dictionary<string, string> searchText = new(StringComparer.Ordinal);
    private string[] searchTerms = [];
    private readonly Dictionary<string, string[]> ancestors = new(StringComparer.Ordinal);
    private readonly HashSet<string> knownGroups = new(StringComparer.Ordinal);
    private HashSet<string>? filteredIds;
    private HashSet<string>? pendingMatches;
    private Dictionary<string, int>? pendingGroups;
    private int filterCursor;
    private string? filterSelection;
    public bool Filtering => pendingMatches is not null;
    private TextureBrowserRow[] catalog = [];
    private TextureBrowserRow[] visible = [];
    private readonly Dictionary<int, bool> sourceModes = [];
    private int scope;
    private bool resolved = true;

    public TextureBrowserController(Action close, Action<int> command)
    {
        this.close = close;
        this.command = command;
        view = new(new TextureBrowserElement(this) { Focusable = true, Id = "texture-browser" }) { DesignWidth = 960, DesignHeight = 672, Cancel = Cancel };
    }

    public Func<string, string, string>? LocalizeText { get; set; }
    public string Text(string id, string text) => Memoria.FF8.Resources.Localization.SystemTextCatalog.Translate(LocalizeText, id, text);
    public HashSet<string> Selection { get; } = new(StringComparer.Ordinal);
    public IReadOnlyList<TextureBrowserRow> Rows => visible;
    public IReadOnlyList<TextureBrowserRow> Leaves => catalog;
    public TextureBrowserRow? Selected => visible.ElementAtOrDefault(Index);
    public int Index { get; private set; }
    public int Page => Index / RowsPerPage;
    public int Scope
    {
        get => scope;
        set
        {
            if (value is < 0 or > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            scope = value;
            resolved = sourceModes.GetValueOrDefault(scope, scope == 0);
        }
    }
    public bool Resolved
    {
        get => resolved;
        set
        {
            resolved = value;
            sourceModes[scope] = value;
        }
    }
    public IReadOnlyDictionary<int, bool> SourceModes => sourceModes;
    public string ScopeDescription { get; set; } = "";

    public void RestoreSourceModes(IReadOnlyDictionary<int, bool> choices)
    {
        sourceModes.Clear();
        foreach ((int mode, bool source) in choices.Where(pair => pair.Key is >= 0 and <= 2))
        {
            sourceModes[mode] = source;
        }
        resolved = sourceModes.GetValueOrDefault(scope, scope == 0);
    }
    public bool Busy { get; set; }
    public bool Matching { get; set; }
    public bool PreviewAvailable { get; set; }
    public bool ImportedPreviewAvailable { get; set; }
    public Action<string>? Match { get; set; }
    public Action<string>? Unmatched { get; set; }
    public Action? CancelWork { get; set; }
    public Action? ManageMods { get; set; }
    public Action<string>? ExpandSourceRequested { get; set; }
    public string Message { get; set; } = "";
    public string Search { get; private set; } = "";
    public bool SearchFocused { get; private set; }
    public int CommandIndex { get; private set; }
    public bool CommandsFocused { get; private set; }
    public UiView BuildView() => view;

    public void SetRows(IEnumerable<TextureBrowserRow> rows)
    {
        string? selected = Selected?.Id;
        catalog = rows.GroupBy(row => row.Id, StringComparer.Ordinal).Select(group => group.First()).OrderBy(row => row.Group, StringComparer.Ordinal).ThenBy(row => row.Name, StringComparer.Ordinal).ToArray();
        root = new("", "", -1);
        groups.Clear();
        searchText.Clear();
        ancestors.Clear();
        foreach (TextureBrowserRow row in catalog)
        {
            searchText[row.Id] = row.Name + " " + row.Group + " " + row.Id + " " + row.Detail;
            Group parent = root;
            List<string> rowAncestors = [];
            string path = "";
            foreach (string part in row.Group.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                path += "/" + part;
                string id = "\u0001group:" + path;
                if (!parent.Children.TryGetValue(part, out Group? child))
                {
                    child = new(id, part, parent.Depth + 1);
                    parent.Children.Add(part, child);
                    groups[id] = child;
                    if (knownGroups.Add(id))
                    {
                        collapsed.Add(id);
                    }
                }
                rowAncestors.Add(id);
                child.Descendants.Add(row);
                parent = child;
            }
            parent.Leaves.Add(row with { Depth = parent.Depth + 1 });
            ancestors[row.Id] = rowAncestors.ToArray();
        }
        visible = [];
        Filter(selected);
    }

    private bool Matches(TextureBrowserRow row)
    {
        return searchTerms.All(term => searchText[row.Id].Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private void Filter(string? preserve = null)
    {
        preserve ??= Selected?.Id;
        searchTerms = Search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        pendingMatches = null;
        pendingGroups = null;
        if (searchTerms.Length == 0)
        {
            BuildVisible(null, null, preserve);
            return;
        }
        pendingMatches = new(StringComparer.Ordinal);
        pendingGroups = new(StringComparer.Ordinal);
        filterCursor = 0;
        filterSelection = preserve;
        if (catalog.Length <= 4096)
        {
            AdvanceFiltering(int.MaxValue);
        }
    }

    public void AdvanceFiltering(int maximumItems = 2048)
    {
        if (pendingMatches is null || pendingGroups is null)
        {
            return;
        }
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        int count = 0;
        while (filterCursor < catalog.Length && count++ < maximumItems)
        {
            TextureBrowserRow leaf = catalog[filterCursor++];
            if (Matches(leaf))
            {
                pendingMatches.Add(leaf.Id);
                foreach (string ancestor in ancestors[leaf.Id])
                {
                    pendingGroups[ancestor] = pendingGroups.GetValueOrDefault(ancestor) + 1;
                }
            }
            if (maximumItems != int.MaxValue && System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds >= 2)
            {
                break;
            }
        }
        if (filterCursor == catalog.Length)
        {
            HashSet<string> matches = pendingMatches;
            Dictionary<string, int> groupMatches = pendingGroups;
            pendingMatches = null;
            pendingGroups = null;
            BuildVisible(matches, groupMatches, filterSelection);
        }
    }

    private void BuildVisible(HashSet<string>? matches, IReadOnlyDictionary<string, int>? groupMatches, string? preserve)
    {
        filteredIds = matches;
        List<TextureBrowserRow> rows = [];
        void Visit(Group parent)
        {
            foreach (Group child in parent.Children.Values)
            {
                int count = groupMatches?.GetValueOrDefault(child.Id) ?? (matches is null ? child.Descendants.Count : 0);
                if (count == 0)
                {
                    continue;
                }
                rows.Add(new(child.Id, child.Name, "", $"{child.Descendants.Count} shared logical targets; group selection includes this complete subtree.") { GroupNode = true, Depth = child.Depth });
                if (Search.Length != 0 || !collapsed.Contains(child.Id))
                {
                    Visit(child);
                }
            }
            rows.AddRange(parent.Leaves.Where(row => matches is null || matches.Contains(row.Id)));
        }
        Visit(root);
        visible = rows.ToArray();
        int retained = Array.FindIndex(visible, row => row.Id == preserve);
        Index = retained >= 0 ? retained : Math.Clamp(Index, 0, Math.Max(0, visible.Length - 1));
    }

    public int CheckState(TextureBrowserRow row)
    {
        if (!row.GroupNode)
        {
            return Selection.Contains(row.Id) ? 2 : 0;
        }
        int supported = 0;
        int selected = 0;
        foreach (TextureBrowserRow leaf in groups[row.Id].Descendants)
        {
            if (leaf.Supported)
            {
                supported++;
                if (Selection.Contains(leaf.Id))
                {
                    selected++;
                }
            }
        }
        return selected == 0 ? 0 : selected == supported ? 2 : 1;
    }

    public string RowLabel(TextureBrowserRow row)
    {
        string mark = CheckState(row) switch { 2 => "[x] ", 1 => "[-] ", _ => "[ ] " };
        string arrow = row.GroupNode ? Search.Length != 0 || !collapsed.Contains(row.Id) ? "v " : "> " : "";
        return new string(' ', Math.Min(row.Depth, 12) * 2) + mark + arrow + row.Name;
    }

    public void SetExpanded(bool expanded)
    {
        if (Selected is not { GroupNode: true } row)
        {
            return;
        }
        if (expanded)
        {
            collapsed.Remove(row.Id);
        }
        else
        {
            collapsed.Add(row.Id);
        }
        Filter(row.Id);
        if (expanded && groups[row.Id].Leaves.FirstOrDefault(leaf => leaf.Expandable) is { } source)
        {
            ExpandSourceRequested?.Invoke(source.Id);
        }
    }

    public void ToggleExpansion()
    {
        if (Selected is { GroupNode: true } row)
        {
            SetExpanded(collapsed.Contains(row.Id));
        }
    }

    public void SelectAll()
    {
        if (Busy || Matching)
        {
            return;
        }
        foreach (TextureBrowserRow row in catalog.Where(row => row.Supported))
        {
            Selection.Add(row.Id);
        }
    }

    public void ClearSelection()
    {
        if (!Busy && !Matching)
        {
            Selection.Clear();
        }
    }

    public void SelectFiltered()
    {
        if (Busy || Matching || Filtering)
        {
            return;
        }
        TextureBrowserRow[] leaves = catalog.Where(row => row.Supported && (filteredIds is null || filteredIds.Contains(row.Id))).ToArray();
        bool select = leaves.Any(row => !Selection.Contains(row.Id));
        foreach (TextureBrowserRow row in leaves)
        {
            if (select)
            {
                Selection.Add(row.Id);
            }
            else
            {
                Selection.Remove(row.Id);
            }
        }
    }

    public void TextInput(char character)
    {
        if (!SearchFocused || char.IsControl(character) || Search.Length >= 256)
        {
            return;
        }
        Search += character;
        Filter();
    }

    public void ClearSearch()
    {
        Search = "";
        Filter();
    }

    public void Backspace()
    {
        if (Search.Length != 0)
        {
            Search = Search[..^1];
            Filter();
        }
    }

    public void Select(int index) => Index = Math.Clamp(index, 0, Math.Max(0, visible.Length - 1));
    public void FocusSearch() => SearchFocused = !SearchFocused;

    public void Execute(int index)
    {
        if (Busy)
        {
            CancelWork?.Invoke();
            return;
        }
        command(index);
    }

    public void Toggle()
    {
        if (Selected is not { } row || Busy)
        {
            return;
        }
        if (row.GroupNode)
        {
            if (Matching)
            {
                ToggleExpansion();
                return;
            }
            bool select = CheckState(row) != 2;
            foreach (TextureBrowserRow leaf in groups[row.Id].Descendants.Where(leaf => leaf.Supported))
            {
                if (select)
                {
                    Selection.Add(leaf.Id);
                }
                else
                {
                    Selection.Remove(leaf.Id);
                }
            }
            return;
        }
        if (!row.Supported)
        {
            Unmatched?.Invoke(row.Id);
            return;
        }
        if (Matching)
        {
            Match?.Invoke(row.Id);
            return;
        }
        if (!Selection.Add(row.Id))
        {
            Selection.Remove(row.Id);
        }
    }

    public void Navigate(MenuInput input)
    {
        if (SearchFocused && input is not (MenuInput.Tab or MenuInput.Cancel or MenuInput.Confirm))
        {
            return;
        }
        switch (input)
        {
            case MenuInput.Cancel:
                Cancel();
                break;
            case MenuInput.Tab:
                FocusSearch();
                break;
            case MenuInput.Actions:
                SelectFiltered();
                break;
            case MenuInput.Alternate:
                ManageMods?.Invoke();
                break;
            case MenuInput.Left:
            case MenuInput.Right:
                if (Selected?.GroupNode == true && !CommandsFocused)
                {
                    SetExpanded(input == MenuInput.Right);
                    break;
                }
                goto case MenuInput.PageRight;
            case MenuInput.PageLeft:
            case MenuInput.PageRight:
                CommandsFocused = true;
                SearchFocused = false;
                CommandIndex = (CommandIndex + (input is MenuInput.Right or MenuInput.PageRight ? 1 : 9)) % 10;
                break;
            case MenuInput.Up:
            case MenuInput.Down:
                CommandsFocused = false;
                Select(Index + (input == MenuInput.Down ? 1 : -1));
                break;
            case MenuInput.PageUp:
            case MenuInput.PageDown:
                Select(Index + (input == MenuInput.PageDown ? RowsPerPage : -RowsPerPage));
                break;
            case MenuInput.Confirm:
                if (SearchFocused)
                {
                    SearchFocused = false;
                }
                else if (CommandsFocused)
                {
                    Execute(CommandIndex);
                }
                else if (Selected?.GroupNode == true)
                {
                    ToggleExpansion();
                }
                else
                {
                    Toggle();
                }
                break;
        }
    }

    public void Cancel()
    {
        if (Busy)
        {
            CancelWork?.Invoke();
        }
        else if (SearchFocused)
        {
            SearchFocused = false;
        }
        else
        {
            close();
        }
    }
}

internal sealed class TextureBrowserElement(TextureBrowserController controller) : UiElement
{
    private float fontScale = 1;
    private static UiRect CommandBounds(int index) => new(18 + index % 5 * 184, 76 + index / 5 * 40, 176, 34);
    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        fontScale = theme.FontSize / 16;
        theme.Panel(canvas, new(10, 10, 940, 652));
        Label(canvas, controller.Text("textures.title", "FFVIII · Textures"), new(24, 22, 200, 30), theme.Foreground, 22);
        Button(canvas, theme, new(250, 20, 680, 34), controller.Search.Length == 0 ? controller.Text("textures.search", "Search names, categories and targets (Tab)") : controller.Search, controller.SearchFocused);
        Label(canvas, controller.ScopeDescription, new(24, 54, 906, 18), theme.Muted, 11);
        string[] commands = [controller.Text("textures.scope.scene", "Current scene"), controller.Text("textures.scope.all", "All textures"), controller.Text("textures.scope.selected", "Selected"), controller.Resolved ? controller.Text("textures.source.resolved", "Resolved") : controller.Text("textures.source.original", "Original"), controller.Text("textures.export", "Export folder"), controller.Text("textures.import", "Import folder"), controller.Text("textures.refresh", "Reimport"), controller.Text("textures.open.folder", "Open working folder"), controller.Text("textures.import.files", "Import files"), controller.Text("textures.open.mod", "Open mod folder")];
        for (int index = 0; index < commands.Length; index++)
        {
            Button(canvas, theme, CommandBounds(index), commands[index], controller.CommandsFocused && controller.CommandIndex == index || index < 3 && index == controller.Scope);
        }
        int start = controller.Page * TextureBrowserController.RowsPerPage;
        for (int index = start; index < Math.Min(start + TextureBrowserController.RowsPerPage, controller.Rows.Count); index++)
        {
            TextureBrowserRow row = controller.Rows[index];
            Button(canvas, theme, new(18, 166 + (index - start) * 34, controller.Matching ? 514 : 714, 32), controller.RowLabel(row), index == controller.Index);
        }
        if (controller.PreviewAvailable)
        {
            canvas.Image(new(UiImageKind.HostArtwork, TextureBrowserController.PreviewImageId), new(controller.Matching ? 548 : 748, 180, 180, 180));
        }
        if (controller.Matching)
        {
            Label(canvas, controller.Resolved ? controller.Text("textures.match.resolved", "Candidate: Resolved") : controller.Text("textures.match.original", "Candidate: Original"), new(548, 154, 180, 24), theme.Foreground, 13);
            Label(canvas, controller.Text("textures.match.imported", "Imported image"), new(748, 154, 180, 24), theme.Foreground, 13);
            if (controller.ImportedPreviewAvailable)
            {
                canvas.Image(new(UiImageKind.HostArtwork, TextureBrowserController.ImportedPreviewImageId), new(748, 180, 180, 180));
            }
        }
        Button(canvas, theme, new(748, 376, 56, 32), controller.Text("textures.select.all", "All"));
        Button(canvas, theme, new(810, 376, 56, 32), controller.Text("textures.select.clear", "Clear"));
        Button(canvas, theme, new(872, 376, 56, 32), controller.Text("textures.select.filtered", "Filter"));
        Label(canvas, $"{controller.Leaves.Count} targets / {controller.Selection.Count} checked\nPage {controller.Page + 1} / {Math.Max(1, (controller.Rows.Count + TextureBrowserController.RowsPerPage - 1) / TextureBrowserController.RowsPerPage)}", new(748, 420, 180, 48), theme.Muted, 12);
        Button(canvas, theme, new(830, 566, 100, 32), controller.Text("textures.mods", "Mods"));
        Label(canvas, controller.Selected?.Detail ?? "", new(24, 474, 906, 44), theme.Muted, 13);
        Label(canvas, controller.Filtering ? controller.Text("textures.filtering", "Filtering catalog…") : controller.Message, new(24, 522, 790, 70), theme.Foreground, 14);
        Label(canvas, controller.Text("textures.hints", "Arrows: rows / branches | Space: checkbox | Confirm: expand / select\nShoulders: commands | Tab: search | Detail: Mods | Back: cancel"), new(24, 606, 780, 44), theme.Muted, 12);
        Button(canvas, theme, new(830, 610, 100, 32), controller.Busy ? controller.Text("textures.cancel", "Cancel") : controller.Text("common.back", "Back"));
    }
    public override void Pointer(float x, float y)
    {
        if (new UiRect(250, 20, 680, 34).Contains(x, y))
        {
            controller.FocusSearch();
            return;
        }
        for (int index = 0; index < 10; index++)
        {
            if (CommandBounds(index).Contains(x, y))
            {
                controller.Execute(index);
                return;
            }
        }
        if (y >= 166 && y < 472 && x >= 18 && x <= (controller.Matching ? 532 : 732))
        {
            int row = controller.Page * TextureBrowserController.RowsPerPage + (int)((y - 166) / 34);
            if (row < controller.Rows.Count)
            {
                controller.Select(row);
                if (controller.Selected?.GroupNode == true && x > 72 + controller.Selected.Depth * 8)
                {
                    controller.ToggleExpansion();
                }
                else
                {
                    controller.Toggle();
                }
            }
        }
        if (new UiRect(748, 376, 56, 32).Contains(x, y))
        {
            controller.SelectAll();
        }
        if (new UiRect(810, 376, 56, 32).Contains(x, y))
        {
            controller.ClearSelection();
        }
        if (new UiRect(872, 376, 56, 32).Contains(x, y))
        {
            controller.SelectFiltered();
        }
        if (new UiRect(830, 566, 100, 32).Contains(x, y))
        {
            controller.ManageMods?.Invoke();
        }
        if (new UiRect(830, 610, 100, 32).Contains(x, y))
        {
            controller.Cancel();
        }
    }
    private void Label(IUiCanvas canvas, string text, UiRect bounds, UiColor color, float size)
    {
        canvas.PushClip(bounds);
        try
        {
            canvas.Text(text, size * fontScale, bounds, color);
        }
        finally
        {
            canvas.PopClip();
        }
    }
    private void Button(IUiCanvas canvas, UiTheme theme, UiRect bounds, string text, bool selected = false)
    {
        canvas.Fill(bounds, selected ? theme.Highlight : new UiColor(50, 50, 54));
        Label(canvas, text, new(bounds.X + 6, bounds.Y + 6, bounds.Width - 12, bounds.Height - 6), theme.Foreground, 13);
    }
}
