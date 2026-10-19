using System.Collections.Immutable;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleDuel212ResourcesReader
{
    public static NativeBattleDuel212Resources Read(byte[] executable, NativeBattleEffectHeapAllocation initialTim, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleSearchSpriteResource> sprites = new uint[] { 0x11823C8, 0x1182534 }.Select(address => new NativeBattleSearchSpriteResource(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0x11826E0, 0x1183FA4, 0x1184174, 0x11843DC, 0x1184694 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray();
        NativeBattleDuel212Resources result = new(memory.Read(NativeBattleDuel212Resources.Address, checked((int)(NativeBattleDuel212Resources.EndAddress - NativeBattleDuel212Resources.Address))).ToImmutableArray(), memory.ReadLoader(0x2510258, 0x68).ToImmutableArray(), memory.Read(0x11AC200, 80).ToImmutableArray(), sprites, meshes, initialTim) { Files = files };
        result.Validate();
        return result;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleDuel212Resources? nativeDuel212Resources;

    public NativeBattleDuel212Resources ReadNativeDuel212Resources()
    {
        if (nativeDuel212Resources is null)
        {
            using IFlArchive magic = OpenArchive("magic");
            NativeBattleEffectHeapAllocation initializer = new("mag211.tim", Read(magic, "mag211.tim").ToImmutableArray());
            using IFlArchive battle = OpenArchive("battle");
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleEffectFileDirectoryReader.Read(executable, new short[] { 360, 361 }).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeDuel212Resources = NativeBattleDuel212ResourcesReader.Read(executable, initializer, files);
        }
        return nativeDuel212Resources;
    }
}
