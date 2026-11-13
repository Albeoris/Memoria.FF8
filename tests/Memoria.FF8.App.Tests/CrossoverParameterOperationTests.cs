using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverParameterOperationTests
{
    private static readonly Ff7BattleDamageActor Source = new(10, 100, 100, 0, 0, 0, 0, 0, 0);
    private static readonly Ff7BattleDamageActor Target = new(10, 10, 10, 0, 0, 0, 0, 201, 151);
    private static readonly Ff7BattleActionVitals Vitals = new(9000, 9000, 0, 0, 0);
    private static readonly Ff7BattleParameterOperation Native = new(NativeBattleParameterRules.DefenseReductionStatus, 32, 100, 100);

    [Theory]
    [InlineData(100, 100, 0, 0)]
    [InlineData(50, 25, 100, 113)]
    [InlineData(0, 100, 201, 0)]
    public void Host_retains_exact_nonstacking_percentages_without_overwriting_base_stats(int defenseReduction, int magicReduction, int defense, int magicDefense)
    {
        Ff7BattleParameterOperation operation = Native with { DefenseReductionPercent = defenseReduction, MagicDefenseReductionPercent = magicReduction };
        Ff7BattleActionReceipt receipt = Resolve(Action(operation), Target);
        Assert.False(receipt.Damage.Missed);
        Assert.Equal(0u, receipt.Target.Statuses);
        Assert.Equal(Vitals.Hp, receipt.Target.Hp);
        Assert.Equal(operation, receipt.AddedParameterStatus);
        Ff7BattleDamageActor projected = operation.Project(Target);
        Assert.Equal((ushort)defense, projected.Defense);
        Assert.Equal((ushort)magicDefense, projected.MagicDefense);
        Assert.Equal((ushort)201, Target.Defense);
        Assert.Equal((ushort)151, Target.MagicDefense);
        Assert.Null(Resolve(Action(Native), projected).AddedParameterStatus);
        Assert.Equal(operation, JsonSerializer.Deserialize<Ff7BattleActionReceipt>(JsonSerializer.Serialize(receipt)).AddedParameterStatus);
    }

    [Fact]
    public void Resistance_miss_and_whole_action_immunity_prevent_parameter_publication()
    {
        byte[] resistances = Enumerable.Repeat((byte)100, 40).ToArray();
        resistances[32] = 200;
        Assert.Null(Resolve(Action(Native) with { GuestOriginStatusAttack = 250 }, Target, resistances).AddedParameterStatus);
        Assert.Equal(Native, Resolve(Action(Native), Target, resistances).AddedParameterStatus);
        Assert.Null(Resolve(Action(Native) with { RequiredTargetLevelMultiple = 3 }, Target).AddedParameterStatus);
        Ff7BattleActionVitals immune = Vitals with { Statuses = Ff7NativeBattleStatuses.Peerless };
        Assert.Null(Ff7NativeBattleActionResolver.ResolveGuestContact(Source, Vitals, Target, immune, Action(Native), null, Random()).AddedParameterStatus);
    }

    [Fact]
    public void Cure_removes_only_its_status_owner_and_prepared_receipt_keeps_the_transaction()
    {
        Ff7BattleDamageActor projected = Native.Project(Target);
        Ff7BattleActionReceipt cure = Resolve(Action(Native) with { Healing = true }, projected);
        Assert.Equal(Native.SourceStatusMask, cure.RemovedParameterStatus);
        Assert.Null(cure.AddedParameterStatus);
        Assert.Equal(0u, Resolve(Action(Native with { SourceStatusMask = 0x2000000 }) with { Healing = true }, projected).RemovedParameterStatus);
        Ff7BattleActionReceipt prepared = Resolve(Action(Native), Target);
        MethodInfo apply = typeof(Ff7NativeBattleActionResolver).GetMethod("ApplyPreparedGuestContact", BindingFlags.Static | BindingFlags.NonPublic)!;
        Ff7BattleActionReceipt published = (Ff7BattleActionReceipt)apply.Invoke(null, [Vitals, Vitals, JsonSerializer.Deserialize<Ff7BattleActionReceipt>(JsonSerializer.Serialize(prepared)), Action(Native), false])!;
        Assert.Equal(Native, published.AddedParameterStatus);
        Assert.Equal(prepared.Target, published.Target);
    }

    [Fact]
    public void Native_source_descriptor_preserves_tuning_and_other_sources_retain_native_reduction()
    {
        BattleNativeAction native = new(2, 49, 1, 49)
        {
            MagicId = 49, AttackType = 2, Power = 32, HitCount = 1, TargetFlags = 0x10, AttackFlags = 1, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 200, Statuses0 = 0, Statuses1 = Native.SourceStatusMask, Hit = 100, Critical = 0, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
        GameplayTuning tuning = new() { MeltdownVitalityReductionPercent = 50, MeltdownSpiritReductionPercent = 25 };
        Assert.Equal(new NativeBattleParameterOperation(Native.SourceStatusMask, 32, 50, 25), NativeBattleParameterRules.Describe(native, tuning));
        Assert.Equal(new NativeBattleParameterOperation(Native.SourceStatusMask, 32, 100, 100), NativeBattleParameterRules.Describe(native with { ActionId = 1 }, tuning));
        Assert.Null(CrossoverBattleAbilityRules.UnsupportedReason(native with { AttackType = 2, HitCount = 1 }));
    }

    [Fact]
    public void Legacy_origin_status_defaults_without_replaying_and_a_cured_status_stays_cured_after_json()
    {
        using CrossoverGuestStatusHostTests.Fixture fixture = new();
        Ff7BattleSnapshot before = fixture.Host.Capture();
        Ff7BattleSnapshot legacy = before with
        {
            GuestOriginStatuses = [new(0, Native.SourceStatusMask)],
            Actors = before.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with { ParameterStatus = null, ParameterStatusInitialized = false } : actor).ToArray()
        };
        fixture.RestoreQueuedBoundary(legacy);
        Ff7BattleSnapshot restored = fixture.Host.Capture();
        Assert.Equal(Native, restored.Actors.Single(actor => actor.Actor.Slot == 0).ParameterStatus);
        Assert.Equal(before.Tick, restored.Tick);
        Assert.Equal(JsonSerializer.Serialize(before.Random), JsonSerializer.Serialize(restored.Random));
        Assert.Equal(Native.SourceStatusMask, fixture.Host.GetGuestOriginBattleStatuses(0) & Native.SourceStatusMask);
        Ff7BattleSnapshot cured = restored with { Actors = restored.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with { ParameterStatus = null } : actor).ToArray() };
        fixture.RestoreQueuedBoundary(cured);
        Assert.Equal(0u, fixture.Host.GetGuestOriginBattleStatuses(0) & Native.SourceStatusMask);
        Assert.Null(fixture.Host.Capture().Actors.Single(actor => actor.Actor.Slot == 0).ParameterStatus);
    }

    private static Ff7BattleDamageAction Action(Ff7BattleParameterOperation operation)
    {
        return new(0x22, 0, 255, HasStatusOperation: true, AddsStatuses: true) { AlwaysHits = true, ParameterOperation = operation, GuestOriginStatusAttack = 255 };
    }

    private static Ff7BattleActionReceipt Resolve(Ff7BattleDamageAction action, Ff7BattleDamageActor target, byte[]? resistances = null)
    {
        return Ff7NativeBattleActionResolver.ResolveGuestContact(Source, Vitals, target, Vitals, action, null, Random(), originStatusResistances: resistances);
    }

    private static Ff7BattleRng Random() => new(new byte[3988], new(new byte[8]));
}
