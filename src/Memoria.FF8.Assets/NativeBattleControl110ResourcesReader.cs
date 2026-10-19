using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleControl110ResourcesReader
{
    public static NativeBattleControl110Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> Mesh(uint address) => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address).Bytes;
        ImmutableArray<NativeBattleModuleResource> sprites = new uint[] { 0x12E3290, 0x12E3130 }.Select(address => new NativeBattleModuleResource(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        return new(Mesh(0x12E0650), Mesh(0x12E2BD8), Mesh(0x12E2F40), sprites, memory.ReadLoader(0x2521A50, 20).ToImmutableArray(), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x12E336C, 4)));
    }
}
public sealed partial class GameAssets
{
    private NativeBattleControl110Resources? nativeControl110Resources;
    public NativeBattleControl110Resources ReadNativeControl110Resources() => nativeControl110Resources ??= NativeBattleControl110ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
