using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private DiagnosticBattleDriver? diagnosticDriver;
    private DiagnosticBattleDriverRecipe? diagnosticDriverRecipe;
    private BattleSession? diagnosticDriverBattle;
    private DiagnosticLiveTelemetry? diagnosticTelemetry;
    private GpuTriangleRenderer? diagnosticRenderer;
    private bool diagnosticTargetVerified;
    private bool diagnosticHostWasActive = true;
    private long diagnosticPresentedFrames;
    private int diagnosticProgressSequence;
    private string? diagnosticLastStage;
    private long diagnosticProgressTimestamp;
    private long diagnosticProgressCallback = -1;
    private readonly List<string> diagnosticCompletedStages = [];
    private static readonly string[] DiagnosticCampaignStages = ["initialization", "action_one", "cleanup_rearm", "action_two", "teardown"];

    private void StartDiagnosticDriver(DiagnosticBattleDriverRecipe recipe)
    {
        if (options.CaptureFramePath is not null)
        {
            throw new NotSupportedException("Scored live diagnostics require captures in a separate evidence replay.");
        }
        diagnosticDriverRecipe = recipe;
        diagnosticDriverBattle = world?.Battle ?? throw new InvalidOperationException("Diagnostic driver requires its published live battle.");
        diagnosticDriver = new(recipe, Stopwatch.GetElapsedTime(presentationClockStarted).TotalSeconds);
        diagnosticTelemetry = new(diagnosticDriverBattle, world!.DiagnosticReceipt!.BattleInstanceId, recipe.ExpectedEffect);
        diagnosticDriverBattle.DiagnosticEffectDispatched += diagnosticDriver.ObserveDispatch;
        PublishDiagnosticDriverProgress();
    }

    private void AdvanceDiagnosticDriver(TimeSpan now)
    {
        if (diagnosticDriver is not { Finished: false } driver)
        {
            return;
        }
        diagnosticPresentedFrames++;
        long presentedTimestamp = Stopwatch.GetTimestamp();
        try
        {
            diagnosticTelemetry!.Presented(presentedTimestamp, diagnosticPresentedFrames, driver.Stage.ToString(), battleGpu?.PresentationVersion ?? 0, battleGpu?.FrameDrawCalls ?? 0, battleGpu?.FrameTextureUploads ?? 0);
            if (!ReferenceEquals(diagnosticDriverBattle, world?.Battle))
            {
                driver.Fail("BATTLE_SESSION_REPLACED");
            }
            else if (!IsActive || fieldFaulted || fieldRenderFaulted)
            {
                diagnosticHostWasActive = false;
                driver.Fail("GRAPHICAL_HOST_INACTIVE_OR_FAULTED");
            }
            else
            {
                BattleSession active = diagnosticDriverBattle!;
                string[] blockers = active.NativeEntryReady ? active.ObserveDiagnosticDrainBlockers().ToArray() : ["native-entry"];
                if (active.NativeEntryReady && blockers.Length == 0 && !diagnosticTargetVerified)
                {
                    diagnosticTargetVerified = world!.VerifyDiagnosticTarget() is not null;
                }
                if (diagnosticRenderer is not null && !ReferenceEquals(diagnosticRenderer, battleGpu))
                {
                    throw new InvalidOperationException("Diagnostic renderer owner was replaced during an uninterrupted battle.");
                }
                bool ready = active.NativeEntryReady && (world!.DiagnosticReceipt?.Preparation.Target is null || diagnosticTargetVerified) && active.CanChoosePlayerCommand(diagnosticDriverRecipe!.Input.ActorId!);
                DiagnosticDriverObservation observation = new(now.TotalSeconds, active.NativeHostCallbacks ?? throw new NotSupportedException("Diagnostic driver requires the live callback ledger."), diagnosticPresentedFrames, ready, blockers.Length == 0, active.Outcome != BattleOutcome.Active, blockers);
                DiagnosticDriverStage previousStage = driver.Stage;
                if (driver.Advance(observation))
                {
                    diagnosticRenderer ??= battleGpu ?? throw new NotSupportedException("Scored diagnostic execution requires the GPU renderer.");
                    diagnosticTelemetry.Begin(presentedTimestamp, diagnosticPresentedFrames, diagnosticRenderer.PresentationVersion, diagnosticRenderer.TelemetryOwnerId.ToString());
                    ApplyBattleCommand(diagnosticDriverRecipe!.Input);
                }
                if (previousStage == DiagnosticDriverStage.Tail && driver.Stage is DiagnosticDriverStage.Readiness or DiagnosticDriverStage.Completed)
                {
                    diagnosticTelemetry.Complete(diagnosticPresentedFrames, true, diagnosticDriverRecipe!.TailFrames, null);
                }
            }
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException or InvalidDataException or ArgumentException or IOException)
        {
            driver.Fail($"DRIVER_PREREQUISITE: {exception.GetType().Name}: {exception.Message}");
        }
        PublishDiagnosticDriverProgress();
        if (driver.Finished)
        {
            diagnosticTelemetry!.Complete(diagnosticPresentedFrames, false, 0, driver.Blocker);
            PublishDiagnosticCandidate();
            diagnosticTelemetry.Dispose();
            diagnosticDriverBattle!.DiagnosticEffectDispatched -= driver.ObserveDispatch;
            if (world?.IsDiagnosticBattle == true)
            {
                world.EndDiagnosticBattle();
                ClearBattlePresentation();
            }
            Exit();
        }
    }

    private void PublishDiagnosticDriverProgress()
    {
        DiagnosticBattleDriver driver = diagnosticDriver!;
        string stage = driver.CampaignStage;
        long callback = diagnosticDriverBattle?.NativeHostCallbacks ?? -1;
        long timestamp = Stopwatch.GetTimestamp();
        bool progressHeartbeat = callback > diagnosticProgressCallback && Stopwatch.GetElapsedTime(diagnosticProgressTimestamp).TotalSeconds >= 1;
        if (stage == diagnosticLastStage && !driver.Finished && !progressHeartbeat)
        {
            return;
        }
        int position = Array.IndexOf(DiagnosticCampaignStages, stage);
        diagnosticCompletedStages.Clear();
        diagnosticCompletedStages.AddRange(DiagnosticCampaignStages.Take(position));
        diagnosticLastStage = stage;
        diagnosticProgressTimestamp = timestamp;
        diagnosticProgressCallback = callback;
        diagnosticProgressSequence++;
        if (options.ControlDirectory is { } control)
        {
            WriteDiagnosticJson(Path.Combine(control, "progress.json"), new { stage, sequence = diagnosticProgressSequence, completed_stages = diagnosticCompletedStages.ToArray() });
        }
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_OUTPUT_DIRECTORY");
        string? receipt = diagnosticDriverRecipe!.ProgressPath ?? (string.IsNullOrWhiteSpace(output) ? null : Path.Combine(output, "diagnostic-driver.json"));
        if (receipt is not null && (stage != diagnosticLastReceiptStage || driver.Finished))
        {
            diagnosticLastReceiptStage = stage;
            WriteDiagnosticJson(receipt, new { Status = driver.Stage == DiagnosticDriverStage.Completed ? "CANDIDATE_COMPLETED" : driver.Finished ? "BLOCKED" : "RUNNING", Coverage = "LIVE_DIAGNOSTIC", Stage = driver.Stage.ToString(), driver.Execution, driver.Blocker, driver.LastDrainBlockers, Dispatches = driver.Dispatches, Initialization = world?.DiagnosticReceipt });
        }
    }

    private string? diagnosticLastReceiptStage;

    private void PublishDiagnosticCandidate()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_OUTPUT_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(output))
        {
            DiagnosticLiveTelemetry telemetry = diagnosticTelemetry!;
            string telemetryPath = Path.Combine(output, "telemetry-report.json");
            bool fixtureVerified = diagnosticTargetVerified && world?.DiagnosticReceipt?.Preparation.Target is not null;
            WriteDiagnosticJson(telemetryPath, new
            {
                schema_version = 1,
                battle_instance_id = world?.DiagnosticReceipt?.BattleInstanceId,
                timestamp_frequency = Stopwatch.Frequency,
                expected_effect = diagnosticDriverRecipe!.ExpectedEffect,
                input = diagnosticDriverRecipe.Input,
                initialization_verified = world?.DiagnosticReceipt is not null,
                fixture_verified = fixtureVerified,
                fixture_blockers = fixtureVerified ? Array.Empty<string>() : new[] { "DECLARED_TARGET_NOT_VERIFIED" },
                environment = new { renderer = GraphicsDevice.Adapter.Description, width = GraphicsDevice.Viewport.Width, height = GraphicsDevice.Viewport.Height, gpu = diagnosticRenderer is not null, real_time = !options.ControlStep, active = diagnosticHostWasActive },
                audio = new { entry_receipts = world?.Events.Scene.NativeBattleMusicEntry is null ? 0 : 1, dispatch_receipts = telemetry.Executions.Sum(execution => execution.Events.Count(entry => entry.NativeModuleSound is not null || entry.NativeMusicCommand is not null)), enabled = options.AudioEnabled, audibility_verified = false },
                blocker = diagnosticDriver!.Blocker,
                coverage = telemetry.Coverage,
                instrumentation_blind_spots = NativeBattleTelemetry.KnownBlindSpots,
                executions = telemetry.Executions,
                initialization = world?.DiagnosticReceipt
            }, false);
            string hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(telemetryPath)));
            WriteDiagnosticJson(Path.Combine(output, "candidate.json"), new { status = diagnosticDriver.Blocker is null ? "CANDIDATE_COMPLETED" : "ERROR", reason = diagnosticDriver.Blocker, coverage = "LIVE_DIAGNOSTIC", telemetry_report = "telemetry-report.json", telemetry_sha256 = hash, execution_count = diagnosticDriver.Dispatches.Count, battle_instance_id = world?.DiagnosticReceipt?.BattleInstanceId }, false);
        }
    }

    private static void WriteDiagnosticJson(string path, object value, bool replace = true)
    {
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        string temporary = full + ".tmp";
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, DiagnosticBattleJson);
        File.WriteAllBytes(temporary, bytes);
        NativeBattleTelemetry.Record(2005, NativeBattleTrafficKind.DiagnosticOutput, bytes.Length);
        File.Move(temporary, full, replace);
    }
}
