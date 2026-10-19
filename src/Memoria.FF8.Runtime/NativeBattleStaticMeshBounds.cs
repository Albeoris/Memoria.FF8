using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

/// <summary>6D6070's signed extrema over the current eight-byte vertex records.</summary>
public static class NativeBattleStaticMeshBounds
{
    public static NativeBattleEffectLocalBounds Read6D6070(ReadOnlySpan<byte> model)
    {
        if (model.Length < 8)
        {
            throw new InvalidDataException("Native model bounds require their current vertex count.");
        }
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(model[4..]);
        if (count < 2 || count > (model.Length - 8) / 8)
        {
            throw new InvalidDataException("Native model bounds require at least two complete original eight-byte vertices.");
        }
        short minimumX = BinaryPrimitives.ReadInt16LittleEndian(model[8..]);
        short minimumY = BinaryPrimitives.ReadInt16LittleEndian(model[10..]);
        short minimumZ = BinaryPrimitives.ReadInt16LittleEndian(model[12..]);
        short maximumX = minimumX;
        short maximumY = minimumY;
        short maximumZ = minimumZ;
        for (int index = 1; index < count; index++)
        {
            int offset = 8 + index * 8;
            short x = BinaryPrimitives.ReadInt16LittleEndian(model[offset..]);
            short y = BinaryPrimitives.ReadInt16LittleEndian(model[(offset + 2)..]);
            short z = BinaryPrimitives.ReadInt16LittleEndian(model[(offset + 4)..]);
            minimumX = Math.Min(minimumX, x);
            minimumY = Math.Min(minimumY, y);
            minimumZ = Math.Min(minimumZ, z);
            maximumX = Math.Max(maximumX, x);
            maximumY = Math.Max(maximumY, y);
            maximumZ = Math.Max(maximumZ, z);
        }
        return new(new(minimumX, minimumY, minimumZ), new(maximumX, maximumY, maximumZ));
    }
}
