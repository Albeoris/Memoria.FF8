using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class CounterRepairProjectionTests
{
    internal static (BattleDiagnosticRepairReceipt Repair, BattleDiagnosticResolution Projection) Fixture(int beforeHp = 486, int requestedHp = 486)
    {
        BattleDiagnosticRepairRandomState random = new(NativeBattleRandomState.Initialize(0), null, null, null);
        BattleDiagnosticCombatRepair request = new("party0", requestedHp, true);
        ushort Status(int hp) => hp < 250 ? (ushort)0x300 : hp < 500 ? (ushort)0x200 : (ushort)0;
        BattleDiagnosticCombatant before = new("party0", beforeHp, 1000, Status(beforeHp), 0, []) { NativeIndex = 0, ModelId = 0 };
        BattleDiagnosticCombatant after = before with { CurrentHp = requestedHp, Statuses0 = Status(requestedHp) };
        BattleDiagnosticRepairReceipt repair = new(new Guid("32120000-0000-0000-0000-000000000001"), 15, 15, 15, random, random, 0, 0, [], [], [new(request, before, after)], "Component completed declared preparation.");
        int amount = Math.Abs(requestedHp - beforeHp);
        BattleDiagnosticResolution projection = new(15, 15, requestedHp >= beforeHp ? "healing" : "diagnostic_repair_hp", null, "party0", "party0", amount, amount, beforeHp, requestedHp, "character") { DiagnosticRepair = new(repair.LifecycleId, 15, 15, request, 0) };
        return (repair, projection);
    }

    [Theory]
    [InlineData(486, 486)]
    [InlineData(100, 486)]
    [InlineData(800, 486)]
    public void Only_the_exact_completed_source_projection_is_recognized(int before, int requested)
    {
        var (repair, projection) = Fixture(before, requested);
        Assert.Equal(repair.Changes[0], DiagnosticCounterCycleValidation.ValidateRepairProjection(projection, [repair]));
        Assert.Null(projection.ActorId);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("actor")]
    [InlineData("callback")]
    [InlineData("frame")]
    [InlineData("lifecycle")]
    [InlineData("recipient")]
    [InlineData("request")]
    [InlineData("before")]
    [InlineData("after")]
    [InlineData("amount")]
    [InlineData("kind")]
    [InlineData("hp_owner")]
    [InlineData("unbound")]
    [InlineData("ambiguous")]
    [InlineData("periodic")]
    public void Null_actor_or_zero_delta_does_not_waive_any_source_join(string change)
    {
        var (repair, resolution) = Fixture();
        BattleDiagnosticRepairReceipt[] repairs = [repair];
        resolution = change switch
        {
            "missing" => resolution with { DiagnosticRepair = null },
            "actor" => resolution with { ActorId = "party0" },
            "callback" => resolution with { DiagnosticRepair = resolution.DiagnosticRepair! with { Callback = 16 } },
            "frame" => resolution with { DiagnosticRepair = resolution.DiagnosticRepair! with { Frame = 16 } },
            "lifecycle" => resolution with { DiagnosticRepair = resolution.DiagnosticRepair! with { LifecycleId = Guid.NewGuid() } },
            "recipient" => resolution with { DiagnosticRepair = resolution.DiagnosticRepair! with { RecipientIndex = 1 } },
            "request" => resolution with { DiagnosticRepair = resolution.DiagnosticRepair! with { Request = repair.Changes[0].Request with { HitPoints = 487 } } },
            "before" => resolution with { BeforeHp = 485 },
            "after" => resolution with { AfterHp = 487 },
            "amount" => resolution with { ResolvedAmount = 1 },
            "kind" => resolution with { Kind = "damage" },
            "hp_owner" => resolution with { HpOwner = "enemy" },
            "periodic" => resolution with { PeriodicStatus = new(0x483470, "party0", 0, 0, 1, 1, 0, 0, 0, 1000, 0, 15) },
            _ => resolution
        };
        if (change == "unbound")
        {
            repairs = [];
        }
        else if (change == "ambiguous")
        {
            repairs = [repair, repair];
        }
        Assert.Throws<InvalidDataException>(() => DiagnosticCounterCycleValidation.ValidateRepairProjection(resolution, repairs));
    }

    private static List<string> Semantics(BattleDiagnosticRepairReceipt repair, BattleDiagnosticResolution[] resolutions, BattleDiagnosticRepairReceipt[]? typedRepairs)
    {
        BattleDiagnosticCombatant before = repair.Changes[0].Before;
        BattleDiagnosticCombatant after = repair.Changes[0].After;
        JsonElement execution = JsonSerializer.SerializeToElement(new { before = new[] { before }, after = new[] { after }, events = new[] { new { kind = "ActionComplete", actor_id = "party0" } }, resolutions }, CounterCycleValidationTests.Json);
        List<string> errors = [];
        try
        {
            typeof(LiveEvidenceValidation).GetMethod("ValidateSemantics", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [execution, "party0", "party0", "Attack", 0L, 230L, 178L, 10L, 20L, errors, typedRepairs]);
        }
        catch (TargetInvocationException failure) when (failure.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Capture(inner).Throw();
            throw;
        }
        return errors;
    }

    [Theory]
    [InlineData(486, 486)]
    [InlineData(100, 486)]
    [InlineData(800, 486)]
    public void Verified_projection_covers_actor_guard_and_counts_the_typed_HP_change_only_once(int before, int requested)
    {
        var (repair, projection) = Fixture(before, requested);
        Assert.Empty(Semantics(repair, [projection], [repair]));
    }

    [Fact]
    public void Unattributed_zero_healing_keeps_the_ordinary_unknown_actor_guard()
    {
        var (repair, projection) = Fixture();
        Assert.Contains("RESOLUTION_ACTOR_UNKNOWN", Semantics(repair, [projection with { DiagnosticRepair = null }], null));
    }

    [Fact]
    public void Every_typed_change_requires_exactly_one_in_execution_projection()
    {
        var (repair, projection) = Fixture();
        Assert.Throws<InvalidDataException>(() => Semantics(repair, [], [repair]));
        Assert.Throws<InvalidDataException>(() => Semantics(repair, [projection, projection], [repair]));
    }

    [Fact]
    public void A_projection_cannot_create_a_counter_repair_role_in_an_ordinary_execution()
    {
        var (repair, projection) = Fixture();
        Assert.Throws<InvalidDataException>(() => Semantics(repair, [projection], null));
    }

    [Fact]
    public void Writer_provenance_cannot_omit_a_required_physical_source_field()
    {
        var (_, resolution) = Fixture();
        JsonObject source = JsonSerializer.SerializeToNode(resolution.DiagnosticRepair, CounterCycleValidationTests.Json)!.AsObject();
        source.Remove("recipient_index");
        Assert.Throws<JsonException>(() => source.Deserialize<BattleDiagnosticRepairProjection>(CounterCycleValidationTests.Json));
    }
}
