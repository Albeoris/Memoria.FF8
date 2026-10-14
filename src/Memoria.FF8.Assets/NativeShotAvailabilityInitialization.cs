using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeShotAvailabilityInitialization
{
    public static NativeShotAvailabilityState Read(byte[] initial, byte[] kernel)
    {
        if (initial.Length != 2812 || kernel.Length < 228 || BinaryPrimitives.ReadUInt32LittleEndian(kernel) != 56)
        {
            throw new InvalidDataException("Native shot availability requires the complete original New Game and kernel structures.");
        }
        int start = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(kernel.AsSpan(4 + 4 * 21)));
        int end = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(kernel.AsSpan(8 + 4 * 21)));
        if (start < 228 || end > kernel.Length || end - start != 8 * 24)
        {
            throw new InvalidDataException("Native shot availability requires all eight original 24-byte Irvine limit records.");
        }
        ImmutableArray<byte> items = Enumerable.Range(0, 8).Select(row => kernel[start + 24 * row + 18]).ToImmutableArray();
        //56DA10 copies init.out to1CFDCA8; source2760 produces saved BYTE1CFE770.
        //Higher bytes have distinct world/battle writers and are deliberately not constructed here.
        NativeShotAvailabilityState state = new(initial[2760], items, Convert.ToHexString(SHA256.HashData(initial)), Convert.ToHexString(SHA256.HashData(kernel)));
        state.Validate();
        return state;
    }
}
