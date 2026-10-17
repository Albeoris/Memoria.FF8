namespace Memoria.FF8.Runtime;

/// <summary>Authenticated physical zero storage outside the native field record and resource write extents.</summary>
public sealed record NativeActorInfoArenaDefinition(int Version, string ExecutableHash, int MaximumDeclaredRecords, int KernelBytes, int WorldTableBytes)
{
    private const int ArenaAddress = 0x188B818;
    private const int BodyBytes = 612;
    private const int LargestPrefixRecordBytes = 436;
    private const int ZeroStartAddress = 0x18B1B44;
    private const int ZeroEndAddress = 0x18CCE42;
    private const int KernelAddress = 0x188B018;
    private const int WorldTableAddress = 0x1893018 - 1728;

    public void Validate()
    {
        if (Version != 1 || !NativeFieldRuntimeProfile.IsSupportedImage(ExecutableHash) || MaximumDeclaredRecords is < 1 or > 255 || KernelBytes <= 0 || (long)KernelAddress + KernelBytes > ZeroStartAddress || WorldTableBytes <= 0 || (long)WorldTableAddress + WorldTableBytes > ZeroStartAddress)
        {
            throw new InvalidDataException("Native actor-info arena requires the authenticated executable, bounded declared records and resource writes below its physical zero range.");
        }
    }

    public bool TryReadZero(int prefixBytes, byte bodyIndex, int bodyOffset, int width, out int raw)
    {
        Validate();
        if (prefixBytes < 0 || prefixBytes > 255 * LargestPrefixRecordBytes || prefixBytes > MaximumDeclaredRecords * LargestPrefixRecordBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(prefixBytes), "The field prefix exceeds the authenticated declared-record extent.");
        }
        if (width is not (1 or 2 or 4))
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }
        if (bodyOffset < 0 || bodyOffset > BodyBytes - width)
        {
            throw new ArgumentOutOfRangeException(nameof(bodyOffset));
        }
        long address = (long)ArenaAddress + prefixBytes + BodyBytes * bodyIndex + bodyOffset;
        raw = 0;
        return address >= ZeroStartAddress && address + width <= ZeroEndAddress;
    }
}
