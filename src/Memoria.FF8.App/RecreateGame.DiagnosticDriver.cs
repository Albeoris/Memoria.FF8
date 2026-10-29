using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private DiagnosticBattleDriver? diagnosticDriver;
    private Exception? diagnosticBattleFault;
    private DiagnosticBattleDriverRecipe? diagnosticDriverRecipe;
    private BattleSession? diagnosticDriverBattle;
    private DiagnosticLiveTelemetry? diagnosticTelemetry;
    private GpuTriangleRenderer? diagnosticRenderer;
    private bool diagnosticTargetVerified;
    private bool diagnosticHostWasActive = true;
    private long diagnosticPresentedFrames;
    private int diagnosticProgressSequence;
    private string? diagnosticLastStage;
    private DiagnosticDriverStage? diagnosticLastDriverStage;
    private long diagnosticProgressTimestamp;
    private long diagnosticProgressCallback = -1;
    private readonly List<string> diagnosticCompletedStages = [];
    private readonly List<BattleDiagnosticLimitSelectionReceipt> diagnosticLimitSelections = [];
    private readonly List<long> diagnosticGunbladeTriggers = [];
    private readonly List<BattleDiagnosticRepairReceipt> diagnosticRepairs = [];
    private readonly List<BattleDiagnosticEnemySubmission> diagnosticEnemySubmissions = [];
    private static readonly string[] DiagnosticCampaignStages = ["initialization", "action_one", "cleanup_rearm", "action_two", "teardown"];

    private void StartDiagnosticDriver(DiagnosticBattleDriverRecipe recipe)
    {
        if (options.CaptureFramePath is not null)
        {
            throw new NotSupportedException("Scored live diagnostics require captures in a separate evidence replay.");
        }
        diagnosticHostWasActive = IsActive;
        diagnosticBattleFault = null;
        diagnosticDriverRecipe = recipe;
        diagnosticDriverBattle = world?.Battle ?? throw new InvalidOperationException("Diagnostic driver requires its published live battle.");
        diagnosticEnemySubmissions.Clear();
        diagnosticDriver = new(recipe, Stopwatch.GetElapsedTime(presentationClockStarted).TotalSeconds);
        diagnosticTelemetry = new(diagnosticDriverBattle, world!.DiagnosticReceipt!.BattleInstanceId, recipe.ExpectedEffect, recipe.ShotCycle is null ? null : (byte)237);
        diagnosticDriverBattle.DiagnosticEffectDispatched += ObserveDiagnosticDriverDispatch;
        PublishDiagnosticDriverProgress();
    }

    private void AdvanceDiagnosticDriver(TimeSpan now)
    {
        if (diagnosticDriver is not { } driver)
        {
            return;
        }
        if (driver.Finished)
        {
            FinalizeDiagnosticDriver(driver);
            return;
        }
        diagnosticHostWasActive &= IsActive;
        diagnosticPresentedFrames++;
        long presentedTimestamp = Stopwatch.GetTimestamp();
        try
        {
            if (diagnosticDriverRecipe!.LimitSelection is not null && diagnosticDriverBattle!.TriggerDiagnosticGunbladeWindow())
            {
                if (diagnosticGunbladeTriggers.Count >= 1024)
                {
                    throw new InvalidOperationException("Diagnostic timed-input observation capacity exceeded.");
                }
                diagnosticGunbladeTriggers.Add(diagnosticDriverBattle.NativeHostCallbacks ?? diagnosticDriverBattle.Frame);
            }
            diagnosticTelemetry!.Presented(presentedTimestamp, diagnosticPresentedFrames, driver.Stage.ToString(), battleGpu?.PresentationVersion ?? 0, battleGpu?.FrameDrawCalls ?? 0, battleGpu?.FrameTextureUploads ?? 0);
            if (!ReferenceEquals(diagnosticDriverBattle, world?.Battle))
            {
                driver.Fail("BATTLE_SESSION_REPLACED");
            }
            else if (fieldFaulted || fieldRenderFaulted)
            {
                world!.AbandonFaultedDiagnosticBattle();
                driver.Fail($"GRAPHICAL_HOST_FAULTED: {diagnosticBattleFault?.ToString() ?? error ?? "No host error detail was retained."}");
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
                AdvanceDiagnosticShot(active, driver, blockers.Length == 0);
                bool ready = active.NativeEntryReady && (world!.DiagnosticReceipt?.Preparation.Target is null || diagnosticTargetVerified) && DiagnosticCommandReady(active, diagnosticDriverRecipe!);
                DiagnosticDriverObservation observation = new(now.TotalSeconds, active.NativeHostCallbacks ?? throw new NotSupportedException("Diagnostic driver requires the live callback ledger."), diagnosticPresentedFrames, ready, blockers.Length == 0, active.Outcome != BattleOutcome.Active, blockers);
                DiagnosticDriverStage previousStage = driver.Stage;
                if (driver.Advance(observation))
                {
                    diagnosticRenderer ??= battleGpu ?? throw new NotSupportedException("Scored diagnostic execution requires the GPU renderer.");
                    diagnosticTelemetry.Begin(presentedTimestamp, diagnosticPresentedFrames, diagnosticRenderer.PresentationVersion, diagnosticRenderer.TelemetryOwnerId.ToString());
                    if (diagnosticDriverRecipe!.EnemyAbility is { } enemySelection)
                    {
                        if (diagnosticEnemySubmissions.Count >= 2)
                        {
                            throw new InvalidOperationException("Diagnostic enemy submission evidence is bounded to the two declared executions.");
                        }
                        active.SubmitDiagnosticEnemyAbility(diagnosticDriverRecipe.Input.ActorId!, enemySelection, diagnosticDriverRecipe.ExpectedEffect);
                        BattleDiagnosticEnemySubmission submission = active.LastDiagnosticEnemySubmission ?? throw new InvalidOperationException("Successful diagnostic enemy submission did not retain its actual native selection receipt.");
                        diagnosticEnemySubmissions.Add(submission);
                    }
                    else
                    {
                        BattleDiagnosticLimitSelectionReceipt? selectedLimit = null;
                        if (diagnosticDriverRecipe.LimitSelection is { } limitSelection)
                        {
                            BattleDiagnosticLimitSelectionReceipt selection = active.SelectDiagnosticLimit(diagnosticDriverRecipe.Input.ActorId!, limitSelection);
                            diagnosticLimitSelections.Add(selection);
                            selectedLimit = selection;
                            if (!selection.Accepted)
                            {
                                throw new NotSupportedException("The bounded ordinary native selections did not reach the declared Limit crisis range.");
                            }
                        }
                        if (diagnosticDriverRecipe.ShotCycle is not null)
                        {
                            BeginDiagnosticShot(active, driver, selectedLimit ?? throw new InvalidDataException("Shot cycle did not select its real Limit."));
                        }
                        ApplyBattleCommand(diagnosticDriverRecipe.Input);
                        if (diagnosticDriverRecipe.ShotCycle is not null)
                        {
                            driver.ShotCycle!.Opened(ObserveDiagnosticShot(active));
                        }
                    }
                }
                if (previousStage == DiagnosticDriverStage.Drain && driver.Stage == DiagnosticDriverStage.Tail)
                {
                    diagnosticTelemetry.MarkDrained(presentedTimestamp);
                }
                if (previousStage == DiagnosticDriverStage.Tail && driver.Stage is DiagnosticDriverStage.Readiness or DiagnosticDriverStage.Completed)
                {
                    diagnosticTelemetry.Complete(diagnosticPresentedFrames, true, diagnosticDriverRecipe!.TailFrames, null);
                    if (driver.Stage == DiagnosticDriverStage.Readiness && diagnosticDriverRecipe.Repairs.Length > 0)
                    {
                        diagnosticRepairs.Add(active.ApplyDiagnosticCombatRepairs(diagnosticDriverRecipe.Repairs, active.NativeHostCallbacks ?? active.Frame));
                        if (active.DiagnosticTerminalHold?.Phase == BattleDiagnosticTerminalHoldPhase.Held)
                        {
                            active.RearmDiagnosticTerminalHold(active.NativeHostCallbacks ?? active.Frame);
                        }
                    }
                    if (driver.Stage == DiagnosticDriverStage.Completed)
                    {
                        ReleaseDiagnosticTerminalHold(active);
                    }
                }
            }
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException or InvalidDataException or ArgumentException or IOException)
        {
            driver.Fail($"DRIVER_PREREQUISITE: {exception.GetType().Name}: {exception.Message}");
        }
        if (driver.Finished)
        {
            FinalizeDiagnosticDriver(driver);
        }
        else
        {
            PublishDiagnosticDriverProgress();
        }
    }

    private void FinalizeDiagnosticDriver(DiagnosticBattleDriver driver)
    {
        WriteDiagnosticFinalizationStage("begin");
        try
        {
            FinalizeDiagnosticDriverCore(driver);
        }
        catch (Exception exception)
        {
            DiagnosticHostFailure.Write(exception, Environment.GetEnvironmentVariable("MEMORIA_FF8_OUTPUT_DIRECTORY"), Console.Error);
            throw;
        }
    }

    private void FinalizeDiagnosticDriverCore(DiagnosticBattleDriver driver)
    {
        ReleaseDiagnosticTerminalHold(diagnosticDriverBattle!);
        PublishDiagnosticDriverProgress();
        diagnosticTelemetry!.Complete(diagnosticPresentedFrames, false, 0, driver.Blocker);
        DisposeDiagnosticBattleImage();
        WriteDiagnosticFinalizationStage("telemetry-completed");
        PublishDiagnosticCandidate();
        WriteDiagnosticFinalizationStage("candidate-published");
        diagnosticTelemetry.Dispose();
        diagnosticDriverBattle!.DiagnosticEffectDispatched -= ObserveDiagnosticDriverDispatch;
        if (world?.IsDiagnosticBattle == true)
        {
            world.EndDiagnosticBattle();
            ClearBattlePresentation();
        }
        diagnosticDriver = null;
        WriteDiagnosticFinalizationStage("exit-requested");
        Exit();
    }

    private void WriteDiagnosticFinalizationStage(string stage)
    {
        DiagnosticHostFailure.WriteStage(Environment.GetEnvironmentVariable("MEMORIA_FF8_OUTPUT_DIRECTORY"), stage, diagnosticDriverBattle?.NativeHostCallbacks, diagnosticDriverBattle?.Frame);
    }

    private void PublishDiagnosticDriverProgress()
    {
        DiagnosticBattleDriver driver = diagnosticDriver!;
        string stage = driver.CampaignStage;
        long callback = diagnosticDriverBattle?.NativeHostCallbacks ?? -1;
        long timestamp = Stopwatch.GetTimestamp();
        bool progressHeartbeat = callback > diagnosticProgressCallback && Stopwatch.GetElapsedTime(diagnosticProgressTimestamp).TotalSeconds >= 1;
        bool driverStageChanged = diagnosticLastDriverStage != driver.Stage;
        if (stage == diagnosticLastStage && !driverStageChanged && !driver.Finished && !progressHeartbeat)
        {
            return;
        }
        int position = Array.IndexOf(DiagnosticCampaignStages, stage);
        diagnosticCompletedStages.Clear();
        diagnosticCompletedStages.AddRange(DiagnosticCampaignStages.Take(position));
        diagnosticLastStage = stage;
        diagnosticLastDriverStage = driver.Stage;
        diagnosticProgressTimestamp = timestamp;
        diagnosticProgressCallback = callback;
        diagnosticProgressSequence++;
        if (options.ControlDirectory is { } control)
        {
            WriteDiagnosticJson(Path.Combine(control, "progress.json"), new { stage, sequence = diagnosticProgressSequence, completed_stages = diagnosticCompletedStages.ToArray(), driver_phase = driver.Stage.ToString(), execution = driver.Execution, callback, frame = diagnosticDriverBattle?.Frame, native_graphics_callback = diagnosticDriverBattle?.NativeGraphics?.Callback, native_ui_pause = diagnosticDriverBattle?.NativeGraphics?.UiServices?.PauseDE9, terminal_hold_phase = diagnosticDriverBattle?.DiagnosticTerminalHold?.Phase.ToString(), drain_blockers = driver.LastDrainBlockers, loop_metrics = metrics.Latest, texture_decoding = battleScene?.TextureDecoding, presentation_pacing = diagnosticTelemetry?.PresentationPacingProgress() });
            if (diagnosticTelemetry is { Active: true } telemetry)
            {
                WriteDiagnosticJson(Path.Combine(control, "presentation-progress.json"), telemetry.PresentationProgress(diagnosticProgressSequence));
            }
        }
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_OUTPUT_DIRECTORY");
        string? receipt = diagnosticDriverRecipe!.ProgressPath ?? (string.IsNullOrWhiteSpace(output) ? null : Path.Combine(output, "diagnostic-driver.json"));
        if (receipt is not null && (stage != diagnosticLastReceiptStage || driver.Stage != diagnosticLastReceiptDriverStage || driver.Finished))
        {
            if (WriteDiagnosticJson(receipt, new { Status = driver.Stage == DiagnosticDriverStage.Completed ? "CANDIDATE_COMPLETED" : driver.Finished ? "BLOCKED" : "RUNNING", Coverage = "LIVE_DIAGNOSTIC", Stage = driver.Stage.ToString(), driver.Execution, driver.Blocker, driver.LastDrainBlockers, Dispatches = driver.Dispatches, Initialization = world?.DiagnosticReceipt, Callback = callback, Frame = diagnosticDriverBattle?.Frame }))
            {
                diagnosticLastReceiptStage = stage;
                diagnosticLastReceiptDriverStage = driver.Stage;
            }
        }
    }

    private string? diagnosticLastReceiptStage;
    private DiagnosticDriverStage? diagnosticLastReceiptDriverStage;

    internal static bool DiagnosticCommandReady(BattleSession active, DiagnosticBattleDriverRecipe recipe)
    {
        if (active.DiagnosticTerminalHold?.Phase == BattleDiagnosticTerminalHoldPhase.Held)
        {
            return false;
        }
        return recipe.EnemyAbility is { } enemy ? active.CanSubmitDiagnosticEnemyAbility(recipe.Input.ActorId!, enemy) : active.CanChoosePlayerCommand(recipe.Input.ActorId!);
    }

    private static void ReleaseDiagnosticTerminalHold(BattleSession active)
    {
        if (active.DiagnosticTerminalHold?.Phase is BattleDiagnosticTerminalHoldPhase.Armed or BattleDiagnosticTerminalHoldPhase.Held)
        {
            active.ReleaseDiagnosticTerminalHold(active.NativeHostCallbacks ?? active.Frame);
        }
    }

    private void PublishDiagnosticCandidate()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_OUTPUT_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(output))
        {
            DiagnosticLiveTelemetry telemetry = diagnosticTelemetry!;
            string telemetryPath = Path.Combine(output, "telemetry-report.json");
            bool fixtureVerified = diagnosticTargetVerified && world?.DiagnosticReceipt?.Preparation.Target is not null;
            WriteDiagnosticFinalizationStage("invocation-owners-begin");
            object? invocationOwners = diagnosticDriverBattle?.ObserveDiagnosticInvocationOwners();
            WriteDiagnosticFinalizationStage("invocation-owners-completed");
            object report = new
            {
                schema_version = 1,
                battle_instance_id = world?.DiagnosticReceipt?.BattleInstanceId,
                timestamp_frequency = Stopwatch.Frequency,
                expected_effect = diagnosticDriverRecipe!.ExpectedEffect,
                input = diagnosticDriverRecipe.Input,
                expected_native_action = diagnosticDriverRecipe.ExpectedNativeAction,
                expected_native_command = diagnosticDriverRecipe.ExpectedNativeCommand,
                shot_cycle = diagnosticDriverRecipe.ShotCycle,
                shot_cycles = diagnosticDriver!.ShotReceipts,
                supporting_effects = diagnosticDriverRecipe.SupportingEffects,
                supporting_invocations = diagnosticDriverRecipe.SupportingInvocations,
                declared_repairs = diagnosticDriverRecipe.Repairs,
                declared_terminal_hold = diagnosticDriverRecipe.TerminalHold,
                initialization_verified = world?.DiagnosticReceipt is not null,
                fixture_verified = fixtureVerified,
                fixture_blockers = fixtureVerified ? Array.Empty<string>() : new[] { "DECLARED_TARGET_NOT_VERIFIED" },
                environment = new { renderer = GraphicsDevice.Adapter.Description, width = GraphicsDevice.Viewport.Width, height = GraphicsDevice.Viewport.Height, gpu = diagnosticRenderer is not null, real_time = !options.ControlStep, active = diagnosticHostWasActive },
                audio = new { entry_receipts = world?.Events.Scene.NativeBattleMusicEntry is null ? 0 : 1, dispatch_receipts = telemetry.Executions.Sum(execution => execution.Events.Count(entry => entry.NativeModuleSound is not null || entry.NativeMusicCommand is not null)), enabled = options.AudioEnabled, audibility_verified = false },
                blocker = diagnosticDriver!.Blocker,
                host_status = new { active = IsActive, update_faulted = fieldFaulted, render_faulted = fieldRenderFaulted, error },
                coverage = telemetry.Coverage,
                instrumentation_blind_spots = NativeBattleTelemetry.KnownBlindSpots,
                executions = telemetry.Executions,
                initialization = world?.DiagnosticReceipt,
                diagnostic_capabilities = world?.DiagnosticCapabilities,
                limit_selections = diagnosticLimitSelections.ToArray(),
                enemy_submissions = diagnosticEnemySubmissions.ToArray(),
                gunblade_triggers = diagnosticGunbladeTriggers.ToArray(),
                repairs = diagnosticRepairs.ToArray(),
                terminal_holds = diagnosticDriverBattle?.DiagnosticTerminalHoldReceipts.ToArray(),
                invocation_owners = invocationOwners
            };
            WriteDiagnosticFinalizationStage("report-write-begin");
            WriteDiagnosticJson(telemetryPath, report, false);
            WriteDiagnosticFinalizationStage("report-write-completed");
            string hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(telemetryPath)));
            WriteDiagnosticJson(Path.Combine(output, "candidate.json"), new { status = diagnosticDriver.Blocker is null ? "CANDIDATE_COMPLETED" : "ERROR", reason = diagnosticDriver.Blocker, coverage = "LIVE_DIAGNOSTIC", telemetry_report = "telemetry-report.json", telemetry_sha256 = hash, execution_count = diagnosticDriver.Dispatches.Count, battle_instance_id = world?.DiagnosticReceipt?.BattleInstanceId }, false);
        }
    }

    private static bool WriteDiagnosticJson(string path, object value, bool replace = true)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, DiagnosticBattleJson);
        bool published = DiagnosticJsonPublication.Publish(path, bytes, progress: replace);
        NativeBattleTelemetry.Record(2005, NativeBattleTrafficKind.DiagnosticOutput, bytes.Length);
        return published;
    }
}
