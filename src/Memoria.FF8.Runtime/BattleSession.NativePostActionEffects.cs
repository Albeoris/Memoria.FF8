using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static BattleNativeAction NativePostActionEffect(ushort id)
    {
        return new(0, id, -1, -1) { Name = id == 2 ? "Poison" : id == 4 ? "Regen" : "Darkside cost", MagicId = 0, AttackType = 0, Power = 0, HitCount = 1, TargetFlags = null, AttackFlags = 3, DamagePresentation = id == 2 ? (byte)5 : (byte)0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = ImmutableArray<byte>.Empty, SupplementalRecord = ImmutableArray<byte>.Empty };
    }

    private bool ResolveNativePostActionHit(BattleCombatant actor, BattleCombatant recipient, BattleNativeAction native)
    {
        if (native.Command != 0 || native.ActionId is not (2 or 4 or 6))
        {
            return false;
        }
        if (native.ActionId == 4)
        {
            int amount = recipient.MaximumHp / 20;
            bool zombie = (NativeSavedStatusBits(recipient) & 0x40) != 0;
            if (!zombie)
            {
                MarkNativeContactFlags(0, 1);
            }
            SetNativeContactDamage(zombie ? amount : -amount);
            FinalizeNativeContactPresentation();
            if (zombie)
            {
                ApplyDamage(actor, recipient, amount, native.Name!, counterEligible: false, bypassDamageLimit: true);
            }
            else
            {
                ApplyHealing(recipient, amount, native.Name!);
            }
            return true;
        }
        int damage = native.ActionId == 2 ? (NextRandomByte() % 5 + 5) * recipient.MaximumHp / 100 : recipient.MaximumHp / 10;
        if (native.ActionId == 2)
        {
            damage = Math.Min(damage, definition.Tuning.MaximumDamage);
        }
        SetNativeContactDamage(damage);
        FinalizeNativeContactPresentation();
        ApplyDamage(actor, recipient, damage, native.Name!, counterEligible: false, bypassDamageLimit: native.ActionId == 6);
        return true;
    }
}
