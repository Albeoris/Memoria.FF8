using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScriptedMeshMorphServices(Func<NativeBattleWaterExecutionFrame, short, uint> ResolveBone, Func<NativeBattleWaterExecutionFrame, short, uint> ResolveShape);

/// <summary>Current cinematic position and normal interpolation over addressed arena owners.</summary>
public static class NativeBattleScriptedMeshMorph
{
    public static NativeBattleWaterExecutionFrame Morph(NativeBattleWaterExecutionFrame frame, NativeBattleScriptedMeshMorphServices services)
    {
        uint tuple = frame.ReadTemporaryDword(0x4C);
        if (tuple == 0)
        {
            return frame;
        }
        uint reference = services.ResolveBone(frame, Word(tuple));
        uint start = services.ResolveShape(frame, Word(unchecked(tuple + 2)));
        uint end = services.ResolveShape(frame, Word(unchecked(tuple + 4)));
        if (frame.Arena.Transaction is not null)
        {
            return MorphResolved(frame, reference, start, end);
        }
        using NativeBattleEffectArenaTransaction transaction = new(frame.Arena);
        NativeBattleWaterExecutionFrame result = MorphResolved(frame with { Arena = transaction.View }, reference, start, end);
        return result with { Arena = transaction.Freeze() };

        short Word(uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
    }

    private static NativeBattleWaterExecutionFrame MorphResolved(NativeBattleWaterExecutionFrame frame, uint reference, uint start, uint end)
    {
        for (int lane = 0; lane < 3; lane++)
        {
            frame = frame.WriteTemporaryDword(0xE0 + 4 * lane, unchecked((uint)(int)Word(unchecked(reference + 0x94u + (uint)(2 * lane)))));
        }
        uint flags = Dword(start) & Dword(end);
        if ((flags & 2) != 0)
        {
            uint source = unchecked(start + Dword(unchecked(start + 0x14)));
            uint target = unchecked(end + Dword(unchecked(end + 0x14)));
            int count = unchecked((int)Dword(unchecked(start + 0x18)));
            uint output = frame.ReadTemporaryDword(0x7C);
            frame = frame.WriteTemporaryDword(0x74, output).WriteTemporaryDword(0xF0, source).WriteTemporaryDword(0xF4, target).WriteTemporaryDword(0xF8, unchecked((uint)count));
            if (count <= 0)
            {
                throw new NotSupportedException("Native cinematic position morph requires a positive count for its unconditional first iteration.");
            }
            for (int vertex = 0; vertex < count; vertex++)
            {
                short x = Position(0);
                short y = Position(1);
                Store(output, x);
                short z = Position(2);
                Store(unchecked(output + 2), y);
                Store(unchecked(output + 4), z);
                source = unchecked(source + 8);
                target = unchecked(target + 8);
                output = unchecked(output + 8);
            }

            short Position(int lane)
            {
                uint offset = (uint)(2 * lane);
                int a = Word(unchecked(source + offset));
                int b = Word(unchecked(target + offset));
                int factor = unchecked((short)frame.ReadTemporaryWord(0xE0 + 4 * lane));
                int product = unchecked((b - a) * factor);
                return unchecked((short)(a + (product >> 8)));
            }
        }
        if ((flags & 4) != 0)
        {
            uint source = unchecked(start + Dword(unchecked(start + 0x1C)));
            uint target = unchecked(end + Dword(unchecked(end + 0x1C)));
            int count = unchecked((int)Dword(unchecked(start + 0x20)));
            uint output = frame.ReadTemporaryDword(0x80);
            frame = frame.WriteTemporaryDword(0x78, output);
            for (int vertex = 0; vertex < count; vertex++)
            {
                for (int lane = 0; lane < 3; lane++)
                {
                    uint offset = (uint)(2 * lane);
                    int a = Word(unchecked(source + offset));
                    int b = Word(unchecked(target + offset));
                    int factor = unchecked((int)frame.ReadTemporaryDword(0xE0 + 4 * lane));
                    int product = unchecked((b - a) * factor);
                    int rounded = unchecked(product + (product < 0 ? 255 : 0)) >> 8;
                    Store(unchecked(output + offset), unchecked((short)(a + rounded)));
                }
                source = unchecked(source + 8);
                target = unchecked(target + 8);
                output = unchecked(output + 8);
            }
        }
        return frame;

        short Word(uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        void Store(uint address, short value)
        {
            byte[] bytes = new byte[2];
            BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), bytes) };
        }
    }
}
