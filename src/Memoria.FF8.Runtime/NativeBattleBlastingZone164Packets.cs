using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleBlastingZone164Packets
{
    public static NativeBattleVector ReadSelectedVertex(NativeBattleFileStaging staging, uint selected, int byteOffset)
    {
        if (byteOffset < 0)
        {
            throw new InvalidDataException("Native selected vertex requires its actual nonnegative byte displacement.");
        }
        byte[] bytes = staging.Read(checked(selected + (uint)byteOffset), 6);
        return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
    }
}
