using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleQuake38ResourcesReader
{
    public static NativeBattleQuake38Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new(new("FF8_EN:18295EC", Bytes(0x18295EC, 0x29020)), new("FF8_EN:1642D70", Bytes(0x1642D70, 0x4A8)), Sprite(0x1643218), Sprite(0x164335C), Sprite(0x16434A0), Bytes(0x1642568, 0x400), Bytes(0x1642968, 0x400), Words(0x164253C, 14), Bytes(0x1642558, 16), Words(0x164364C, 22), Words(0x1643678, 16), Dword(0x1642D68), Dword(0x1642D6C));

        ImmutableArray<byte> Bytes(uint address, int length) => NativeBattleEffectStaticResourcesReader.Read(memory, address, length);
        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        ImmutableArray<short> Words(uint address, int count)
        {
            byte[] bytes = memory.Read(address, count * 2);
            return Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2))).ToImmutableArray();
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleQuake38Resources? nativeQuake38Resources;
    public NativeBattleQuake38Resources ReadNativeQuake38Resources() => nativeQuake38Resources ??= NativeBattleQuake38ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
