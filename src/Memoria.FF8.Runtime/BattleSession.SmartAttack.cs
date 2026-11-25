using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace Memoria.FF8.Runtime;

public sealed record BattleSmartAttackPreview(string ActorId, string TargetId, BattleAbility Ability, int ActionId, string Name, int CastCount, bool ForcePhysical, string Revision, int? TargetIndex, double ExpectedDamage, double KillProbability)
{
    public string? DrawSourceId { get; init; }
    public int? DrawSourceIndex { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public BattleRosterSlot? TargetSlot { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public BattleRosterSlot? DrawSourceSlot { get; init; }
}

public sealed partial class BattleSession
{
    private readonly Dictionary<(string Actor, string Target, bool ForcePhysical), BattleSmartAttackPreview> smartPreviewCache = [];

    public BattleSmartAttackPreview PreviewSmartAttack(string actorId, string targetId, bool forcePhysical = false)
    {
        BattleCombatant actor = GetActor(actorId);
        BattleCombatant target = GetActor(targetId);
        string revision = SmartRevision(actor, target);
        var key = (actorId, targetId, forcePhysical);
        if (smartPreviewCache.TryGetValue(key, out BattleSmartAttackPreview? cached) && cached.Revision == revision)
        {
            return cached;
        }
        BattleSmartAttackPreview preview = SelectSmartAttack(actor, target, forcePhysical, null);
        smartPreviewCache[key] = preview;
        return preview;
    }

    public void QueueSmartAttack(string actorId, string targetId, bool forcePhysical = false)
    {
        QueueSmartAttack(PreviewSmartAttack(actorId, targetId, forcePhysical));
    }

    public void QueueSmartAttack(BattleSmartAttackPreview preview)
    {
        BattleSmartAttackPreview current = PreviewSmartAttack(preview.ActorId, preview.TargetId, preview.ForcePhysical);
        if (current != preview)
        {
            throw new InvalidOperationException("The displayed Smart Attack choice changed; select the target again.");
        }
        if (preview.Ability is BattleAbility.Magic or BattleAbility.Draw)
        {
            pendingSmartAttack = preview;
            try
            {
                if (preview.DrawSourceId is { } source)
                {
                    QueueAction(preview.ActorId, source, BattleAbility.Draw, preview.ActionId, true, drawCastTargetId: preview.TargetId);
                }
                else
                {
                    QueueAction(preview.ActorId, preview.TargetId, BattleAbility.Magic, preview.ActionId, magicTargets: preview.CastCount > 1 ? Enumerable.Repeat(preview.TargetId, preview.CastCount).ToArray() : null);
                }
            }
            finally
            {
                pendingSmartAttack = null;
            }
        }
        else
        {
            QueueAttack(preview.ActorId, preview.TargetId);
        }
    }

    private BattleSmartAttackPreview? pendingSmartAttack;

    private static byte PhysicalCommand(BattleCombatant actor)
    {
        return actor.NativeCommandSlots.IsDefaultOrEmpty ? (byte)1 : actor.NativeCommandSlots[0];
    }

    private bool HasMugItem(BattleCombatant target)
    {
        if ((target.NativeCombatantFlags & 0x800) != 0 || target.NativeMug is not { Rate: > 0 } mug)
        {
            return false;
        }
        return Enumerable.Range(0, 4).Any(slot => mug.Item(target.Level, slot) is { Id: > 0, Count: > 0 });
    }

    public bool CanSmartCast(BattleCombatant actor, BattleCombatant target)
    {
        return magicVitals is not null && actor.MagicRecordId is int row && magicVitals.MagicSystem.AttackModes[row] == MagicAttackMode.SmartMagic && actor.IsPlayer && actor.IsAlive && target.IsAlive && target.IsActive && !target.IsPlayer && !target.Untargetable && PhysicalCommand(actor) is 1 or 12 && (actor.Abilities.Contains(BattleAbility.Magic) || actor.Abilities.Contains(BattleAbility.Draw)) && CanNativeBattleActorAct(actor) && !RequiresNativeCompelledCommand(actor) && (NativeSavedStatusBits(actor) & 0x10) == 0 && !HasStatus(actor, BattleStatusKind.Silence);
    }

    private BattleSmartAttackPreview SelectSmartAttack(BattleCombatant actor, BattleCombatant target, bool forcePhysical, string? reservation)
    {
        BattleDamageForecast physical = ForecastPhysical(actor, target);
        string physicalName = PhysicalCommand(actor) == 12 ? actor.NativeCommands.FirstOrDefault(binding => binding.Command == 12)?.Name ?? "Mug" : actor.NativeAttack?.Name ?? "Attack";
        BattleSmartAttackPreview result = new(actor.Id, target.Id, BattleAbility.Attack, 0, physicalName, 1, forcePhysical, SmartRevision(actor, target), target.NativeActorIndex, physical.ExpectedDamage, physical.KillProbability) { TargetSlot = target.Slot };
        if (forcePhysical || !CanSmartCast(actor, target) || PhysicalCommand(actor) == 12 && HasMugItem(target) || physical.KillProbability > 0.9)
        {
            return result;
        }
        List<BattleSmartAttackCandidate> candidates = [];
        foreach (BattleActionDefinition action in definition.ActionDefinitions.Where(action => IsSmartSpell(action) && (SupportsExpandedAction(action))))
        {
            if (NativeActionReflects(target, action.NativeAction!))
            {
                continue;
            }
            if (actor.Abilities.Contains(BattleAbility.Draw) && !IsPlayerCommandSealed(actor, 6) && nativeKnownMagic?.Contains(action.Id) == true)
            {
                foreach (BattleCombatant source in combatants.Where(source => source.IsActive && source.IsAlive && !source.IsPlayer && !source.Untargetable && source.NativeDrawSlots.Any(slot => slot.Id == action.Id)))
                {
                    candidates.Add(new(action, 1, 0, 1, ForecastMagic(actor, target, action.NativeAction!, 1, source, action.DrawResist), source.Id, null, CanSmartApplyParameterStatus(actor, target, action.NativeAction!)));
                }
            }
            int available = actor.Abilities.Contains(BattleAbility.Magic) && !IsPlayerCommandSealed(actor, 2) ? GetAvailableMagicCasts(actor.Id, action.Id, reservation) : 0;
            if (available > 0)
            {
                int casts = AffordableSmartCasts(actor, action.Id, available);
                candidates.Add(new(action, available, actor.MagicStocks.FirstOrDefault(stock => stock.Id == action.Id)?.Count ?? 0, casts, ForecastMagic(actor, target, action.NativeAction!, casts), CanApplyParameterStatus: CanSmartApplyParameterStatus(actor, target, action.NativeAction!)));
            }
        }
        BattleSmartAttackPreview selected = BattleSmartAttackSelector.Select(result, candidates);
        return selected.DrawSourceId is { } sourceId ? selected with { DrawSourceSlot = GetActor(sourceId).Slot } : selected;
    }

    public static bool IsSmartSpell(BattleActionDefinition action)
    {
        return action.Ability == BattleAbility.Magic && action.Id is >= 1 and <= 56 && action.Available && !action.Allies && !action.AllTargets && action.Statuses is null or { Length: 0 } && action.NativeAction is { Command: 2, AttackType: 2 or 22, Power: > 0, HitCount: 1, Statuses0: 0, TargetFlags: byte flags } native && (native.Statuses1 is 0 or NativeBattleParameterRules.DefenseReductionStatus) && (flags & 0x30) == 0x10 && (flags & 2) == 0;
    }

    private bool CanSmartApplyParameterStatus(BattleCombatant actor, BattleCombatant target, BattleNativeAction action)
    {
        uint mask = action.Statuses1;
        uint statuses = NativeBattleStatusBits(target);
        ushort saved = NativeSavedStatusBits(target);
        if (mask == 0 || (statuses & mask) != 0 || (saved & 5) != 0 || (statuses & 0x200800) != 0 ||
            (action.Element & 8) != 0 && (statuses & 0x2000) != 0 ||
            !StatusStatRules.TryGetStatusId(false, mask, out NativeStatusId status))
        {
            return false;
        }
        bool reducesStat = definition.StatusStatModifiers.Any(modifier => modifier.Status == status &&
            EffectiveStat(target, modifier.Stat, saved, statuses | mask) < EffectiveStat(target, modifier.Stat, saved, statuses));
        return reducesStat && NativeStatusChanceSucceeds(target, (int)status - 8, actor.Magic, EffectiveStat(target, BattleStatId.Spirit), action.StatusAttack, null);
    }

    private int AffordableSmartCasts(BattleCombatant actor, int spell, int available)
    {
        uint statuses = NativeBattleStatusBits(actor);
        uint passives = actor.NativePlayerStatRules?.NativeStatusPassiveFlags ?? 0;
        if ((statuses & 0x40000) != 0 && (available >= 3 || (magicVitals?.MagicSystem.InfiniteMagic != true || magicVitals?.MagicSystem.UnlimitedMagic == true) && (passives & 0x40) != 0))
        {
            return 3;
        }
        if ((statuses & 0x20000) != 0 && (available >= 2 || (magicVitals?.MagicSystem.InfiniteMagic != true || magicVitals?.MagicSystem.UnlimitedMagic == true) && (passives & 0x20) != 0))
        {
            return 2;
        }
        return 1;
    }

    private string SmartRevision(BattleCombatant actor, BattleCombatant target)
    {
        string state = string.Join('|', actor.Id, target.Id, SmartIndexIdentity(target), actor.Attack, actor.Magic, actor.Luck, actor.WeaponId, actor.ModelId, actor.IsActive, actor.IsAlive, actor.NativeAttack, actor.NativePlayerStatRules?.NativeStatusPassiveFlags, string.Join(',', actor.NativePlayerStatRules?.MagicJunctions ?? []), string.Join(',', actor.NativeCommandSlots), string.Join(',', actor.Abilities), actor.MagicRecordId is int row ? magicVitals?.MagicSystem.AttackModes[row] : null, target.Defense, target.Spirit, target.Evasion, target.Luck, target.CurrentHp, target.IsActive, target.Untargetable, NativeSavedStatusBits(actor), NativeBattleStatusBits(actor), NativeSavedStatusBits(target), NativeBattleStatusBits(target), target.NativeCombatantFlags, string.Join(',', target.NativeStatusResistances), string.Join(',', target.ElementDefenses), GunbladeMode, magicVitals?.MagicSystem.InfiniteMagic, magicVitals?.MagicSystem.UnlimitedMagic, magicVitals?.MagicSystem.SharedMagicStock, string.Join(',', actor.MagicStocks.Select(stock => $"{stock.Id}:{stock.Count}:{GetAvailableMagicCasts(actor.Id, stock.Id)}")));
        state += "|" + string.Join(',', definition.StatusStatModifiers);
        state += "|" + string.Join(',', nativeKnownMagic?.Words ?? []) + "|" + string.Join(';', enemies.Select(source => $"{source.Id}:{SmartIndexIdentity(source)}:{source.Level}:{source.IsActive}:{source.IsAlive}:{source.Untargetable}:{string.Join(',', source.NativeDrawSlots.Select(slot => $"{slot.Id}:{slot.Resistance}"))}")) + "|" + actor.Level;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state)));
    }

    private string SmartIndexIdentity(BattleCombatant actor) => actor.Slot?.ToString() ?? "";

    private BattleDamageForecast ForecastMagic(BattleCombatant actor, BattleCombatant target, BattleNativeAction native, int casts, BattleCombatant? drawSource = null, int drawResist = 0)
    {
        uint statuses = NativeBattleStatusBits(target);
        ushort saved = NativeSavedStatusBits(target);
        bool immune = NativeMagicContactBlocked(native, saved, statuses);
        uint statusMask = native.Statuses1;
        int resistanceIndex = StatusStatRules.TryGetStatusId(false, statusMask, out NativeStatusId status) ? (int)status - 8 : -1;
        int[] powers = new int[casts];
        List<NativeMagicStock> stocks = actor.MagicStocks.Select(stock => new NativeMagicStock((byte)stock.Id, (byte)stock.Count)).ToList();
        for (int cast = 0; cast < casts; cast++)
        {
            powers[cast] = cast == 0 || actor.NativePlayerStatRules is null || actor.NativeGrowth is null ? actor.Magic : actor.NativePlayerStatRules.Project(stocks, actor.NativeGrowth).Magic;
            if (!UnlimitedMagic && magicVitals?.MagicSystem.InfiniteMagic != true && MagicCost(actor, casts) > 1)
            {
                int index = stocks.FindIndex(stock => stock.SpellId == native.ActionId);
                if (index >= 0)
                {
                    stocks[index] = stocks[index] with { Count = (byte)Math.Max(0, stocks[index].Count - 1) };
                }
            }
        }
        return BattleActionForecaster.FromNativePhases(draw =>
        {
            if (drawSource is not null)
            {
                NativeBattleDrawSlot entry = drawSource.NativeDrawSlots.First(slot => slot.Id == native.ActionId);
                int quantity = NativeDrawQuantity(actor, drawSource, drawResist, entry.Resistance, draw());
                if (quantity == 0)
                {
                    return 0;
                }
            }
            if (immune)
            {
                return 0;
            }
            int total = 0;
            uint currentStatuses = statuses;
            for (int cast = 0; cast < casts && total < target.CurrentHp; cast++)
            {
                int spirit = EffectiveStat(target, BattleStatId.Spirit, saved, currentStatuses);
                int damage = NativeBattleDamage.Magic(powers[cast], native.AttackType == 22 ? 0 : spirit, native.Power, draw(), !actor.IsPlayer, Difficulty);
                damage = NativeBattleDamage.MagicMitigation(damage, native.AttackFlags, currentStatuses);
                damage = ForecastElement(target, native, damage, false);
                if (resistanceIndex >= 0 && (currentStatuses & statusMask) == 0 &&
                    NativeStatusChanceSucceeds(target, resistanceIndex, powers[cast], spirit, native.StatusAttack, draw))
                {
                    currentStatuses |= statusMask;
                }
                if (drawSource is not null)
                {
                    damage = damage * (draw() + 10) / 150;
                }
                total += Math.Clamp(damage, (native.AttackFlags & 8) != 0 ? -60_000 : -9_999, NativePrimaryDamageLimit(native.AttackFlags));
            }
            return total;
        }, Math.Max(1, target.CurrentHp));
    }

    private BattleDamageForecast ForecastPhysical(BattleCombatant actor, BattleCombatant target)
    {
        BattleNativeAction? native = actor.NativeAttack;
        uint statuses = NativeBattleStatusBits(target);
        ushort saved = NativeSavedStatusBits(target);
        bool gunblade = IsGunbladeAttack(actor, BattleAbility.Attack);
        return BattleActionForecaster.FromNativePhases(draw =>
        {
            if (((native?.Statuses1 ?? 0) & 0x4000000) == 0 && ((saved & 4) != 0 || (statuses & 0x180800) != 0))
            {
                return 0;
            }
            int hit = native?.Hit ?? actor.Hit;
            if (!gunblade && !NativeBattleDamage.PhysicalHits(hit, actor.Luck, target.Evasion, target.Luck, NativeSavedStatusBits(actor), statuses, (statuses & 9) != 0 || hit == 255 ? 0 : draw()))
            {
                return 0;
            }
            bool critical = !gunblade && NativeBattleDamage.PhysicalCritical(native?.Critical ?? 0, actor.Luck, draw());
            int strength = EffectiveStat(actor, BattleStatId.Strength);
            int vitality = native?.AttackType == 36 ? 0 : EffectiveStat(target, BattleStatId.Vitality);
            int damage = gunblade ? NativeGunbladeAttack.Damage(strength, vitality, native?.Power ?? actor.PhysicalPower, draw(), HasStatus(actor, BattleStatusKind.Blind) ? (short)1 : GunbladeMode == BattleGunbladeMode.Automatic ? (short)25 : (short)0) : NativeBattleDamage.Physical(strength, vitality, native?.Power ?? actor.PhysicalPower, draw());
            if ((statuses & 0x20) != 0)
            {
                damage >>= 1;
            }
            if ((statuses & 0x800000) != 0)
            {
                damage *= 2;
            }
            if (critical)
            {
                damage *= 2;
            }
            if ((saved & 0x40) != 0)
            {
                damage >>= 1;
            }
            if (native is not null)
            {
                damage = ForecastElement(target, native, damage, true);
            }
            else if (actor.ElementAttack != 0)
            {
                int element = System.Numerics.BitOperations.TrailingZeroCount((uint)actor.ElementAttack);
                damage = NativeBattleDamage.Element(damage, (byte)actor.ElementAttack, target.ElementDefenses[element], true, actor.ElementAttackPercent);
            }
            int maximum = ((native?.AttackFlags ?? 0) & 8) != 0 ? 60_000 : 9_999;
            return Math.Clamp(damage, -maximum, NativePrimaryDamageLimit(native?.AttackFlags ?? 0));
        }, Math.Max(1, target.CurrentHp));
    }

    private static int ForecastElement(BattleCombatant target, BattleNativeAction native, int damage, bool physical)
    {
        int element = native.Element == 0 ? 0 : System.Numerics.BitOperations.TrailingZeroCount((uint)native.Element);
        int defense = EffectiveNativeElementDefense(target, element);
        return NativeBattleDamage.Element(damage, native.Element, defense, physical, native.ElementPercent.GetValueOrDefault());
    }
}
