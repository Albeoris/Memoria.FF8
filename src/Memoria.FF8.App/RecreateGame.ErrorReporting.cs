using Memoria.FF8.Events;
using Memoria.FF8.Platform;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private ErrorReportPresenter? sharedErrorReport;
    private ErrorReportPresenter ErrorReporter => sharedErrorReport ??= new ErrorReportPresenter(authoringClipboard.Copy);
    private ErrorIncident? errorIncident
    {
        get => ErrorReporter.Incident;
        set => ErrorReporter.Incident = value;
    }
    private Task<ErrorIncident>? reportEvidence
    {
        get => ErrorReporter.Evidence;
        set => ErrorReporter.Evidence = value;
    }
    private string reportStatus
    {
        get => ErrorReporter.Status;
        set => ErrorReporter.Status = value;
    }
    private string originalErrorDetails
    {
        get => ErrorReporter.Details;
        set => ErrorReporter.Details = value;
    }
    private BattleInput? lastReportBattleInput;
    private readonly ErrorBreadcrumbs reportBreadcrumbs = new();
    private string? reportScene;
    private (int Horizontal, int Vertical) reportMovement;

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
        ErrorReporter.Reset(ReportPrivacy.Text(exception.ToString()));

        object? runtime = null;
        object? environment = null;
        string? collectionFailure = null;
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
            collectionFailure = "Runtime contributor failed: " + ReportPrivacy.Text(contributor.Message);
        }
        try
        {
            errorIncident = ErrorIncident.Capture(exception, subsystem, runtime, environment, logPaths: Environment.GetEnvironmentVariable("MEMORIA_FF8_REPORT_LOG_PATH") is { } logPath ? [logPath] : [], breadcrumbs: reportBreadcrumbs, saveUnavailableReason: "Save collection pending; no validated reproduction save has been preserved yet.");
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
        ErrorReportSnapshot snapshot;
        try
        {
            snapshot = ErrorReportSnapshotCapture.Capture(ReadSaveBoundaryState().Assess(), CaptureUpdateState, ReadEarlierReportSnapshots, ValidatePlayerState, DateTimeOffset.UtcNow);
        }
        catch (Exception snapshotFailure)
        {
            snapshot = new(null, "unavailable", DateTimeOffset.UtcNow, "Save collection failed: " + snapshotFailure.Message);
        }
        string saveReason = string.Join(" ", new[] { collectionFailure, snapshot.UnavailableReason }.Where(value => !string.IsNullOrWhiteSpace(value)));
        ErrorIncident captured = errorIncident.WithSaveUnavailableReason(saveReason);
        errorIncident = captured;
        reportEvidence = Task.Run(() =>
        {
            if (snapshot.State is not null)
            {
                return CompleteReportSave(captured, snapshot.State, snapshot.Kind, snapshot.CapturedUtc);
            }
            try
            {
                captured.Persist();
            }
            catch (Exception persistenceFailure)
            {
                Console.Error.WriteLine("Report evidence update failed: " + persistenceFailure.Message);
            }
            return captured;
        });
    }

    private IEnumerable<(GameState State, DateTimeOffset CapturedUtc)> ReadEarlierReportSnapshots()
    {
        return ErrorReportSnapshotCapture.ReadEarlierPlayerSnapshots(playerSaves, options.StatePath, path => PlayerStateLoader.Load(path, options.GameDirectory, world?.ValidationAssets, resourceHost?.Resources, resourceHost, () => resourceHost?.GameplayPolicy ?? GameplayTuningPolicy.Empty, initialized => resourceHost is null ? initialized : GameplaySaveGuardianForceDefinitions.Select(initialized, resourceHost)), failure => Console.Error.WriteLine("Earlier report save could not be loaded: " + failure.Message));
    }

    private void ReleasePreparedErrorReport() => sharedErrorReport?.Dispose();

    private static ErrorIncident CompleteReportSave(ErrorIncident incident, GameState detached, string kind, DateTimeOffset capturedUtc)
    {
        string path = Path.Combine(ApplicationPaths.Current.Cache, "report-save-" + Guid.NewGuid().ToString("N") + ".ff8save");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Only the independent session is serialized here. Resource continuation was validated on the owner thread.
            new GameSession(detached).Save(path, compact: true);
            _ = GameSession.Load(path);
            ErrorIncident completed = incident.WithSave(new ReportSave(File.ReadAllBytes(path), kind, capturedUtc, "Storage integrity and owner-thread resource continuation validated. Captured UTC identifies the source state; the container display timestamp identifies report serialization, not gameplay progress."));
            completed.Persist();
            return completed;
        }
        catch (Exception failure)
        {
            Console.Error.WriteLine("Report save unavailable: " + failure.Message);
            ErrorIncident partial = incident.WithSaveUnavailableReason("Save serialization or publication failed: " + failure.Message);
            try
            {
                partial.Persist();
            }
            catch (Exception persistenceFailure)
            {
                Console.Error.WriteLine("Report evidence update failed: " + persistenceFailure.Message);
            }
            return partial;
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception cleanupFailure) when (cleanupFailure is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private UiView BuildErrorReportView() => ErrorReporter.View(error);

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
        sharedErrorReport?.PollPreparation();
        if (errorIncident is null || error is null || saveMenu.IsOpen || gameMenus?.IsOpen == true || input.QuickLoad || input.SaveMenu || input.LoadMenu)
        {
            return false;
        }
        return ErrorReporter.Update(error, input, keyboard, physicalInput.ReadMouse(), GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
    }
}
