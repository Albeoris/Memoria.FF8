using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DiagnosticGuardianRepeatTests
{
    [Fact]
    public void External_canonical_Quezacotl_recipe_repeats_twice_without_restoring_native_owners()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? recipePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_DIAGNOSTIC_GF_RECIPE");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory) && recipePath is not null && File.Exists(recipePath), "Select installed resources and an external canonical GF0 recipe.");
        JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
        DiagnosticBattleRequest request = JsonSerializer.Deserialize<DiagnosticBattleRequest>(File.ReadAllText(recipePath!), options)!;
        request.Validate();
        Assert.Equal(982, request.Encounter);
        DiagnosticBattleDriverRecipe driver = Assert.IsType<DiagnosticBattleDriverRecipe>(request.Driver);
        Assert.Equal(BattleAbility.GuardianForce, driver.Input.Ability);
        Assert.Equal(0, driver.Input.ActionId);
        Assert.Equal((ushort)116, driver.ExpectedEffect);
        GameSession session = GameWorld.CreateColdProcess(directory!);
        session.StartNewGame();
        using GameWorld world = new(session, directory!);
        BattleSession battle = world.StartDiagnosticBattle(request);
        BattleAudioPresentation audio = new(_ => { }, playModule: _ => { }, stopSounds: () => { });
        Guid lifecycle = battle.DiagnosticLifecycleId;
        List<BattleDiagnosticEffectDispatch> dispatches = [];
        List<JsonElement> boundaries = [];
        battle.DiagnosticEffectDispatched += dispatch =>
        {
            if (dispatch.ActorId == driver.Input.ActorId && dispatch.RequestedEffect == 116)
            {
                dispatches.Add(dispatch);
            }
        };
        int cast = 0;
        string stage = "entry preparation";
        try
        {
            if (request.PrepareEntryBeforePresentation)
            {
                world.PrepareDiagnosticEntryBeforePresentation(request);
            }
            for (cast = 1; cast <= 2; cast++)
            {
                stage = "readiness";
                for (int callback = 0; callback < 4096 && !Ready(); callback++)
                {
                    Tick();
                }
                if (!Ready())
                {
                    Fail("Readiness did not complete within4096 native callbacks.");
                }
                Record("before-cast-" + cast);
                world.ApplyBattleInput(driver.Input);
                stage = "graphical completion";
                bool released = false;
                for (int callback = 0; callback < 4096; callback++)
                {
                    Tick();
                    if (dispatches.Count == cast && battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.GuardianForceCasts.Count == 0 && battle.ObserveDiagnosticDrainBlockers().Count == 0)
                    {
                        released = true;
                        break;
                    }
                }
                if (!released)
                {
                    Fail("GF invocation did not release its normal owners within4096 native callbacks.");
                }
                Record("after-cast-" + cast);
                Assert.Equal(lifecycle, battle.DiagnosticLifecycleId);
                Assert.Equal(0, battle.DiagnosticRestoreCount);
                Assert.Equal(0, battle.DiagnosticResetCount);
                Assert.Equal(cast, dispatches.Count);
                Assert.Equal((ushort)116, dispatches[^1].ExecutedEffect);
            }
            Assert.True(dispatches[1].Serial > dispatches[0].Serial);
            Assert.True(dispatches[1].Callback > dispatches[0].Callback);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or IOException)
        {
            Fail(exception.GetType().Name + ": " + exception.Message);
        }

        bool Ready()
        {
            return battle.Outcome == BattleOutcome.Active && battle.NativeEntryReady && battle.CanChoosePlayerCommand(driver.Input.ActorId!) && battle.ObserveDiagnosticDrainBlockers().Count == 0;
        }

        void Tick()
        {
            battle.Tick();
            audio.Update(battle);
        }

        void Record(string boundary)
        {
            boundaries.Add(JsonSerializer.SerializeToElement(new { Boundary = boundary, Owners = battle.ObserveDiagnosticInvocationOwners() }));
        }

        void Fail(string reason)
        {
            Record("failure");
            string diagnostic = JsonSerializer.Serialize(new { Cast = cast, Stage = stage, Reason = reason, Dispatches = dispatches, Boundaries = boundaries });
            const int maximumDiagnosticLength = 65536;
            Assert.Fail(diagnostic.Length <= maximumDiagnosticLength ? diagnostic : diagnostic[..maximumDiagnosticLength] + " [diagnostic truncated]");
        }
    }
}
