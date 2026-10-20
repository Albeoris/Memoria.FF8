using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRegen147ResourcesReader
{
    public static NativeBattleRegen147Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new(new("FF8_EN:DCE760", NativeBattleEffectStaticResourcesReader.Read(memory, 0xDCE760, 0x870)), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xDCFC64), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xDCEFD0), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xDD05CC, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleRegen147Resources? nativeRegen147Resources;
    public NativeBattleRegen147Resources ReadNativeRegen147Resources() => nativeRegen147Resources ??= NativeBattleRegen147ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
