using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicNodeMeasurementServices(Func<NativeBattleWaterExecutionFrame, short, uint> ResolveNode, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> CacheAngles, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> UpdateSelected);

public static class NativeBattleCinematicNodeMeasurements
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleCinematicNodeMeasurementServices services)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        int opcode = instruction & 0x1FF;
        if (opcode is not (0x112 or 0x113))
        {
            throw new NotSupportedException("Native node measurement requires its selected instruction.");
        }
        uint first = services.ResolveNode(frame, Operand(4));
        uint second = services.ResolveNode(frame, Operand(6));
        int firstX = Word(first + 0x94);
        int secondX = Word(second + 0x94);
        int firstZ = Word(first + 0x98);
        int secondZ = Word(second + 0x98);
        if (opcode == 0x113)
        {
            int angle;
            (frame, angle) = NativeBattleCinematicPath179.DirectionB66B30(frame, firstX, firstZ, secondX, secondZ);
            int offset = unchecked(Operand(2) * 4);
            frame = StoreIndexedDword(frame, unchecked(0x50 + offset), unchecked((uint)(angle << 16)));
            frame = offset >= 12 ? services.UpdateSelected(frame) : services.CacheAngles(frame);
            return NativeBattleScriptedCommandReads.Advance(frame, 8);
        }
        int dx = unchecked(secondX - firstX);
        int dy = unchecked(Word(second + 0x96) - Word(first + 0x96));
        int dz = unchecked(secondZ - firstZ);
        if ((instruction & 0x8000) == 0)
        {
            int xy = NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(dx * dx + dy * dy));
            int distance = NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(xy * xy + dz * dz));
            int value = distance == 0 ? 0 : unchecked(distance << 16) / Operand(8);
            frame = StoreIndexedDword(frame, unchecked(0x50 + Operand(2) * 4), unchecked((uint)(value << 8)));
        }
        int mask = Operand(2);
        int squared = (mask & 0x100) != 0 ? unchecked(dx * dx) : 0;
        if ((mask & 0x200) != 0)
        {
            squared = unchecked(squared + dy * dy);
        }
        if ((mask & 0x400) != 0)
        {
            squared = unchecked(squared + dz * dz);
        }
        int selected = unchecked(NativeBattleRayBomb10Geometry.Sqrt56BEC0(squared) << 16) / Operand(8);
        frame = WriteSlotDword(frame, 0x50 + (mask & 7) * 4, unchecked((uint)(selected << 8)));
        frame = services.CacheAngles(frame);
        return NativeBattleScriptedCommandReads.Advance(frame, 10);

        short Operand(uint offset) => BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, offset, 2));
        short Word(uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
    }

    private static NativeBattleWaterExecutionFrame StoreIndexedDword(NativeBattleWaterExecutionFrame frame, int offset, uint value)
    {
        uint address = unchecked(CoreDword(frame, 0x27973E8) + (uint)offset);
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), bytes) };
    }
}
