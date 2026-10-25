using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticCombatRepairTests
{
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

    private static void ApplyOwnerStep(BattleSession session, BattleDiagnosticCombatRepair repair)
    {
        // Exercise the HP/status owners independently of native drain admission.
        // The public boundary test above proves this synthetic fixture cannot enter that API.
        Method("ValidateDiagnosticRepairActor").Invoke(session, [session.Combatants[0], repair]);
        Method("ApplyDiagnosticRepairActor").Invoke(session, [session.Combatants[0], repair]);
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
    private static MethodInfo Method(string name) => typeof(BattleSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static PropertyInfo Property(string name) => typeof(BattleSession).GetProperty(name)!;
}
