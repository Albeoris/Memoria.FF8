using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDiablos325WingMorph
{
    public static NativeBattleEffectArena Vertices6590E0(NativeBattleEffectArena arena, uint first, uint second, int ratio, uint target)
    {
        using NativeBattleEffectArenaTransaction? owned = arena.Transaction is null ? new(arena) : null;
        using NativeBattleEffectArenaWriter writer = new(owned?.View ?? arena);
        int count = checked((int)ReadDword(writer, unchecked(first + 4)));
        for (int index = 0; index < count; index++)
        {
            uint step = checked((uint)(8 + index * 8));
            byte[] a = Read(writer, unchecked(first + step), 6);
            byte[] b = Read(writer, unchecked(second + step), 6);
            NativeBattleVector delta = new(unchecked((short)(Word(b, 0) - Word(a, 0))), unchecked((short)(Word(b, 2) - Word(a, 2))), unchecked((short)(Word(b, 4) - Word(a, 4))));
            NativeBattleVector scaled = NativeBattleQuezacotlRibbonMath.Scale(delta, ratio);
            byte[] output = new byte[6];
            BinaryPrimitives.WriteUInt16LittleEndian(output, unchecked((ushort)(Word(a, 0) + scaled.X)));
            BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(2), unchecked((ushort)(Word(a, 2) + scaled.Y)));
            BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(4), unchecked((ushort)(Word(a, 4) + scaled.Z)));
            Write(writer, unchecked(target + (uint)(index * 8)), output);
        }
        return owned?.Freeze() ?? writer.Freeze();
    }

    public static NativeBattleEffectArena Polygons659200(NativeBattleEffectArena arena, uint first, uint second, int ratio, uint target)
    {
        using NativeBattleEffectArenaTransaction? owned = arena.Transaction is null ? new(arena) : null;
        using NativeBattleEffectArenaWriter writer = new(owned?.View ?? arena);
        uint a = unchecked(first + ReadDword(writer, first));
        uint b = unchecked(second + ReadDword(writer, second));
        WriteDword(writer, target, 8);
        WriteDword(writer, unchecked(target + 4), ReadDword(writer, unchecked(first + 4)));
        uint output = unchecked(target + 8);
        for (int group = 0; group < 6; group++)
        {
            if (group is 2 or 3)
            {
                a = unchecked(a + 4);
                b = unchecked(b + 4);
                output = unchecked(output + 4);
                continue;
            }
            uint count = ReadDword(writer, a);
            WriteDword(writer, output, count);
            a = unchecked(a + 4);
            b = unchecked(b + 4);
            output = unchecked(output + 4);
            int length = group < 2 ? 12 : group == 4 ? 20 : 24;
            int colors = group < 2 ? 1 : group == 4 ? 3 : 4;
            for (uint index = 0; index < count; index++)
            {
                Write(writer, unchecked(output + 4), Read(writer, unchecked(a + 4), 8));
                for (int color = 0; color < colors; color++)
                {
                    uint offset = color == 0 ? 0 : (uint)(8 + color * 4);
                    byte[] firstRgb = Read(writer, unchecked(a + offset), 3);
                    byte[] secondRgb = Read(writer, unchecked(b + offset), 3);
                    NativeBattleVector blended = NativeBattleQuezacotlRibbonMath.Blend(new(firstRgb[0], firstRgb[1], firstRgb[2]), new(secondRgb[0], secondRgb[1], secondRgb[2]), unchecked(4096 - ratio), ratio);
                    Write(writer, unchecked(output + offset), [unchecked((byte)blended.X), unchecked((byte)blended.Y), unchecked((byte)blended.Z)]);
                }
                Write(writer, unchecked(output + 3), Read(writer, unchecked(a + 3), 1));
                a = unchecked(a + (uint)length);
                b = unchecked(b + (uint)length);
                output = unchecked(output + (uint)length);
            }
        }
        return owned?.Freeze() ?? writer.Freeze();
    }

    private static ushort Word(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    private static uint ReadDword(NativeBattleEffectArenaWriter writer, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(writer, address, 4));
    private static byte[] Read(NativeBattleEffectArenaWriter writer, uint address, int length) => writer.ReadCurrent(NativeBattleWaterExecutionFields.ArenaOffset(address, length), length);

    private static void Write(NativeBattleEffectArenaWriter writer, uint address, byte[] bytes)
    {
        writer.Write(NativeBattleWaterExecutionFields.ArenaOffset(address, bytes.Length), bytes);
    }

    private static void WriteDword(NativeBattleEffectArenaWriter writer, uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        Write(writer, address, bytes);
    }
}
