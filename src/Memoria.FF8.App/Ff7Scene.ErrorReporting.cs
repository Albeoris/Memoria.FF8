using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Platform;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private readonly LocalizationAuthoringClipboard reportClipboard = new();
    private ErrorReportPresenter? errorReporter;
    private CombinedSessionSave? reportCheckpoint;
    private DateTimeOffset reportCheckpointUtc;

    private void RememberReportCheckpoint()
    {
        if (Session is null || Session.Battle is not null || reportCheckpoint?.Ff7 is { } previous && previous.FieldGeneration == Session.FieldGeneration && (previous.World is null) == (Session.World is null) && (Session.World?.Machine.TickCount ?? Session.Machine.Tick) % 30 != 0)
        {
            return;
        }
        try
        {
            reportCheckpoint = Capture();
            reportCheckpointUtc = DateTimeOffset.UtcNow;
        }
        catch (Exception failure) when (failure is InvalidDataException or InvalidOperationException or NotSupportedException)
        {
            // Evidence collection cannot replace a simulation failure or alter its continuation.
            Console.Error.WriteLine("FFVII report checkpoint unavailable: " + failure.Message);
        }
    }

    private void CaptureFf7Error(Exception exception)
    {
        try
        {
            errorReporter ??= new ErrorReportPresenter(reportClipboard.Copy, incident => Ff7ErrorReport.PrepareAsync(incident));
            errorReporter.Reset(ReportPrivacy.Text(exception.ToString()));
            ReportSave? save = null;
            string unavailable = "No validated pre-fault FFVII continuation is available.";
            if (reportCheckpoint?.Ff7 is { } checkpoint)
            {
                try
                {
                    using Ff7Session validated = Ff7Session.Restore(installation, checkpoint);
                    using MemoryStream stream = new();
                    CombinedSessionCodec.Write(stream, reportCheckpoint);
                    save = new ReportSave(stream.ToArray(), "pre-fault combined continuation", reportCheckpointUtc, "Independent codec and original-resource cold restore validated without advancing scripts. This checkpoint precedes the reported fault.", ".ffsave");
                    unavailable = "";
                }
                catch (Exception failure)
                {
                    unavailable = "Report continuation validation failed: " + failure.Message;
                }
            }
            ErrorIncident incident = ErrorIncident.Capture(exception, Session?.World is null ? "FFVII field" : "FFVII world", new { Game = "FFVII", Field = Session?.Field.Name, World = Session?.World?.Resources.World, Tick = Session?.World?.Machine.TickCount ?? Session?.Machine.Tick, Generation = Session?.FieldGeneration }, save: save, saveUnavailableReason: unavailable);
            incident.Persist();
            errorReporter.Incident = incident;
            errorReporter.Evidence = Task.FromResult(incident);
            exception.Data["Memoria.ErrorIncidentCaptured"] = true;
        }
        catch (Exception failure)
        {
            Console.Error.WriteLine("FFVII report evidence unavailable: " + failure.Message);
        }
    }

    private void UpdateErrorPresentation()
    {
        errorReporter?.Update(error, semanticInput, sharedInput, Mouse.GetState(), device.Viewport.Width, device.Viewport.Height);
    }

    private void DrawErrorPresentation(Rectangle bounds)
    {
        if (errorReporter is null || dialogueCanvas is null)
        {
            return;
        }
        UiPreferences preferences = dialoguePreferences?.Invoke() ?? new UiPreferences();
        UiTheme theme = dialogueTheme?.Invoke() ?? (preferences.MenuPresentation == MenuPresentation.Vector ? new VectorUiTheme { FontFamily = preferences.UiFontFamily } : new UiTheme { FontFamily = preferences.UiFontFamily });
        UiView view = errorReporter.View(error);
        view.Resize(bounds.Width, bounds.Height);
        view.Draw(dialogueCanvas, theme);

    }
}
