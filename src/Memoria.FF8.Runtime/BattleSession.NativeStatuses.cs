namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal static ushort NativeSavedStatusBits(BattleCombatant actor) => actor.NativeStatus0;

    private static uint NativeBattleStatusBits(BattleCombatant actor) => actor.NativeStatus1;

    private static NativeBattleStatusSnapshot CaptureNativeStatus(BattleCombatant actor)
    {
        return new(actor.NativeStatus0, actor.NativeStatus1, actor.NativeStatusTimers.ToArray()) { Resistances = actor.MutableNativeStatusResistances.ToArray(), Presentation = actor.NativePresentationStatus };
    }

    private static int GetNativeStatusResistance(BattleCombatant actor, int index)
    {
        if (actor.NativeStatusResistances.Count != 40 || (uint)index >= 40)
        {
            throw new NotSupportedException("Native status resistance requires its complete original forty-byte owner and valid index.");
        }
        return actor.NativeStatusResistances[index];
    }

    private static void SetNativeAiStatus(BattleCombatant actor, int status, int value)
    {
        if (status is < 0 or >= 48)
        {
            throw new NotSupportedException("Native status operation requires one of the original WORD/DWORD bits.");
        }
        if (status < 16)
        {
            ushort mask = (ushort)(1u << status);
            actor.NativeStatus0 = value == 1 ? (ushort)(actor.NativeStatus0 | mask) : (ushort)(actor.NativeStatus0 & ~mask);
        }
        else
        {
            uint mask = 1u << (status - 16);
            actor.NativeStatus1 = value == 1 ? actor.NativeStatus1 | mask : actor.NativeStatus1 & ~mask;
        }
        if (value != 1)
        {

        }
    }

    private static void RestoreNativeStatus(BattleCombatant actor, NativeBattleStatusSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            throw new InvalidDataException("Battle snapshot requires its explicitly migrated complete native status bank.");
        }
        if (snapshot.Timers is null || snapshot.Timers.Length != 14)
        {
            throw new InvalidDataException("Native battle statuses require all fourteen original signed timer WORDs.");
        }
        if (snapshot.Resistances is null || snapshot.Resistances.Length is not (0 or 40) || snapshot.Resistances.Length != actor.MutableNativeStatusResistances.Length)
        {
            throw new InvalidDataException("Battle snapshot requires its explicitly migrated native status resistance bank.");
        }
        (snapshot.Presentation ?? throw new InvalidDataException("Native B92/B1C mirror owner is missing; migrate the standalone snapshot explicitly.")).Validate();
        actor.NativePresentationStatus = snapshot.Presentation;
        actor.NativeStatus0 = snapshot.Saved;
        actor.NativeStatus1 = snapshot.Battle;
        snapshot.Timers.CopyTo(actor.NativeStatusTimers, 0);
        snapshot.Resistances.CopyTo(actor.MutableNativeStatusResistances, 0);
    }

    private static void RemoveNativeStatuses(BattleCombatant recipient, ushort savedMask, uint battleMask)
    {
        //48F140 preserves timed bits whose sentinel marks a permanent installed effect.
        for (int bit = 0; bit < 14; bit++)
        {
            if ((recipient.NativeStatus1 & (1u << bit)) != 0 && recipient.NativeStatusTimers[bit] == -1111)
            {
                battleMask &= ~(1u << bit);
            }
        }
        recipient.NativeStatus0 &= (ushort)~savedMask;
        recipient.NativeStatus1 &= ~battleMask;
        for (int bit = 0; bit < 14; bit++)
        {
            if ((battleMask & (1u << bit)) != 0)
            {
                recipient.NativeStatusTimers[bit] = -1111;
            }
        }

    }
}
