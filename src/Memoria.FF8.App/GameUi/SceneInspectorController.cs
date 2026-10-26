using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>Inspects a paused field and applies temporary presentation or event overrides through its host.</summary>
public sealed class SceneInspectorController
{
    internal Func<string, string, string>? LocalizeText { get; set; }

    internal string SystemText(string id, string original) => Memoria.FF8.Resources.Localization.SystemTextCatalog.Translate(LocalizeText, id, original);

    internal string FormatSystemText(string id, string original, params object?[] arguments) => Memoria.FF8.Resources.Localization.SystemTextCatalog.Format(SystemText(id, original), original, arguments);

    public const int RowsPerPage = 8;
    public const int DetailLinesPerPage = 4;
    private readonly Func<SceneInspectorSnapshot?> read;
    private readonly Action<SceneInspectorCommand> apply;
    private readonly Action close;
    private readonly UiView view;
    private SceneInspectorEntry[] entries = [];
    private string[] detailLines = [];

    public SceneInspectorController(Func<SceneInspectorSnapshot?> read, Action<SceneInspectorCommand> apply, Action close)
    {
        this.read = read;
        this.apply = apply;
        this.close = close;
        view = new UiView(new SceneInspectorElement(this)) { DesignWidth = 960, DesignHeight = 672, Cancel = Close };
        Refresh();
        view.Resize(960, 672);
    }

    public event Action<MenuSound>? SoundRequested;
    public SceneInspectorSnapshot? Snapshot { get; private set; }
    public SceneInspectorKind Kind { get; private set; }
    public SceneInspectorFilter Filter { get; private set; }
    public SceneInspectorFocus Focus { get; private set; }
    public IReadOnlyList<SceneInspectorEntry> Entries => entries;
    public SceneInspectorEntry? Selected => entries.ElementAtOrDefault(SelectedIndex);
    public int SelectedIndex { get; private set; }
    public int Page => SelectedIndex / RowsPerPage;
    public int PageCount => Math.Max(1, (entries.Length + RowsPerPage - 1) / RowsPerPage);
    public int DetailPage { get; private set; }
    public int DetailPageCount => Math.Max(1, (detailLines.Length + DetailLinesPerPage - 1) / DetailLinesPerPage);
    public IEnumerable<string> VisibleDetails => detailLines.Skip(DetailPage * DetailLinesPerPage).Take(DetailLinesPerPage);
    private Func<string>? messageText;
    public string Message => messageText?.Invoke() ?? SystemText("inspector.message.initial", "Temporary overrides clear on Reset all or field change.");

    internal string KindLabel(SceneInspectorKind kind) => kind switch
    {
        SceneInspectorKind.Layer => SystemText("inspector.tab.layers", "Layers"),
        SceneInspectorKind.Object => SystemText("inspector.tab.objects", "Objects"),
        SceneInspectorKind.Event => SystemText("inspector.tab.events", "Events"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    internal string FilterLabel => Filter switch
    {
        SceneInspectorFilter.All => SystemText("inspector.filter.all", "All"),
        SceneInspectorFilter.Enabled => SystemText("inspector.filter.enabled", "Enabled"),
        SceneInspectorFilter.Disabled => SystemText("inspector.filter.disabled", "Disabled"),
        _ => throw new InvalidOperationException("Unknown scene inspector filter.")
    };

    public UiView BuildView()
    {
        return view;
    }

    public void Refresh()
    {
        string? selected = Selected?.Id;
        int? fieldId = Snapshot?.FieldId;
        Snapshot = read();
        if (Snapshot?.FieldId != fieldId)
        {
            selected = null;
            SelectedIndex = 0;
            DetailPage = 0;
        }
        entries = Snapshot?.Entries.Where(entry => entry.Kind == Kind && (Filter == SceneInspectorFilter.All || entry.Enabled == (Filter == SceneInspectorFilter.Enabled))).ToArray() ?? [];
        int preserved = Array.FindIndex(entries, entry => entry.Id == selected);
        SelectedIndex = preserved >= 0 ? preserved : Math.Clamp(SelectedIndex, 0, Math.Max(0, entries.Length - 1));
        UpdateDetails();
    }

    public void Navigate(MenuInput input)
    {
        switch (input)
        {
            case MenuInput.Cancel:
                Close();
                break;
            case MenuInput.Tab:
                SetFocus((SceneInspectorFocus)(((int)Focus + 1) % 4));
                break;
            case MenuInput.Left:
            case MenuInput.Right:
                SetKind((SceneInspectorKind)(((int)Kind + (input == MenuInput.Left ? 2 : 1)) % 3));
                break;
            case MenuInput.Alternate:
                CycleFilter();
                break;
            case MenuInput.PageLeft:
            case MenuInput.PageRight:
                ChangeDetailPage(input == MenuInput.PageLeft ? -1 : 1);
                break;
            case MenuInput.Up:
            case MenuInput.Down:
            case MenuInput.PageUp:
            case MenuInput.PageDown:
                int direction = input is MenuInput.Up or MenuInput.PageUp ? -1 : 1;
                int distance = input is MenuInput.PageUp or MenuInput.PageDown ? RowsPerPage : 1;
                Select(SelectedIndex + direction * distance);
                break;
            case MenuInput.Confirm:
                ActivateFocused();
                break;
        }
    }

    public void SetKind(SceneInspectorKind kind)
    {
        if (Kind == kind)
        {
            return;
        }
        Kind = kind;
        SelectedIndex = 0;
        DetailPage = 0;
        Focus = SceneInspectorFocus.Entries;
        Refresh();
        SoundRequested?.Invoke(MenuSound.Move);
    }

    public void SetFocus(SceneInspectorFocus focus)
    {
        Focus = focus;
        SoundRequested?.Invoke(MenuSound.Move);
    }

    public void Select(int index)
    {
        int next = Math.Clamp(index, 0, Math.Max(0, entries.Length - 1));
        if (next != SelectedIndex)
        {
            DetailPage = 0;
            SoundRequested?.Invoke(MenuSound.Move);
        }
        SelectedIndex = next;
        Focus = SceneInspectorFocus.Entries;
        UpdateDetails();
    }

    public void CycleFilter()
    {
        Filter = (SceneInspectorFilter)(((int)Filter + 1) % 3);
        SelectedIndex = 0;
        DetailPage = 0;
        Refresh();
        SoundRequested?.Invoke(MenuSound.Move);
    }

    public void ToggleSelected()
    {
        if (Snapshot is null || Selected is not { } selected)
        {
            return;
        }
        Apply(new SceneInspectorCommand(selected.Id, !selected.Enabled), () => FormatSystemText("inspector.message.toggle", "{0}: {1} for this field session.", selected.Name, !selected.Enabled ? SystemText("inspector.state.enabled", "enabled") : SystemText("inspector.state.disabled", "disabled")));
    }

    public void ResetAll()
    {
        if (Snapshot is not null)
        {
            Apply(new SceneInspectorCommand(Reset: true), () => SystemText("inspector.message.reset", "Temporary overrides reset. Native state and configured mods apply."));
        }
    }

    public void ChangeDetailPage(int direction)
    {
        DetailPage = Math.Clamp(DetailPage + direction, 0, DetailPageCount - 1);
    }

    public void Close()
    {
        SoundRequested?.Invoke(MenuSound.Cancel);
        close();
    }

    private void ActivateFocused()
    {
        switch (Focus)
        {
            case SceneInspectorFocus.Entries:
                ToggleSelected();
                break;
            case SceneInspectorFocus.Filter:
                CycleFilter();
                break;
            case SceneInspectorFocus.Reset:
                ResetAll();
                break;
            case SceneInspectorFocus.Back:
                Close();
                break;
        }
    }

    private void Apply(SceneInspectorCommand command, Func<string> message)
    {
        try
        {
            apply(command);
            messageText = message;
            SoundRequested?.Invoke(MenuSound.Confirm);
        }
        catch (Exception error) when (error is NotSupportedException or InvalidDataException or ArgumentException or InvalidOperationException)
        {
            messageText = () => FormatSystemText("inspector.message.error", "Cannot apply override: {0}", error.Message);
            SoundRequested?.Invoke(MenuSound.Cancel);
        }
        Refresh();
    }

    private void UpdateDetails()
    {
        detailLines = Selected is { } selected ? new[] { FormatSystemText("inspector.details.identity", "ID: {0}", selected.Id) }.Concat(selected.Details).SelectMany(WrapDetail).ToArray() : [];
        DetailPage = Math.Clamp(DetailPage, 0, DetailPageCount - 1);
    }

    private static IEnumerable<string> WrapDetail(string text)
    {
        foreach (string paragraph in text.Replace("\r", "", StringComparison.Ordinal).Split('\n'))
        {
            string remaining = paragraph;
            while (remaining.Length > 60)
            {
                int space = remaining.LastIndexOf(' ', 60);
                int count = space > 0 ? space : 60;
                yield return remaining[..count];
                remaining = remaining[count..].TrimStart();
            }
            yield return remaining;
        }
    }
}
