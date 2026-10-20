using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleStop119ResourcesReader
{
    public static NativeBattleStop119Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new(new(0x127DB5C, NativeBattleEffectStaticResourcesReader.Read(memory, 0x127DB5C, 0x92A8)), new("FF8_EN:1286E04", NativeBattleEffectStaticResourcesReader.Read(memory, 0x1286E04, 0xE30)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x1287C34, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleStop119Resources? nativeStop119Resources;
    public NativeBattleStop119Resources ReadNativeStop119Resources() => nativeStop119Resources ??= NativeBattleStop119ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
