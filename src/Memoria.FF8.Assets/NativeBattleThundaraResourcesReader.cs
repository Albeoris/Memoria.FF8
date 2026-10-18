using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleThundaraResourcesReader
{
    public static NativeBattleThundaraResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        uint[] addresses = [0x131E760, 0x131E878, 0x131EABC, 0x131ECB0, 0x131EECC, 0x131F0E8, 0x131F244, 0x131F458, 0x131F5B4, 0x131FA68, 0x131FF74];
        ImmutableArray<NativeBattleModuleResource> sprites = addresses.Select(address => NativeBattleEffectStaticResourcesReader.ReadSprite(memory, address)).ToImmutableArray();
        return new(new("FF8_EN:1321588", NativeBattleEffectStaticResourcesReader.Read(memory, 0x1321588, 0x650)), sprites, NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x13200D0), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x13207B8), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x1320EA0), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x1321BD8, 4)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x1321BDC, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleThundaraResources? nativeThundaraResources;
    public NativeBattleThundaraResources ReadNativeThundaraResources() => nativeThundaraResources ??= NativeBattleThundaraResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
