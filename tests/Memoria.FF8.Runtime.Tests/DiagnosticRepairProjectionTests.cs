using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticRepairProjectionTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object? Invoke(BattleSession battle, string name, params object[] args)
    {
        try
        {
            return typeof(BattleSession).GetMethod(name, Instance)!.Invoke(battle, args);
        }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Capture(inner).Throw();
            throw;
        }
    }

    private static BattleSession Fixture()
    {
        BattleSession battle = new(new() { NativeFieldBattleRules = 0, Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, MaximumHp = 1000, CurrentHp = 400 }, new() { Id = "survivor", Name = "Survivor", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
        typeof(BattleSession).GetField("nativePartyPresentation", Instance)!.SetValue(battle, new NativeBattlePartyPresentation([new(400, 1000, null, null) { DisplayedHitPointsQ12 = 400 << 12 }, null, null]));
        return battle;
    }

    [Theory]
    [InlineData(400, "healing")]
    [InlineData(900, "healing")]
    [InlineData(100, "diagnostic_repair_hp")]
    public void Actual_writer_attaches_scoped_provenance_without_changing_null_actor_or_native_state(int requested, string kind)
    {
        // Component owner fixture; public drain/admission remains guarded independently.
        BattleSession control = Fixture();
        BattleSession observed = Fixture();
        List<BattleDiagnosticResolution> resolutions = [];
        observed.DiagnosticResolution += resolutions.Add;
        BattleDiagnosticCombatRepair request = new("party", requested);
        Invoke(control, "ApplyDiagnosticRepairActor", control.Combatants[0], request);
        Invoke(observed, "ApplyDiagnosticRepairActor", observed.Combatants[0], request);
        BattleDiagnosticResolution resolution = Assert.Single(resolutions);
        BattleDiagnosticRepairProjection source = Assert.IsType<BattleDiagnosticRepairProjection>(resolution.DiagnosticRepair);
        Assert.Null(resolution.ActorId);
        Assert.Equal(kind, resolution.Kind);
        Assert.Equal(observed.DiagnosticLifecycleId, source.LifecycleId);
        Assert.Equal(resolution.Frame, source.Frame);
        Assert.Equal(resolution.Callback, source.Callback);
        Assert.Equal(request, source.Request);
        Assert.Equal((byte)0, source.RecipientIndex);
        Assert.Equal(400, resolution.BeforeHp);
        Assert.Equal(requested, resolution.AfterHp);
        Assert.Equal(Math.Abs(requested - 400), resolution.ActualAmount);
        Assert.Equal(JsonSerializer.Serialize(control.Capture()), JsonSerializer.Serialize(observed.Capture()));
        Assert.Null(typeof(BattleSession).GetField("diagnosticRepairProjection", Instance)!.GetValue(observed));
        Invoke(observed, "ApplyHealing", observed.Combatants[0], 0, "Component ordinary healing");
        Assert.Null(resolutions[^1].DiagnosticRepair);
    }

    [Fact]
    public void Writer_scope_is_restored_even_when_an_existing_observer_throws()
    {
        BattleSession battle = Fixture();
        InvalidOperationException expected = new("Component observer failure");
        battle.DiagnosticResolution += _ => throw expected;
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => Invoke(battle, "ApplyDiagnosticRepairActor", battle.Combatants[0], new BattleDiagnosticCombatRepair("party", 500))));
        Assert.Null(typeof(BattleSession).GetField("diagnosticRepairProjection", Instance)!.GetValue(battle));
    }
}
