using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticPreparationProgressTests(ITestOutputHelper output)
{
    [Fact]
    public void Installed_511_ordinary_prelude_reports_its_actual_state_within_5000_callbacks_without_focal_warmup()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? inputPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_PRELUDE_PROBE_INPUT");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory) && inputPath is not null && File.Exists(inputPath), "Select installed resources and the unchanged declared formation511 recipe for the bounded preparation probe.");
        JsonSerializerOptions json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true, WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
        DiagnosticBattleRequest original = JsonSerializer.Deserialize<DiagnosticBattleRequest>(File.ReadAllText(inputPath!), json)!;
        Assert.Equal(511, original.Encounter);
        Assert.Equal(122, original.Target!.Model);
        Assert.Equal(121, original.Prelude!.DefeatedModel);
        DiagnosticBattleRequest request = original with { Prelude = original.Prelude with { MaximumCallbacks = 5000 } };
        request.Validate();
        GameSession session = GameWorld.CreateColdProcess(directory!);
        session.StartNewGame();
        using GameWorld world = new(session, directory!);
        BattleSession active = world.StartDiagnosticBattle(request);
        Guid lifecycle = active.DiagnosticLifecycleId;
        BattleCombatant predecessor = active.Combatants.Single(actor => actor.Id == request.Prelude.Input.TargetId);
        int initialHp = predecessor.CurrentHp;
        List<DiagnosticPreparationProgress> observations = [];
        List<DiagnosticPreparationProgress> unchangedCallbacks = [];
        DiagnosticPreparationProgressObserver observer = new(active, unchangedCallbacks.Add);
        observer.Report("entry", boundary: true);
        observer.Report("entry", boundary: true);
        Assert.Single(unchangedCallbacks);
        Assert.Equal(0, unchangedCallbacks[0].Callback);
        List<BattleDiagnosticEffectDispatch> dispatches = [];
        active.DiagnosticEffectDispatched += dispatches.Add;
        long started = Stopwatch.GetTimestamp();
        string? boundedFailure = null;
        try
        {
            try
            {
                world.PrepareDiagnosticEntryBeforePresentation(request, observations.Add);
            }
            catch (NotSupportedException exception) when (exception.Message == "Ordinary command prelude did not reach the original initialized target within its callback budget.")
            {
                boundedFailure = exception.Message;
            }
            Assert.Same(active, world.Battle);
            Assert.Equal(lifecycle, active.DiagnosticLifecycleId);
            Assert.Equal(BattleAtbMode.Normal, active.Mode);
            Assert.Equal(0, active.DiagnosticRestoreCount);
            Assert.Equal(0, active.DiagnosticResetCount);
            Assert.DoesNotContain(dispatches, value => value.RequestedEffect == request.Driver!.ExpectedEffect || value.ExecutedEffect == request.Driver!.ExpectedEffect);
            Assert.True(observations.Count >= 2);
            Assert.Equal(0, observations[0].Callback);
            Assert.True(observations.Zip(observations.Skip(1)).All(pair => pair.Second.Callback > pair.First.Callback));
            DiagnosticPreparationProgress last = observations.Last(value => value.Phase == "ordinary_attack_prelude");
            Assert.InRange(last.Callback, 1, 5000);
            Assert.InRange(last.Attacks, 0, request.Prelude.MaximumAttacks);
            Assert.Equal(predecessor.CurrentHp, last.PredecessorHitPoints);
            Assert.Equal(predecessor.NativeCombatantFlags, last.PredecessorFlags);
            if (boundedFailure is not null)
            {
                Assert.Equal(5000, active.NativeHostCallbacks);
            }
            object result = new { scope = "BOUNDED_PREPARATION_PROBE_ONLY", original_request = original, probe_request = request, lifecycle, initial_hp = initialHp, current_hp = predecessor.CurrentHp, milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds, bounded_failure = boundedFailure, observations, dispatches };
            string serialized = JsonSerializer.Serialize(result, json);
            output.WriteLine(serialized);
            if (Environment.GetEnvironmentVariable("MEMORIA_FF8_PRELUDE_PROBE_OUTPUT") is { Length: > 0 } destination)
            {
                using FileStream stream = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                JsonSerializer.Serialize(stream, result, json);
            }
        }
        finally
        {
            active.DiagnosticEffectDispatched -= dispatches.Add;
            world.AbandonFaultedDiagnosticBattle();
            world.EndDiagnosticBattle();
        }
    }
}
