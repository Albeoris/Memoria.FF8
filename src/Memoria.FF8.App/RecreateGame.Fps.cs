using System.Diagnostics;
using System.Reflection;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private readonly DiagnosticHostPhaseTiming diagnosticHostPhaseTiming = new(Environment.GetEnvironmentVariable("MEMORIA_DIAGNOSTIC_PACING") == "1", Environment.GetEnvironmentVariable("MEMORIA_DIAGNOSTIC_NATIVE_CPU_CYCLES") == "1");
    private readonly PerformanceTrace performanceTrace = new();
    private VisualFrameRate? visualFrameRate;
    private readonly long presentationClockStarted = Stopwatch.GetTimestamp();
    private TimeSpan nextFpsLabelUpdate;
    private string fpsLabel = "FPS: --";
    internal Action<long, long, PresentationPacerObservation, DiagnosticHostFramePhases?>? PresentedFrameObserver { get; set; }

    private void BeginMeasuredFrame()
    {
        if (!uiPreferences.ShowFps || diagnosticDriver is { Finished: false })
        {
            DisposeFrameRate();
            return;
        }
        visualFrameRate ??= GraphicsHostReadiness.Initialize(() => new VisualFrameRate(GraphicsDevice));
        visualFrameRate.BeginFrame();
    }

    protected override bool ObservePresentTiming => diagnosticHostPhaseTiming.Enabled || PresentedFrameObserver is not null;

    protected override void OnFramePresented(long presentBegin, long presentEnd)
    {
        bool observePacing = ObservePresentTiming;
        DiagnosticHostFramePhases? phases = diagnosticHostPhaseTiming.Enabled ? diagnosticHostPhaseTiming.CompleteInterval() : null;
        using DiagnosticHostPhaseTiming.Scope postPresentTiming = diagnosticHostPhaseTiming.Measure(DiagnosticHostPhase.PostPresent);
        long visualTimestamp = diagnosticTelemetry?.VisualChangesEnabled == true ? Stopwatch.GetTimestamp() : 0;
        PresentationPacerObservation pacer = observePacing ? presentationPacer.TimingObservation : default;
        if (performanceTrace.IsRecording && phases is { } framePhases)
        {
            BattleSession? battle = InFf7 ? null : ActiveBattle;
            string scene = PerformanceTraceScene();
            int? fieldId = scene == "Field" ? session.State.FieldId : null;
            performanceTrace.Presented(scene, fieldId, battle?.EncounterId, presentBegin, presentEnd, pacer, framePhases);
        }
        PresentedFrameObserver?.Invoke(presentBegin, presentEnd, pacer, phases);
        if (observePacing)
        {
            diagnosticTelemetry?.ObservePresentationPacing(presentBegin, presentEnd, pacer, phases);
        }
        if (visualTimestamp != 0)
        {
            diagnosticTelemetry!.ObserveBattleImagePresented(diagnosticPresentedFrames + 1, visualTimestamp);
            PublishDiagnosticBattleImage(Stopwatch.GetElapsedTime(presentationClockStarted, visualTimestamp));
        }
        TimeSpan now = Stopwatch.GetElapsedTime(presentationClockStarted);
        visualFrameRate?.Presented(now);
        AdvanceDiagnosticDriver(now);
        ObserveDiagnosticIdlePresent(presentBegin, presentEnd, pacer, phases);
        if (effectPreview is not null)
        {
            diagnosticPresentedFrames++;
            if ((effectPreviewDispatch is not null || effectPreviewSourceAdmission is not null) && !effectPreviewCompleted && !effectPreviewFailed)
            {
                effectComponentMeasurements?.Presented(Stopwatch.GetTimestamp(), presentBegin, presentEnd, pacer, phases);
                if (effectComponentMeasurements is not null && !IsActive)
                {
                    effectComponentInactivePresentedFrames++;
                }
            }
        }
        AdvanceEffectPreview();
        presentationPacer.ObserveTiming(observePacing);
        if (now >= nextFpsLabelUpdate)
        {
            double? fps = visualFrameRate?.PresentationsPerSecond;
            double? changes = visualFrameRate?.FramesPerSecond;
            string changesLabel = changes is double visual ? FormattableString.Invariant($"Changes/s: {visual:F1}") : "Changes/s: --";
            fpsLabel = fps is double value ? FormattableString.Invariant($"FPS: {value:F1} | {changesLabel}") : $"FPS: -- | {changesLabel}";
            nextFpsLabelUpdate = now + TimeSpan.FromSeconds(0.25);
        }
    }

    private string PerformanceTraceScene()
    {
        if (gameMenus?.IsOpen == true)
        {
            return InFf7 ? "FFVII Menu" : "Menu";
        }
        if (InFf7)
        {
            return ff7Scene?.Session switch
            {
                null => "FFVII Title",
                { Battle: not null } => "FFVII Battle",
                { World: not null } => "FFVII WorldMap",
                { Condor: not null } => "FFVII Fort Condor",
                { Submarine: not null } => "FFVII Submarine",
                { ChocoboRace: not null } => "FFVII Chocobo Race",
                _ => "FFVII Field"
            };
        }
        if (ActiveBattle is not null)
        {
            return "Battle";
        }
        if (world?.ActiveTripleTriad is not null)
        {
            return "Triple Triad";
        }
        return session.State.Scene.ToString();
    }

    private string PerformanceTraceContext()
    {
        var assembly = typeof(RecreateGame).Assembly;
        var presentation = GraphicsDevice.PresentationParameters;
        string? build = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assembly.GetName().Version?.ToString();
        string activeMods = string.Join(", ", resourceHost?.Mods.Catalog.Packages.Where(package => package.Status == ModEffectiveStatus.Active).Select(package => package.Id) ?? []);
        return $"""
            Build: {build}
            Runtime: .NET {Environment.Version} ({System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture})
            Operating system: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}
            Logical processors available: {Environment.ProcessorCount}
            Active mods: {activeMods}
            Graphics backend: {GraphicsHostReadiness.Backend}
            Graphics adapter: {GraphicsDevice.Adapter.Description}
            FFVIII scene renderer: {(options.SoftwareRenderer ? "software" : "GPU")}
            Viewport: {GraphicsDevice.Viewport.Width} x {GraphicsDevice.Viewport.Height}
            Back buffer: {presentation.BackBufferWidth} x {presentation.BackBufferHeight}, MSAA samples: {presentation.MultiSampleCount}
            VSync: {uiPreferences.VSync}, present interval: {presentation.PresentationInterval}
            Configured frame limits (FPS): field {uiPreferences.FieldFps}, world {uiPreferences.WorldFps}, battle {uiPreferences.BattleFps}, menu {uiPreferences.MenuFps}
            """;
    }

    private void DrawFps(Rectangle bounds)
    {
        if (visualFrameRate is not null)
        {
            batch.End();
            visualFrameRate.CompleteFrame();
            batch.Begin(samplerState: SamplerState.LinearClamp);
        }
        if (!uiPreferences.ShowFps)
        {
            return;
        }
        float scale = ViewportLayout.UiScale(bounds.Width, bounds.Height);
        float margin = 8 * scale;
        float fontSize = 16 * scale;
        Vector2 size = text.MeasureWrapped(fpsLabel, fontSize, float.PositiveInfinity);
        Vector2 position = new(bounds.Right - size.X - margin * 2, bounds.Top + margin);
        Rectangle background = new((int)(position.X - margin), (int)position.Y, (int)MathF.Ceiling(size.X + margin * 2), (int)MathF.Ceiling(size.Y));
        batch.Draw(pixel, background, Color.Black * 0.75f);
        text.Draw(batch, fpsLabel, fontSize, position, Color.White);
    }

    private void DisposeFrameRate()
    {
        visualFrameRate?.Dispose();
        visualFrameRate = null;
        fpsLabel = "FPS: --";
        nextFpsLabelUpdate = TimeSpan.Zero;
    }
}
