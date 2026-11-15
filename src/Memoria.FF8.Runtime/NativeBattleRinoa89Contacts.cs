using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleRinoa89Contacts
{
    private const uint CursorAddress = 0x2556620;

    internal static NativeBattleFileStaging WriteCursor(NativeBattleFileStaging staging, BattleContactCursor cursor)
    {
        ImmutableArray<byte>? nativeBytes = cursor.Ordinal <= NativeBattleContactAddress.NativeRowCount ? [.. BitConverter.GetBytes(NativeBattleContactAddress.EncodeOriginalCursor(cursor))] : null;
        return staging.WriteExecutionReference(CursorAddress, 4, new BattleExecutionReference.Cursor(cursor), nativeBytes);
    }

    public static NativeBattleRinoa89Frame Apply6FC410(NativeBattleRinoa89Frame frame, NativeBattleActionPacketsState packets, Func<NativeBattleRinoa89Frame, BattleContactOccurrence, NativeBattleRinoa89Frame> apply506690)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native6FC410 requires its actual selected action parameters.");
        int count = packets.ReadHeader(parameters.Header).ReadContactCount() >> 3;
        for (int index = 0; index < count; index++)
        {
            BattleContactCursor cursor = frame.Staging.ReadExecutionReference<BattleExecutionReference.Cursor>(CursorAddress, 4).Reference;
            BattleContactOccurrence occurrence = packets.ReadContact(cursor).Occurrence ?? throw new InvalidDataException("Wishing Star contact has no occurrence reference.");
            frame = apply506690(frame, occurrence);
            BattleContactCursor current = frame.Staging.ReadExecutionReference<BattleExecutionReference.Cursor>(CursorAddress, 4).Reference;
            frame = frame with { Staging = WriteCursor(frame.Staging, current.Advance(1)) };
        }
        return frame;
    }
}
