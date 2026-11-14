using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverGuestLimitLearningSettlement(Guid EncounterId, byte? ShotBefore = null, byte ShotSetMask = 0, byte? SlotsBefore = null, byte SlotsSetMask = 0)
{
    public bool HasChanges => ShotSetMask != 0 || SlotsSetMask != 0;
    public byte? CurrentShot => ShotBefore is byte before ? (byte)(before | ShotSetMask) : null;

    public CrossoverGuestLimitLearningSettlement Bind(byte? shot, byte? slots)
    {
        if (shot is not null && ShotBefore is not null && shot != ShotBefore || slots is not null && SlotsBefore is not null && slots != SlotsBefore)
        {
            throw new InvalidDataException("The source Limit learning owners changed during the encounter.");
        }
        return this with { ShotBefore = ShotBefore ?? shot, SlotsBefore = SlotsBefore ?? slots };
    }

    public CrossoverGuestLimitLearningSettlement Dispatch(BattleNativeAction action)
    {
        byte shot = NativeBattleLimitLearningRules.ShotMask(action);
        byte slots = NativeBattleLimitLearningRules.SlotsMask(action);
        if (shot != 0 && ShotBefore is null || slots != 0 && SlotsBefore is null)
        {
            throw new NotSupportedException("This source Limit requires its original durable learned-byte owner.");
        }
        return this with { ShotSetMask = (byte)(ShotSetMask | shot), SlotsSetMask = (byte)(SlotsSetMask | slots) };
    }
}
