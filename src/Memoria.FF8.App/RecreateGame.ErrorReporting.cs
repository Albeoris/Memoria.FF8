using Memoria.FF8.Events;
using Memoria.FF8.Platform;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private ErrorIncident? errorIncident;
    private BattleInput? lastReportBattleInput;
    private Task<PreparedErrorReport>? reportPreparation;
    private PreparedErrorReport? preparedReport;
    private readonly ErrorBreadcrumbs reportBreadcrumbs = new();
    private UiView? errorReportView;
    private string? errorReportViewKey;
    private string reportStatus = "";
    private string originalErrorDetails = "";
    private bool reportMousePressed;
    private bool reportDetails;
    private string? reportScene;
    private (int Horizontal, int Vertical) reportMovement;
    private DateTimeOffset reportBrowserOpenedUtc;
    private DateTimeOffset reportFileRevealedUtc;

    internal void RunWithErrorCapture()
    {
        try
        {
            Run();
        }
        catch (Exception exception)
        {
            if (exception.Data["Memoria.ErrorIncidentCaptured"] is not true && (Environment.GetEnvironmentVariable("MEMORIA_FF8_HOST_FAILURE_FILE") is null || !GraphicsHostReadiness.IsInitializationFailure(exception)))
            {
                CaptureErrorIncident(exception, "game host", force: true);
            }
            throw;
        }
    }

    private void CaptureErrorIncident(Exception exception, string subsystem, bool force = false)
    {
        if (exception is OperationCanceledException || !force && errorIncident is not null && error is not null)
        {
            return;
        }
        originalErrorDetails = ReportPrivacy.Text(exception.ToString());
        ReleasePreparedErrorReport();
        reportStatus = "";
        reportDetails = false;
        object? runtime = null;
        object? environment = null;
        try
        {
            BattleSession? battle = ActiveBattle;
            runtime = new
            {
                Scene = session.State.Scene.ToString(), session.State.FieldId, session.State.FieldName, session.State.EntryPoint,
                Frame = updateFrames, VmFrame = events?.Frame, Fault = (exception as EventScriptException)?.FaultContext ?? events?.LatestFaultContext,
                LastFault = events?.LastFault, Transition = events?.NextTrigger.Kind.ToString(),
                EventRandom = session.State.EventRandom, MenuRandom = session.State.MenuRandom,
                WorldFrame = session.State.World?.Frame,
                Battle = battle is null ? null : new { battle.EncounterId, battle.Name, battle.Frame, Outcome = battle.Outcome.ToString(), LastInput = lastReportBattleInput, Combatants = battle.Combatants.Select(actor => new { actor.Id, actor.IsPlayer, actor.CurrentHp, actor.IsActive, actor.NativeCombatantFlags }).ToArray(), ActiveAction = battle.ActiveAction, PendingActions = battle.QueuedActions.Take(32).ToArray() },
                MenuOpen = gameMenus?.IsOpen, SaveMenuOpen = saveMenu.IsOpen, StartupStage = startup.Stage.ToString(),
                CompletedTimingSample = metrics.Latest,
                CompletedTimingSampleAgeSeconds = metrics.LatestCompletedUtc is { } completed ? (double?)(DateTimeOffset.UtcNow - completed).TotalSeconds : null,
                Omissions = "Native stack/symbols, GPU driver details, process allocation deltas and full VM call stacks are unavailable."
            };
            environment = new
            {
                Renderer = GraphicsHostReadiness.Backend, Audio = options.AudioEnabled ? "configured game audio" : "disabled",
                Edition = resourceHost?.Resources.Source.Edition.ToString(), Language = options.ResourceLanguage,
                Mods = resourceHost?.Mods.Catalog.Packages.Select(package => new { package.Id, package.Priority, Status = package.Status.ToString(), Version = package.JunctionMetadata?.Version }).ToArray(),
                Gameplay = session.State.GameplayPreferences,
                ResourceProvider = "Unavailable: the fault did not provide a resource key. GPU observations are cached only; the failed device is not queried."
            };
        }
        catch (Exception contributor)
        {
            runtime = new { unavailable = "Runtime contributor failed: " + ReportPrivacy.Text(contributor.Message) };
        }
        try
        {
            errorIncident = ErrorIncident.Capture(exception, subsystem, runtime, environment, logPaths: Environment.GetEnvironmentVariable("MEMORIA_FF8_REPORT_LOG_PATH") is { } logPath ? [logPath] : [], breadcrumbs: reportBreadcrumbs, saveUnavailableReason: "Automatic reproduction saves are excluded because complete snapshots may contain private data.");
            // The small incident must survive a renderer exit even if no worker gets time to run.
            errorIncident.Persist();
            exception.Data["Memoria.ErrorIncidentCaptured"] = true;
        }
        catch (Exception captureFailure)
        {
            reportStatus = "Local evidence could not be preserved: " + captureFailure.Message;
            Console.Error.WriteLine(reportStatus);
            return;
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
        if (reportPreparation is not null || errorIncident is null)
        {
            return;
        }
        reportStatus = "Preparing report files...";
        reportPreparation = ErrorReport.PrepareAsync(errorIncident);
    }

    private void CopyReportText(string value)
    {
        try
        {
            authoringClipboard.Copy(value);
            reportStatus = "Copied.";
        }
        catch (Exception failure)
        {
            reportStatus = failure.Message;
        }
    }

    private void OpenReportDraft()
    {
        if (preparedReport is not null && DateTimeOffset.UtcNow - reportBrowserOpenedUtc >= TimeSpan.FromSeconds(2))
        {
            reportBrowserOpenedUtc = DateTimeOffset.UtcNow;
            reportStatus = ReportDesktop.OpenUrl(preparedReport.IssueUrl) ?? "GitHub form opened. Attach the ZIP and submit in your browser.";
        }
    }

    private void RevealReport()
    {
        if (preparedReport is not null && DateTimeOffset.UtcNow - reportFileRevealedUtc >= TimeSpan.FromSeconds(2))
        {
            reportFileRevealedUtc = DateTimeOffset.UtcNow;
            reportStatus = ReportDesktop.RevealFile(preparedReport.ArchivePath) ?? "Report file shown.";
        }
    }

    private UiView BuildErrorReportView()
    {
        string key = string.Join("|", error, reportStatus, preparedReport?.ArchivePath, reportDetails, reportPreparation?.Status);
        if (errorReportView is not null && errorReportViewKey == key)
        {
            return errorReportView;
        }
        UiAnchorPanel root = new();
        UiPanel panel = new() { Anchors = new UiAnchors(0.03f, 0.34f, 0.97f, 0.97f), Padding = 12 };
        UiStack stack = new() { Gap = 5 };
        stack.Children.Add(new UiLabel { Text = error ?? "Game error", FontSize = 17, Height = 48 });
        UiStack buttons = new() { Orientation = UiOrientation.Horizontal, Gap = 5, Height = 35 };
        buttons.Children.Add(new UiButton { Text = "Report error", FontSize = 15, Enabled = reportPreparation is null || preparedReport is not null, Click = BeginErrorReport });
        buttons.Children.Add(new UiButton { Text = "Details", FontSize = 15, Click = () => reportDetails = !reportDetails });
        buttons.Children.Add(new UiButton { Text = "Copy full description", FontSize = 15, Click = () => CopyReportText(preparedReport?.Description ?? originalErrorDetails) });
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
        stack.Children.Add(new UiLabel { Text = "Sign in to GitHub if needed, drag this ZIP into the issue, wait for the upload, then submit the issue. The report and attached files will be public.", FontSize = 14, Height = 42 });
        stack.Children.Add(new UiLabel { Text = reportStatus, FontSize = 14, Height = 40 });
        if (reportDetails)
        {
            string detailText = (preparedReport?.Description ?? errorIncident?.SaveUnavailableReason) + "\n\n" + originalErrorDetails;
            UiScrollPanel details = new() { Height = 100, ContentHeight = Math.Max(100, detailText.Split('\n').Sum(line => Math.Max(1, (line.Length + 99) / 100)) * 18) };
            details.Children.Add(new UiLabel { Text = detailText, FontSize = 13 });
            stack.Children.Add(details);
        }
        panel.Children.Add(stack);
        root.Children.Add(panel);
        errorReportView = CreateWindowView(root);
        errorReportViewKey = key;
        return errorReportView;
    }

    private bool UpdateErrorReport(InputFrame input)
    {
        if (error is null && errorIncident is not null)
        {
            errorIncident = null;
            ReleasePreparedErrorReport();
        }
        string scene = session.State.Scene + ":" + session.State.FieldId;
        if (reportScene != scene)
        {
            reportBreadcrumbs.Add("transition", scene);
            reportScene = scene;
        }
        (int Horizontal, int Vertical) movement = (Math.Sign(input.Horizontal), Math.Sign(input.Vertical));
        if (movement != reportMovement)
        {
            reportBreadcrumbs.Add("movement", $"direction {movement.Horizontal},{movement.Vertical}");
            reportMovement = movement;
        }
        if (input.Confirm || input.Cancel || input.QuickLoad || input.Save || input.LoadMenu)
        {
            reportBreadcrumbs.Add("input", input.Confirm ? "confirm" : input.Cancel ? "cancel" : input.QuickLoad ? "quick load" : input.Save ? "save" : "load menu");
        }
        if (reportPreparation is { IsCompleted: true } && preparedReport is null)
        {
            try
            {
                preparedReport = reportPreparation.GetAwaiter().GetResult();
                RevealReport();
                string fileStatus = reportStatus;
                OpenReportDraft();
                reportStatus = fileStatus + " " + reportStatus;
            }
            catch (Exception failure)
            {
                reportStatus = "Report preparation failed: " + failure.Message + ". Copy the error details below.";
                reportPreparation = null;
            }
        }
        if (errorIncident is null || error is null || saveMenu.IsOpen || gameMenus?.IsOpen == true || input.QuickLoad || input.SaveMenu || input.LoadMenu)
        {
            return false;
        }
        UiView view = BuildErrorReportView();
        view.Resize(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
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
            reportDetails = false;
        }
        MouseState mouse = physicalInput.ReadMouse();
        bool pressed = mouse.LeftButton == ButtonState.Pressed;
        view.Pointer(mouse.X, mouse.Y, pressed && !reportMousePressed);
        reportMousePressed = pressed;
        return true;
    }
}
