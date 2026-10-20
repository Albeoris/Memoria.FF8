using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>The selected node's planar orbit around its current reference node.</summary>
public static class NativeBattleApocalypse221Orbit11
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, Func<NativeBattleWaterExecutionFrame, short, uint> resolveNode)
    {
        uint reference = resolveNode(frame, unchecked((short)SlotWord(frame, 0xB0)));
        int angle = BinaryPrimitives.ReadInt32LittleEndian(ReadSlot(frame, 0x60, 4)) >> 16;
        int cosine = NativeBattleCameraMath.Cosine(angle) >> 4;
        int radius = BinaryPrimitives.ReadInt32LittleEndian(ReadSlot(frame, 0x5C, 4)) >> 8;
        int x = unchecked(radius * cosine) >> 16;
        frame = WriteSlotWord(frame, 0x94, unchecked((ushort)(x + ReferenceWord(0x94))));
        int sine = NativeBattleCameraMath.Sine(angle) >> 4;
        radius = BinaryPrimitives.ReadInt32LittleEndian(ReadSlot(frame, 0x5C, 4)) >> 8;
        int y = unchecked(radius * sine) >> 16;
        frame = WriteSlotWord(frame, 0x96, unchecked((ushort)(y + ReferenceWord(0x96))));
        int z = BinaryPrimitives.ReadInt32LittleEndian(ReadSlot(frame, 0x64, 4)) >> 16;
        return WriteSlotWord(frame, 0x98, unchecked((ushort)(z + ReferenceWord(0x98))));

        ushort ReferenceWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(reference + (uint)offset), 2), 2));
    }
}
