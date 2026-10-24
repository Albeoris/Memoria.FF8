using System.Collections.Immutable;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect324Resources;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect324ResourcesReader
{
    public static NativeBattleEffect324Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim)
    {
        NativeExecutableMemory memory = new(executable);
        Dictionary<uint, ImmutableArray<byte>> fronts = StaticRanges.ToDictionary(range => range.Address, range => Bytes(range.Address, range.Length));
        fronts.Add(0xCD09F0, Bytes(0xCD09F0, 316));
        fronts.Add(0xCD1104, Bytes(0xCD1104, 268));
        ImmutableDictionary<uint, NativeBattleModuleResource> sprites = SpriteAddresses.Select((address, index) => new NativeBattleModuleResource(0, address, 0, 0, Bytes(address, SpriteLengths[index]))).ToImmutableDictionary(source => source.Address);
        foreach (NativeBattleModuleResource source in sprites.Values)
        {
            fronts.Add(source.Address, source.Bytes);
        }
        NativeBattleEffect324Resources result = new(mainTim, new("effect324-camera", fronts[0xCD09F0]), new("effect324-vibration", fronts[0xCD1104]), sprites, fronts.ToImmutableDictionary());
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect324Resources? nativeEffect324Resources;

    public NativeBattleEffect324Resources ReadNativeEffect324Resources()
    {
        if (nativeEffect324Resources is null)
        {
            using IFlArchive magic = OpenArchive("magic");
            nativeEffect324Resources = NativeBattleEffect324ResourcesReader.Read(ReadNativeExecutable(), new("mag323.tim", Read(magic, "mag323.tim").ToImmutableArray()));
        }
        return nativeEffect324Resources;
    }
}
