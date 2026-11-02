using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLoaderZeroRegion(uint Address, int Length);

public sealed partial record NativeBattleFileStaging
{
    public ImmutableArray<NativeBattleLoaderZeroRegion> LoaderZeroRanges
    {
        get => Transaction is { } transaction ? transaction.CaptureLoaderZeros() : owner?.CaptureLoaderZeros() ?? loaderZeroRanges;
        init
        {
            RequireInitializer();
            loaderZeroRanges = value;
        }
    }

    public static NativeBattleFileStaging FromColdProcess(string executableHash = NativeFieldRuntimeProfile.FF8EnExecutableHash)
    {
        NativeFieldRegistrationLayout layout = NativeFieldRegistrationLayout.ForImage(executableHash, NativeFieldRegistrationEntry.FieldEntry);
        ImmutableArray<NativeBattleLoaderZeroRegion> zeros = [new(0x188C000, 0xF13000)];
        if (executableHash == NativeFieldRuntimeProfile.RemasteredEnImageHash)
        {
            zeros = zeros.Add(new(layout.ArenaAddress, checked((int)(layout.ArenaEnd - layout.ArenaAddress))));
        }
        return new([]) { BaseAddress = 0x188C000, LoaderZeroRanges = zeros };
    }

    private bool IntersectsLoaderZero(uint address, int length)
    {
        if (Transaction is { } transaction)
        {
            return transaction.IntersectsLoaderZero(address, length);
        }
        if (!snapshot)
        {
            return RequireOwner().IntersectsLoaderZero(address, length);
        }
        return length != 0 && loaderZeroRanges.Any(region => address < (ulong)region.Address + (uint)region.Length && region.Address < (ulong)address + (uint)length);
    }

    private bool HasKnownStorage(uint address, int length) => IsKnown(address, length);

    internal NativeBattleFileStaging ForgetLoaderZero(uint address, int length)
    {
        RequireWritableRange(address, length);
        if (Transaction is { } transaction)
        {
            transaction.ForgetLoaderZero(address, length);
        }
        else
        {
            RequireOwner().ForgetLoaderZero(address, length);
        }
        return this;
    }
}
