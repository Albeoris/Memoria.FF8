using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleFiragaResourcesReader
{
    public static NativeBattleFiragaResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new(new("FF8_EN:DD9A4C", NativeBattleEffectStaticResourcesReader.Read(memory, 0xDD9A4C, 0xB48)), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xDDA594), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xDDA740), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xDDC578), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xDDCE48), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xDDD208), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xDDD8F0, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleFiragaResources? nativeFiragaResources;
    public NativeBattleFiragaResources ReadNativeFiragaResources() => nativeFiragaResources ??= NativeBattleFiragaResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
