using System.Collections.Immutable;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect321Resources;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect321ResourcesReader
{
    public static NativeBattleEffect321Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim)
    {
        NativeExecutableMemory memory = new(executable);
        Dictionary<uint, ImmutableArray<byte>> fronts = StaticRanges.ToDictionary(range => range.Address, range => Bytes(range.Address, range.Length));
        fronts.Add(0xCD1284, Bytes(0xCD1284, 268));
        fronts.Add(0xCD29DC, Bytes(0xCD29DC, 276));
        ImmutableDictionary<uint, NativeBattleStaticMeshResource> meshes = MeshAddresses.Select((address, index) => new NativeBattleStaticMeshResource(0, address, Bytes(address, MeshLengths[index]))).ToImmutableDictionary(source => source.Address);
        foreach (NativeBattleStaticMeshResource source in meshes.Values)
        {
            fronts.Add(source.Address, source.Bytes);
        }
        ImmutableDictionary<uint, NativeBattleModuleResource> sprites = SpriteAddresses.Select((address, index) => new NativeBattleModuleResource(0, address, 0, 0, Bytes(address, SpriteLengths[index]))).ToImmutableDictionary(source => source.Address);
        foreach (NativeBattleModuleResource source in sprites.Values)
        {
            fronts.Add(source.Address, source.Bytes);
        }
        NativeBattleEffect321Resources result = new(mainTim, new("effect321-camera", fronts[0xCD1284]), meshes, sprites, new("effect321-vibration", fronts[0xCD29DC]), fronts.ToImmutableDictionary());
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect321Resources? nativeEffect321Resources;

    public NativeBattleEffect321Resources ReadNativeEffect321Resources()
    {
        if (nativeEffect321Resources is null)
        {
            using IFlArchive magic = OpenArchive("magic");
            nativeEffect321Resources = NativeBattleEffect321ResourcesReader.Read(ReadNativeExecutable(), new("mag320.tim", Read(magic, "mag320.tim").ToImmutableArray()));
        }
        return nativeEffect321Resources;
    }
}
