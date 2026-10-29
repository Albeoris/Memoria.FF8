using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattleDiagnosticCombatRepair([property: JsonRequired] string ActorId, [property: JsonRequired] int HitPoints, bool Revive = false, ushort RemoveStatuses0 = 0, uint RemoveStatuses1 = 0)
{
    public const ushort AllSavedStatuses = 0xFFFE;
    public const uint AllBattleStatuses = 0x7FFEFFFF;
    [JsonIgnore] public bool ClearsAllStatuses => (RemoveStatuses0 | 1) == ushort.MaxValue && RemoveStatuses1 == AllBattleStatuses;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ActorId) || ActorId.Length > 128 || HitPoints is < 1 or > ushort.MaxValue || !Revive && (RemoveStatuses0 & 1) != 0 || (RemoveStatuses1 & 0x80010000) != 0)
        {
            throw new InvalidDataException("Diagnostic repair requires an explicit actor and positive HP, an explicit revival for KO removal, and cannot remove allocation/guardian-form status owners.");
        }
    }
}

public sealed record BattleDiagnosticRepairChange(BattleDiagnosticCombatRepair Request, BattleDiagnosticCombatant Before, BattleDiagnosticCombatant After);
public sealed record BattleDiagnosticRepairRandomState(NativeBattleRandomState Battle, NativeCrtRandomState? Crt, NativeBattleUiRandomState? Ui, NativeGlobalRandomState? Global);
public sealed record BattleDiagnosticRepairReceipt(Guid LifecycleId, long Frame, long CallbackBefore, long CallbackAfter, BattleDiagnosticRepairRandomState RandomBefore, BattleDiagnosticRepairRandomState RandomAfter, long RestoreCount, long ResetCount, BattleStock[] ItemsBefore, BattleStock[] ItemsAfter, BattleDiagnosticRepairChange[] Changes, string Scope);

public sealed partial class BattleSession
{
    /// <summary>Declared between-execution repairs, permitted only at a drained native callback boundary.</summary>
    public BattleDiagnosticRepairReceipt ApplyDiagnosticCombatRepairs(IReadOnlyList<BattleDiagnosticCombatRepair> repairs, long expectedCallback)
    {
        if (repairs is null || repairs.Count is < 1 or > 3 || repairs.Any(repair => repair is null) || repairs.Select(repair => repair.ActorId).Distinct(StringComparer.Ordinal).Count() != repairs.Count)
        {
            throw new InvalidDataException("Diagnostic repairs require one through three distinct explicit actors.");
        }
        foreach (BattleDiagnosticCombatRepair repair in repairs)
        {
            repair.Validate();
        }
        if (Outcome != BattleOutcome.Active || HasPendingNativeOutcome)
        {
            throw new NotSupportedException("Diagnostic repairs cannot reverse a terminal or pending native battle outcome.");
        }
        long callback = NativeHostCallbacks ?? throw new NotSupportedException("Diagnostic repair requires its live native callback ledger.");
        if (callback != expectedCallback || callback <= 0 || !NativeEntryReady || ObserveDiagnosticDrainBlockers().Count != 0)
        {
            throw new InvalidOperationException("Diagnostic repair requires the exact current initialized callback and full normal owner drain, outside focal execution windows.");
        }
        // Validate the entire selection before changing an owner. No snapshot, allocator,
        // callback, combat outcome, random stream or command scheduler is restored here.
        foreach (BattleDiagnosticCombatRepair repair in repairs)
        {
            ValidateDiagnosticRepairActor(GetActor(repair.ActorId), repair);
        }
        BattleDiagnosticRepairRandomState beforeRandom = ObserveDiagnosticRepairRandom();
        long frame = Frame;
        long restores = DiagnosticRestoreCount;
        long resets = DiagnosticResetCount;
        BattleStock[] itemsBefore = nativeItems.Where(stock => stock.ItemId != 0).Select(stock => new BattleStock(stock.ItemId, stock.Count)).ToArray();
        List<BattleDiagnosticRepairChange> changes = [];
        foreach (BattleDiagnosticCombatRepair repair in repairs)
        {
            BattleCombatant actor = GetActor(repair.ActorId);
            BattleDiagnosticCombatant before = ObserveDiagnosticRepairActor(actor);
            ApplyDiagnosticRepairActor(actor, repair);
            changes.Add(new(repair, before, ObserveDiagnosticRepairActor(actor)));
        }
        BattleDiagnosticRepairRandomState afterRandom = ObserveDiagnosticRepairRandom();
        if (NativeHostCallbacks != callback || Frame != frame || DiagnosticRestoreCount != restores || DiagnosticResetCount != resets || random.Lane != beforeRandom.Battle.Lane || !random.Indexes.SequenceEqual(beforeRandom.Battle.Indexes) || nativeCrtRandom != beforeRandom.Crt || nativeBattleUiRandom != beforeRandom.Ui || nativeGlobalRandom != beforeRandom.Global)
        {
            throw new InvalidOperationException("Diagnostic repair changed a callback or random-stream owner.");
        }
        BattleStock[] itemsAfter = nativeItems.Where(stock => stock.ItemId != 0).Select(stock => new BattleStock(stock.ItemId, stock.Count)).ToArray();
        return new(DiagnosticLifecycleId, frame, callback, callback, beforeRandom, afterRandom, restores, resets, itemsBefore, itemsAfter, changes.ToArray(), "Declared party HP/status or original enemy status-only repair after full normal drain and outside focal presentation windows; no outcome rollback, stock reset, RNG change or snapshot restore.");
    }

    private void ValidateDiagnosticRepairActor(BattleCombatant actor, BattleDiagnosticCombatRepair repair)
    {
        if (!actor.IsActive || actor.GuardianFormVitals is not null || guardianForceCasts.ContainsKey(actor.Id) || (actor.NativeStatus1 & 0x80010000) != 0 || repair.HitPoints > actor.MaximumHp)
        {
            throw new NotSupportedException("Diagnostic repair requires an active ordinary party HP owner within its actual maximum; allocations, GF forms and casts cannot be repaired.");
        }
        if (actor.IsPlayer)
        {
            if (actor.NativeIndex is < 0 or > 2)
            {
                throw new NotSupportedException("Diagnostic party repair requires its physical party HP owner.");
            }
        }
        else if (actor.NativeIndex is < 3 or > 6 || actor.NativeTemplate is null || actor.NativeAi is null || actor.NativeAllocationPendingStatistics || nativeAllocations.Any(allocation => allocation.Id == actor.Id) || !definition.Combatants.Any(original => !original.IsPlayer && original.Id == actor.Id) || (actor.NativeCombatantFlags & 0xC3) != 1 || !actor.IsAlive || repair.Revive || repair.HitPoints != actor.CurrentHp || (repair.RemoveStatuses0 & 1) != 0)
        {
            throw new NotSupportedException("Diagnostic enemy repair requires a living active original physical owner and status removal with unchanged HP; allocations, revival and HP changes cannot be repaired.");
        }
        if ((!actor.IsAlive || actor.CurrentHp == 0) && !repair.Revive)
        {
            throw new InvalidOperationException("A KO diagnostic recipient requires an explicit revival declaration.");
        }
        if ((!actor.IsAlive || actor.CurrentHp == 0) && repair.Revive && ((definition.NativeFieldBattleRules ?? throw new NotSupportedException("Diagnostic revival requires the actual field battle prohibition owner.")) & 0x40) != 0)
        {
            throw new NotSupportedException("The original battle rules prohibit revival of this diagnostic recipient.");
        }
        for (int bit = 0; bit < 14; bit++)
        {
            if (!repair.ClearsAllStatuses && (repair.RemoveStatuses1 & actor.NativeStatus1 & (1u << bit)) != 0 && actor.NativeStatusTimers[bit] == -1111)
            {
                throw new NotSupportedException("Diagnostic repair cannot remove an original permanent timed status.");
            }
        }
        if (actor.IsPlayer)
        {
            NativeBattlePartyPresentedVital presented = RequireNativePartyPresentedRow(actor.NativeIndex);
            if (presented.HitPoints != actor.CurrentHp || presented.MaximumHitPoints != actor.MaximumHp)
            {
                throw new InvalidOperationException("Diagnostic repair requires the normally drained party presentation HP cache to match its current combat owner.");
            }
        }
    }

    private void ApplyDiagnosticRepairActor(BattleCombatant actor, BattleDiagnosticCombatRepair repair)
    {
        ushort removeSaved = (ushort)(repair.RemoveStatuses0 | (repair.Revive ? 1 : 0));
        if (repair.ClearsAllStatuses)
        {
            // A declared full fixture reset also removes permanent ordinary statuses.
            // Native gameplay cures retain their original permanent-status sentinel rule.
            for (int bit = 0; bit < 14; bit++)
            {
                if ((actor.NativeStatus1 & (1u << bit)) != 0 && actor.NativeStatusTimers[bit] == -1111)
                {
                    actor.NativeStatusTimers[bit] = 0;
                }
            }
        }
        RemoveNativeStatuses(actor, removeSaved, repair.RemoveStatuses1);
        if (!actor.IsPlayer)
        {
            RefreshNativePresentationStatus(actor);
            Log("DiagnosticRepair", actor.Id, actor.Id, 0, "Explicit between-execution enemy status-only repair; original HP, clocks, random streams and scheduler owners retained.");
            return;
        }
        int before = actor.CurrentHp;
        if (repair.HitPoints >= before)
        {
            ApplyHealing(actor, repair.HitPoints - before, "Declared diagnostic repair");
        }
        else
        {
            // Lowering initial HP for a declared crisis/recovery precondition is an
            // explicit fixture intervention, not a fake damage action or reaction.
            actor.CurrentHp = repair.HitPoints;
            ObserveDiagnosticResolution("diagnostic_repair_hp", null, actor, before - repair.HitPoints, before, actor.CurrentHp);
        }
        RefreshNativePresentationStatus(actor);
        NativeBattlePartyPresentedVital presented = RequireNativePartyPresentedRow(actor.NativeIndex);
        int difference = actor.CurrentHp - presented.HitPoints;
        PublishNativePresentedContact(new(checked((byte)actor.NativeIndex), 0, 0, difference >= 0 ? (byte)1 : (byte)0, NativeSavedStatusBits(actor), checked((ushort)Math.Abs(difference)), NativeBattleStatusBits(actor)));
        if (actor.CurrentHp != repair.HitPoints || RequireNativePartyPresentedRow(actor.NativeIndex).HitPoints != repair.HitPoints)
        {
            throw new InvalidOperationException("Diagnostic repair did not produce its exact declared HP in the combat and presentation owners.");
        }
        Log("DiagnosticRepair", actor.Id, actor.Id, actor.CurrentHp - before, "Explicit between-execution HP/status repair; original clocks, random streams and scheduler owners retained.");
    }

    private BattleDiagnosticRepairRandomState ObserveDiagnosticRepairRandom() => new(random, nativeCrtRandom, nativeBattleUiRandom, nativeGlobalRandom);

    private static BattleDiagnosticCombatant ObserveDiagnosticRepairActor(BattleCombatant actor)
    {
        return ObserveDiagnosticCombatant(actor);
    }
}
