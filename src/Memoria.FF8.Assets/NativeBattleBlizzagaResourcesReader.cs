using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleBlizzagaResourcesReader
{
    public static NativeBattleBlizzagaResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleBlizzagaSeparateMeshResource primary = new(0x1312918, 177, NativeBattleEffectStaticResourcesReader.Read(memory, 0x1312918, 0x2328));
        NativeBattleBlizzagaSeparateMeshResource partner = new(0x1315760, 177, NativeBattleEffectStaticResourcesReader.Read(memory, 0x1315760, 0x1928));
        NativeBattleBlizzagaVertexBank morph = new(0x1314C40, NativeBattleEffectStaticResourcesReader.Read(memory, 0x1314C40, 0x590));
        NativeBattleBlizzagaVertexBank reference = new(0x13151D0, NativeBattleEffectStaticResourcesReader.Read(memory, 0x13151D0, 0x590));
        primary.Validate(6, 28);
        partner.Validate(2, 20);
        morph.Validate();
        reference.Validate();
        return new(new("FF8_EN:1318B78", NativeBattleEffectStaticResourcesReader.Read(memory, 0x1318B78, 0xC30)), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0x1312690), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0x13127A0), primary, partner, morph, reference, NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x1317088), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x1318490), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x13197A8, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleBlizzagaResources? nativeBlizzagaResources;
    public NativeBattleBlizzagaResources ReadNativeBlizzagaResources() => nativeBlizzagaResources ??= NativeBattleBlizzagaResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
