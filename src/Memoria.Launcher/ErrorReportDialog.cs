using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Memoria.FF8.Platform;
using Memoria.FF8.Runtime;

namespace Memoria.Launcher;

internal sealed class ErrorReportDialog : Window
{
    private readonly ErrorIncident incident;
    private readonly TextBlock progress = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBox location = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap };
    private readonly TextBox url = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap };
    private readonly TextBox summary = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
    private readonly Button prepare = new() { Content = "Report error" };
    private readonly Button delete = new() { Content = "Delete incident" };
    private readonly StackPanel recovery = new() { Spacing = 8 };
    private readonly Expander recoverySection = new() { Header = "Prepared report and recovery actions", IsExpanded = true, IsVisible = false };
    private Task presentation = Task.CompletedTask;
    private PreparedErrorReport? report;
    private bool preparing;
    private DateTimeOffset lastOpen;

    public ErrorReportDialog(string message, ErrorIncident incident)
    {
        this.incident = incident;
        Title = "Memoria — Error";
        Width = 660;
        Height = 680;
        MinWidth = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        StackPanel content = new() { Margin = new Thickness(24), Spacing = 12 };
        content.Children.Add(new TextBox { Text = ReportPrivacy.Text(message), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 220 });
        RefreshSummary();
        content.Children.Add(summary);
        AddCopy(content, "Copy incident path", () => incident.GetStoragePath());
        WrapPanel actions = new() { Orientation = Orientation.Horizontal };
        prepare.Margin = new Thickness(0, 0, 12, 8);
        delete.Margin = new Thickness(0, 0, 0, 8);
        actions.Children.Add(prepare);
        actions.Children.Add(delete);
        content.Children.Add(actions);
        content.Children.Add(progress);
        recovery.Children.Add(new TextBlock { Text = ErrorReport.PublicInstructions, TextWrapping = TextWrapping.Wrap });
        recovery.Children.Add(location);
        AddAction(recovery, "Open GitHub again", OpenGitHub);
        AddAction(recovery, "Show ZIP", () => DisplayDesktopResult(ReportDesktop.RevealFile(report!.ArchivePath)));
        AddCopy(recovery, "Copy full description", () => report!.Description);
        AddCopy(recovery, "Copy report path", () => report!.ArchivePath);
        recovery.Children.Add(url);
        AddCopy(recovery, "Copy issue URL", () => report!.IssueUrl);
        recoverySection.Content = recovery;
        content.Children.Add(recoverySection);
        string saveDetails = incident.Save is null ? "Save unavailable: " + incident.SaveUnavailableReason : $"Save: {incident.Save.Kind}; {incident.Save.CapturedUtc:O}; {incident.Save.Validation}";
        TextBox details = new() { Text = ReportPrivacy.Text(incident.ExceptionText + Environment.NewLine + saveDetails), IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100, MaxHeight = 260 };
        StackPanel detailPanel = new() { Spacing = 8 };
        detailPanel.Children.Add(details);
        AddCopy(detailPanel, "Copy error details", () => ReportPrivacy.Text(incident.ExceptionText));
        content.Children.Add(new Expander { Header = "Details", Content = detailPanel });
        Button close = new() { Content = "Close", IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();
        content.Children.Add(close);
        Content = new ScrollViewer { Content = content };
        Opened += (_, _) => presentation = MarkPresentedAsync();
        prepare.Click += async (_, _) => await PrepareAsync();
        delete.Click += async (_, _) => await DeleteAsync();
        Closing += (_, args) => args.Cancel = preparing;
        Closed += (_, _) => report?.Dispose();
    }

    private void RefreshSummary()
    {
        summary.Text = $"Incident time: {incident.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz}{Environment.NewLine}" + ReportPrivacy.Text($"{incident.Subsystem}: {incident.Exception.Type}: {incident.Exception.Message}") + $"{Environment.NewLine}Incident file: {incident.GetStoragePath()}";
    }

    private async Task MarkPresentedAsync()
    {
        try
        {
            await Task.Run(() => incident.MarkPresented());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            progress.Text = "Could not record that this report was shown: " + exception.Message;
        }
    }

    private async Task DeleteAsync()
    {
        if (preparing)
        {
            return;
        }
        preparing = true;
        prepare.IsEnabled = false;
        delete.IsEnabled = false;
        bool deleted = false;
        try
        {
            if (!await LauncherDialogs.ConfirmAsync(this, "Delete incident", "Delete this local incident and its prepared ZIP, if any? This cannot be undone. Ordinary saves and original logs will be kept. Nothing will be sent to GitHub."))
            {
                return;
            }
            await presentation;
            report?.Dispose();
            report = null;
            recoverySection.IsVisible = false;
            await Task.Run(() => incident.Delete());
            deleted = true;
        }
        catch (Exception exception)
        {
            progress.Text = "Could not delete incident: " + exception.Message;
        }
        finally
        {
            preparing = false;
            prepare.IsEnabled = true;
            delete.IsEnabled = true;
        }
        if (deleted)
        {
            Close();
        }
    }

    private async Task PrepareAsync()
    {
        if (preparing)
        {
            return;
        }
        if (report is not null)
        {
            OpenGitHub();
            return;
        }
        preparing = true;
        prepare.IsEnabled = false;
        delete.IsEnabled = false;
        progress.Text = "Preparing…";
        try
        {
            await presentation;
            report = await RuntimeErrorReport.PrepareAsync(incident);
            RefreshSummary();
            location.Text = $"{report.ArchivePath}{Environment.NewLine}{new FileInfo(report.ArchivePath).Length:N0} bytes";
            url.Text = report.IssueUrl;
            recoverySection.IsVisible = true;
            progress.Text = "Ready";
            string? revealFailure = ReportDesktop.RevealFile(report.ArchivePath);
            OpenGitHub();
            if (revealFailure is not null)
            {
                progress.Text += Environment.NewLine + revealFailure;
            }
        }
        catch (Exception exception)
        {
            progress.Text = "Preparation failed: " + exception.Message;
        }
        finally
        {
            preparing = false;
            prepare.IsEnabled = true;
            delete.IsEnabled = true;
        }
    }

    private void OpenGitHub()
    {
        if (report is null || DateTimeOffset.UtcNow - lastOpen < TimeSpan.FromSeconds(1))
        {
            return;
        }
        lastOpen = DateTimeOffset.UtcNow;
        string? failure = ReportDesktop.OpenUrl(report.IssueUrl);
        progress.Text = failure is null ? "GitHub form opened. Attach the ZIP in your browser." : "Ready. " + failure;
    }

    private void DisplayDesktopResult(string? failure)
    {
        progress.Text = failure ?? "Ready";
    }

    private static void AddAction(StackPanel panel, string label, Action action)
    {
        Button button = new() { Content = label };
        button.Click += (_, _) => action();
        panel.Children.Add(button);
    }

    private void AddCopy(StackPanel panel, string label, Func<string> value)
    {
        Button button = new() { Content = label };
        button.Click += async (_, _) =>
        {
            try
            {
                if (Clipboard is null)
                {
                    progress.Text = "Clipboard unavailable. Select and copy the text in Details or the prepared report fields.";
                    return;
                }
                await Clipboard.SetTextAsync(value());
            }
            catch (Exception exception)
            {
                progress.Text = "Could not copy: " + exception.Message;
            }
        };
        panel.Children.Add(button);
    }
}
