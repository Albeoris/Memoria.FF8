using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleStorm11ResourcesReader
{
    public static NativeBattleStorm11Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> stream = Read(0x1365720, 0x2588);
        if (BinaryPrimitives.ReadUInt32LittleEndian(stream.AsSpan()) != 0x9A8 || BinaryPrimitives.ReadUInt32LittleEndian(stream.AsSpan().Slice(4)) != 308 || BinaryPrimitives.ReadUInt32LittleEndian(stream.AsSpan().Slice(0x9B4)) != 128 || BinaryPrimitives.ReadUInt32LittleEndian(stream.AsSpan().Slice(0x15C4)) != 112)
        {
            throw new InvalidDataException("Native Storm Breath requires its complete two-list quad stream model.");
        }
        return new(stream, new(0x13693D0, Read(0x13693D0, 0x25A0)), new(0x13686C4, Read(0x13686C4, 0xD0C)), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x1368010).Bytes, NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x1367CA8).Bytes, Words(0x136BAFC, 10), Words(0x136BB24, 3), new uint[] { 0x1368200, 0x1368580, 0x136843C, 0x1368360 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadSprite(memory, address)).ToImmutableArray(), new("FF8_EN:136B974", Read(0x136B974, 0x188)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x136B970, 4)));

        ImmutableArray<byte> Read(uint address, int length)
        {
            return NativeBattleEffectStaticResourcesReader.Read(memory, address, length);
        }

        ImmutableArray<uint> Words(uint address, int count)
        {
            ImmutableArray<byte> bytes = Read(address, checked(count * 4));
            return Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(index * 4))).ToImmutableArray();
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleStorm11Resources? nativeStorm11Resources;

    public NativeBattleStorm11Resources ReadNativeStorm11Resources()
    {
        return nativeStorm11Resources ??= NativeBattleStorm11ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }
}
