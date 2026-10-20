using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleDrain39ResourcesReader
{
    public static NativeBattleDrain39Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> bank = memory.Read(NativeBattleDrain39Nodes.BankAddress, NativeBattleDrain39Nodes.BankLength).ToImmutableArray();
        int directory = checked((int)(NativeBattleDrain39Nodes.DirectoryAddress - NativeBattleDrain39Nodes.BankAddress));
        if (BinaryPrimitives.ReadUInt32LittleEndian(bank.AsSpan().Slice(directory)) != 1)
        {
            throw new InvalidDataException("Native Drain requires its preinitialized absolute-pointer node directory.");
        }
        ImmutableArray<uint> Program(uint address, int count) => Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address + (uint)index * 4, 4))).ToImmutableArray();
        return new(new("FF8_EN:15D5B94", memory.Read(0x15D5B94, 0x738).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x15D1938, 4)), bank, Program(0x15D190C, 3), Program(0x15D1918, 8)) { Channels = memory.ReadLoader(NativeBattleDrain39Resources.ChannelsAddress, NativeBattleDrain39Resources.ChannelsLength).ToImmutableArray() };
    }
}

public sealed partial class GameAssets
{
    private NativeBattleDrain39Resources? nativeDrain39Resources;
    public NativeBattleDrain39Resources ReadNativeDrain39Resources() => nativeDrain39Resources ??= NativeBattleDrain39ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
