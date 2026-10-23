using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed record ModelBrowserAssetRow(string Id, string Name, string Group, bool IsClip, bool Supported = true, string Detail = "", bool DefaultSelected = true)
{
    public string? RigId { get; init; }
}

public sealed partial class ModelBrowserController
{
    private readonly List<ModelBrowserAssetRow> assets = [];
    private readonly HashSet<string> assetIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> selectedAssets = new(StringComparer.Ordinal);
    private Action? assetContinue;
    private bool manuallySelectedAssets;
    private string? beforeAssetHints;
    public bool AssetsOpen { get; private set; }
    public bool ImportSelection { get; private set; }
    public bool ClipsTab { get; private set; }
    public bool PreviewFocused { get; private set; }
    public string AssetSearch { get; private set; } = "";
    public string DiscoveryStatus { get; private set; } = "Discovering";
    public int AssetIndex { get; private set; }
    public string? ActiveObject { get; private set; }
    public string? ActiveClip { get; private set; }
    public bool Playing { get; private set; } = true;
    public bool Loop { get; private set; } = true;
    public float PlaybackSpeed { get; private set; } = 1;
    public float PlaybackSeconds { get; private set; }
    public float ClipDuration { get; private set; }
    public float ClipFrameSeconds { get; private set; } = 1f / 30;
    public IReadOnlyList<ModelBrowserAssetRow> Assets => assets;
    public IReadOnlySet<string> SelectedAssets => selectedAssets;
    public IReadOnlyList<ModelBrowserAssetRow> VisibleAssets => assets.Where(row => row.IsClip == ClipsTab && MatchesSearch(row.Name + " " + row.Group + " " + row.Detail, AssetSearch)).ToArray();
    public int HiddenSelectedCount
    {
        get
        {
            HashSet<string> visible = VisibleAssets.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
            return selectedAssets.Count(id => !visible.Contains(id));
        }
    }
    public string SelectionSummary => $"{assets.Count(row => !row.IsClip && selectedAssets.Contains(row.Id))} objects · {assets.Where(row => row.IsClip && selectedAssets.Contains(row.Id)).Select(row => row.Group).Distinct().Count()} groups · {assets.Count(row => row.IsClip && selectedAssets.Contains(row.Id))} components · {assets.Where(row => selectedAssets.Contains(row.Id)).Select(row => row.RigId).Where(rig => rig is not null).Distinct().Count()} rigs · {assets.Count} discovered · {HiddenSelectedCount} hidden";

    public void ResetAssets()
    {
        assets.Clear();
        assetIds.Clear();
        selectedAssets.Clear();
        manuallySelectedAssets = false;
        ActiveObject = null;
        ActiveClip = null;
        AssetIndex = 0;
        PlaybackSeconds = 0;
        DiscoveryStatus = "Discovering";
    }

    public void AddAsset(ModelBrowserAssetRow row)
    {
        if (!assetIds.Add(row.Id))
        {
            return;
        }
        assets.Add(row);
        if (row.Supported && row.DefaultSelected && !manuallySelectedAssets)
        {
            selectedAssets.Add(row.Id);
        }
        if (row.IsClip && row.Supported && row.DefaultSelected)
        {
            ActiveClip ??= row.Id;
        }
        if (!row.IsClip && row.Supported)
        {
            ActiveObject ??= row.Id;
        }
    }

    public void SetDiscoveryStatus(string status) => DiscoveryStatus = status;

    public void OpenAssetPanel(Action? continueAction = null, bool importing = false)
    {
        if (!AssetsOpen)
        {
            beforeAssetHints = Hints;
        }
        Hints = "Tab: preview / list · Arrows: navigate / rotate · Confirm / Space: checkbox\nClick label: preview · Select all includes hidden rows · Back: cancel selection";
        AssetsOpen = true;
        ImportSelection = importing;
        assetContinue = continueAction;
        PreviewFocused = false;
        SearchFocused = false;
        Mode = ModelBrowserMode.Viewer;
    }

    public void CloseAssetPanel()
    {
        AssetsOpen = false;
        if (beforeAssetHints is not null)
        {
            Hints = beforeAssetHints;
            beforeAssetHints = null;
        }
        assetContinue = null;
        SearchFocused = false;
        PreviewFocused = false;
    }

    public void ContinueAssetSelection()
    {
        if (selectedAssets.Count > 0)
        {
            assetContinue?.Invoke();
        }
    }

    public void SetAssetSearch(string search)
    {
        AssetSearch = search;
        AssetIndex = 0;
    }

    public void SetAssetTab(bool clips)
    {
        ClipsTab = clips;
        AssetIndex = 0;
        SearchFocused = false;
        PreviewFocused = false;
    }

    public void ToggleAsset(string id)
    {
        ModelBrowserAssetRow? row = assets.FirstOrDefault(row => row.Id == id);
        if (row?.Supported != true)
        {
            return;
        }
        manuallySelectedAssets = true;
        if (!selectedAssets.Remove(id))
        {
            selectedAssets.Add(id);
        }
    }

    public int GroupSelection(string group)
    {
        ModelBrowserAssetRow[] rows = assets.Where(row => row.Group == group && row.Supported && row.IsClip == ClipsTab).ToArray();
        int count = rows.Count(row => selectedAssets.Contains(row.Id));
        return count == 0 ? 0 : count == rows.Length ? 2 : 1;
    }

    public void ToggleGroup(string group)
    {
        bool select = GroupSelection(group) != 2;
        manuallySelectedAssets = true;
        foreach (ModelBrowserAssetRow row in assets.Where(row => row.Group == group && row.Supported && row.IsClip == ClipsTab))
        {
            if (select)
            {
                selectedAssets.Add(row.Id);
            }
            else
            {
                selectedAssets.Remove(row.Id);
            }
        }
    }

    public void SelectAssets(bool visibleOnly, bool clear = false)
    {
        manuallySelectedAssets = true;
        if (clear)
        {
            selectedAssets.Clear();
            return;
        }
        foreach (ModelBrowserAssetRow row in visibleOnly ? VisibleAssets : assets)
        {
            if (row.Supported)
            {
                selectedAssets.Add(row.Id);
            }
        }
    }

    public void SelectAssetRow(int index, bool preview = false)
    {
        IReadOnlyList<ModelBrowserAssetRow> visible = VisibleAssets;
        AssetIndex = Math.Clamp(index, 0, Math.Max(0, visible.Count - 1));
        PreviewFocused = false;
        SearchFocused = false;
        if (preview && visible.ElementAtOrDefault(AssetIndex) is { Supported: true } row)
        {
            if (row.IsClip)
            {
                ActiveClip = row.Id;
                PlaybackSeconds = 0;
            }
            else
            {
                ActiveObject = row.Id;
            }
        }
    }

    public void SetPreviewFocused()
    {
        PreviewFocused = true;
        SearchFocused = false;
    }
    public void TogglePlayback() => Playing = !Playing;
    public void ToggleLoop() => Loop = !Loop;
    public void CyclePlaybackSpeed() => PlaybackSpeed = PlaybackSpeed >= 2 ? 0.25f : PlaybackSpeed * 2;
    public void ResetView()
    {
        RotationX = 0;
        RotationY = 0;
        Zoom = 1;
    }
    public void SetClipTiming(float duration, float frameSeconds)
    {
        ClipDuration = Math.Max(0, duration);
        ClipFrameSeconds = float.IsFinite(frameSeconds) && frameSeconds > 0 ? frameSeconds : 1f / 30;
    }
    public void Seek(float seconds) => PlaybackSeconds = Math.Clamp(seconds, 0, ClipDuration);
    public void StepFrame(int direction)
    {
        Playing = false;
        Seek(PlaybackSeconds + direction * ClipFrameSeconds);
    }
    public void AdvancePlayback(float seconds)
    {
        if (Playing && ClipDuration > 0)
        {
            float next = PlaybackSeconds + Math.Max(0, seconds) * PlaybackSpeed;
            PlaybackSeconds = Loop ? next % (ClipDuration + ClipFrameSeconds) : Math.Min(next, ClipDuration);
        }
    }

    private void NavigateAssets(MenuInput input)
    {
        if (input == MenuInput.Tab)
        {
            PreviewFocused = !PreviewFocused;
            SearchFocused = false;
        }
        else if (input == MenuInput.Confirm)
        {
            if (!PreviewFocused && VisibleAssets.ElementAtOrDefault(AssetIndex) is { } row)
            {
                ToggleAsset(row.Id);
                SelectAssetRow(AssetIndex, true);
            }
        }
        else if (PreviewFocused)
        {
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
            }
        }
        else if (input is MenuInput.Up or MenuInput.Down or MenuInput.PageUp or MenuInput.PageDown)
        {
            int step = input is MenuInput.PageUp or MenuInput.PageDown ? 7 : 1;
            SelectAssetRow(AssetIndex + (input is MenuInput.Up or MenuInput.PageUp ? -step : step), true);
        }
        else if (input is MenuInput.Left or MenuInput.Right)
        {
            SetAssetTab(input == MenuInput.Right);
        }
    }
}
