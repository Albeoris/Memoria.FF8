using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DiagnosticReactionDrainProbeTests
{
    [Fact]
    public void External_effect234_recipe_retains_the_actual_pending_reaction_owner_at_normal_drain()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? recipePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_REACTION_PROBE_RECIPE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_REACTION_PROBE_OUTPUT");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory) && recipePath is not null && File.Exists(recipePath) && output is not null, "Select installed resources, the external effect234 recipe and a fresh facts output file.");
        Assert.True(Path.IsPathFullyQualified(output!));
        Assert.False(File.Exists(output!));
        JsonSerializerOptions json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true, WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
        byte[] recipeBytes = File.ReadAllBytes(recipePath!);
        DiagnosticBattleRequest request = JsonSerializer.Deserialize<DiagnosticBattleRequest>(recipeBytes, json)!;
        request.Validate();
        DiagnosticBattleDriverRecipe driver = Assert.IsType<DiagnosticBattleDriverRecipe>(request.Driver);
        Assert.Equal((ushort)234, driver.ExpectedEffect);
        BattleDiagnosticEnemyAbility ability = Assert.IsType<BattleDiagnosticEnemyAbility>(driver.EnemyAbility);
        Assert.Equal(28, ability.Model);
        Assert.Equal(1, ability.LevelBand);
        Assert.Equal(1, ability.AbilitySlot);
        Assert.Equal((ushort)110, ability.ActionId);
        GameSession session = GameWorld.CreateColdProcess(directory!);
        session.StartNewGame();
        using GameWorld world = new(session, directory!);
        BattleSession battle = world.StartDiagnosticBattle(request);
        BattleAudioPresentation audio = new(_ => { }, playModule: _ => { }, stopSounds: () => { });
        List<BattleDiagnosticEffectDispatch> dispatches = [];
        List<BattleDiagnosticResolution> resolutions = [];
        List<object> changes = [];
        List<BattleEvent> completions = [];
        battle.DiagnosticEffectDispatched += dispatches.Add;
        battle.DiagnosticResolution += resolutions.Add;
        battle.DiagnosticEventPublished += entry =>
        {
            if (entry.Kind == "ActionComplete" && entry.ActorId == driver.Input.ActorId)
            {
                completions.Add(entry);
            }
        };
        bool completed = false;
        Exception? fault = null;
        string stage = "entry";
        string? previous = null;
        try
        {
            if (request.PrepareEntryBeforePresentation)
            {
                world.PrepareDiagnosticEntryBeforePresentation(request);
            }
            stage = "readiness";
            for (int tick = 0; tick < 4096 && !Ready(); tick++)
            {
                Tick();
            }
            if (!Ready())
            {
                throw new InvalidOperationException("The unchanged original enemy recipe did not reach its normal ready/drained boundary within4096 callbacks.");
            }
            for (int frame = 0; frame < Math.Ceiling(driver.BaselineSeconds * 60); frame++)
            {
                Tick();
            }
            Assert.True(Ready());
            dispatches.Clear();
            completions.Clear();
            resolutions.Clear();
            changes.Clear();
            previous = null;
            stage = "focal";
            battle.SubmitDiagnosticEnemyAbility(driver.Input.ActorId!, ability, driver.ExpectedEffect);
            for (int tick = 0; tick < 4096; tick++)
            {
                Tick();
                if (dispatches.Any(value => value.ActorId == driver.Input.ActorId && value.RequestedEffect == 234 && value.ExecutedEffect == 234) && completions.Count > 0 && battle.ObserveDiagnosticDrainBlockers().Count == 0)
                {
                    completed = true;
                    break;
                }
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or InvalidDataException or NotSupportedException or IOException or ArgumentException)
        {
            fault = exception;
        }
        object final = Observe();
        using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            JsonSerializer.Serialize(stream, new { stage, completed, recipeSha256 = Convert.ToHexString(SHA256.HashData(recipeBytes)), dispatches, completions, resolutions, changes, final, fault = fault is null ? null : new { type = fault.GetType().FullName, fault.Message } }, json);
        }
        Assert.True(fault is null, $"The scoped probe faulted; retained facts are in the explicitly selected output: {fault?.Message}");
        Assert.True(completed, "The exact pending native drain owner was retained in the selected facts output; no repair or timeout exemption was applied.");

        bool Ready()
        {
            return battle.Outcome == BattleOutcome.Active && battle.NativeEntryReady && RecreateGame.DiagnosticCommandReady(battle, driver) && battle.ObserveDiagnosticDrainBlockers().Count == 0 && world.VerifyDiagnosticTarget() is not null;
        }

        void Tick()
        {
            battle.Tick();
            audio.Update(battle);
            object state = Observe();
            string key = JsonSerializer.Serialize(state, json);
            if (key != previous && changes.Count < 128)
            {
                changes.Add(new { battle.Frame, battle.NativeHostCallbacks, state });
                previous = key;
            }
        }

        object Observe()
        {
            return new
            {
                battle.Outcome,
                blockers = battle.NativeEntryReady ? battle.ObserveDiagnosticDrainBlockers().ToArray() : ["native-entry"],
                reactionOrder = Read("nativeReactionOrder"),
                pendingReactions = typeof(BattleSession).GetProperty("HasNativePendingReactions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle),
                allocationPending = typeof(BattleSession).GetProperty("NativeScriptAllocationPending", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle),
                commitDepth = Read("nativeActionCommitDepth"),
                flushing = Read("flushingNativeReactions"),
                timedAction = Scalars(Read("nativeTimedAction")),
                slotsPending = Read("pendingSlotsDeal") is not null,
                shotProcesses = ((IDictionary)Read("nativeShotProcesses")!).Count,
                duelProcesses = ((IDictionary)Read("nativeDuelProcesses")!).Count,
                renzokukenProcesses = ((IDictionary)Read("nativeRenzokukenProcesses")!).Count,
                action = battle.ActiveAction is { } action ? new { action.ActorId, action.TargetId, action.NativeHeaderIndex, action.NativeAiCallbackStage } : null,
                queued = battle.QueuedActions.Select(command => new { command.ActorId, command.NativeCallback, command.NativeHeaderIndex }).ToArray(),
                actors = battle.Combatants.Where(actor => Actor("NativeIndex", actor) is int index && index is >= 0 and < 7).Select(actor => new
                {
                    actor.Id, actor.IsPlayer, actor.CurrentHp, actor.MaximumHp, actor.IsActive, actor.IsAlive,
                    index = Actor("NativeIndex", actor),
                    pending = Actor("NativePendingReaction", actor),
                    deathHandled = Actor("AiDeathHandled", actor),
                    saved = Actor("NativeStatus0", actor),
                    statuses = Actor("NativeStatus1", actor),
                    flags = Actor("NativeCombatantFlags", actor)
                }).ToArray()
            };
        }

        object? Read(string name) => typeof(BattleSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle);
        object? Actor(string name, BattleCombatant actor) => typeof(BattleCombatant).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(actor);

        object? Scalars(object? value)
        {
            if (value is null)
            {
                return null;
            }
            Dictionary<string, object?> fields = new() { ["type"] = value.GetType().Name };
            foreach (PropertyInfo property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(property => property.GetIndexParameters().Length == 0).Take(32))
            {
                object? member = property.GetValue(value);
                if (member is null || member is string || member.GetType().IsPrimitive || member.GetType().IsEnum)
                {
                    fields[property.Name] = member;
                }
            }
            return fields;
        }
    }
}
