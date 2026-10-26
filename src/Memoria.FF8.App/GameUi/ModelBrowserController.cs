using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed record ModelBrowserVariant(string Id, string Name);
public sealed record ModelBrowserEntry(string Id, string Name, string Category, string SearchText, IReadOnlyList<ModelBrowserVariant> Variants, IReadOnlyList<string>? Details = null);
public enum ModelBrowserMode
{
    Catalog,
    Viewer,
    Commands
}

/// <summary>Renderer-independent model catalog, viewer and interchange controls.</summary>
public sealed partial class ModelBrowserController
{
    internal Func<string, string, string>? LocalizeText { get; set; }

    internal string SystemText(string id, string original) => LocalizeText?.Invoke(Memoria.FF8.Resources.Localization.SystemTextCatalog.Key(id), original) ?? original;

    internal string FormatSystemText(string id, string original, params object?[] arguments) => Memoria.FF8.Resources.Localization.SystemTextCatalog.Format(SystemText(id, original), original, arguments);

    public const int RowsPerPage = 12;
    private readonly Action close;
    private readonly Action<ModelBrowserEntry, ModelBrowserVariant> export;
    private readonly Action<ModelBrowserEntry, ModelBrowserVariant> import;
    private readonly UiView view;
    private ModelBrowserEntry[] catalog = [];
    private ModelBrowserEntry[] entries = [];

    public ModelBrowserController(IEnumerable<ModelBrowserEntry> entries, Action close, Action<ModelBrowserEntry, ModelBrowserVariant> export, Action<ModelBrowserEntry, ModelBrowserVariant> import)
    {
        this.close = close;
        this.export = export;
        this.import = import;
        view = new UiView(new ModelBrowserElement(this)) { DesignWidth = 960, DesignHeight = 672, Cancel = Cancel };
        Refresh(entries);
        view.Resize(960, 672);
    }

    public event Action<MenuSound>? SoundRequested;
    public IReadOnlyList<ModelBrowserEntry> Entries => entries;
    public ModelBrowserEntry? Selected => entries.ElementAtOrDefault(SelectedIndex);
    public ModelBrowserVariant? CurrentVariant => Selected?.Variants.ElementAtOrDefault(VariantIndex);
    public int SelectedIndex { get; private set; }
    public int VariantIndex { get; private set; }
    public int Page => SelectedIndex / RowsPerPage;
    public int PageCount => Math.Max(1, (entries.Length + RowsPerPage - 1) / RowsPerPage);
    public ModelBrowserMode Mode { get; private set; }
    public int CommandIndex { get; private set; }
    public string Search { get; private set; } = "";
    public bool SearchFocused { get; private set; }
    public float RotationX { get; private set; }
    public float RotationY { get; private set; }
    public float Zoom { get; private set; } = 1;
    public string Message { get; private set; } = "";
    private string? hints;
    public string Hints
    {
        get => hints ?? SystemText("model.hints.default", "Arrows: select / rotate · Confirm: view / commands · Cancel: back\nShoulders: previous / next model · Page Up / Down: zoom · Alternate: variant");
        private set => hints = value;
    }
    public UiRect PreviewBounds => AssetsOpen ? new UiRect(18, 92, 480, 360) : Mode == ModelBrowserMode.Catalog ? new UiRect(18, 134, 390, 390) : new UiRect(18, 92, 924, 432);

    public UiView BuildView()
    {
        return view;
    }

    public void SetMessage(string message)
    {
        Message = message;
    }

    public void SetHints(string hints)
    {
        Hints = hints;
    }

    public void Refresh(IEnumerable<ModelBrowserEntry> source)
    {
        string? selected = Selected?.Id;
        string? variant = CurrentVariant?.Id;
        catalog = source.ToArray();
        Filter(selected);
        VariantIndex = Math.Max(0, Selected?.Variants.ToList().FindIndex(item => item.Id == variant) ?? 0);
    }

    public void SetSearch(string search)
    {
        string? selected = Selected?.Id;
        Search = search;
        Filter(selected);
    }

    private void Filter(string? selected)
    {
        entries = catalog.Where(entry => MatchesSearch(entry.SearchText + " " + entry.Id + " " + entry.Name + " " + entry.Category + " " + string.Join(" ", entry.Variants.Select(variant => variant.Id + " " + variant.Name)) + " " + string.Join(" ", entry.Details ?? []), Search)).ToArray();
        int preserved = Array.FindIndex(entries, entry => entry.Id == selected);
        SelectedIndex = preserved >= 0 ? preserved : 0;
        if (preserved < 0)
        {
            ResetModel();
        }
    }

    public static bool MatchesSearch(string text, string search)
    {
        // Wildcards are unanchored, just like a plain substring query, without regex backtracking.
        string pattern = "*" + search.Trim() + "*";
        int input = 0;
        int token = 0;
        int star = -1;
        int retry = 0;
        while (input < text.Length)
        {
            if (token < pattern.Length && pattern[token] != '*' && (pattern[token] == '?' || char.ToUpperInvariant(pattern[token]) == char.ToUpperInvariant(text[input])))
            {
                input++;
                token++;
            }
            else if (token < pattern.Length && pattern[token] == '*')
            {
                star = token++;
                retry = input;
            }
            else if (star >= 0)
            {
                token = star + 1;
                input = ++retry;
            }
            else
            {
                return false;
            }
        }
        while (token < pattern.Length && pattern[token] == '*')
        {
            token++;
        }
        return token == pattern.Length;
    }

    public void SetSearchFocused(bool focused)
    {
        SearchFocused = focused && (Mode == ModelBrowserMode.Catalog || AssetsOpen);
    }

    public void TextInput(char character)
    {
        if (SearchFocused && !char.IsControl(character))
        {
            if (AssetsOpen)
            {
                SetAssetSearch(AssetSearch + character);
            }
            else
            {
                SetSearch(Search + character);
            }
        }
    }

    public void Backspace()
    {
        if (SearchFocused && AssetsOpen)
        {
            SetAssetSearch(AssetSearch.Length > 0 ? AssetSearch[..^1] : "");
            return;
        }
        if (SearchFocused && Search.Length > 0)
        {
            SetSearch(Search[..^1]);
        }
    }

    public void Select(int index)
    {
        int next = Math.Clamp(index, 0, Math.Max(0, entries.Length - 1));
        if (next != SelectedIndex)
        {
            SelectedIndex = next;
            ResetModel();
            SoundRequested?.Invoke(MenuSound.Move);
        }
        SearchFocused = false;
    }

    private void ResetModel()
    {
        CloseAssetPanel();
        VariantIndex = 0;
        RotationX = 0;
        RotationY = 0;
        Zoom = 1;
        Message = "";
    }

    public void SelectVariant(int index)
    {
        CloseAssetPanel();
        VariantIndex = Math.Clamp(index, 0, Math.Max(0, (Selected?.Variants.Count ?? 0) - 1));
    }

    public void CycleVariant()
    {
        if (Selected is { Variants.Count: > 0 } entry)
        {
            SelectVariant((VariantIndex + 1) % entry.Variants.Count);
        }
    }

    public void Rotate(float horizontal, float vertical)
    {
        if (Mode == ModelBrowserMode.Viewer && float.IsFinite(horizontal) && float.IsFinite(vertical))
        {
            RotationY = MathF.IEEERemainder(RotationY + horizontal, MathF.Tau);
            RotationX = Math.Clamp(RotationX + vertical, -MathF.PI / 2, MathF.PI / 2);
        }
    }

    public void ZoomBy(float factor)
    {
        if (Mode == ModelBrowserMode.Viewer && float.IsFinite(factor) && factor > 0)
        {
            Zoom = Math.Clamp(Zoom * factor, 0.1f, 10f);
        }
    }

    public void StepModel(int direction)
    {
        if (entries.Length > 0 && direction != 0)
        {
            Select((SelectedIndex + Math.Sign(direction) + entries.Length) % entries.Length);
        }
    }

    public void OpenViewer()
    {
        if (Selected is not null)
        {
            Mode = ModelBrowserMode.Viewer;
            SearchFocused = false;
        }
    }

    public void OpenCommands()
    {
        if (Mode != ModelBrowserMode.Catalog && CurrentVariant is not null)
        {
            Mode = ModelBrowserMode.Commands;
            CommandIndex = 0;
        }
    }

    public void ExecuteCommand(int index)
    {
        if (Mode != ModelBrowserMode.Commands || Selected is not { } entry || CurrentVariant is not { } variant || index is < 0 or > 1)
        {
            return;
        }
        Mode = ModelBrowserMode.Viewer;
        if (index == 0)
        {
            export(entry, variant);
        }
        else
        {
            import(entry, variant);
        }
    }

    public void Cancel()
    {
        SoundRequested?.Invoke(MenuSound.Cancel);
        if (AssetsOpen)
        {
            CloseAssetPanel();
            return;
        }
        if (Mode == ModelBrowserMode.Commands)
        {
            Mode = ModelBrowserMode.Viewer;
        }
        else if (Mode == ModelBrowserMode.Viewer)
        {
            Mode = ModelBrowserMode.Catalog;
        }
        else
        {
            close();
        }
    }

    public void Navigate(MenuInput input)
    {
        if (input == MenuInput.Cancel)
        {
            Cancel();
            return;
        }
        if (AssetsOpen)
        {
            NavigateAssets(input);
            return;
        }
        if (Mode == ModelBrowserMode.Commands)
        {
            if (input is MenuInput.Up or MenuInput.Down)
            {
                CommandIndex = 1 - CommandIndex;
            }
            else if (input == MenuInput.Confirm)
            {
                ExecuteCommand(CommandIndex);
            }
            return;
        }
        if (input is MenuInput.PageLeft or MenuInput.PageRight)
        {
            StepModel(input == MenuInput.PageLeft ? -1 : 1);
            return;
        }
        if (input == MenuInput.Alternate)
        {
            CycleVariant();
            return;
        }
        if (Mode == ModelBrowserMode.Catalog)
        {
            switch (input)
            {
                case MenuInput.Tab:
                    SetSearchFocused(!SearchFocused);
                    break;
                case MenuInput.Up:
                case MenuInput.Down:
                    Select(SelectedIndex + (input == MenuInput.Up ? -1 : 1));
                    break;
                case MenuInput.PageUp:
                case MenuInput.PageDown:
                    Select(SelectedIndex + (input == MenuInput.PageUp ? -RowsPerPage : RowsPerPage));
                    break;
                case MenuInput.Confirm:
                    OpenViewer();
                    break;
            }
            return;
        }
        switch (input)
        {
            case MenuInput.Left:
                Rotate(-0.1f, 0);
                break;
            case MenuInput.Right:
                Rotate(0.1f, 0);
                break;
            case MenuInput.Up:
                Rotate(0, -0.1f);
                break;
            case MenuInput.Down:
                Rotate(0, 0.1f);
                break;
            case MenuInput.PageUp:
                ZoomBy(1.1f);
                break;
            case MenuInput.PageDown:
                ZoomBy(1 / 1.1f);
                break;
            case MenuInput.Confirm:
                OpenCommands();
                break;
        }
    }
}
