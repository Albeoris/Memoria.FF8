using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private void SaveOriginalPlayerSlotWithGuardianForceReview(int slot)
    {
        GameState snapshot = session.PrepareSnapshot();
        NativeCharacterVitals targetSource = ReviewOriginalGuardianForceExport(snapshot);
        playerSaves.Save(slot, session, ValidatePlayerState, targetSource, allowGuardianForceLoss: true);
    }

    private NativeCharacterVitals ReviewOriginalGuardianForceExport(GameState snapshot)
    {
        if (PlayerSaveStore.RequiresMemoriaFormat(snapshot, allowGuardianForceProjection: true))
        {
            throw new NotSupportedException("This game's gameplay rules require a Memoria save slot.");
        }
        using GameAssets assets = new(options.GameDirectory, resources: resourceHost?.Resources);
        NativeCharacterVitals targetSource = assets.ReadNewGameCharacterVitals();
        NativeGuardianForceLegacyExportPlan plan = NativeGuardianForceLegacyCompatibility.Analyze(snapshot, targetSource);
        if (plan.RequiresConfirmation)
        {
            bool confirmed;
            try
            {
                confirmed = GuardianForceLegacyExportReview.Confirm(Window.Handle, plan);
            }
            finally
            {
                DrainGuardianEditorDialogInput();
            }
            if (!confirmed)
            {
                throw new OperationCanceledException("Original save export cancelled.");
            }
        }
        return targetSource;
    }
}

internal static class GuardianForceLegacyExportReview
{
    public static bool Confirm(nint owner, NativeGuardianForceLegacyExportPlan plan)
    {
        System.Windows.Window dialog = new()
        {
            Title = "Original FFVIII GF conversion",
            Width = 760,
            Height = 560,
            MinWidth = 420,
            MinHeight = 300,
            MaxWidth = System.Windows.SystemParameters.WorkArea.Width,
            MaxHeight = System.Windows.SystemParameters.WorkArea.Height,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false
        };
        _ = new System.Windows.Interop.WindowInteropHelper(dialog) { Owner = owner };
        System.Windows.Controls.DockPanel panel = new()
        {
            Margin = new System.Windows.Thickness(16)
        };
        System.Windows.Controls.TextBlock explanation = new()
        {
            Text = "Original FFVIII saves use the target game's GF definitions. Review every change below. The active Memoria game keeps its full GF configuration and progression.",
            TextWrapping = System.Windows.TextWrapping.Wrap,
            Margin = new System.Windows.Thickness(0, 0, 0, 12)
        };
        System.Windows.Controls.DockPanel.SetDock(explanation, System.Windows.Controls.Dock.Top);
        panel.Children.Add(explanation);
        System.Windows.Controls.StackPanel buttons = new()
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new System.Windows.Thickness(0, 12, 0, 0)
        };
        System.Windows.Controls.Button cancel = new()
        {
            Content = "Cancel",
            IsCancel = true,
            MinWidth = 100,
            Padding = new System.Windows.Thickness(12, 6, 12, 6)
        };
        System.Windows.Controls.Button export = new()
        {
            Content = "Export this copy",
            MinWidth = 140,
            Margin = new System.Windows.Thickness(12, 0, 0, 0),
            Padding = new System.Windows.Thickness(12, 6, 12, 6)
        };
        export.Click += (_, _) => dialog.DialogResult = true;
        buttons.Children.Add(cancel);
        buttons.Children.Add(export);
        System.Windows.Controls.DockPanel.SetDock(buttons, System.Windows.Controls.Dock.Bottom);
        panel.Children.Add(buttons);
        panel.Children.Add(new System.Windows.Controls.TextBox
        {
            Text = string.Join(Environment.NewLine + Environment.NewLine, plan.Losses.Select(loss => loss.Description)),
            IsReadOnly = true,
            TextWrapping = System.Windows.TextWrapping.Wrap,
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled,
            Padding = new System.Windows.Thickness(8)
        });
        dialog.Content = panel;
        return dialog.ShowDialog() == true;
    }
}
