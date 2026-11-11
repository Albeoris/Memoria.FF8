using System.Text.Json.Serialization;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverNativeRenzokukenPlan(ushort Variant, byte? Finisher)
{
    public static Ff7BattleGuestPhysicalHit ProjectContact(CrossoverFf8BattleKit kit, short timing)
    {
        if (timing is not (0 or 1 or >= 20 and <= 32) || kit.RenzokukenPower == 0)
        {
            throw new InvalidDataException("The guest Renzokuken contact requires its original trigger timing and character power.");
        }
        // 485160 command251 takes character byte33. The host profile uses the same 20-to-8 conversion as Gunblade.
        return ProjectPhysicalStrike(kit, kit.RenzokukenPower, timing / 20 + 2, false);
    }

    internal static Ff7BattleGuestPhysicalHit ProjectPhysicalStrike(CrossoverFf8BattleKit kit, byte nativePower, int multiplier, bool critical)
    {
        byte power = (byte)Math.Min(byte.MaxValue, 8 * nativePower * multiplier / 20);
        Ff7BattleDamageAction attack = new(0x11, power, 255, ForceCritical: critical, UsePhysicalStats: true) { SuppressCritical = true, AlwaysHits = true };
        return new(CrossoverGunbladeHostAttack.ProjectPhysicalSource(kit), attack, 0);
    }
}

internal sealed record CrossoverNativeRenzokukenSelection([property: JsonRequired] NativeBattleRandomState Random, long? ReadyTick = null, byte Crisis = 0)
{
    public static CrossoverNativeRenzokukenSelection Begin(long requestId, int formationId)
    {
        // The FFVII encounter has no FFVIII entry seed. Profile 1 derives one from the immutable host occurrence.
        return new(NativeBattleRandomState.Initialize(unchecked((byte)(requestId ^ formationId))));
    }

    public CrossoverNativeRenzokukenSelection ObserveReady(long tick, Ff7BattleAiActor source, int deadParty, bool disabled, CrossoverFf8BattleKit kit)
    {
        if (ReadyTick == tick)
        {
            return this;
        }
        if (tick < 0 || ReadyTick > tick || kit.Limit.Command != 5)
        {
            throw new InvalidDataException("The guest Limit readiness must follow its native command and host clock.");
        }
        ushort saved = Ff7GuestBattleStatuses.ToOrigin(source.Statuses);
        uint battle = kit.Statuses.Battle;
        battle |= (source.Statuses & 4) != 0 ? 1u : 0;
        battle |= (source.Statuses & 0x100) != 0 ? 2u : 0;
        battle |= (source.Statuses & 0x200) != 0 ? 4u : 0;
        byte roll = 0;
        NativeBattleRandomState random = disabled || (battle & 0x200) != 0 ? Random : Random.Draw(out roll);
        byte crisis = NativeBattleLimitRules.CrisisLevel(kit.Limit, checked((int)source.HitPoints), checked((int)source.MaxHitPoints), saved, battle, deadParty, roll, disabled);
        return new(random, tick, crisis);
    }

    public CrossoverNativeRenzokukenSelection Select(byte finisherFlags, out CrossoverNativeRenzokukenPlan plan)
    {
        if (ReadyTick is null || Crisis is < 1 or > 4)
        {
            throw new InvalidOperationException("The guest Renzokuken needs a retained original Crisis selection.");
        }
        NativeBattleRandomState random = Random.Draw(out byte variant).Draw(out byte finisherRoll);
        byte? finisher = null;
        if (NativeBattleRenzokukenRules.HasFinisher(Crisis, finisherRoll))
        {
            random = random.Draw(out byte learnedRoll);
            finisher = NativeBattleRenzokukenRules.Finisher(finisherFlags, learnedRoll);
        }
        plan = new((variant & 1) == 0 ? (ushort)141 : (ushort)160, finisher);
        return new(random);
    }

    public CrossoverNativeRenzokukenSelection ProjectRoughDivide(CrossoverFf8BattleKit kit, BattleNativeAction action, out Ff7BattleGuestPhysicalHit hit)
    {
        if (ReadyTick is not null || action is not { Command: 249, DispatchCommand: 250, ActionId: 0, MagicId: 163, AttackType: 9, Power: 48, HitCount: 1, Hit: null or 255, Critical: null or 0, AttackFlags: 3, TargetFlags: 0x58, Element: 0, StatusAttack: 0, Statuses0: 0, Statuses1: 0 })
        {
            throw new NotSupportedException("Guest Rough Divide requires its selected original Revolver finisher record.");
        }
        NativeBattleRandomState random = Random.Draw(out byte criticalRoll);
        bool critical = NativeBattleDamage.PhysicalCritical(action.Critical.GetValueOrDefault(), kit.Stats.Luck, criticalRoll);
        // The type9 physical formula uses P/16, matching the untriggered type10 multiplier of 2/32.
        hit = CrossoverNativeRenzokukenPlan.ProjectPhysicalStrike(kit, action.Power, 2, critical);
        return this with { Random = random };
    }

    public void Validate(long hostTick)
    {
        if (Random is null || ReadyTick is < 0 || ReadyTick > hostTick || Crisis > 4 || ReadyTick is null && Crisis != 0)
        {
            throw new InvalidDataException("The saved guest Limit selection contradicts the host clock.");
        }
        Random.Validate();
    }
}
