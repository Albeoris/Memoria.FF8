using System.Diagnostics;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private bool ObserveEffectComponentTerminalTextures(BattleSession active)
    {
        BattleComponentTextureQuiescenceObservation? observation = active.ObserveComponentTerminalTextures(effectPreviewExpectedEffect);
        if (!effectComponentTerminalTextures.Observe(observation, diagnosticPresentedFrames, Stopwatch.GetTimestamp()))
        {
            return false;
        }
        EffectPreviewRequest request = effectPreview!;
        BattleComponentAdmissionIdentity? admission = effectPreviewSourceAdmission is { } source ? new(source.Effect, source.Command, source.Action, source.Actor, source.HeaderIndex, source.Callback) : null;
        BattleComponentTerminalTextureCertificate certificate = effectComponentTerminalTextures.Certificate(effectPreviewExpectedEffect, request.SourceSha256!, effectPreviewDispatch, admission);
        BattleComponentTerminalTextureCertificateValidation.Validate(certificate, effectPreviewExpectedEffect, request.SourceSha256!);
        EffectComponentFrameReport measurements = effectComponentMeasurements!.Complete(effectComponentTerminalTextures.BoundaryTimestamp);
        string output = request.Presentation?.ReceiptDirectory ?? options.ControlDirectory!;
        bool written = WriteDiagnosticJson(Path.Combine(output, "component-measurements.json"), new
        {
            scope = "installed-source-controlled-component-gpu-presentation",
            source_sha256 = request.SourceSha256,
            effect = effectPreviewExpectedEffect,
            native_dispatch = effectPreviewDispatch,
            native_admission = effectPreviewSourceAdmission,
            completion_kind = certificate.CompletionKind,
            completed = true,
            drained = false,
            resident_tasks_remain = true,
            terminal_texture_certificate = certificate,
            drain_blockers = certificate.Last.RawBlockers,
            tail_presented_frames = certificate.TailPresentedFrames,
            host = new { backend = GraphicsHostReadiness.Backend, vsync = uiPreferences.VSync, presentation_interval = GraphicsDevice.PresentationParameters.PresentationInterval.ToString(), frame_limit = uiPreferences.BattleFps, width = GraphicsDevice.PresentationParameters.BackBufferWidth, height = GraphicsDevice.PresentationParameters.BackBufferHeight, msaa_samples = GraphicsDevice.PresentationParameters.MultiSampleCount, inactive_presented_frames = effectComponentInactivePresentedFrames },
            measurements
        });
        if (!written)
        {
            throw new IOException("The component terminal-texture certificate could not be published.");
        }
        effectPreviewGate!.Write("completed", active.NativeHostCallbacks ?? 0, diagnosticPresentedFrames, DateTimeOffset.UtcNow, nativeIdentity: new { scope = "controlled-component", completion_kind = certificate.CompletionKind, completed = true, drained = false, resident_tasks_remain = true, terminal_texture_certificate = certificate, effect = effectPreviewExpectedEffect });
        return true;
    }
}
