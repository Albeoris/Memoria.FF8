using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicRibbonRampResult(NativeBattleWaterExecutionFrame Frame, NativeBattleScriptedColorRegisters Registers);

public static class NativeBattleCinematicRibbonWork
{
    public static NativeBattleWaterExecutionFrame BuildCrossSection(NativeBattleWaterExecutionFrame frame, NativeBattleActionMatrix matrix, int width, int distance, uint output, bool flip)
    {
        if (flip)
        {
            width = unchecked(-width);
        }
        int inner = unchecked(width * 3 << 9) / 4096;
        int z = unchecked(-distance);
        NativeBattleVector[] points = [new(0, 0, unchecked((short)z)), new(unchecked((short)width), 0, unchecked((short)z)), new(unchecked((short)-width), 0, unchecked((short)z)), new(unchecked((short)inner), 0, unchecked((short)z)), new(unchecked((short)-inner), 0, unchecked((short)z)), new(0, unchecked((short)width), unchecked((short)z)), new(0, unchecked((short)-width), unchecked((short)z)), new(0, unchecked((short)inner), unchecked((short)z)), new(0, unchecked((short)-inner), unchecked((short)z))];
        for (int index = 0; index < points.Length; index++)
        {
            NativeBattleVector point = points[index];
            frame = frame.WriteTemporaryWord(0xF0, unchecked((ushort)point.X)).WriteTemporaryWord(0xF2, unchecked((ushort)point.Y)).WriteTemporaryWord(0xF4, unchecked((ushort)point.Z));
            NativeBattleFullMacVertex mac = NativeBattleGfMatrices.TransformMac460840(matrix, point);
            byte[] vector = new byte[6];
            BinaryPrimitives.WriteInt16LittleEndian(vector, (short)Math.Clamp(mac.X, short.MinValue, short.MaxValue));
            BinaryPrimitives.WriteInt16LittleEndian(vector.AsSpan(2), (short)Math.Clamp(mac.Y, short.MinValue, short.MaxValue));
            BinaryPrimitives.WriteInt16LittleEndian(vector.AsSpan(4), (short)Math.Clamp(mac.Z, short.MinValue, short.MaxValue));
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(unchecked(output + (uint)(index * 8)), 6), vector) };
            frame = frame.WriteTemporaryDword(0xFC, mac.Flags);
        }
        return frame;
    }

    public static NativeBattleCinematicRibbonRampResult BuildColorRamp(NativeBattleWaterExecutionFrame frame, NativeBattleScriptedColorRegisters registers, uint first, uint second, uint output)
    {
        registers = registers with { Material = 0x3A000000 };
        frame = frame.WriteTemporaryDword(0xE0, (first & 0x02FFFFFF) | 0x38000000).WriteTemporaryDword(0xE4, (second & 0x02FFFFFF) | 0x38000000);
        int fadeIn = unchecked((int)frame.ReadTemporaryDword(0xF0));
        if (fadeIn != 0)
        {
            int step = 4096 / fadeIn;
            int coefficient = 0;
            for (int index = 0; index < fadeIn; index++)
            {
                ScalePair(coefficient);
                coefficient = unchecked(coefficient + step);
            }
        }
        int plateau = unchecked((int)frame.ReadTemporaryDword(0xF4) - (int)frame.ReadTemporaryDword(0xF0));
        uint firstPlateau = frame.ReadTemporaryDword(0xE0);
        uint secondPlateau = frame.ReadTemporaryDword(0xE4);
        for (int index = 0; index < plateau; index++)
        {
            Store(firstPlateau);
            Store(secondPlateau);
        }
        int fadeOut = unchecked((int)frame.ReadTemporaryDword(0xF8) - (int)frame.ReadTemporaryDword(0xF4));
        if (fadeOut != 0)
        {
            int step = 4096 / fadeOut;
            int coefficient = 4096;
            for (int index = 0; index < fadeOut; index++)
            {
                ScalePair(coefficient);
                coefficient = unchecked(coefficient - step);
            }
        }
        int capacity = unchecked((int)frame.ReadTemporaryDword(0xF8));
        for (int index = 0; index < capacity; index++)
        {
            Store(0x3A000000);
            Store(0x3A000000);
        }
        return new(frame, registers);

        void ScalePair(int coefficient)
        {
            NativeBattleCinematicColorScaleResult scaled = NativeBattleCinematicColorScale.ScaleRawIr0(frame.ReadTemporaryDword(0xE0), unchecked((short)coefficient), registers);
            registers = scaled.Registers;
            Store((scaled.Color & 0xFFFFFF) | 0x3A000000);
            scaled = NativeBattleCinematicColorScale.ScaleRawIr0(frame.ReadTemporaryDword(0xE4), unchecked((short)coefficient), registers);
            registers = scaled.Registers;
            Store((scaled.Color & 0xFFFFFF) | 0x3A000000);
        }

        void Store(uint value)
        {
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(output, 4), BitConverter.GetBytes(value)) };
            output = unchecked(output + 4);
        }
    }
}
