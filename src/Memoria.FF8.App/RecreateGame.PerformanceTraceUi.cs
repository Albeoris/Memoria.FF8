using System.Diagnostics;
using Memoria.FF8.Platform;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private readonly GameConfirmationOverlay tracePrompt = new();
    private Task<bool>? traceDecision;
    private ErrorReportPresenter? traceReport;
    private string traceReportTitle = "";
    private bool traceKeyHeld;
    private bool traceStarted;
    private long nextTraceLabelUpdate;
    private float traceLabelScale;
    private Vector2 traceLabelSize;
    private string traceLabel = "";

    private bool TraceModalOpen => traceDecision is not null || traceReport is not null;

    private void StartPerformanceTrace()
    {
        if (performanceTrace.IsRecording)
        {
            return;
        }
        traceStarted = performanceTrace.Start(Path.Combine(ApplicationPaths.Current.Reports, "Performance"));
        nextTraceLabelUpdate = 0;
        if (!traceStarted && performanceTrace.Error is { } failure)
        {
            ShowTraceFailure(failure);
        }
    }

    private void ShowTraceFailure(string failure)
    {
        traceDecision = tracePrompt.ShowAsync(new GameConfirmationRequest(failure, "FPS trace recording failed", "Close", "Retry", ""));
        SetTraceModalPause();
    }

    private bool UpdatePerformanceTraceUi()
    {
        KeyboardState keys = physicalInput.ReadKeyboard();
        bool down = keys.IsKeyDown(Keys.F2);
        bool pressed = down && !traceKeyHeld;
        traceKeyHeld = down || !physicalInput.CanRead;

        if (traceStarted && (!performanceTrace.IsRecording || pressed))
        {
            FinishPerformanceTrace();
            return true;
        }

        if (TraceModalOpen)
        {
            UpdateTraceModal(keys, pressed);
            return true;
        }

        return false;
    }

    private void FinishPerformanceTrace()
    {
        traceStarted = false;
        performanceTrace.Stop();
        if (performanceTrace.FilePath is { } path)
        {
            traceReportTitle = performanceTrace.Error is { } failure ? "FPS trace interrupted: " + failure : "FPS trace recorded";
            traceReport = new ErrorReportPresenter(authoringClipboard.Copy);
            traceReport.ShowPerformanceReport(path, PerformanceTraceScene(), CloseTraceReport, PerformanceTraceContext());
            SetTraceModalPause();
        }
    }

    private void UpdateTraceModal(KeyboardState keys, bool pressed)
    {
        if (!physicalInput.CanRead)
        {
            SuppressDraw();
            return;
        }
        keyboard.NativeButtonsOwnInput = false;
        keyboard.TextEntryEnabled = false;
        keyboard.NamingNavigationEnabled = false;
        if (traceReport is not null)
        {
            InputFrame input = keyboard.Read(keys, physicalInput.ReadController(), true);
            traceReport.Update(traceReportTitle, input, keyboard, physicalInput.ReadMouse(), GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        }
        else if (traceDecision is { } decision)
        {
            if (pressed)
            {
                tracePrompt.Handle(UiKey.Cancel);
            }
            else
            {
                UpdateGameConfirmation(tracePrompt);
            }
            if (decision.IsCompletedSuccessfully)
            {
                traceDecision = null;
                if (decision.Result)
                {
                    StartPerformanceTrace();
                }
                SetTraceModalPause();
            }
        }
    }

    private void CloseTraceReport()
    {
        traceReport?.Dispose();
        traceReport = null;
        ff7Scene?.SuppressInitialPresses();
        SetTraceModalPause();
    }

    private void SetTraceModalPause()
    {
        keyboard.Read(physicalInput.ReadKeyboard(), physicalInput.ReadController(), physicalInput.CanRead);
        keyboard.SuppressHeldUiInput();
        SynchronizeUiMoviePause();
        SynchronizeFieldAudioPause();
        ff7Scene?.SetFocusPaused(TraceModalOpen || focusPaused || updateSave.Paused || saveMenu.IsOpen || worldTransfer is not null);
        controllerFeedback?.Update(session.State.VibrationProcess, false, keyboard.Profile.Controller.VibrationEnabled);
        presentationTiming.DiscardPendingTime();
        battleEntryCallbackStarted = Stopwatch.GetTimestamp();
        ResetElapsedTime();
    }

    private void DrawPerformanceTraceModal(GameTime time)
    {
        Rectangle bounds = GraphicsDevice.Viewport.Bounds;
        GraphicsDevice.Clear(Color.Black);
        text.BeginFrame();
        uiCanvas?.BeginFrame(time.TotalGameTime);
        batch.Begin(samplerState: SamplerState.LinearClamp);
        if (traceReport is not null)
        {
            DrawWindowView(traceReport.View(traceReportTitle), bounds);
        }
        else if (uiCanvas is not null)
        {
            tracePrompt.Draw(uiCanvas, uiTheme, new UiRect(0, 0, bounds.Width, bounds.Height), uiPreferences.UiFontFamily);
        }
        batch.End();
    }

    private void DrawPerformanceTraceStatus()
    {
        if (!performanceTrace.IsRecording)
        {
            return;
        }
        Rectangle bounds = GraphicsDevice.Viewport.Bounds;
        float scale = ViewportLayout.UiScale(bounds.Width, bounds.Height);
        float font = 16 * scale;
        long now = Stopwatch.GetTimestamp();
        if (now >= nextTraceLabelUpdate || scale != traceLabelScale)
        {
            long bytes = performanceTrace.BytesWritten;
            string size = bytes < 1024 ? $"{bytes} B" : bytes < 1048576 ? $"{bytes / 1024d:F1} KiB" : $"{bytes / 1048576d:F1} MiB";
            traceLabel = FormatAuthored("debug.fps-trace.recording", "Recording FPS trace · {0} written · F2 to stop", size);
            traceLabelSize = text.MeasureWrapped(traceLabel, font, float.PositiveInfinity);
            traceLabelScale = scale;
            nextTraceLabelUpdate = now + Stopwatch.Frequency;
        }
        Vector2 position = new(8 * scale, 8 * scale);
        batch.Begin(samplerState: SamplerState.LinearClamp);
        batch.Draw(pixel, new Rectangle(0, 0, (int)Math.Ceiling(traceLabelSize.X + 2 * position.X), (int)Math.Ceiling(traceLabelSize.Y + 2 * position.Y)), Color.Black * 0.85f);
        text.Draw(batch, traceLabel, font, position, Color.White);
        batch.End();
    }
}
