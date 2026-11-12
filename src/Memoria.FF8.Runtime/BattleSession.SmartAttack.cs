using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace Memoria.FF8.Runtime;

public sealed record BattleSmartAttackPreview(string ActorId, string TargetId, BattleAbility Ability, int ActionId, string Name, int CastCount, bool ForcePhysical, string Revision, int TargetIndex, double ExpectedDamage, double KillProbability)
{
    public string? DrawSourceId { get; init; }
    public int? DrawSourceIndex { get; init; }
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
        BattleCombatant actor = GetActor(preview.ActorId);
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
        else if (PhysicalCommand(actor) == 12)
        {
            QueueNativePlayerCommand(preview.ActorId, preview.TargetId, 12, 0);
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

    private bool CanSmartCast(BattleCombatant actor, BattleCombatant target)
    {
        return magicVitals is not null && actor.MagicRecordId is int row && magicVitals.MagicSystem.AttackModes[row] == MagicAttackMode.SmartMagic && actor.IsPlayer && actor.IsAlive && target.IsAlive && target.IsActive && !target.IsPlayer && !target.Untargetable && PhysicalCommand(actor) is 1 or 12 && (actor.Abilities.Contains(BattleAbility.Magic) || actor.Abilities.Contains(BattleAbility.Draw)) && CanNativeBattleActorAct(actor) && !RequiresNativeCompelledCommand(actor) && (NativeSavedStatusBits(actor) & 0x10) == 0 && !HasStatus(actor, BattleStatusKind.Silence);
    }

    private BattleSmartAttackPreview SelectSmartAttack(BattleCombatant actor, BattleCombatant target, bool forcePhysical, string? reservation)
    {
        BattleDamageForecast physical = ForecastPhysical(actor, target);
        string physicalName = PhysicalCommand(actor) == 12 ? actor.NativeCommands.FirstOrDefault(binding => binding.Command == 12)?.Name ?? "Mug" : actor.NativeAttack?.Name ?? "Attack";
        BattleSmartAttackPreview result = new(actor.Id, target.Id, BattleAbility.Attack, 0, physicalName, 1, forcePhysical, SmartRevision(actor, target), target.NativeIndex, physical.ExpectedDamage, physical.KillProbability);
        if (forcePhysical || !CanSmartCast(actor, target) || PhysicalCommand(actor) == 12 && HasMugItem(target) || physical.KillProbability > 0.9)
        {
            return result;
        }
        List<BattleSmartAttackCandidate> candidates = [];
        foreach (BattleActionDefinition action in definition.ActionDefinitions.Where(IsSmartSpell))
        {
            if (NativeActionReflects(target, action.NativeAction!))
            {
                continue;
            }
            if (actor.Abilities.Contains(BattleAbility.Draw) && nativeKnownMagic?.Contains(action.Id) == true)
            {
                foreach (BattleCombatant source in combatants.Where(source => source.IsActive && source.IsAlive && !source.IsPlayer && !source.Untargetable && source.NativeDrawSlots.Any(slot => slot.Id == action.Id)))
                {
                    candidates.Add(new(action, 1, 0, 1, ForecastMagic(actor, target, action.NativeAction!, 1, source, action.DrawResist), source.Id, source.NativeIndex, IsSmartMeltdown(action) && CanSmartApplyMeltdown(actor, target, action.NativeAction!)));
                }
            }
            int available = actor.Abilities.Contains(BattleAbility.Magic) ? GetAvailableMagicCasts(actor.Id, action.Id, reservation) : 0;
            if (available > 0)
            {
                int casts = AffordableSmartCasts(actor, action.Id, available);
                candidates.Add(new(action, available, actor.MagicStocks.FirstOrDefault(stock => stock.Id == action.Id)?.Count ?? 0, casts, ForecastMagic(actor, target, action.NativeAction!, casts), CanApplyMeltdown: IsSmartMeltdown(action) && CanSmartApplyMeltdown(actor, target, action.NativeAction!)));
            }
        }
        return BattleSmartAttackSelector.Select(result, candidates);
    }

    public static bool IsSmartSpell(BattleActionDefinition action)
    {
        return action.Ability == BattleAbility.Magic && action.Id is >= 1 and <= 56 && action.Available && !action.Allies && !action.AllTargets && action.Statuses is null or { Length: 0 } && action.NativeAction is { Command: 2, AttackType: 2 or 22, Power: > 0, HitCount: 1, Statuses0: 0, TargetFlags: byte flags } native && (native.Statuses1 == 0 || IsSmartMeltdown(action)) && (flags & 0x30) == 0x10 && (flags & 2) == 0;
    }

    private static bool IsSmartMeltdown(BattleActionDefinition action)
    {
        return action.Id == 49 && action.NativeAction is { AttackType: 2 or 22, Statuses0: 0, Statuses1: 0x1000000 };
    }

    private bool CanSmartApplyMeltdown(BattleCombatant actor, BattleCombatant target, BattleNativeAction action)
    {
        uint statuses = NativeBattleStatusBits(target);
        if (IsNativeMeltdown(action) && ReduceMeltdownDefense(target.Defense, definition.Tuning.MeltdownVitalityReductionPercent) == target.Defense && ReduceMeltdownDefense(target.Spirit, definition.Tuning.MeltdownSpiritReductionPercent) == target.Spirit || (statuses & 0x1000000) != 0 || (NativeSavedStatusBits(target) & 5) != 0 || (statuses & 0x200800) != 0)
        {
            return false;
        }
        if (action.StatusAttack == 255)
        {
            return true;
        }
        int resistance = GetNativeStatusResistance(target, 32);
        return resistance < 200 && action.StatusAttack + actor.Magic / 4 - target.Spirit / 4 - resistance > 0;
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
        string state = string.Join('|', actor.Id, target.Id, target.NativeIndex, actor.Attack, actor.Magic, actor.Luck, actor.WeaponId, actor.ModelId, actor.IsActive, actor.IsAlive, actor.NativeAttack, actor.NativePlayerStatRules?.NativeStatusPassiveFlags, string.Join(',', actor.NativePlayerStatRules?.MagicJunctions ?? []), string.Join(',', actor.NativeCommandSlots), string.Join(',', actor.Abilities), actor.MagicRecordId is int row ? magicVitals?.MagicSystem.AttackModes[row] : null, target.Defense, target.Spirit, target.Evasion, target.Luck, target.CurrentHp, target.IsActive, target.Untargetable, NativeSavedStatusBits(actor), NativeBattleStatusBits(actor), NativeSavedStatusBits(target), NativeBattleStatusBits(target), target.NativeCombatantFlags, string.Join(',', target.NativeStatusResistances), string.Join(',', target.ElementDefenses), GunbladeMode, magicVitals?.MagicSystem.InfiniteMagic, magicVitals?.MagicSystem.UnlimitedMagic, magicVitals?.MagicSystem.SharedMagicStock, string.Join(',', actor.MagicStocks.Select(stock => $"{stock.Id}:{stock.Count}:{GetAvailableMagicCasts(actor.Id, stock.Id)}")));
        state += "|" + definition.Tuning.MeltdownVitalityReductionPercent + "|" + definition.Tuning.MeltdownSpiritReductionPercent + "|" + target.MeltdownVitalityReductionPercent + "|" + target.MeltdownSpiritReductionPercent;
        state += "|" + string.Join(',', nativeKnownMagic?.Words ?? []) + "|" + string.Join(';', combatants.Where(source => !source.IsPlayer).Select(source => $"{source.Id}:{source.NativeIndex}:{source.Level}:{source.IsActive}:{source.IsAlive}:{source.Untargetable}:{string.Join(',', source.NativeDrawSlots.Select(slot => $"{slot.Id}:{slot.Resistance}"))}")) + "|" + actor.Level;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state)));
    }

    private BattleDamageForecast ForecastMagic(BattleCombatant actor, BattleCombatant target, BattleNativeAction native, int casts, BattleCombatant? drawSource = null, int drawResist = 0)
    {
        uint statuses = NativeBattleStatusBits(target);
        ushort saved = NativeSavedStatusBits(target);
        bool immune = (native.Statuses1 & 0x4000000) == 0 && ((saved & 5) != 0 || (statuses & 0x200800) != 0 || (native.Element & 8) != 0 && (statuses & 0x2000) != 0);
        int spirit = native.AttackType == 22 ? 0 : EffectiveNativeDefense(target, target.Spirit, false);
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
            int total = 0;
            bool vitZero = (statuses & 0x1000000) != 0;
            int currentSpirit = spirit;
            for (int cast = 0; cast < casts && total < target.CurrentHp; cast++)
            {
                int damage = immune ? 0 : NativeBattleDamage.Magic(powers[cast], currentSpirit, native.Power, draw(), !actor.IsPlayer, Difficulty);
                damage = NativeBattleDamage.MagicMitigation(damage, native.AttackFlags, statuses);
                damage = ForecastElement(target, native, damage, false);
                if (drawSource is not null)
                {
                    damage = damage * (draw() + 10) / 150;
                }
                if (!immune && !vitZero && native.ActionId == 49 && native.Statuses1 == 0x1000000)
                {
                    if (native.StatusAttack == 255)
                    {
                        vitZero = true;
                    }
                    else
                    {
                        int resistance = GetNativeStatusResistance(target, 32);
                        int effective = native.StatusAttack + powers[cast] / 4 - target.Spirit / 4 - resistance;
                        if (resistance < 200 && effective > 0)
                        {
                            int threshold = 255 * (255 * effective / 100) / 255;
                            vitZero = native.StatusAttack >= 250 || threshold != 0 && threshold >= draw();
                        }
                    }
                }
                if (vitZero && (statuses & 0x1000000) == 0)
                {
                    currentSpirit = native.AttackType == 22 ? 0 : ReduceMeltdownDefense(target.Spirit, IsNativeMeltdown(native) ? definition.Tuning.MeltdownSpiritReductionPercent : 100);
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
            int vitality = EffectiveNativeDefense(target, target.Defense, true);
            int damage = gunblade ? NativeGunbladeAttack.Damage(actor.Attack, vitality, native?.Power ?? actor.PhysicalPower, draw(), HasStatus(actor, BattleStatusKind.Blind) ? (short)1 : GunbladeMode == BattleGunbladeMode.Automatic ? (short)25 : (short)0) : NativeBattleDamage.Physical(actor.Attack, vitality, native?.Power ?? actor.PhysicalPower, draw());
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
        int defense = (NativeSavedStatusBits(target) & 0x40) != 0 && element == 7 ? 700 : target.ElementDefenses[element];
        return NativeBattleDamage.Element(damage, native.Element, defense, physical, native.ElementPercent.GetValueOrDefault());
    }
}
