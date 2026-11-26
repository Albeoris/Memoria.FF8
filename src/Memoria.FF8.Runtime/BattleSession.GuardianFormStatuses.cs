namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static readonly NativeStatusId[] guardianFormNegativeStatuses =
    [
        NativeStatusId.Poison, NativeStatusId.Petrify, NativeStatusId.Blind, NativeStatusId.Silence, NativeStatusId.Berserk, NativeStatusId.Zombie,
        NativeStatusId.Sleep, NativeStatusId.Slow, NativeStatusId.Stop, NativeStatusId.Curse, NativeStatusId.Doom, NativeStatusId.Petrifying, NativeStatusId.Confuse, NativeStatusId.Eject, NativeStatusId.Vit0
    ];

    private void RecheckGuardianFormNegativeStatuses(BattleCombatant actor)
    {
        ushort saved = NativeSavedStatusBits(actor);
        uint battle = NativeBattleStatusBits(actor);
        ushort removeSaved = 0;
        uint removeBattle = 0;
        // Retained statuses have no source attack. At neutral stats, native power 200 gives a
        // full chance against the unmodified enemy resistance byte of 100.
        const byte existingStatusPower = 200;
        foreach (NativeStatusId status in guardianFormNegativeStatuses)
        {
            int bit = (int)status;
            if (bit < 16)
            {
                ushort mask = (ushort)(1 << bit);
                if ((saved & mask) != 0 && !NativeStatusChanceSucceeds(actor, bit, 0, 0, existingStatusPower, () => (byte)NextRandomByte()))
                {
                    removeSaved |= mask;
                }
            }
            else
            {
                uint mask = 1u << (bit - 16);
                if ((battle & mask) != 0 && !NativeStatusChanceSucceeds(actor, bit - 8, 0, 0, existingStatusPower, () => (byte)NextRandomByte()))
                {
                    removeBattle |= mask;
                }
            }
        }
        RemoveNativeStatuses(actor, removeSaved, removeBattle, preservePermanent: false);
        RefreshNativePresentationStatus(actor);
    }
}
