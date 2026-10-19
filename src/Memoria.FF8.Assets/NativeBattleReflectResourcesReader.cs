using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleReflectResourcesReader
{
    public static NativeBattleReflectResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new(new("FF8_EN:12FC200", NativeBattleEffectStaticResourcesReader.Read(memory, 0x12FC200, 0x7B0)), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x12F9B80u), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x12FBAA0u), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x12FBB78u), NativeBattleEffectStaticResourcesReader.Read(memory, 0x12FC9B4, 0x4000), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x12FC9B0, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleReflectResources? nativeReflectResources;

    public NativeBattleReflectResources ReadNativeReflectResources()
    {
        return nativeReflectResources ??= NativeBattleReflectResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }
}
