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
        return magicVitals is not null && magicVitals.MagicSystem.SmartAttack && actor.MagicRecordId is int row && magicVitals.MagicSystem.AttackModes[row] == MagicAttackMode.SmartMagic && actor.IsPlayer && actor.IsAlive && target.IsAlive && target.IsActive && !target.IsPlayer && !target.Untargetable && PhysicalCommand(actor) is 1 or 12 && (actor.Abilities.Contains(BattleAbility.Magic) || actor.Abilities.Contains(BattleAbility.Draw)) && CanNativeBattleActorAct(actor) && !RequiresNativeCompelledCommand(actor) && (NativeSavedStatusBits(actor) & 0x10) == 0 && !HasStatus(actor, BattleStatusKind.Silence);
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
        List<(BattleActionDefinition Action, int Available, int Stock, int Casts, BattleDamageForecast Forecast, BattleCombatant? Source)> candidates = [];
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
                    candidates.Add((action, 1, 0, 1, ForecastMagic(actor, target, action.NativeAction!, 1, source, action.DrawResist), source));
                }
            }
            int available = actor.Abilities.Contains(BattleAbility.Magic) ? GetAvailableMagicCasts(actor.Id, action.Id, reservation) : 0;
            if (available > 0)
            {
                int casts = AffordableSmartCasts(actor, action.Id, available);
                candidates.Add((action, available, actor.MagicStocks.FirstOrDefault(stock => stock.Id == action.Id)?.Count ?? 0, casts, ForecastMagic(actor, target, action.NativeAction!, casts), null));
            }
        }
        var drawnKill = candidates.Where(candidate => candidate.Source is not null && candidate.Forecast.KillProbability > 0.9).OrderBy(candidate => candidate.Forecast.ExpectedDamage).ThenBy(candidate => candidate.Action.Id).ThenBy(candidate => candidate.Source!.NativeIndex).ToArray();
        if (drawnKill.Length > 0)
        {
            var chosen = drawnKill[0];
            return result with { Ability = BattleAbility.Draw, ActionId = chosen.Action.Id, Name = "Draw: " + (chosen.Action.DisplayName ?? chosen.Action.Name), CastCount = 1, ExpectedDamage = chosen.Forecast.ExpectedDamage, KillProbability = chosen.Forecast.KillProbability, DrawSourceId = chosen.Source!.Id, DrawSourceIndex = chosen.Source.NativeIndex };
        }
        foreach (var group in candidates.Where(candidate => candidate.Source is null).GroupBy(candidate => candidate.Action.NativeAction!.Element).OrderByDescending(group => group.Sum(candidate => candidate.Available)).ThenByDescending(group => group.Sum(candidate => candidate.Stock)).ThenBy(group => group.Key))
        {
            var sufficient = group.Where(candidate => candidate.Forecast.KillProbability > 0.9).OrderBy(candidate => candidate.Forecast.ExpectedDamage).ThenBy(candidate => candidate.Action.Id).ToArray();
            if (sufficient.Length > 0)
            {
                var chosen = sufficient[0];
                return result with { Ability = BattleAbility.Magic, ActionId = chosen.Action.Id, Name = chosen.Action.DisplayName ?? chosen.Action.Name, CastCount = chosen.Casts, ExpectedDamage = chosen.Forecast.ExpectedDamage, KillProbability = chosen.Forecast.KillProbability };
            }
        }
        var strongest = candidates.OrderByDescending(candidate => candidate.Forecast.ExpectedDamage).ThenByDescending(candidate => candidate.Source is not null).ThenByDescending(candidate => candidate.Available).ThenBy(candidate => candidate.Action.Id).ThenBy(candidate => candidate.Source?.NativeIndex).ToArray();
        if (strongest.Length > 0 && strongest[0].Forecast.ExpectedDamage > 0 && strongest[0].Forecast.ExpectedDamage > physical.ExpectedDamage)
        {
            var chosen = strongest[0];
            return result with { Ability = chosen.Source is null ? BattleAbility.Magic : BattleAbility.Draw, ActionId = chosen.Action.Id, Name = (chosen.Source is null ? "" : "Draw: ") + (chosen.Action.DisplayName ?? chosen.Action.Name), CastCount = chosen.Casts, ExpectedDamage = chosen.Forecast.ExpectedDamage, KillProbability = chosen.Forecast.KillProbability, DrawSourceId = chosen.Source?.Id, DrawSourceIndex = chosen.Source?.NativeIndex };
        }
        return result;
    }

    public static bool IsSmartSpell(BattleActionDefinition action)
    {
        return action.Ability == BattleAbility.Magic && action.Id is >= 1 and <= 56 && action.Available && !action.Allies && !action.AllTargets && action.Statuses is null or { Length: 0 } && action.NativeAction is { Command: 2, AttackType: 2 or 22, Power: > 0, HitCount: 1, Statuses0: 0, Statuses1: 0, TargetFlags: byte flags } && (flags & 0x30) == 0x10 && (flags & 2) == 0;
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
        string state = string.Join('|', actor.Id, target.Id, target.NativeIndex, actor.Attack, actor.Magic, actor.Luck, actor.WeaponId, actor.ModelId, actor.IsActive, actor.IsAlive, actor.NativeAttack, actor.NativePlayerStatRules?.NativeStatusPassiveFlags, string.Join(',', actor.NativePlayerStatRules?.MagicJunctions ?? []), string.Join(',', actor.NativeCommandSlots), string.Join(',', actor.Abilities), actor.MagicRecordId is int row ? magicVitals?.MagicSystem.AttackModes[row] : null, target.Defense, target.Spirit, target.Evasion, target.Luck, target.CurrentHp, target.IsActive, target.Untargetable, NativeSavedStatusBits(actor), NativeBattleStatusBits(actor), NativeSavedStatusBits(target), NativeBattleStatusBits(target), target.NativeCombatantFlags, string.Join(',', target.ElementDefenses), GunbladeMode, magicVitals?.MagicSystem.SmartAttack, magicVitals?.MagicSystem.InfiniteMagic, magicVitals?.MagicSystem.UnlimitedMagic, magicVitals?.MagicSystem.SharedMagicStock, string.Join(',', actor.MagicStocks.Select(stock => $"{stock.Id}:{stock.Count}:{GetAvailableMagicCasts(actor.Id, stock.Id)}")));
        state += "|" + string.Join(',', nativeKnownMagic?.Words ?? []) + "|" + string.Join(';', combatants.Where(source => !source.IsPlayer).Select(source => $"{source.Id}:{source.NativeIndex}:{source.Level}:{source.IsActive}:{source.IsAlive}:{source.Untargetable}:{string.Join(',', source.NativeDrawSlots.Select(slot => $"{slot.Id}:{slot.Resistance}"))}")) + "|" + actor.Level;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state)));
    }

    private BattleDamageForecast ForecastMagic(BattleCombatant actor, BattleCombatant target, BattleNativeAction native, int casts, BattleCombatant? drawSource = null, int drawResist = 0)
    {
        uint statuses = NativeBattleStatusBits(target);
        ushort saved = NativeSavedStatusBits(target);
        bool immune = (native.AttackFlags & 0x80) == 0 && ((saved & 5) != 0 || (statuses & 0x200800) != 0 || (native.Element & 8) != 0 && (statuses & 0x2000) != 0);
        int spirit = (statuses & 0x1000000) != 0 || native.AttackType == 22 ? 0 : target.Spirit;
        int[] powers = new int[casts];
        List<NativeMagicStock> stocks = actor.MagicStocks.Select(stock => new NativeMagicStock((byte)stock.Id, (byte)stock.Count)).ToList();
        for (int cast = 0; cast < casts; cast++)
        {
            powers[cast] = cast == 0 || actor.NativePlayerStatRules is null || actor.NativeGrowth is null ? actor.Magic : actor.NativePlayerStatRules.Project(stocks, actor.NativeGrowth).Magic;
            if ((magicVitals?.MagicSystem.InfiniteMagic != true || magicVitals?.MagicSystem.UnlimitedMagic == true) && MagicCost(actor, casts) > 1)
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
            for (int cast = 0; cast < casts && total < target.CurrentHp; cast++)
            {
                int damage = immune ? 0 : NativeBattleDamage.Magic(powers[cast], spirit, native.Power, draw(), !actor.IsPlayer, Difficulty);
                damage = NativeBattleDamage.MagicMitigation(damage, native.AttackFlags, statuses);
                damage = ForecastElement(target, native, damage, false);
                if (drawSource is not null)
                {
                    damage = damage * (draw() + 10) / 150;
                }
                total += Math.Clamp(damage, (native.AttackFlags & 8) != 0 ? -60_000 : -9_999, definition.Tuning.MaximumDamage);
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
            if (((native?.AttackFlags ?? 0) & 0x80) == 0 && ((saved & 4) != 0 || (statuses & 0x180800) != 0))
            {
                return 0;
            }
            int hit = native?.Hit ?? actor.Hit;
            if (!gunblade && !NativeBattleDamage.PhysicalHits(hit, actor.Luck, target.Evasion, target.Luck, NativeSavedStatusBits(actor), statuses, (statuses & 9) != 0 || hit == 255 ? 0 : draw()))
            {
                return 0;
            }
            bool critical = !gunblade && NativeBattleDamage.PhysicalCritical(native?.Critical ?? 0, actor.Luck, draw());
            int vitality = (statuses & 0x1000000) != 0 ? 0 : target.Defense;
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
            return Math.Clamp(damage, -maximum, definition.Tuning.MaximumDamage);
        }, Math.Max(1, target.CurrentHp));
    }

    private static int ForecastElement(BattleCombatant target, BattleNativeAction native, int damage, bool physical)
    {
        int element = native.Element == 0 ? 0 : System.Numerics.BitOperations.TrailingZeroCount((uint)native.Element);
        int defense = (NativeSavedStatusBits(target) & 0x40) != 0 && element == 7 ? 700 : target.ElementDefenses[element];
        return NativeBattleDamage.Element(damage, native.Element, defense, physical, native.ElementPercent.GetValueOrDefault());
    }
}
