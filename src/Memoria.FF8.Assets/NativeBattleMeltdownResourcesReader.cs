using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleMeltdownResourcesReader
{
    public static NativeBattleMeltdownResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new(new("FF8_EN:DC9C28", NativeBattleEffectStaticResourcesReader.Read(memory, 0xDC9C28, 0xDFC)), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xDCDB4Cu), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xDCADB4u), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xDCCDA4u), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xDCABEC), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xDCAA24), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xDCE74C, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleMeltdownResources? nativeMeltdownResources;

    public NativeBattleMeltdownResources ReadNativeMeltdownResources()
    {
        return nativeMeltdownResources ??= NativeBattleMeltdownResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }
}
