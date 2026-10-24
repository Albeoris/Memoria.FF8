using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLoaderZeroRegion(uint Address, int Length);

public sealed partial record NativeBattleFileStaging
{
    public ImmutableArray<NativeBattleLoaderZeroRegion> LoaderZeroRanges { get; init; } = [];

    public static NativeBattleFileStaging FromColdProcess() => new([]) { BaseAddress = 0x188C000, LoaderZeroRanges = [new(0x188C000, 0xF13000)] };

    private void ValidateLoaderZeroRanges()
    {
        if (LoaderZeroRanges.IsDefault)
        {
            throw new InvalidDataException("Native loaded memory requires an explicit loader-zero range collection.");
        }
        ulong previousEnd = 0;
        foreach (NativeBattleLoaderZeroRegion region in LoaderZeroRanges)
        {
            ulong end = (ulong)region.Address + (uint)region.Length;
            if (region.Length <= 0 || end > (ulong)uint.MaxValue + 1 || region.Address < previousEnd)
            {
                throw new InvalidDataException("Native loader-zero ranges must be positive, ordered and disjoint physical intervals.");
            }
            previousEnd = end;
        }
    }

    private bool IntersectsLoaderZero(uint address, int length) => length > 0 && LoaderZeroRanges.Any(region => address < (ulong)region.Address + (uint)region.Length && region.Address < (ulong)address + (uint)length);

    private bool HasKnownStorage(uint address, int length)
    {
        ulong cursor = address;
        ulong end = cursor + (uint)length;
        int explicitIndex = 0;
        int zeroIndex = 0;
        while (explicitIndex < KnownRanges.Length || zeroIndex < LoaderZeroRanges.Length)
        {
            ulong start;
            ulong last;
            if (explicitIndex < KnownRanges.Length && (zeroIndex == LoaderZeroRanges.Length || (ulong)BaseAddress + (uint)KnownRanges[explicitIndex].Offset <= LoaderZeroRanges[zeroIndex].Address))
            {
                NativeBattleEffectMemoryRegion region = KnownRanges[explicitIndex++];
                start = (ulong)BaseAddress + (uint)region.Offset;
                last = start + (uint)region.Data.Length;
            }
            else
            {
                NativeBattleLoaderZeroRegion region = LoaderZeroRanges[zeroIndex++];
                start = region.Address;
                last = start + (uint)region.Length;
            }
            if (last <= cursor)
            {
                continue;
            }
            if (start > cursor)
            {
                return false;
            }
            cursor = last;
            if (cursor >= end)
            {
                return true;
            }
        }
        return length == 0;
    }

    internal NativeBattleFileStaging ForgetLoaderZero(uint address, int length)
    {
        if (length == 0 || LoaderZeroRanges.IsEmpty)
        {
            return this;
        }
        ulong end = (ulong)address + (uint)length;
        ImmutableArray<NativeBattleLoaderZeroRegion>.Builder retained = ImmutableArray.CreateBuilder<NativeBattleLoaderZeroRegion>();
        foreach (NativeBattleLoaderZeroRegion region in LoaderZeroRanges)
        {
            ulong regionEnd = (ulong)region.Address + (uint)region.Length;
            if (regionEnd <= address || region.Address >= end)
            {
                retained.Add(region);
                continue;
            }
            if (region.Address < address)
            {
                retained.Add(new(region.Address, checked((int)(address - region.Address))));
            }
            if (regionEnd > end)
            {
                retained.Add(new(checked((uint)end), checked((int)(regionEnd - end))));
            }
        }
        return this with { LoaderZeroRanges = retained.ToImmutable() };
    }
}
