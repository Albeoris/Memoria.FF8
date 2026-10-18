using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshFlashStep(NativeBattleModulePoolTask UpdatedTask, NativeBattleGfGeometryBatch Batch, int Result);

/// <summary>592BC0's white envelope and592C60's common-heap TILE followed by draw-mode E1.</summary>
public static class NativeBattleGilgameshFlash
{
    public static NativeBattleGilgameshFlashStep Step592BC0(NativeBattleModulePoolTask task, uint graphicsFlags, ImmutableArray<int> tilePrecision = default, ImmutableArray<int> drawModePrecision = default)
    {
        if (task.Payload.IsDefault || task.Payload.Length != 0x24 - 12)
        {
            throw new InvalidDataException("Native592BC0 requires its original raw0x24-byte task.");
        }
        int counter = unchecked((short)task.ReadWord(0x0C));
        int intensity = counter < 4 ? (255 * counter / 4) & 0xFF : counter < 8 ? 255 : 255 - ((255 * (counter - 8) / 16) & 0xFF);
        NativeBattleGfGeometryBatch batch = Emit592C60(intensity, intensity, intensity, 0, tilePrecision, drawModePrecision);
        if ((graphicsFlags & 0x201) != 0)
        {
            return new(task, batch, 0);
        }
        task = task.WriteWord(0x0C, unchecked((ushort)(task.ReadWord(0x0C) + 1)));
        return new(task, batch, unchecked((short)task.ReadWord(0x0C)) >= 24 ? 2 : 0);
    }

    public static NativeBattleGfGeometryBatch Emit592C60(int red, int green, int blue, int orderingOffset, ImmutableArray<int> tilePrecision = default, ImmutableArray<int> drawModePrecision = default)
    {
        int bucket = unchecked(17 + orderingOffset);
        uint color = unchecked((byte)red | (uint)(byte)green << 8 | (uint)(byte)blue << 16);
        NativeBattleGfGeometryAttempt tile = Packet(0, bucket, [0x03000000, 0x62000000 | color, 0, 0x06C00A00], tilePrecision);
        NativeBattleGfGeometryAttempt mode = Packet(16, bucket, [0x01000000, 0xE1000220], drawModePrecision);
        return new([tile, mode], 24);
    }

    private static NativeBattleGfGeometryAttempt Packet(int offset, int bucket, ImmutableArray<uint> words, ImmutableArray<int> precision)
    {
        if (!precision.IsDefaultOrEmpty && precision.Length != 4)
        {
            throw new InvalidDataException("Native592C60 requires retained precision DWORDs or unknown precision.");
        }
        byte[] bytes = new byte[words.Length * 4];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, bytes.Length).ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(bucket, words, precision.IsDefault ? [] : precision) { PhysicalWriteMask = mask };
        return new(offset, bytes.ToImmutableArray(), mask, primitive);
    }
}
