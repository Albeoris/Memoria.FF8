using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleCinematicContacts
{
    private const uint HeaderBase = 0x1D280C4;
    private const uint HeaderSize = 20;

    internal static int ReadCount(NativeBattleWaterExecutionFrame frame, uint headerAddress, Func<NativeBattleWaterExecutionFrame, uint, int, byte[]> readActionBytes, NativeBattleActionPacketsState? packets, bool signedNative)
    {
        return ReadCount(headerAddress, (address, length) => ReadBytes(frame, address, length, readActionBytes), packets, signedNative);
    }

    internal static int ReadCount(uint headerAddress, Func<uint, int, byte[]> readActionBytes, NativeBattleActionPacketsState? packets, bool signedNative)
    {
        if (packets?.PresentationRoster is not null)
        {
            uint relative = unchecked(headerAddress - HeaderBase);
            if (relative % HeaderSize != 0 || relative / HeaderSize >= NativeBattleActionPacketsState.HeaderCapacity ||
                packets.Headers[(int)(relative / HeaderSize)] is not { FirstContactIndex: not null, PresentationContactCount: not null } header)
            {
                throw new InvalidDataException("Hosted cinematic contact count requires its prepared action header address.");
            }
            return header.ReadContactCount();
        }
        byte count = readActionBytes(unchecked(headerAddress + 0x10), 1)[0];
        return signedNative ? unchecked((sbyte)count) : count;
    }

    internal static uint Offset(uint first, int ordinal, NativeBattleActionPacketsState? packets)
    {
        return packets?.PresentationRoster is not null
            ? NativeBattleContactAddress.OffsetCursor(first, ordinal, packets.StorageCapacity)
            : unchecked(first + (uint)(NativeBattleContactAddress.RowSize * ordinal));
    }

    internal static byte[] ReadBytes(NativeBattleWaterExecutionFrame frame, uint address, int length, Func<NativeBattleWaterExecutionFrame, uint, int, byte[]> readActionBytes)
    {
        if (address >= NativeBattleEffectNodeMemory.ArenaAddress && (long)address - NativeBattleEffectNodeMemory.ArenaAddress <= NativeBattleEffectArena.Length - length)
        {
            return frame.Arena.Read(ArenaOffset(address, length), length);
        }
        byte[] bytes = readActionBytes(frame, address, length);
        return bytes is not null && bytes.Length == length ? bytes : throw new InvalidDataException("Native cinematic contact read requires its exact addressed action bytes.");
    }
}
