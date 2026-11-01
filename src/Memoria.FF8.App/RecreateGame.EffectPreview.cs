using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private EffectPreviewRequest? effectPreview;
    private BattleSession? effectPreviewBattle;
    private DiagnosticPresentationGate? effectPreviewGate;
    private bool effectPreviewSubmitted;
    private bool effectPreviewReady;
    private bool effectPreviewPresented;
    private bool effectPreviewFailed;
    private BattleDiagnosticEffectDispatch? effectPreviewDispatch;
    private DateTimeOffset? effectPreviewDispatchUtc;
    private double effectPreviewStartedSeconds;

    private void StartEffectPreviewHost()
    {
        EffectPreviewRequest request = JsonSerializer.Deserialize<EffectPreviewRequest>(File.ReadAllText(options.EffectPreviewPath!), DiagnosticBattleJson) ?? throw new InvalidDataException("Effect preview request is empty.");
        request.Validate();
        effectPreview = request;
        session.StartNewGame();
        world = CreateWorld();
        effectPreviewBattle = world.StartEffectPreview(request);
        string receipts = request.Presentation?.ReceiptDirectory ?? options.ControlDirectory!;
        DiagnosticPresentationDeclaration transport = request.Presentation ?? new(Path.Combine(receipts, "unused-preview-signal.json"), receipts, Guid.NewGuid().ToString("N"));
        effectPreviewGate = new(transport, request.EffectId);
        effectPreviewBattle.DiagnosticEffectDispatched += dispatch =>
        {
            if (dispatch.RequestedEffect == request.EffectId && dispatch.ExecutedEffect == request.EffectId)
            {
                effectPreviewDispatch = dispatch;
                effectPreviewDispatchUtc = DateTimeOffset.UtcNow;
            }
        };
        if (transport.VisualSignalPath is not null)
        {
            effectPreviewBattle.ArmDiagnosticVisualEntry("party-0", 2, 1, request.EffectId, boundary =>
            {
                try
                {
                    effectPreviewGate.VisualReady(boundary.Callback, diagnosticPresentedFrames, DateTimeOffset.UtcNow, boundary);
                    effectPreviewGate.WaitForVisualRelease(TimeSpan.FromMinutes(10));
                    ResetElapsedTime();
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException("Graphical preview visual admission failed.", exception);
                }
            });
        }
        gameMenus?.Close();
    }

    private void AdvanceEffectPreview()
    {
        if (effectPreview is not { } request || effectPreviewBattle is not { } active || effectPreviewFailed)
        {
            return;
        }
        try
        {
            if (fieldFaulted || fieldRenderFaulted)
            {
                throw new InvalidOperationException(error ?? "The graphical preview host faulted.");
            }
            if (!effectPreviewReady)
            {
                if (battleScene?.PresentedNativeFrame is null)
                {
                    return;
                }
                effectPreviewGate!.Ready(active.NativeHostCallbacks ?? 0, diagnosticPresentedFrames, DateTimeOffset.UtcNow, atbGating: false);
                effectPreviewReady = true;
                return;
            }
            if (!effectPreviewSubmitted && (request.Presentation is null || effectPreviewGate!.Released(DateTimeOffset.UtcNow)))
            {
                active.PublishDiagnosticEffectPreview(request.EffectId);
                effectPreviewSubmitted = true;
                effectPreviewGate!.Write("started", active.NativeHostCallbacks ?? 0, diagnosticPresentedFrames, DateTimeOffset.UtcNow);
            }
            if (effectPreviewDispatch is { } dispatch && !effectPreviewPresented)
            {
                effectPreviewGate!.Write("visual-started", active.NativeHostCallbacks ?? 0, diagnosticPresentedFrames, DateTimeOffset.UtcNow, nativeIdentity: dispatch, dispatchUtc: effectPreviewDispatchUtc);
                effectPreviewGate.Write("completed", active.NativeHostCallbacks ?? 0, diagnosticPresentedFrames, DateTimeOffset.UtcNow, nativeIdentity: new { scope = "graphical-preview-initialized", effect = request.EffectId });
                effectPreviewPresented = true;
                effectPreviewStartedSeconds = Stopwatch.GetElapsedTime(presentationClockStarted).TotalSeconds;
            }
            if (effectPreviewPresented && Stopwatch.GetElapsedTime(presentationClockStarted).TotalSeconds - effectPreviewStartedSeconds >= request.DurationSeconds)
            {
                Exit();
            }
        }
        catch (Exception exception)
        {
            effectPreviewFailed = true;
            effectPreviewGate!.Write("blocked", active.NativeHostCallbacks ?? 0, diagnosticPresentedFrames, DateTimeOffset.UtcNow, exception.ToString());
            Exit();
        }
    }
}
