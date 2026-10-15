using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleExperienceDefinitionReader
{
    public static NativeBattleExperienceDefinition Read(byte[] kernel)
    {
        if (kernel.Length < 228 || BinaryPrimitives.ReadUInt32LittleEndian(kernel) != 56)
        {
            throw new InvalidDataException("Battle experience requires the original 56-section kernel.");
        }
        int start = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(kernel.AsSpan(4 + 4 * 14)));
        int end = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(kernel.AsSpan(8 + 4 * 14)));
        if (start < 228 || end > kernel.Length || end - start != 20 * 8)
        {
            throw new InvalidDataException("Battle experience character ability section14 is invalid.");
        }
        uint[] flags = new uint[20];
        for (int index = 0; index < flags.Length; index++)
        {
            ReadOnlySpan<byte> row = kernel.AsSpan(start + 8 * index, 8);
            flags[index] = (uint)(row[5] | row[6] << 8 | row[7] << 16);
        }
        NativeBattleExperienceDefinition definition = new(Convert.ToHexString(SHA256.HashData(kernel)), flags.ToImmutableArray());
        definition.Validate();
        return definition;
    }
}
