namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuezacotlPacketWrite(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleLogicalOrdering Ordering);

/// <summary>Quezacotl's actual draw cursor and original7043B0 sequential/6C5710 nonsequential allocation contracts.</summary>
public static class NativeBattleQuezacotlPacketArena
{
    public static NativeBattleQuezacotlPacketWrite Append(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch, ushort flags20 = 0)
    {
        int start = effect.DrawArenaOffset ?? throw new NotSupportedException("Native6C6060 requires its executed6C3760 draw-buffer pointer.");
        if (batch.Attempts.IsDefault || batch.AllocationBytes < 0 || (start & 3) != 0)
        {
            throw new InvalidDataException("Native6C6060 packet writes require their ordered attempts and actual aligned cursor.");
        }
        int cursor = 0;
        foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
        {
            if (attempt is null || attempt.Data.IsDefaultOrEmpty || attempt.ByteOffsetRelative != cursor)
            {
                throw new InvalidDataException("Native7043B0 packet attempt does not match its current physical cursor.");
            }
            if (attempt.Primitive is not null)
            {
                cursor = checked(cursor + attempt.PacketLength);
            }
        }
        if (cursor != batch.AllocationBytes)
        {
            throw new InvalidDataException("Native7043B0 final cursor differs from its accepted-only allocation size.");
        }
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, start, ordering, batch, flags20);
        return new(written.Memory, effect with { DrawArenaOffset = written.Cursor }, written.Ordering);
    }

    public static NativeBattleQuezacotlPacketWrite AppendCaptured6C5710(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch)
    {
        int start = effect.DrawArenaOffset ?? throw new NotSupportedException("Native6C5710 requires its executed6C3760 draw-buffer pointer.");
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, start, ordering, batch);
        return new(written.Memory, effect with { DrawArenaOffset = written.Cursor }, written.Ordering);
    }
}
