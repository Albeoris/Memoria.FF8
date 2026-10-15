using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Verified installed English executable tables consumed by47CA90 and541C80.</summary>
public sealed record NativeEncounterTables(ImmutableArray<byte> FieldRandom, ImmutableArray<byte> WorldRandom, ImmutableArray<byte> WorldWeights)
{
    public static NativeEncounterTables ReadExecutable(byte[] executable)
    {
        if (Convert.ToHexString(SHA256.HashData(executable)) != "064D466B5FE2BA901FD44ABF19F37C0FD6A2DB40AABD95C9E5959195B6589570")
        {
            throw new NotSupportedException("Native encounter tables require the authenticated original English executable.");
        }
        return new(executable.AsSpan(0x780A18, 256).ToArray().ToImmutableArray(), executable.AsSpan(0x875D20, 256).ToArray().ToImmutableArray(), executable.AsSpan(0x875F10, 8).ToArray().ToImmutableArray());
    }

    public static NativeFieldEncounterDefinition ReadField(byte[] rate, byte[] formations)
    {
        if (rate.Length < 1 || formations.Length < 8)
        {
            throw new InvalidDataException("Original field encounters require RAT byte0 and four MRT formation WORDs.");
        }
        ImmutableArray<ushort>.Builder values = ImmutableArray.CreateBuilder<ushort>(4);
        for (int index = 0; index < 4; index++)
        {
            values.Add(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(formations.AsSpan(index * 2, 2)));
        }
        return new(rate[0], values.MoveToImmutable());
    }
}
