using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleAbsorb56ResourcesReader
{
    public static NativeBattleDrain39Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x15A38C4, 15)) != "mag038-055.tim\0")
        {
            throw new InvalidDataException("Absorb requires its original shared texture filename.");
        }
        ImmutableArray<byte> bank = memory.Read(0x159EF34, 0x4258).ToImmutableArray();
        if (BinaryPrimitives.ReadUInt32LittleEndian(bank.AsSpan().Slice(0x4154)) != 1)
        {
            throw new InvalidDataException("Absorb requires its original absolute-pointer directory without relocation.");
        }
        ImmutableArray<uint> Program(uint address, int count) => Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address + (uint)index * 4, 4))).ToImmutableArray();
        return new(new("FF8_EN:15A318C", memory.Read(0x15A318C, 0x738).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x159EF30, 4)), bank, Program(0x159EF04, 3), Program(0x159EF10, 8)) { Effect = 56, Channels = memory.ReadLoader(0x260F144, 0x1E60).ToImmutableArray() };
    }
}

public sealed partial class GameAssets
{
    private NativeBattleDrain39Resources? nativeAbsorb56Resources;

    public NativeBattleDrain39Resources ReadNativeAbsorb56Resources()
    {
        return nativeAbsorb56Resources ??= NativeBattleAbsorb56ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }
}
