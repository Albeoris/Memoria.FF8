namespace Memoria.FF7.Runtime;

public static class Ff7NativeBattlePartyRecord
{
    public static Ff7CharacterState ProjectStatusesAndRow(Ff7CharacterState original, Ff7BattleAiActor actor)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(actor);
        if (original.NativeRecord is not { Length: 132 } || actor.Slot is < 0 or >= 3)
        {
            throw new InvalidDataException("Native party battle publication requires its physical party actor and complete character record.");
        }
        byte[] record = original.NativeRecord.ToArray();
        record[31] = (byte)((record[31] & ~Ff7GuestBattleStatuses.SupportedNativeOnly) | (actor.Statuses & Ff7GuestBattleStatuses.SupportedNativeOnly));
        record[32] = (byte)((record[32] & ~1) | ((actor.Conditions & 0x40) != 0 ? 0 : 1));
        return original with { NativeRecord = record };
    }
}
