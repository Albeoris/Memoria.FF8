using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public enum Ff7BattleRecoveryKind
{
    None,
    HitPoints,
    Revive
}

public readonly record struct Ff7BattleRestorativeBenefit(uint GuaranteedHpDamage);

public sealed partial class Ff7BattleSession
{
    /// <summary>Reports only a proved restorative-damage lower bound from a currently available single-target native command. Unknown outcomes have no guaranteed damage.</summary>
    public Ff7BattleRestorativeBenefit DescribeRestorativeDamage(int slot, Ff7BattleCommandOption command, int targetSlot)
    {
        if (targetSlot is < 4 or > 9 || command.Command != Ff7BattleCommand.Item || command.UnsupportedReason is not null || !command.SelectSingle || command.AllTargets || (command.ValidTargets & 1 << targetSlot) == 0 || !IsAvailableRecoveryCommand(slot, command))
        {
            return default;
        }
        IFf7BattleGuestAttack? guest = GetGuestAttack(slot);
        if (command.Command == Ff7BattleCommand.Item && guest is IFf7BattleGuestItems { HasOriginItems: true })
        {
            return default;
        }
        Ff7BattleAttack? record = ReadBattleItem(command.Argument);
        if (record is null)
        {
            return default;
        }
        Ff7BattleActorState target = Actor(targetSlot);
        Ff7BattleDamageAction action = Ff7BattleDamageAction.FromAttack(record) with { IsItem = command.Command == Ff7BattleCommand.Item, Affinity = ResolveNativeEnemyAffinity(targetSlot, record.Elements) };
        Ff7BattleDamageActor stats = Stats(target) with { CurrentHitPoints = target.Actor.HitPoints, MaximumHitPoints = target.Actor.MaxHitPoints };
        return RestorativeDamageBound(action, stats, record);
    }

    public static Ff7BattleRestorativeBenefit RestorativeDamageBound(Ff7BattleDamageAction action, Ff7BattleDamageActor target, Ff7BattleAttack record)
    {
        uint maximum = target.MaximumHitPoints.GetValueOrDefault();
        if (!action.IsItem || !action.Healing || action.TargetsMagicPoints || action.Formula != 0x24 || action.Accuracy < byte.MaxValue || action.CanBeBlinded || action.IgnoreNativeAffinity || action.Power == 0 || action.DamageScalePercent != 100 || action.OriginDamageMultiplier != 1 || action.Split || action.FractionDenominator != 0 || action.FractionNumerator != 0 || action.SourceMissingHitPoints || action.BaseDamage is not null || action.FixedDamage is not null || action.GuestElementPercent is not null || action.GuestElementAdjustment != 0 || action.NativeReflectPending || record.AdditionalEffect != byte.MaxValue || Ff7NativeBattleStatuses.ReadRecordMask(record) != 0 || target.CurrentHitPoints is not > 0 || maximum is 0 or > int.MaxValue || (target.Statuses & (Ff7NativeBattleActionResolver.Death | Ff7NativeBattleStatuses.Barrier | Ff7NativeBattleStatuses.MagicBarrier | 0x01004000)) != 0 || Ff7OriginSavedStatusRules.IsZombie(target.OriginSavedStatuses) || Ff7NativeBattleDamage.IsConditionImmune(target, action))
        {
            return default;
        }
        Ff7BattleDamageAffinity affinity = Ff7NativeBattleStatuses.ShieldAffinity(target.Statuses, action.NativeElements, action.IsItem, action.Affinity);
        if ((affinity & Ff7BattleDamageAffinity.Absorb) == 0 || (affinity & Ff7BattleDamageAffinity.Nullify) != 0)
        {
            return default;
        }
        uint damage = (uint)Math.Min(9999ul, (ulong)maximum * action.Power / 32);
        return new(damage);
    }

    /// <summary>Describes available recovery without preparing a contact, spending stock or sampling battle RNG.</summary>
    public Ff7BattleRecoveryKind DescribeRecovery(int slot, Ff7BattleCommandOption command, int targetSlot)
    {
        if (command.UnsupportedReason is not null || (command.ValidTargets & 1 << targetSlot) == 0 || !IsAvailableRecoveryCommand(slot, command))
        {
            return Ff7BattleRecoveryKind.None;
        }
        Ff7BattleRecoveryKind kind;
        Ff7BattleDamageAction action;
        Ff7BattleAttack? record = null;
        IFf7BattleGuestAttack? guest = GetGuestAttack(slot);
        if (command.Command == Ff7BattleCommand.Item && guest is IFf7BattleGuestItems { HasOriginItems: true } items)
        {
            Ff7BattleGuestItemEffect effect = items.DescribeItem(command.Argument);
            kind = effect.ReviveDivisor != 0 ? Ff7BattleRecoveryKind.Revive : effect.HitPoints > 0 || effect.MaximumHitPointFraction > 0 ? Ff7BattleRecoveryKind.HitPoints : Ff7BattleRecoveryKind.None;
            action = new(0, 1, 255, Healing: true) { IsItem = true, RequiresKnockedOutTarget = kind == Ff7BattleRecoveryKind.Revive };
        }
        else if (command.Command == Ff7BattleCommand.Magic && guest is IFf7BattleGuestMagic magic)
        {
            Ff7BattleGuestPhysicalHit hit = magic.DescribeMagic(command.Argument);
            action = ProjectGuestOutgoingAttack(targetSlot, hit);
            record = hit.StatusRecord;
            kind = ClassifyRecovery(action, record);
        }
        else
        {
            record = command.Command switch
            {
                Ff7BattleCommand.Item => ReadBattleItem(command.Argument),
                Ff7BattleCommand.Magic when guest is null => NativeBattleSpells(slot).SingleOrDefault(spell => spell.AttackId == command.Argument)?.Attack,
                _ => null
            };
            action = record is null ? default : Ff7BattleDamageAction.FromAttack(record) with { IsItem = command.Command == Ff7BattleCommand.Item };
            kind = record is null ? Ff7BattleRecoveryKind.None : ClassifyRecovery(action, record);
        }
        Ff7BattleActorState target = Actor(targetSlot);
        Ff7BattleDamageActor stats = Stats(target) with { CurrentHitPoints = target.Actor.HitPoints, MaximumHitPoints = target.Actor.MaxHitPoints };
        return CanRecoverTarget(kind, action, stats, record) ? kind : Ff7BattleRecoveryKind.None;
    }

    private bool IsAvailableRecoveryCommand(int slot, Ff7BattleCommandOption command)
    {
        return ReadAvailableCommand(slot, command.Command, command.Argument) == command;
    }

    public static bool CanRecoverTarget(Ff7BattleRecoveryKind kind, Ff7BattleDamageAction action, Ff7BattleDamageActor target, Ff7BattleAttack? record = null)
    {
        bool knockedOut = target.CurrentHitPoints == 0 || (target.Statuses & Ff7NativeBattleActionResolver.Death) != 0;
        if (kind == Ff7BattleRecoveryKind.None || (kind == Ff7BattleRecoveryKind.Revive) != knockedOut || Ff7OriginSavedStatusRules.IsZombie(target.OriginSavedStatuses) || (target.Statuses & 0x01004000) != 0 || Ff7NativeBattleDamage.IsConditionImmune(target, action))
        {
            return false;
        }
        if (!action.IsItem && (target.Statuses & Ff7NativeBattleStatuses.Reflect) != 0 && (action.Reflectable || record is { SpecialFlags: var flags } && (flags & 0x200) == 0))
        {
            return false;
        }
        Ff7BattleDamageAffinity affinity = action.IgnoreNativeAffinity ? Ff7BattleDamageAffinity.Normal : Ff7NativeBattleStatuses.ShieldAffinity(target.Statuses, action.NativeElements, action.IsItem, action.Affinity);
        return (affinity & (Ff7BattleDamageAffinity.Absorb | Ff7BattleDamageAffinity.Nullify)) == 0 && (action.IgnoreNativeAffinity || action.GuestElementAdjustment > -100);
    }

    public static Ff7BattleRecoveryKind ClassifyRecovery(Ff7BattleDamageAction action, Ff7BattleAttack? record = null)
    {
        if (action.RequiresKnockedOutTarget || record is not null && (record.StatusMask & Ff7NativeBattleActionResolver.Death) != 0 && Ff7NativeBattleStatuses.ReadRecordMode(record) == 1)
        {
            return Ff7BattleRecoveryKind.Revive;
        }
        return action.Healing && !action.TargetsMagicPoints && (action.Power > 0 || action.BaseDamage > 0 || action.FixedDamage > 0 || action.FractionNumerator > 0) ? Ff7BattleRecoveryKind.HitPoints : Ff7BattleRecoveryKind.None;
    }
}
