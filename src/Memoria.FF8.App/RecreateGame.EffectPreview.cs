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
        string receipts = request.Presentation?.ReceiptDirectory ?? options.ControlDirectory!;
        DiagnosticPresentationDeclaration transport = request.Presentation ?? new(Path.Combine(receipts, "unused-preview-signal.json"), receipts, Guid.NewGuid().ToString("N"));
        effectPreviewGate = new(transport, effectPreviewExpectedEffect, request.AbilityId is null ? null : request.AbilityCommand, request.AbilityId);
        effectPreviewBattle.DiagnosticEffectDispatched += dispatch =>
        {
            if (dispatch.RequestedEffect == effectPreviewExpectedEffect && dispatch.ExecutedEffect == effectPreviewExpectedEffect)
            {
                effectPreviewDispatch = dispatch;
                effectPreviewDispatchUtc = DateTimeOffset.UtcNow;
            }
        };
        if (transport.VisualSignalPath is not null)
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
                if (request.AbilityId is int abilityId)
                {
                    active.PublishDiagnosticAbilityPreview(request.AbilityCommand, abilityId);
                }
                else
                {
                    active.PublishDiagnosticEffectPreview(effectPreviewExpectedEffect);
                }
                effectPreviewSubmitted = true;
                effectPreviewSubmittedSeconds = Stopwatch.GetElapsedTime(presentationClockStarted).TotalSeconds;
                effectPreviewGate!.Write("started", active.NativeHostCallbacks ?? 0, diagnosticPresentedFrames, DateTimeOffset.UtcNow);
            }
            if (effectPreviewDispatch is { } dispatch && !effectPreviewPresented)
            {
                effectPreviewGate!.Write("visual-started", active.NativeHostCallbacks ?? 0, diagnosticPresentedFrames, DateTimeOffset.UtcNow, nativeIdentity: dispatch, dispatchUtc: effectPreviewDispatchUtc);
                if (request.AbilityId is null)
                {
                    effectPreviewGate.Write("completed", active.NativeHostCallbacks ?? 0, diagnosticPresentedFrames, DateTimeOffset.UtcNow, nativeIdentity: new { scope = "graphical-preview-initialized", effect = effectPreviewExpectedEffect });
                    effectPreviewCompleted = true;
                }
                effectPreviewPresented = true;
                effectPreviewStartedSeconds = Stopwatch.GetElapsedTime(presentationClockStarted).TotalSeconds;
            }
            if (request.AbilityId is not null && effectPreviewPresented && !effectPreviewCompleted)
            {
                if (active.ObserveDiagnosticDrainBlockers().Count != 0)
                {
                    effectPreviewDrainedFrame = null;
                }
                else if (effectPreviewDrainedFrame is null)
                {
                    effectPreviewDrainedFrame = diagnosticPresentedFrames;
                }
                else if (diagnosticPresentedFrames - effectPreviewDrainedFrame.Value >= 2)
                {
                    effectPreviewGate!.Write("completed", active.NativeHostCallbacks ?? 0, diagnosticPresentedFrames, DateTimeOffset.UtcNow, nativeIdentity: new { scope = "full-ability", drained = true, command_id = request.AbilityCommand, ability_id = request.AbilityId, native_command = effectPreviewAbility!.Command, action_id = effectPreviewAbility.ActionId, effect = effectPreviewExpectedEffect });
                    effectPreviewCompleted = true;
                }
            }
            double now = Stopwatch.GetElapsedTime(presentationClockStarted).TotalSeconds;
            if (request.AbilityId is not null && effectPreviewSubmitted && now - effectPreviewSubmittedSeconds >= request.DurationSeconds)
            {
                if (!effectPreviewCompleted)
                {
                    throw new TimeoutException("The complete ability did not drain its native graphics, descriptors, files and camera within the preview deadline.");
                }
                ExitGame();
            }
            else if (request.AbilityId is null && effectPreviewPresented && now - effectPreviewStartedSeconds >= request.DurationSeconds)
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
