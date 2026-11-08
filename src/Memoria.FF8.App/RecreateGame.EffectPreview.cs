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
    private double effectPreviewSubmittedSeconds;
    private bool effectPreviewCompleted;
    private long? effectPreviewDrainedFrame;
    private BattleNativeAction? effectPreviewAbility;
    private ushort effectPreviewExpectedEffect;
    private EffectComponentFrameMeasurements? effectComponentMeasurements;
    private long? effectComponentDrainedTimestamp;
    private ComponentSourceAdmission? effectPreviewSourceAdmission;
    private int effectComponentInactivePresentedFrames;
    private readonly EffectComponentTerminalTextureTracker effectComponentTerminalTextures = new();

    private void StartEffectPreviewHost()
    {
        EffectPreviewRequest request = JsonSerializer.Deserialize<EffectPreviewRequest>(File.ReadAllText(options.EffectPreviewPath!), DiagnosticBattleJson) ?? throw new InvalidDataException("Effect preview request is empty.");
        request.Validate();
        effectPreview = request;
        session.StartNewGame();
        world = CreateWorld();
        effectPreviewBattle = world.StartEffectPreview(request);
        effectPreviewAbility = request.AbilityId is int abilityId ? effectPreviewBattle.ResolveDiagnosticPreviewAbility(request.AbilityCommand, abilityId) : null;
        effectPreviewExpectedEffect = effectPreviewAbility?.MagicId ?? request.EffectId!.Value;
        effectComponentMeasurements = request.SourceSnapshot is null ? null : new(Stopwatch.Frequency);
        effectPreviewSourceAdmission = world.ComponentSourceAdmission;
        string receipts = request.Presentation?.ReceiptDirectory ?? options.ControlDirectory!;
        DiagnosticPresentationDeclaration transport = request.Presentation ?? new(Path.Combine(receipts, "unused-preview-signal.json"), receipts, Guid.NewGuid().ToString("N"));
        effectPreviewGate = new(transport, effectPreviewExpectedEffect, request.AbilityId is null ? null : request.AbilityCommand, request.AbilityId);
        effectPreviewBattle.DiagnosticEffectDispatched += dispatch =>
        {
            if (dispatch.RequestedEffect == effectPreviewExpectedEffect && dispatch.ExecutedEffect == effectPreviewExpectedEffect && (request.SourceSnapshot is null || effectPreviewDispatch is null))
            {
                effectPreviewDispatch = dispatch;
                effectPreviewDispatchUtc = DateTimeOffset.UtcNow;
                if (effectComponentMeasurements is { IsActive: false } measurements)
                {
                    measurements.Begin(Stopwatch.GetTimestamp());
                }
            }
        };
        if (transport.VisualSignalPath is not null && request.SourceSnapshot is null)
        {
            effectPreviewBattle.ArmDiagnosticVisualEntry("party-0", effectPreviewAbility?.Command ?? 2, effectPreviewAbility?.ActionId ?? 1, effectPreviewExpectedEffect, boundary =>
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
                if (request.SourceSnapshot is null)
                {
                    if (request.AbilityId is int abilityId)
                    {
                        active.PublishDiagnosticAbilityPreview(request.AbilityCommand, abilityId);
                    }
                    else
                    {
                        active.PublishDiagnosticEffectPreview(effectPreviewExpectedEffect);
                    }
                }
                effectPreviewSubmitted = true;
                effectPreviewSubmittedSeconds = Stopwatch.GetElapsedTime(presentationClockStarted).TotalSeconds;
                if (effectPreviewSourceAdmission is not null)
                {
                    effectComponentMeasurements!.Begin(Stopwatch.GetTimestamp());
                }
                effectPreviewGate!.Write("started", active.NativeHostCallbacks ?? 0, diagnosticPresentedFrames, DateTimeOffset.UtcNow);
            }
            if ((effectPreviewDispatch is not null || effectPreviewSourceAdmission is not null) && !effectPreviewPresented)
            {
                effectPreviewGate!.Write("visual-started", active.NativeHostCallbacks ?? 0, diagnosticPresentedFrames, DateTimeOffset.UtcNow, nativeIdentity: (object?)effectPreviewDispatch ?? effectPreviewSourceAdmission, dispatchUtc: effectPreviewDispatchUtc);
                if (request.AbilityId is null && request.SourceSnapshot is null)
                {
                    effectPreviewGate.Write("completed", active.NativeHostCallbacks ?? 0, diagnosticPresentedFrames, DateTimeOffset.UtcNow, nativeIdentity: new { scope = "graphical-preview-initialized", effect = effectPreviewExpectedEffect });
                    effectPreviewCompleted = true;
                }
                effectPreviewPresented = true;
                effectPreviewStartedSeconds = Stopwatch.GetElapsedTime(presentationClockStarted).TotalSeconds;
            }
            if ((request.AbilityId is not null || request.SourceSnapshot is not null) && effectPreviewPresented && !effectPreviewCompleted)
            {
                bool blocked = active.ObserveDiagnosticDrainBlockers().Count != 0;
                ObserveEffectPreviewDrain(active, blocked, false);
                if (request.SourceSnapshot is not null && (ObserveEffectComponentTerminalTextures(active) || ObserveEffectComponentHeldScreenBlend(active)))
                {
                    effectPreviewCompleted = true;
                }
                if (blocked)
                {
                    effectPreviewDrainedFrame = null;
                    effectComponentDrainedTimestamp = null;
                }
                else if (effectPreviewDrainedFrame is null)
                {
                    effectPreviewDrainedFrame = diagnosticPresentedFrames;
                    effectComponentDrainedTimestamp = Stopwatch.GetTimestamp();
                }
                else if (diagnosticPresentedFrames - effectPreviewDrainedFrame.Value >= 2)
                {
                    if (request.SourceSnapshot is not null)
                    {
                        EffectComponentFrameReport measurements = effectComponentMeasurements!.Complete(effectComponentDrainedTimestamp);
                        string output = request.Presentation?.ReceiptDirectory ?? options.ControlDirectory!;
                        WriteDiagnosticJson(Path.Combine(output, "component-measurements.json"), new { scope = "installed-source-controlled-component-gpu-presentation", source_sha256 = request.SourceSha256, effect = effectPreviewExpectedEffect, native_dispatch = effectPreviewDispatch, native_admission = effectPreviewSourceAdmission, completion_kind = "drained", completed = true, drained = true, resident_tasks_remain = false, terminal_texture_certificate = (BattleComponentTerminalTextureCertificate?)null, tail_presented_frames = diagnosticPresentedFrames - effectPreviewDrainedFrame.Value, host = new { backend = GraphicsHostReadiness.Backend, vsync = uiPreferences.VSync, presentation_interval = GraphicsDevice.PresentationParameters.PresentationInterval.ToString(), frame_limit = uiPreferences.BattleFps, width = GraphicsDevice.PresentationParameters.BackBufferWidth, height = GraphicsDevice.PresentationParameters.BackBufferHeight, msaa_samples = GraphicsDevice.PresentationParameters.MultiSampleCount, inactive_presented_frames = effectComponentInactivePresentedFrames }, measurements });
                        effectPreviewGate!.Write("completed", active.NativeHostCallbacks ?? 0, diagnosticPresentedFrames, DateTimeOffset.UtcNow, nativeIdentity: new { scope = "controlled-component", completion_kind = "drained", completed = true, drained = true, resident_tasks_remain = false, terminal_texture_certificate = (BattleComponentTerminalTextureCertificate?)null, effect = effectPreviewExpectedEffect });
                    }
                    else
                    {
                        effectPreviewGate!.Write("completed", active.NativeHostCallbacks ?? 0, diagnosticPresentedFrames, DateTimeOffset.UtcNow, nativeIdentity: new { scope = "full-ability", drained = true, command_id = request.AbilityCommand, ability_id = request.AbilityId, native_command = effectPreviewAbility!.Command, action_id = effectPreviewAbility.ActionId, effect = effectPreviewExpectedEffect });
                    }
                    effectPreviewCompleted = true;
                }
            }
            double now = Stopwatch.GetElapsedTime(presentationClockStarted).TotalSeconds;
            if (request.SourceSnapshot is not null && effectPreviewCompleted)
            {
                ExitGame();
            }
            else if ((request.AbilityId is not null || request.SourceSnapshot is not null) && effectPreviewSubmitted && now - effectPreviewSubmittedSeconds >= request.DurationSeconds)
            {
                if (!effectPreviewCompleted)
                {
                    ObserveEffectPreviewDrain(active, true, true);
                    throw new TimeoutException("The complete ability did not drain its native graphics, descriptors, files and camera within the preview deadline.");
                }
                ExitGame();
            }
            else if (request.AbilityId is null && request.SourceSnapshot is null && effectPreviewPresented && now - effectPreviewStartedSeconds >= request.DurationSeconds)
            {
                ExitGame();
            }
        }
        catch (Exception exception)
        {
            effectPreviewFailed = true;
            effectPreviewGate!.Write("blocked", active.NativeHostCallbacks ?? 0, diagnosticPresentedFrames, DateTimeOffset.UtcNow, exception.ToString());
            ExitGame();
        }
    }
}
