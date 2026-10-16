namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void ApplyNativeActionStatuses(BattleCombatant actor, BattleCombatant recipient, BattleNativeAction action, bool cure)
    {
        if (cure)
        {
            RemoveNativeStatuses(recipient, action.Statuses0, action.Statuses1);
            return;
        }
        uint before = NativeBattleStatusBits(recipient);
        bool physical = action.AttackType is 1 or 7 or 9 or 18;
        int attack = physical ? actor.Attack : actor.Magic;
        int defense = physical ? recipient.Defense : recipient.Spirit;
        if ((before & 0x1000000) != 0)
        {
            defense = 0;
        }
        for (int bit = 0; bit < 7; bit++)
        {
            uint mask = 1u << bit;
            if ((action.Statuses0 & mask) != 0)
            {
                TryApplyNativeStatus(recipient, bit, mask, false, attack, defense, action.StatusAttack);
            }
        }
        for (int bit = 0; bit < 32; bit++)
        {
            uint mask = 1u << bit;
            if ((action.Statuses1 & mask) != 0)
            {
                TryApplyNativeStatus(recipient, bit + 8, mask, true, attack, defense, action.StatusAttack);
            }
        }
        //48F9A0/492090 retain unchanged exclusive groups, otherwise only their newly added bits.
        uint after = recipient.NativeStatus1;
        uint speed = after & 0xE;
        uint crisis = after & 0x300;
        if (speed != (before & 0xE))
        {
            speed ^= before & 0xE;
        }
        if (crisis != (before & 0x300))
        {
            crisis ^= before & 0x300;
        }
        recipient.NativeStatus1 = after & 0xFFFFFCF1 | speed | crisis;
    }

    private bool TryApplyNativeStatus(BattleCombatant recipient, int resistanceIndex, uint mask, bool battle, int attack, int defense, byte chance)
    {
        uint current = battle ? NativeBattleStatusBits(recipient) : NativeSavedStatusBits(recipient);
        if ((current & mask) != 0)
        {
            return false;
        }
        if (chance != 255)
        {
            if (recipient.NativeStatusResistances.Count != 40)
            {
                throw new NotSupportedException("Native status application requires the recipient's original forty resistance bytes.");
            }
            int resistance = recipient.NativeStatusResistances[resistanceIndex];
            if (resistance >= 200)
            {
                return false;
            }
            int effective = chance + attack / 4 - defense / 4 - resistance;
            if (effective <= 0)
            {
                return false;
            }
            if (chance < 250)
            {
                int threshold = 255 * (255 * effective / 100) / 255;
                int roll = NextRandomByte();
                if (threshold == 0 || threshold < roll)
                {
                    return false;
                }
            }
        }
        ushort saved = NativeSavedStatusBits(recipient);
        uint statuses = NativeBattleStatusBits(recipient);
        if (battle)
        {
            if ((mask & 0x800) != 0 && recipient.NativeIndex >= 3 || (saved & 0x40) != 0 && (mask & 0x400) != 0 || (statuses & 0x2000000) != 0 && (mask & 0x4000) != 0)
            {
                return false;
            }
            int bit = System.Numerics.BitOperations.TrailingZeroCount(mask);
            if (bit < 14)
            {
                NativeBattleStatusRules rules = definition.NativeStatusRules ?? throw new NotSupportedException("Native timed status requires its installed duration table and actual saved battle-speed byte.");
                if (rules.Durations.IsDefault || rules.Durations.Length != 14)
                {
                    throw new InvalidDataException("Native status duration table requires exactly fourteen original bytes.");
                }
                recipient.NativeStatusTimers[bit] = unchecked((short)(4 * (rules.BattleSpeed + 1) * rules.Durations[bit]));
            }
            recipient.NativeStatus1 |= mask;
        }
        else
        {
            if ((saved & 0x40) != 0 && (mask & 1) != 0 || (statuses & 0x2000000) != 0 && (mask & 0x30) != 0)
            {
                return false;
            }
            if ((statuses & 0x400) != 0 && (mask & 0x40) != 0)
            {
                recipient.NativeStatus1 &= ~0x400u;
                recipient.NativeStatusTimers[10] = -1111;
            }
            recipient.NativeStatus0 |= (ushort)mask;
        }
        return true;
    }
}
