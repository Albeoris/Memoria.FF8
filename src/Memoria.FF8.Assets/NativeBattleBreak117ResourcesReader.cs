using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleBreak117ResourcesReader
{
    public static NativeBattleBreak117Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new(new(0x128E2D0, NativeBattleEffectStaticResourcesReader.Read(memory, 0x128E2D0, 0x9658)), NativeBattleEffectStaticResourcesReader.Read(memory, 0x1297928, 0x1D8), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0x1297B00), new("FF8_EN:1297C78", NativeBattleEffectStaticResourcesReader.Read(memory, 0x1297C78, 0xFE0)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x1298C58, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleBreak117Resources? nativeBreak117Resources;
    public NativeBattleBreak117Resources ReadNativeBreak117Resources() => nativeBreak117Resources ??= NativeBattleBreak117ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
