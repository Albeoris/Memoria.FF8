using System.Globalization;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private Func<int, int>? previewQuickSaveRetention;
    private Func<int, int, QuickSaveCleanupResult>? commitQuickSaveRetention;
    private int quickSaveDraft;
    private int quickSaveRemovalCount;

    internal void ConnectQuickSaveRetention(Func<int, int> preview, Func<int, int, QuickSaveCleanupResult> commit)
    {
        previewQuickSaveRetention = preview;
        commitQuickSaveRetention = commit;
    }

    private void RegisterQuickSaveRetentionScreen()
    {
        Register(new MenuScreenDefinition
        {
            Id = "quick-save-editor",
            Title = "Config · Quick saves to keep",
            Help = "Adjust the draft, then choose Save. Cancel leaves your saved limit and files unchanged. Shared across all saves.",
            Entries = c =>
            [
                new MenuEntry("quick-save-value", $"Quick saves to keep   {c.quickSaveDraft}", "Left and Right adjust the draft. Existing files stay unchanged until Save.", Action: new MenuAction("quick-save-edit-value")),
                new MenuEntry("save", "Save", "Review and save this limit.", Action: new MenuAction("quick-save-review")),
                new MenuEntry("cancel", "Cancel", "Discard the draft.", Action: new MenuAction("menu-back"))
            ],
            BuildContent = (c, root) => root.Children.Add(c.BuildQuickSaveRetentionEditor())
        });
        Register(new MenuScreenDefinition
        {
            Id = "quick-save-confirm",
            Title = "Remove older quick saves?",
            Help = "Deletion cannot be undone. Ordinary save slots are preserved.",
            Entries = c =>
            [
                new MenuEntry("cancel", "Cancel", "Keep the saved limit and every quick save unchanged.", Action: new MenuAction("menu-back")),
                new MenuEntry("confirm", $"Remove {c.quickSaveRemovalCount} quick saves", "Save the new limit, then remove these older quick saves.", Action: new MenuAction("quick-save-commit"))
            ],
            Details = c => [$"New limit: {c.quickSaveDraft}", $"Older quick saves to remove: {c.quickSaveRemovalCount}", "Your newest quick saves and ordinary save slots are kept."]
        });
    }

    private UiElement BuildQuickSaveRetentionEditor()
    {
        UiGrid grid = Grid([1, 1.2f, 1, 1, 1], [1]);
        Place(grid, Header(), 0);
        var slider = new UiNumericSlider
        {
            Id = "quick-save-value",
            Text = "Quick saves to keep",
            Min = 1,
            Max = 999,
            Step = 1,
            Value = Math.Min(999, quickSaveDraft),
            ValueFormat = "'" + quickSaveDraft.ToString(CultureInfo.InvariantCulture) + "'",
            FontSize = 24 * TextScale,
            PointerSize = 20 * TextScale,
            ValueWidth = Math.Max(66, quickSaveDraft.ToString(CultureInfo.InvariantCulture).Length * 16) * TextScale,
            LabelWidthRatio = 0.52f,
            ValueChanged = StageQuickSaveCount,
            FocusChanged = focused => SelectConfigRow(focused, 0)
        };
        Place(grid, Panel(null, slider), 1);
        for (int index = 1; index < 3; index++)
        {
            int selected = index;
            MenuEntry entry = Entries[index];
            var button = new UiButton
            {
                Id = entry.Id,
                Text = entry.Label,
                FontSize = 24 * TextScale,
                PointerSize = 20 * TextScale,
                FocusChanged = focused => SelectConfigRow(focused, selected),
                Click = () => Activate(selected)
            };
            Place(grid, Panel(null, button), index + 1);
        }
        Place(grid, Footer(), 4);
        return grid;
    }

    private void StageQuickSaveCount(float value)
    {
        quickSaveDraft = (int)Math.Clamp(value, 1, 999);
        _view = null;
    }

    private void ChangeQuickSaveDraft(int direction)
    {
        long next = (long)quickSaveDraft + direction;
        quickSaveDraft = (int)Math.Clamp(next, 1, 999);
        _view = null;
    }

    private bool ExecuteQuickSaveRetentionAction(MenuAction action)
    {
        if (action.Kind is not ("quick-save-open" or "quick-save-edit-value" or "quick-save-review" or "quick-save-commit"))
        {
            return false;
        }
        if (action.Kind == "quick-save-open")
        {
            string value = _readPreferences is not null && !IsFixtureContext ? HostSettingValue("Quick saves") : Data.Settings.GetValueOrDefault("Quick saves", "9");
            quickSaveDraft = int.Parse(value, CultureInfo.InvariantCulture);
            Push("quick-save-editor");
            return true;
        }
        if (action.Kind == "quick-save-edit-value")
        {
            return true;
        }
        if (ScreenId is not ("quick-save-editor" or "quick-save-confirm"))
        {
            return true;
        }
        try
        {
            int removalCount = IsFixtureContext ? 0 : (previewQuickSaveRetention ?? throw new InvalidOperationException("Quick-save retention is unavailable."))(quickSaveDraft);
            if (removalCount > 0 && (action.Kind != "quick-save-commit" || removalCount != quickSaveRemovalCount))
            {
                quickSaveRemovalCount = removalCount;
                if (ScreenId != "quick-save-confirm")
                {
                    Push("quick-save-confirm");
                }
                Message = $"This will remove {removalCount} older quick saves.";
                return true;
            }
            QuickSaveCleanupResult result;
            if (IsFixtureContext)
            {
                Data.Settings["Quick saves"] = quickSaveDraft.ToString(CultureInfo.InvariantCulture);
                result = new QuickSaveCleanupResult(0, 0, null);
            }
            else
            {
                result = (commitQuickSaveRetention ?? throw new InvalidOperationException("Quick-save retention is unavailable."))(quickSaveDraft, removalCount);
            }
            if (ScreenId == "quick-save-confirm")
            {
                Back();
            }
            Back();
            _liveData = null;
            Console.WriteLine(result.Message);
            Message = result.Error is not null || result.Remaining > 0 ? result.Message : "";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            Message = exception.Message;
        }
        return true;
    }
}
