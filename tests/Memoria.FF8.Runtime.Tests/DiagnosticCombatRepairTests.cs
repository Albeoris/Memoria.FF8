using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticCombatRepairTests
{
    [Fact]
    public void Repair_receipt_states_preserve_the_same_physical_identity_as_execution_observations()
    {
        BattleSession session = Create(400);
        BattleCombatant actor = session.Combatants[0];
        BattleDiagnosticCombatant before = (BattleDiagnosticCombatant)Method("ObserveDiagnosticRepairActor").Invoke(null, [actor])!;
        Assert.Equal(0, before.NativeIndex);
        Assert.Equal(JsonSerializer.Serialize(session.ObserveDiagnosticCombatants()[0]), JsonSerializer.Serialize(before));
        ApplyOwnerStep(session, new("party", 250));
        BattleDiagnosticCombatant after = (BattleDiagnosticCombatant)Method("ObserveDiagnosticRepairActor").Invoke(null, [actor])!;
        Assert.Equal(JsonSerializer.Serialize(session.ObserveDiagnosticCombatants()[0]), JsonSerializer.Serialize(after));
        BattleDiagnosticCombatant restored = JsonSerializer.Deserialize<BattleDiagnosticCombatant>(JsonSerializer.Serialize(after))!;
        Assert.Equal(0, restored.NativeIndex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Repair_requires_explicit_positive_hit_points(int hp)
    {
        Assert.Throws<InvalidDataException>(() => new BattleDiagnosticCombatRepair("party", hp).Validate());
    }

    [Fact]
    public void Repair_cannot_clear_ko_without_revival_or_remove_allocation_and_guardian_owners()
    {
        Assert.Throws<InvalidDataException>(() => new BattleDiagnosticCombatRepair("party", 100, RemoveStatuses0: 1).Validate());
        Assert.Throws<InvalidDataException>(() => new BattleDiagnosticCombatRepair("party", 100, RemoveStatuses1: 0x10000).Validate());
        Assert.Throws<InvalidDataException>(() => new BattleDiagnosticCombatRepair("party", 100, RemoveStatuses1: 0x80000000).Validate());
    }

    [Fact]
    public void Public_boundary_rejects_terminal_and_uninitialized_callbacks_without_mutation()
    {
        BattleSession session = Create(400);
        BattleDiagnosticCombatRepair[] repairs = [new("party", 200)];
        Assert.Throws<InvalidOperationException>(() => session.ApplyDiagnosticCombatRepairs(repairs, 0));
        Property(nameof(BattleSession.Outcome)).SetValue(session, BattleOutcome.Defeat);
        Assert.Throws<NotSupportedException>(() => session.ApplyDiagnosticCombatRepairs(repairs, 0));
        Assert.Equal(400, session.Combatants[0].CurrentHp);
        Assert.Equal(BattleOutcome.Defeat, session.Outcome);
    }

    [Fact]
    public void Declared_healing_and_lowering_update_combat_and_presented_hp_without_rng_or_callback_changes()
    {
        BattleSession session = Create(400);
        object randomBefore = Field("random").GetValue(session)!;
        long callback = session.NativeHostCallbacks!.Value;
        ApplyOwnerStep(session, new("party", 900));
        Assert.Equal(900, session.Combatants[0].CurrentHp);
        Assert.Equal(900, Presentation(session).Rows[0]!.HitPoints);
        ApplyOwnerStep(session, new("party", 100));
        Assert.Equal(100, session.Combatants[0].CurrentHp);
        Assert.Equal(100, Presentation(session).Rows[0]!.HitPoints);
        Assert.Equal(randomBefore, Field("random").GetValue(session));
        Assert.Equal(callback, session.NativeHostCallbacks);
        Assert.Equal(0, session.DiagnosticRestoreCount);
        Assert.Equal(0, session.DiagnosticResetCount);
        Assert.Equal(2, session.Events.Count(item => item.Kind == "DiagnosticRepair"));
        Assert.DoesNotContain(session.Events, item => item.Kind == "Damage");
    }

    [Fact]
    public void Nonterminal_party_revival_clears_ko_through_native_status_owner_and_heals_exactly()
    {
        BattleSession session = Create(0, 1);
        Assert.Equal(BattleOutcome.Active, session.Outcome);
        Assert.False(session.Combatants[0].IsAlive);
        ApplyOwnerStep(session, new("party", 250, Revive: true));
        Assert.True(session.Combatants[0].IsAlive);
        Assert.Equal(250, session.Combatants[0].CurrentHp);
        Assert.Equal(250, Presentation(session).Rows[0]!.HitPoints);
        Assert.Equal(BattleOutcome.Active, session.Outcome);
    }

    [Fact]
    public void Status_repair_preserves_unselected_statuses_and_rejects_incoherent_cache_before_mutation()
    {
        BattleSession session = Create(400, 18);
        ApplyOwnerStep(session, new("party", 400, RemoveStatuses0: 2));
        BattleDiagnosticCombatant actor = session.ObserveDiagnosticCombatants()[0];
        Assert.Equal(0, actor.Statuses0 & 2);
        Assert.NotEqual(0, actor.Statuses0 & 16);
        Field("nativePartyPresentation").SetValue(session, new NativeBattlePartyPresentation([new(399, 1000, null, null), null, null]));
        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() => Method("ValidateDiagnosticRepairActor").Invoke(session, [session.Combatants[0], new BattleDiagnosticCombatRepair("party", 800)]));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal(400, session.Combatants[0].CurrentHp);
    }

    [Fact]
    public void Explicit_full_cleanup_clears_permanent_and_compelled_statuses_without_replacing_native_owners()
    {
        BattleSession session = Create(400, 0x22);
        BattleCombatant actor = session.Combatants[0];
        typeof(BattleCombatant).GetProperty("NativeStatus1", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(actor, 0x4002u);
        short[] timers = (short[])typeof(BattleCombatant).GetProperty("NativeStatusTimers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(actor)!;
        timers[1] = -1111;
        string randomBefore = JsonSerializer.Serialize(Method("ObserveDiagnosticRepairRandom").Invoke(session, null));
        string itemsBefore = JsonSerializer.Serialize(Field("nativeItems").GetValue(session));
        long? callback = session.NativeHostCallbacks;
        long frame = session.Frame;
        uint flags = actor.NativeCombatantFlags;
        int? index = session.ObserveDiagnosticCombatants()[0].NativeIndex;
        BattleDiagnosticCombatRepair repair = new("party", 400, RemoveStatuses0: BattleDiagnosticCombatRepair.AllSavedStatuses, RemoveStatuses1: BattleDiagnosticCombatRepair.AllBattleStatuses);
        repair.Validate();
        Assert.True(repair.ClearsAllStatuses);

        ApplyOwnerStep(session, repair);

        Assert.Same(actor, session.Combatants[0]);
        Assert.Same(timers, typeof(BattleCombatant).GetProperty("NativeStatusTimers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(actor));
        BattleDiagnosticCombatant observed = session.ObserveDiagnosticCombatants()[0];
        Assert.Equal((ushort)0x200, observed.Statuses0); // Native low-HP classification is derived from400/1000HP.
        Assert.Equal(0u, observed.Statuses1);
        Assert.All(timers, timer => Assert.Equal((short)-1111, timer));
        Assert.Equal(400, actor.CurrentHp);
        Assert.Equal(1000, actor.MaximumHp);
        Assert.Equal(400, Presentation(session).Rows[0]!.HitPoints);
        Assert.Equal(flags, actor.NativeCombatantFlags);
        Assert.Equal(index, observed.NativeIndex);
        Assert.Equal(callback, session.NativeHostCallbacks);
        Assert.Equal(frame, session.Frame);
        Assert.Equal(randomBefore, JsonSerializer.Serialize(Method("ObserveDiagnosticRepairRandom").Invoke(session, null)));
        Assert.Equal(itemsBefore, JsonSerializer.Serialize(Field("nativeItems").GetValue(session)));
        Assert.Equal(0, session.DiagnosticRestoreCount);
        Assert.Equal(0, session.DiagnosticResetCount);
        Assert.Equal(BattleOutcome.Active, session.Outcome);
    }

    [Fact]
    public void Partial_cleanup_still_rejects_permanent_party_status_without_any_mutation()
    {
        BattleSession session = Create(400, 0x22);
        BattleCombatant actor = session.Combatants[0];
        typeof(BattleCombatant).GetProperty("NativeStatus1", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(actor, 0x4002u);
        short[] timers = (short[])typeof(BattleCombatant).GetProperty("NativeStatusTimers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(actor)!;
        timers[1] = -1111;
        string before = JsonSerializer.Serialize(session.ObserveDiagnosticCombatants());
        short[] timersBefore = timers.ToArray();
        BattleDiagnosticCombatRepair repair = new("party", 900, RemoveStatuses0: 0x22, RemoveStatuses1: 0x4002);
        Assert.False(repair.ClearsAllStatuses);

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() => ApplyOwnerStep(session, repair));

        Assert.IsType<NotSupportedException>(exception.InnerException);
        Assert.Equal(before, JsonSerializer.Serialize(session.ObserveDiagnosticCombatants()));
        Assert.Equal(timersBefore, timers);
        Assert.Equal(400, Presentation(session).Rows[0]!.HitPoints);
    }

    [Fact]
    public void Full_cleanup_serializes_only_the_declared_repair_contract_and_keeps_ko_explicit()
    {
        BattleDiagnosticCombatRepair repair = new("party", 400, RemoveStatuses0: BattleDiagnosticCombatRepair.AllSavedStatuses, RemoveStatuses1: BattleDiagnosticCombatRepair.AllBattleStatuses);
        JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        string json = JsonSerializer.Serialize(repair, options);
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.True(repair.ClearsAllStatuses);
        Assert.False(repair.Revive);
        Assert.Equal((ushort)0xFFFE, repair.RemoveStatuses0);
        Assert.Equal(0x7FFEFFFFu, repair.RemoveStatuses1);
        Assert.Equal(new[] { "actor_id", "hit_points", "remove_statuses0", "remove_statuses1", "revive" }, document.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.True(JsonSerializer.Deserialize<BattleDiagnosticCombatRepair>(json, options)!.ClearsAllStatuses);
        Assert.False((repair with { RemoveStatuses0 = 0xFFFC }).ClearsAllStatuses);
        Assert.False((repair with { RemoveStatuses1 = 0x7FFEFFFD }).ClearsAllStatuses);
        Assert.Throws<InvalidDataException>(() => (repair with { RemoveStatuses0 = ushort.MaxValue }).Validate());
    }

    [Theory]
    [InlineData(0x10000u)]
    [InlineData(0x80000000u)]
    public void Full_cleanup_still_rejects_allocation_and_guardian_status_owners(uint status)
    {
        BattleSession session = Create(400);
        typeof(BattleCombatant).GetProperty("NativeStatus1", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(session.Combatants[0], status);
        string before = JsonSerializer.Serialize(session.ObserveDiagnosticCombatants());
        BattleDiagnosticCombatRepair repair = new("party", 400, RemoveStatuses0: BattleDiagnosticCombatRepair.AllSavedStatuses, RemoveStatuses1: BattleDiagnosticCombatRepair.AllBattleStatuses);

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() => ApplyOwnerStep(session, repair));

        Assert.IsType<NotSupportedException>(exception.InnerException);
        Assert.Equal(before, JsonSerializer.Serialize(session.ObserveDiagnosticCombatants()));
    }

    private static void ApplyOwnerStep(BattleSession session, BattleDiagnosticCombatRepair repair)
    {
        // Exercise the HP/status owners independently of native drain admission.
        // The public boundary test above proves this synthetic fixture cannot enter that API.
        BattleCombatant actor = session.Combatants.Single(candidate => candidate.Id == repair.ActorId);
        Method("ValidateDiagnosticRepairActor").Invoke(session, [actor, repair]);
        Method("ApplyDiagnosticRepairActor").Invoke(session, [actor, repair]);
    }

    [Fact]
    public void Original_enemy_status_only_repair_preserves_hp_party_presentation_and_callback_owners()
    {
        BattleSession session = CreateEnemy();
        BattleCombatant enemy = session.Combatants[1];
        object random = Field("random").GetValue(session)!;
        long? callback = session.NativeHostCallbacks;
        NativeBattlePartyPresentation presentation = Presentation(session);
        ApplyOwnerStep(session, new("enemy", enemy.CurrentHp, RemoveStatuses1: 128));
        BattleDiagnosticCombatant observed = session.ObserveDiagnosticCombatants()[1];
        Assert.Equal(400, observed.CurrentHp);
        Assert.Equal(8192u, observed.Statuses1);
        Assert.Same(presentation, Presentation(session));
        Assert.Equal(random, Field("random").GetValue(session));
        Assert.Equal(callback, session.NativeHostCallbacks);
        Assert.Equal(0, session.DiagnosticRestoreCount);
        Assert.Equal(0, session.DiagnosticResetCount);
        Assert.DoesNotContain(session.Events, item => item.Kind is "Damage" or "Heal");
    }

    [Theory]
    [InlineData(399, false)]
    [InlineData(401, false)]
    [InlineData(400, true)]
    public void Enemy_status_repair_rejects_hp_changes_and_revival_without_mutation(int hp, bool revive)
    {
        BattleSession session = CreateEnemy();
        AssertEnemyRepairRejected(session, new("enemy", hp, Revive: revive, RemoveStatuses1: 128));
    }

    [Theory]
    [InlineData("NativeIndex", 7)]
    [InlineData("NativeCombatantFlags", 0u)]
    [InlineData("NativeAllocationPendingStatistics", true)]
    public void Enemy_status_repair_rejects_nonoriginal_physical_owner_states(string property, object value)
    {
        BattleSession session = CreateEnemy();
        typeof(BattleCombatant).GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(session.Combatants[1], value);
        AssertEnemyRepairRejected(session, new("enemy", 400, RemoveStatuses1: 128));
    }

    [Fact]
    public void Enemy_status_repair_rejects_permanent_status_and_allocated_identity()
    {
        BattleSession permanent = CreateEnemy();
        short[] timers = (short[])typeof(BattleCombatant).GetProperty("NativeStatusTimers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(permanent.Combatants[1])!;
        timers[7] = -1111;
        AssertEnemyRepairRejected(permanent, new("enemy", 400, RemoveStatuses1: 128));
        BattleSession allocated = CreateEnemy();
        ((List<NativeBattleEnemyAllocation>)Field("nativeAllocations").GetValue(allocated)!).Add(new("enemy", 0, 1));
        AssertEnemyRepairRejected(allocated, new("enemy", 400, RemoveStatuses1: 128));
    }

    private static void AssertEnemyRepairRejected(BattleSession session, BattleDiagnosticCombatRepair repair)
    {
        string before = JsonSerializer.Serialize(session.ObserveDiagnosticCombatants());
        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() => ApplyOwnerStep(session, repair));
        Assert.IsType<NotSupportedException>(exception.InnerException);
        Assert.Equal(before, JsonSerializer.Serialize(session.ObserveDiagnosticCombatants()));
    }

    private static BattleSession CreateEnemy()
    {
        NativeBattleEnemyLevel level = new(400, 10, 10, 10, 10, 10, new int[8], [], [], 0);
        BattleSession session = new(new()
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, MaximumHp = 1000, CurrentHp = 400 },
                new() { Id = "enemy", Name = "Enemy", SceneSlot = 0, MaximumHp = 400, CurrentHp = 400, NativeCombatantFlags = 1, NativeTemplate = new(1, Enumerable.Repeat(level, 101).ToArray()), NativeAi = new([[0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []) }
            ]
        }, BattleAtbMode.TurnBased);
        BattleCombatant enemy = session.Combatants[1];
        typeof(BattleCombatant).GetProperty("NativeStatus1", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(enemy, 8320u);
        short[] timers = (short[])typeof(BattleCombatant).GetProperty("NativeStatusTimers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(enemy)!;
        timers[7] = 100;
        return session;
    }

    private static BattleSession Create(int hp, ushort savedStatuses = 0)
    {
        BattleSession session = new(new()
        {
            NativeFieldBattleRules = 0,
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, CurrentHp = hp, MaximumHp = 1000, NativeStatus0 = savedStatuses },
                new() { Id = "survivor", Name = "Survivor", IsPlayer = true },
                new() { Id = "enemy", Name = "Enemy" }
            ]
        }, BattleAtbMode.TurnBased);
        Field("nativePartyPresentation").SetValue(session, new NativeBattlePartyPresentation([new((ushort)hp, 1000, null, null) { DisplayedHitPointsQ12 = hp << 12 }, null, null]));
        return session;
    }

    private static NativeBattlePartyPresentation Presentation(BattleSession session) => (NativeBattlePartyPresentation)Field("nativePartyPresentation").GetValue(session)!;
    private static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static MethodInfo Method(string name) => typeof(BattleSession).GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)!;
    private static PropertyInfo Property(string name) => typeof(BattleSession).GetProperty(name)!;
}
