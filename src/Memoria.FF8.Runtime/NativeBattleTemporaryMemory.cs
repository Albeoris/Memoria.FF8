namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTemporaryAllocation(NativeBattleFileStaging Staging, uint Address);

/// <summary>5082B0/5082D0 retain released bytes in401000's shared physical memory.</summary>
public static class NativeBattleTemporaryMemory
{
    public const uint Address = 0x1A77238;
    public const uint Cursor = 0x1D999C4;
    public const int ReachedLength = 532;

    public static NativeBattleFileStaging Reset5082F0(NativeBattleFileStaging staging) => WriteCursor(staging, Address);

    public static NativeBattleTemporaryAllocation Allocate5082B0(NativeBattleFileStaging staging, int size)
    {
        int length = Aligned(size);
        uint address = staging.ReadUInt32(Cursor);
        if (address < Address || address > uint.MaxValue - (uint)length)
        {
            throw new InvalidDataException("Native5082B0 requires its initialized physical cursor.");
        }
        return new(WriteCursor(staging, address + (uint)length), address);
    }

    public static NativeBattleFileStaging Release5082D0(NativeBattleFileStaging staging, int size)
    {
        int length = Aligned(size);
        uint cursor = staging.ReadUInt32(Cursor);
        if (cursor < Address || cursor - Address < length)
        {
            throw new InvalidDataException("Native5082D0 cannot release below its physical scratch origin.");
        }
        return WriteCursor(staging, cursor - (uint)length);
    }

    public static NativeBattleEffectArena ReadView(NativeBattleFileStaging staging, uint address, int length)
    {
        ValidateViewRange(staging, address, length);
        return CopyToArenaValidated(staging, address, NativeBattleEffectArena.CreateUnknown(length), 0, length);
    }

    internal static NativeBattleEffectArena CopyToArena(NativeBattleFileStaging staging, uint address, NativeBattleEffectArena destination, int destinationOffset, int length)
    {
        ValidateViewRange(staging, address, length);
        return CopyToArenaValidated(staging, address, destination, destinationOffset, length);
    }

    private static void ValidateViewRange(NativeBattleFileStaging staging, uint address, int length)
    {
        if (length < 0 || length > NativeBattleEffectArena.Length || (ulong)address + (uint)length > (ulong)uint.MaxValue + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        staging.Validate();
    }

    private static NativeBattleEffectArena CopyToArenaValidated(NativeBattleFileStaging staging, uint address, NativeBattleEffectArena destination, int destinationOffset, int length)
    {
        Span<byte> data = length <= 512 ? stackalloc byte[length] : new byte[length];
        Span<bool> known = length <= 512 ? stackalloc bool[length] : new bool[length];
        staging.ReadWithKnownness(address, data, known);
        return destination.WriteWithKnownness(destinationOffset, data, known);
    }

    public static NativeBattleFileStaging PublishView(NativeBattleFileStaging staging, uint address, NativeBattleEffectArena view, int length)
    {
        if (length < 0 || length > NativeBattleEffectArena.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        (byte[] data, bool[] known) = view.ReadSnapshot(0, length);
        return staging.WriteWithKnownness(address, data, known);
    }

    private static int Aligned(int size)
    {
        if (size < 0 || size > int.MaxValue - 3)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }
        return (size + 3) & ~3;
    }

    private static NativeBattleFileStaging WriteCursor(NativeBattleFileStaging staging, uint cursor)
    {
        return staging.WriteUInt32(Cursor, cursor);
    }
}
