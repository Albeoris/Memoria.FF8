using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicWireMorphServices(Func<NativeBattleWaterExecutionFrame, short, uint> ResolveShape, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> PrepareBasis, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleWaterExecutionFrame> Project, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append);

/// <summary>Interpolated mesh vertices with original directional edge flags and unconditional cross edges.</summary>
public static class NativeBattleCinematicWireMorph3F
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicWireMorphServices services)
    {
        uint payload = SlotDword(0xB8);
        uint topology = services.ResolveShape(frame, unchecked((short)Word(payload)));
        uint first = services.ResolveShape(frame, unchecked((short)Word(payload + 2)));
        uint second = services.ResolveShape(frame, unchecked((short)Word(payload + 4)));
        first = unchecked(first + Dword(first + 0x14));
        second = unchecked(second + Dword(second + 0x14));
        int count = unchecked((int)Dword(topology + 0x18));
        int blend = unchecked((short)SlotWord(frame, 0x94));
        uint work = CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x74));
        for (int vertex = 0; vertex < count; vertex++)
        {
            for (uint axis = 0; axis < 3; axis++)
            {
                int from = unchecked((short)Word(first + (uint)(vertex * 8) + axis * 2));
                int to = unchecked((short)Word(second + (uint)(vertex * 8) + axis * 2));
                int value = unchecked(from + unchecked((to - from) * blend) / 256);
                frame = StoreWord(frame, work + (uint)(vertex * 8) + axis * 2, unchecked((ushort)value));
            }
        }
        frame = services.PrepareBasis(frame);
        for (int vertex = 0; vertex < count; vertex++)
        {
            uint current = unchecked(work + (uint)(vertex * 8));
            frame = services.Project(frame, current, current);
            frame = StoreWord(frame, current + 6, 0);
        }
        uint command = SlotDword(0xCC) | 0x40000000;
        int depth = unchecked((short)SlotWord(frame, 0xC0));
        uint fixedOrdering = depth == 0 ? 0 : unchecked(frame.ReadTemporaryDword(0x14C) + ((uint)depth & 0x3FFC));
        frame = frame.WriteTemporaryDword(0xF0, command).WriteTemporaryDword(0xF4, fixedOrdering);
        uint output = CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x7C));
        uint group = unchecked(topology + Dword(topology + 8));
        while (true)
        {
            short type = unchecked((short)Word(group));
            if (type is not (16 or 18))
            {
                return frame;
            }
            int faces = unchecked((short)Word(group + 2));
            uint face = unchecked(group + 4);
            int indexOffset = type == 16 ? 4 : 12;
            int stride = type == 16 ? 12 : 24;
            for (int index = 0; index < faces; index++, face = unchecked(face + (uint)stride))
            {
                uint a = unchecked(work + Word(face + (uint)indexOffset));
                uint b = unchecked(work + Word(face + (uint)indexOffset + 2));
                uint c = unchecked(work + Word(face + (uint)indexOffset + 4));
                uint d = unchecked(work + Word(face + (uint)indexOffset + 6));
                byte firstA = Byte(a + 6);
                byte firstB = Byte(b + 6);
                byte firstC = Byte(c + 6);
                byte firstD = Byte(d + 6);
                if (((firstA & 2) | (firstB & 1)) != 3)
                {
                    Flag(a, 2);
                    Flag(b, 1);
                    Emit(a, b);
                }
                if (((firstC & 8) | (firstD & 4)) != 12)
                {
                    Flag(c, 8);
                    Flag(d, 4);
                    Emit(c, d);
                }
                Flag(a, 4);
                Flag(c, 1);
                Emit(a, c);
                if (((firstB & 8) | (firstD & 2)) != 10)
                {
                    Flag(b, 8);
                    Flag(d, 2);
                    Emit(b, d);
                }
                frame = WriteCoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x7C), output);
            }
            group = face;
        }

        void Emit(uint firstVertex, uint secondVertex)
        {
            var written = NativeBattleCinematicGrid59.AppendLine(frame, output, firstVertex, secondVertex, command, null, services.Append, fixedOrdering);
            frame = written.Frame;
            output = written.Output;
        }

        void Flag(uint vertex, byte flag)
        {
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(vertex + 6, 1), [(byte)(Byte(vertex + 6) | flag)]) };
        }

        uint SlotDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        byte Byte(uint address) => frame.Arena.Read(ArenaOffset(address, 1), 1)[0];
        ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
    }

    private static NativeBattleWaterExecutionFrame StoreWord(NativeBattleWaterExecutionFrame frame, uint address, ushort value) => frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), BitConverter.GetBytes(value)) };
}
