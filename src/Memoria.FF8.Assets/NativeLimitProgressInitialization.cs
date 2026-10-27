using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeLimitProgressInitialization
{
    public static NativeLimitProgress Read(byte[] initial)
    {
        NativeInitialData.Validate(initial);
        //56DA10 copies init.out to1CFDCA8.48CCE0/4B0280/52B320 read these saved owners directly.
        //56D9F0 clears saved WORD1CFE978; the following init.out supported copy ends no later than1CFE928 and leaves it intact.
        NativeLimitProgress result = new(Convert.ToHexString(SHA256.HashData(initial)), BinaryPrimitives.ReadUInt16LittleEndian(initial.AsSpan(2756)), BinaryPrimitives.ReadUInt16LittleEndian(initial.AsSpan(2758)), initial[2762], initial[2747], initial.AsSpan(2764, 8).ToArray().ToImmutableArray()) { SpecialMagicFlags = initial[2761], PetPalsFlags = initial[2763], Options978 = 0 };
        result.Validate();
        return result;
    }
}
