using Memoria.FF8.Platform;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

/// <summary>Shared report preparation, recovery actions and UI; game adapters supply validated evidence.</summary>
internal sealed class ErrorReportPresenter(Action<string> copy, Func<ErrorIncident, Task<PreparedErrorReport>>? prepare = null) : IDisposable
{
    internal ErrorIncident? Incident { get; set; }
    internal Task<ErrorIncident>? Evidence { get; set; }
    internal string Details { get; set; } = "";
    internal string Status { get; set; } = "";
    private string? error;
    private Func<Task<PreparedErrorReport>>? preparePerformance;
    private Action? closePerformance;
    private string? performanceTracePath;
    private Task<PreparedErrorReport>? reportPreparation;
    private PreparedErrorReport? preparedReport;
    private UiView? errorReportView;
    private string? errorReportViewKey;
    private bool reportMousePressed;
    private bool reportDetails;
    private DateTimeOffset reportBrowserOpenedUtc;
    private DateTimeOffset reportFileRevealedUtc;

    internal void Reset(string details)
    {
        ReleasePreparedErrorReport();
        Evidence = null;
        preparePerformance = null;
        closePerformance = null;
        performanceTracePath = null;
        errorReportViewKey = null;
        reportBrowserOpenedUtc = default;
        reportFileRevealedUtc = default;
        Status = "";
        Details = details;
        reportDetails = false;
    }

    internal void ShowPerformanceReport(string tracePath, string scene, Action close, string? context = null)
    {
        string details = $"Frame timing trace recorded. Scene: {ReportPrivacy.Text(scene)}";
        if (!string.IsNullOrWhiteSpace(context))
        {
            details += "\n\n" + ReportPrivacy.Text(context);
        }
        Reset(details);
        performanceTracePath = tracePath;
        Incident = null;
        preparePerformance = () => ErrorReport.PreparePerformanceAsync(tracePath, scene, context: context);
        closePerformance = close;
    }

    internal UiView View(string? message)
    {
        error = message;
        return BuildErrorReportView();
    }

    internal async Task<string?> SaveForExitAsync()
    {
        if (Incident is null)
        {
            return null;
        }
        ErrorIncident incident = Incident;
        if (Evidence is { } evidence)
        {
            try
            {
                incident = await evidence.ConfigureAwait(false);
            }
            catch (Exception failure)
            {
                incident = incident.WithSaveUnavailableReason("Save collection failed during shutdown: " + failure.Message);
            }
        }
        using PreparedErrorReport report = await (prepare?.Invoke(incident) ?? GameErrorReport.PrepareAsync(incident)).ConfigureAwait(false);
        return report.ArchivePath;
    }

    internal bool Update(string? message, InputFrame input, KeyboardInput keyboard, MouseState mouse, int width, int height)
    {
        error = message;
        PollPreparation();
        if ((Incident is null && preparePerformance is null) || error is null)
        {
            return false;
        }
        UiView view = BuildErrorReportView();
        view.Resize(width, height);
        if (input.MenuVertical < 0)
        {
            view.Key(UiKey.Up);
        }
        if (input.MenuVertical > 0)
        {
            view.Key(UiKey.Down);
        }
        if (input.MenuHorizontal < 0)
        {
            view.Key(UiKey.Left);
        }
        if (input.MenuHorizontal > 0)
        {
            view.Key(UiKey.Right);
        }
        if (input.MenuPage != 0)
        {
            view.Key(input.MenuPage < 0 ? UiKey.PageUp : UiKey.PageDown);
        }
        if (keyboard.UiTabRequested)
        {
            view.Key(UiKey.Tab);
        }
        if (input.Confirm || keyboard.UiConfirmRequested)
        {
            view.Key(UiKey.Confirm);
        }
        if (input.Cancel || keyboard.UiCancelRequested)
        {
            if (!reportDetails && closePerformance is not null)
            {
                closePerformance();
                return true;
            }
            reportDetails = false;
        }
        bool pressed = mouse.LeftButton == ButtonState.Pressed;
        view.Pointer(mouse.X, mouse.Y, pressed && !reportMousePressed);
        reportMousePressed = pressed;
        return true;
    }

    internal void PollPreparation()
    {
        if (reportPreparation is { IsCompleted: true } && preparedReport is null)
        {
            try
            {
                preparedReport = reportPreparation.GetAwaiter().GetResult();
                RevealReport();
                string fileStatus = Status;
                OpenReportDraft();
                Status = fileStatus + " " + Status;
            }
            catch (Exception failure)
            {
                Status = "Report preparation failed: " + failure.Message + (performanceTracePath is null ? ". Copy the error details below." : " Find the original CSV path in Details.");
                reportPreparation = null;
            }
        }
    }

    private void ReleasePreparedErrorReport()
    {
        preparedReport?.Dispose();
        preparedReport = null;
        if (reportPreparation is { } abandoned)
        {
            _ = abandoned.ContinueWith(completed =>
            {
                if (completed.IsCompletedSuccessfully)
                {
                    completed.Result.Dispose();
                }
                else
                {
                    _ = completed.Exception;
                }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        reportPreparation = null;
    }

    private void BeginErrorReport()
    {
        if (preparedReport is not null)
        {
            RevealReport();
            OpenReportDraft();
            return;
        }
        if (reportPreparation is not null)
        {
            return;
        }
        Status = "Preparing report files...";
        if (preparePerformance is not null)
        {
            reportPreparation = preparePerformance();
        }
        else if (Incident is not null)
        {
            Task<ErrorIncident> evidence = Evidence ?? Task.FromResult(Incident);
            reportPreparation = PrepareErrorReport(evidence);
        }
    }

    private async Task<PreparedErrorReport> PrepareErrorReport(Task<ErrorIncident> evidence)
    {
        ErrorIncident incident = await evidence.ConfigureAwait(false);
        return await (prepare?.Invoke(incident) ?? GameErrorReport.PrepareAsync(incident)).ConfigureAwait(false);
    }

    private void CopyReportText(string value)
    {
        try
        {
            copy(value);
            Status = "Copied.";
        }
        catch (Exception failure)
        {
            Status = failure.Message;
        }
    }

    private void OpenReportDraft()
    {
        if (preparedReport is not null && DateTimeOffset.UtcNow - reportBrowserOpenedUtc >= TimeSpan.FromSeconds(2))
        {
            reportBrowserOpenedUtc = DateTimeOffset.UtcNow;
            Status = ReportDesktop.OpenUrl(preparedReport.IssueUrl) ?? "GitHub form opened. Attach the ZIP and submit in your browser.";
        }
    }

    private void RevealReport()
    {
        if (preparedReport is not null && DateTimeOffset.UtcNow - reportFileRevealedUtc >= TimeSpan.FromSeconds(2))
        {
            reportFileRevealedUtc = DateTimeOffset.UtcNow;
            Status = ReportDesktop.RevealFile(preparedReport.ArchivePath) ?? "Report file shown.";
        }
    }

    private UiView BuildErrorReportView()
    {
        string key = string.Join("|", error, Status, preparedReport?.ArchivePath, reportDetails, reportPreparation?.Status);
        if (errorReportView is not null && errorReportViewKey == key)
        {
            return errorReportView;
        }
        UiAnchorPanel root = new();
        UiPanel panel = new() { Anchors = new UiAnchors(0.03f, 0.34f, 0.97f, 0.97f), Padding = 12 };
        UiStack stack = new() { Gap = 5 };
        stack.Children.Add(new UiLabel { Text = error ?? "Game error", FontSize = 17, Height = 48 });
        UiStack buttons = new() { Orientation = UiOrientation.Horizontal, Gap = 5, Height = 35 };
        buttons.Children.Add(new UiButton { Text = preparePerformance is null ? "Report error" : "Report performance", FontSize = 15, Enabled = reportPreparation is null || preparedReport is not null, Click = BeginErrorReport });
        buttons.Children.Add(new UiButton { Text = "Details", FontSize = 15, Click = () => reportDetails = !reportDetails });
        buttons.Children.Add(new UiButton { Text = "Copy full description", FontSize = 15, Click = () => CopyReportText(preparedReport?.Description ?? Details) });
        if (closePerformance is not null)
        {
            buttons.Children.Add(new UiButton { Text = "Close", FontSize = 15, Click = closePerformance });
        }
        stack.Children.Add(buttons);
        if (preparedReport is not null)
        {
            UiStack recovery = new() { Orientation = UiOrientation.Horizontal, Height = 35, Gap = 5 };
            recovery.Children.Add(new UiButton { Text = "Open GitHub again", FontSize = 15, Click = OpenReportDraft });
            recovery.Children.Add(new UiButton { Text = "Show file", FontSize = 15, Click = RevealReport });
            recovery.Children.Add(new UiButton { Text = "Copy issue URL", FontSize = 15, Click = () => CopyReportText(preparedReport.IssueUrl) });
            recovery.Children.Add(new UiButton { Text = "Copy ZIP path", FontSize = 15, Click = () => CopyReportText(preparedReport.ArchivePath) });
            stack.Children.Add(recovery);
            stack.Children.Add(new UiLabel { Text = Path.GetFileName(preparedReport.ArchivePath) + " (" + (new FileInfo(preparedReport.ArchivePath).Length / 1024d).ToString("F1") + " KiB)", FontSize = 14, Height = 22 });
        }
        stack.Children.Add(new UiLabel { Text = ErrorReport.PublicInstructions, FontSize = 14, Height = 42 });
        stack.Children.Add(new UiLabel { Text = Status, FontSize = 14, Height = 40 });
        if (reportDetails)
        {
            string? summary = preparedReport?.Description ?? Incident?.SaveUnavailableReason;
            string detailText = string.IsNullOrEmpty(summary) ? Details : summary + "\n\n" + Details;
            if (performanceTracePath is not null)
            {
                detailText += "\nTrace file: " + performanceTracePath;
            }
            UiScrollPanel details = new() { Height = 100, ContentHeight = Math.Max(100, detailText.Split('\n').Sum(line => Math.Max(1, (line.Length + 99) / 100)) * 18) };
            details.Children.Add(new UiLabel { Text = detailText, FontSize = 13 });
            stack.Children.Add(details);
        }
        panel.Children.Add(stack);
        root.Children.Add(panel);
        errorReportView = new UiView(root) { DesignWidth = 960, DesignHeight = 672 };
        errorReportViewKey = key;
        return errorReportView;
    }

    public void Dispose() => ReleasePreparedErrorReport();
}
