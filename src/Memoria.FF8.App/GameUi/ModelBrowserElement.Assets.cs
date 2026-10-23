using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed partial class ModelBrowserElement
{
    private static UiRect AssetButton(int index) => new(514 + index * 139, 455, 133, 28);
    private static UiRect PlaybackButton(int index) => new(18 + index * 79, 466, 74, 27);
    private static readonly UiRect AssetSearchBounds = new(514, 129, 416, 30);
    private static readonly UiRect AssetContinueBounds = new(514, 490, 416, 30);
    private const int AssetPageSize = 8;

    private void DrawAssets(IUiCanvas canvas, UiTheme theme)
    {
        Label(canvas, (controller.ImportSelection ? "IMPORT SELECTION" : "EXPORT SELECTION") + " · " + controller.Selected?.Name + " · " + controller.CurrentVariant?.Name, 16, new UiRect(24, 52, 900, 24), theme.Foreground);
        theme.Panel(canvas, new UiRect(504, 84, 438, 440));
        Button(canvas, theme, new UiRect(514, 92, 202, 29), "Objects", !controller.ClipsTab);
        Button(canvas, theme, new UiRect(724, 92, 206, 29), "Clips", controller.ClipsTab);
        Button(canvas, theme, AssetSearchBounds, controller.AssetSearch.Length == 0 ? "Search assets (* / ?)" : controller.AssetSearch, controller.SearchFocused);
        IReadOnlyList<ModelBrowserAssetRow> rows = controller.VisibleAssets;
        int first = controller.AssetIndex / AssetPageSize * AssetPageSize;
        for (int index = first; index < Math.Min(first + AssetPageSize, rows.Count); index++)
        {
            ModelBrowserAssetRow row = rows[index];
            float y = 167 + (index - first) * 35;
            string check = !row.Supported ? "[!]" : controller.SelectedAssets.Contains(row.Id) ? "[x]" : "[ ]";
            Button(canvas, theme, new UiRect(514, y, 416, 32), check + " " + row.Name, index == controller.AssetIndex && !controller.PreviewFocused);
        }
        ModelBrowserAssetRow? active = rows.ElementAtOrDefault(controller.AssetIndex);
        string state = active is null ? "" : new[] { "[ ]", "[-]", "[x]" }[controller.GroupSelection(active.Group)];
        Button(canvas, theme, new UiRect(514, 421, 416, 27), active is null ? "No matching assets" : state + " Group: " + active.Group);
        Button(canvas, theme, AssetButton(0), "Select all");
        Button(canvas, theme, AssetButton(1), "Select visible");
        Button(canvas, theme, AssetButton(2), "Clear all");
        Button(canvas, theme, AssetContinueBounds, controller.ImportSelection ? "Validate and install selection" : "Export selection to GLB");
        Label(canvas, controller.DiscoveryStatus, 12, new UiRect(18, 94, 474, 26), theme.Foreground);
        string[] controls = [controller.Playing ? "Pause" : "Play", "< frame", "frame >", controller.Loop ? "Loop: on" : "Loop: off", $"{controller.PlaybackSpeed:0.##}x", "Reset"];
        for (int index = 0; index < controls.Length; index++)
        {
            Button(canvas, theme, PlaybackButton(index), controls[index]);
        }
        float fraction = controller.ClipDuration > 0 ? controller.PlaybackSeconds / controller.ClipDuration : 0;
        canvas.Fill(new UiRect(18, 505, 474, 9), theme.BorderLight);
        canvas.Fill(new UiRect(18, 505, 474 * fraction, 9), theme.Highlight);
        int frame = (int)MathF.Round(controller.PlaybackSeconds / controller.ClipFrameSeconds);
        int count = (int)MathF.Round(controller.ClipDuration / controller.ClipFrameSeconds) + 1;
        Label(canvas, $"Frame {frame}/{count} · {controller.SelectionSummary}", 12, new UiRect(24, 542, 894, 28), theme.Foreground);
        Label(canvas, active?.Detail ?? "Rigs, materials and textures are required dependencies.", 12, new UiRect(440, 578, 490, 34), theme.Muted);
    }

    private void AssetsPointer(float x, float y)
    {
        if (BackBounds.Contains(x, y))
        {
            controller.Cancel();
        }
        else if (AssetSearchBounds.Contains(x, y))
        {
            controller.SetSearchFocused(true);
        }
        else if (new UiRect(514, 92, 202, 29).Contains(x, y))
        {
            controller.SetAssetTab(false);
        }
        else if (new UiRect(724, 92, 206, 29).Contains(x, y))
        {
            controller.SetAssetTab(true);
        }
        else if (AssetContinueBounds.Contains(x, y))
        {
            controller.ContinueAssetSelection();
        }
        else if (AssetButton(0).Contains(x, y))
        {
            controller.SelectAssets(false);
        }
        else if (AssetButton(1).Contains(x, y))
        {
            controller.SelectAssets(true);
        }
        else if (AssetButton(2).Contains(x, y))
        {
            controller.SelectAssets(false, true);
        }
        else if (new UiRect(514, 421, 416, 27).Contains(x, y) && controller.VisibleAssets.ElementAtOrDefault(controller.AssetIndex) is { } group)
        {
            controller.ToggleGroup(group.Group);
        }
        else if (new UiRect(514, 167, 416, 280).Contains(x, y) && y < 414)
        {
            int index = controller.AssetIndex / AssetPageSize * AssetPageSize + (int)((y - 167) / 35);
            controller.SelectAssetRow(index, true);
            if (x < 553 && controller.VisibleAssets.ElementAtOrDefault(index) is { } row)
            {
                controller.ToggleAsset(row.Id);
            }
        }
        else if (new UiRect(18, 500, 474, 20).Contains(x, y))
        {
            controller.Seek((x - 18) / 474 * controller.ClipDuration);
        }
        else if (controller.PreviewBounds.Contains(x, y))
        {
            controller.SetPreviewFocused();
        }
        else
        {
            for (int index = 0; index < 6; index++)
            {
                if (!PlaybackButton(index).Contains(x, y))
                {
                    continue;
                }
                switch (index)
                {
                    case 0:
                        controller.TogglePlayback();
                        break;
                    case 1:
                        controller.StepFrame(-1);
                        break;
                    case 2:
                        controller.StepFrame(1);
                        break;
                    case 3:
                        controller.ToggleLoop();
                        break;
                    case 4:
                        controller.CyclePlaybackSpeed();
                        break;
                    case 5:
                        controller.ResetView();
                        break;
                }
                return;
            }
        }
    }
}
