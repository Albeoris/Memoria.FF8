using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicMesh7
{
    public static NativeBattleWaterMeshPrepared Prepare(NativeBattleWaterExecutionFrame frame, short focal)
    {
        byte[] rotation = frame.Core.Read(0x27979E8, 18);
        NativeBattleActionMatrix view = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2))).ToImmutableArray(), unchecked((int)CoreDword(frame, 0x27979FC)), unchecked((int)CoreDword(frame, 0x2797A00)), unchecked((int)CoreDword(frame, 0x2797A04)), 4096);
        NativeBattleVector point = new(unchecked((short)SlotWord(frame, 0x94)), unchecked((short)SlotWord(frame, 0x96)), unchecked((short)SlotWord(frame, 0x98)));
        NativeBattleFullMacVertex translated = NativeBattleGfMatrices.TransformMac460840(view, point);
        NativeBattleVector angles = new(unchecked((short)SlotWord(frame, 0x8C)), unchecked((short)SlotWord(frame, 0x8E)), unchecked((short)SlotWord(frame, 0x90)));
        NativeBattleActionMatrix euler = NativeBattleGfMatrices.Euler56CD50(angles);
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Multiply56C090(view, euler);
        for (int index = 0; index < 9; index++)
        {
            frame = frame.WriteTemporaryWord(0x20 + index * 2, unchecked((ushort)composed.Rotation[index]));
        }
        frame = frame with { TemporaryFrame = frame.TemporaryFrame with { Known = frame.TemporaryFrame.Known.SetItem(0x32, false).SetItem(0x33, false) } };
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xB8, 4));
        frame = frame.WriteTemporaryDword(0x48, value).WriteTemporaryDword(0x44, value).WriteTemporaryDword(0x40, value);
        frame = frame.WriteTemporaryDword(0x4C, BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4)));
        return new(frame, composed with { X = translated.X, Y = translated.Y, Z = translated.Z }, focal);
    }
}
