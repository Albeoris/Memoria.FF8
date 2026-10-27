using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScriptedLightingStep(NativeBattleWaterExecutionFrame Frame, NativeBattleScriptedLightingMemory Lighting);

/// <summary>The cinematic lit traversal's current-slot color, back-color and scaled light-matrix publication.</summary>
public static class NativeBattleScriptedLighting
{
    public static NativeBattleScriptedLightingStep Prepare(NativeBattleWaterExecutionFrame frame, NativeBattleScriptedLightingMemory lighting, Func<NativeBattleWaterExecutionFrame, ushort, uint> resolveParent)
    {
        ArgumentNullException.ThrowIfNull(lighting);
        lighting.Validate();
        sbyte selector = unchecked((sbyte)ReadSlot(frame, 0xE1, 1)[0]);
        if (selector == 0)
        {
            return new(frame, lighting);
        }
        uint record = 0x27977A4u + (uint)(0x50 * (selector & 0x0F));
        NativeBattleScriptedLightingMatrix color = ReadControls(frame, record + 0x20);
        NativeBattleScriptedBackColor back = new(unchecked((int)CoreDword(frame, record + 0x40) * 16), unchecked((int)CoreDword(frame, record + 0x44) * 16), unchecked((int)CoreDword(frame, record + 0x48) * 16));
        uint packed = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xCC, 4));
        int x = (int)(packed & 0xFF) << 5;
        int y = (int)((packed >> 3) & 0x1FE0);
        int z = (int)((packed >> 11) & 0x1FE0);
        frame = frame.WriteTemporaryDword(0x80, (uint)x).WriteTemporaryDword(0x84, (uint)y).WriteTemporaryDword(0x88, (uint)z);
        NativeBattleActionMatrix matrix;
        if ((selector & 0x80) != 0)
        {
            ArgumentNullException.ThrowIfNull(resolveParent);
            uint parent = resolveParent(frame, SlotWord(frame, 0x9C));
            frame = frame.WriteTemporaryDword(0xFC, parent);
            matrix = NativeBattleGfMatrices.Multiply56C090(ReadRotation(frame, record), ReadRotation(frame, parent));
            frame = WriteTemporaryRotation(frame, matrix);
        }
        else
        {
            NativeBattleScriptedLightingMatrix source = ReadControls(frame, record);
            frame = frame.WriteTemporary(0x20, source.Bytes.AsSpan());
            frame.TemporaryFrame.SetKnownness(0x20, source.Known.AsSpan());
            matrix = source.ToActionMatrix();
        }
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, x, y, z);
        frame = WriteTemporaryRotation(frame, matrix);
        byte[] lightBytes = new byte[20];
        bool[] lightKnown = new bool[20];
        frame.TemporaryFrame.ReadWithKnownness(0x20, lightBytes, lightKnown);
        NativeBattleScriptedLightingMatrix light = new(lightBytes.ToImmutableArray(), lightKnown.ToImmutableArray());
        return new(frame, new(light, color, back));
    }

    private static NativeBattleScriptedLightingMatrix ReadControls(NativeBattleWaterExecutionFrame frame, uint address)
    {
        byte[] bytes = new byte[20];
        bool[] known = Enumerable.Repeat(true, 20).ToArray();
        frame.Core.Read(address, 18).CopyTo(bytes, 0);
        for (int index = 18; index < 20; index++)
        {
            uint current = unchecked(address + (uint)index);
            int offset = checked((int)(current - NativeBattleScriptedEffectMemory.Address));
            if (frame.Core.Core.EnumerateUnknownRanges().Any(region => offset >= region.Offset && offset < region.Offset + region.Length))
            {
                known[index] = false;
            }
            else
            {
                bytes[index] = frame.Core.Read(current, 1)[0];
            }
        }
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    private static NativeBattleActionMatrix ReadRotation(NativeBattleWaterExecutionFrame frame, uint address)
    {
        byte[] bytes = frame.Core.Read(address, 18);
        return new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2))).ToImmutableArray(), 0, 0, 0, 4096);
    }

    private static NativeBattleWaterExecutionFrame WriteTemporaryRotation(NativeBattleWaterExecutionFrame frame, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            frame = frame.WriteTemporaryWord(0x20 + index * 2, unchecked((ushort)matrix.Rotation[index]));
        }
        return frame;
    }
}
