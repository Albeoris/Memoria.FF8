using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleDuel112ResourcesReader
{
    public static NativeBattleDuel112Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleSearchSpriteResource> sprites = new uint[] { 0x12C5650, 0x12C5794 }.Select(address => new NativeBattleSearchSpriteResource(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0x12C5928, 0x12C7230 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray();
        NativeBattleDuel112Resources result = new(memory.Read(NativeBattleDuel112Resources.Address, checked((int)(NativeBattleDuel112Resources.EndAddress - NativeBattleDuel112Resources.Address))).ToImmutableArray(), memory.ReadLoader(NativeBattleDuel112Resources.GlobalsAddress, NativeBattleDuel112Resources.GlobalsLength).ToImmutableArray(), sprites, meshes);
        result.Validate();
        return result;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleDuel112Resources? nativeDuel112Resources;

    public NativeBattleDuel112Resources ReadNativeDuel112Resources() => nativeDuel112Resources ??= NativeBattleDuel112ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
