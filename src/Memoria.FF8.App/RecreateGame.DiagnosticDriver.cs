using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private DiagnosticBattleDriver? diagnosticDriver;
    private DiagnosticBattleDriverRecipe? diagnosticDriverRecipe;
    private BattleSession? diagnosticDriverBattle;
    private long diagnosticPresentedFrames;
    private int diagnosticProgressSequence;
    private string? diagnosticLastStage;
    private long diagnosticProgressTimestamp;
    private long diagnosticProgressCallback = -1;
    private readonly List<string> diagnosticCompletedStages = [];
    private static readonly string[] DiagnosticCampaignStages = ["initialization", "action_one", "cleanup_rearm", "action_two", "teardown"];

    private void StartDiagnosticDriver(DiagnosticBattleDriverRecipe recipe)
    {
        diagnosticDriverRecipe = recipe;
        diagnosticDriverBattle = world?.Battle ?? throw new InvalidOperationException("Diagnostic driver requires its published live battle.");
        diagnosticDriver = new(recipe, Stopwatch.GetElapsedTime(presentationClockStarted).TotalSeconds);
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
        try
        {
            if (!ReferenceEquals(diagnosticDriverBattle, world?.Battle))
            {
                driver.Fail("BATTLE_SESSION_REPLACED");
            }
            else if (!IsActive || fieldFaulted || fieldRenderFaulted)
            {
                driver.Fail("GRAPHICAL_HOST_INACTIVE_OR_FAULTED");
            }
            else
            {
                BattleSession active = diagnosticDriverBattle!;
                string[] blockers = active.NativeEntryReady ? active.ObserveDiagnosticDrainBlockers().ToArray() : ["native-entry"];
                bool ready = active.NativeEntryReady && active.CanChoosePlayerCommand(diagnosticDriverRecipe!.Input.ActorId!);
                DiagnosticDriverObservation observation = new(now.TotalSeconds, active.NativeHostCallbacks ?? throw new NotSupportedException("Diagnostic driver requires the live callback ledger."), diagnosticPresentedFrames, ready, blockers.Length == 0, active.Outcome != BattleOutcome.Active, blockers);
                if (driver.Advance(observation))
                {
                    ApplyBattleCommand(diagnosticDriverRecipe!.Input);
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
            diagnosticDriverBattle!.DiagnosticEffectDispatched -= driver.ObserveDispatch;
            if (world?.IsDiagnosticBattle == true)
            {
                world.EndDiagnosticBattle();
                ClearBattlePresentation();
            }
            PublishDiagnosticCandidate();
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
        if (receipt is not null)
        {
            WriteDiagnosticJson(receipt, new { Status = driver.Stage == DiagnosticDriverStage.Completed ? "CANDIDATE_COMPLETED" : driver.Finished ? "BLOCKED" : "RUNNING", Coverage = "PREPARATION_ONLY", Stage = driver.Stage.ToString(), driver.Execution, driver.Blocker, driver.LastDrainBlockers, Dispatches = driver.Dispatches, Initialization = world?.DiagnosticReceipt, AcceptanceBlocker = "TELEMETRY_UNAVAILABLE" });
        }
    }

    private void PublishDiagnosticCandidate()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_OUTPUT_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(output))
        {
            WriteDiagnosticJson(Path.Combine(output, "candidate.json"), new { status = "ERROR", reason = diagnosticDriver!.Blocker ?? "TELEMETRY_UNAVAILABLE", coverage = "PREPARATION_ONLY", diagnostic_receipt = "diagnostic-driver.json", execution_count = diagnosticDriver.Dispatches.Count, battle_instance_id = world?.DiagnosticReceipt?.BattleInstanceId }, false);
        }
    }

    private static void WriteDiagnosticJson(string path, object value, bool replace = true)
    {
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        string temporary = full + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, DiagnosticBattleJson));
        File.Move(temporary, full, replace);
    }
}
