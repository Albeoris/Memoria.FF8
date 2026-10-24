using System.Collections.Immutable;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect54Profile;
using static Memoria.FF8.Runtime.NativeBattleEffect54Resources;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect54ResourcesReader
{
    public static NativeBattleEffect54Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> source = memory.Read(SourceAddress, SourceLength).ToImmutableArray();
        ImmutableDictionary<uint, NativeBattleModuleResource> sprites = SpriteAddresses.Select((address, index) => new NativeBattleModuleResource(0, address, 0, 0, source.AsSpan(checked((int)(address - SourceAddress)), SpriteLengths[index]).ToArray().ToImmutableArray())).ToImmutableDictionary(sprite => sprite.Address);
        ImmutableDictionary<uint, ImmutableArray<byte>> fronts = StaticRanges.ToImmutableDictionary(range => range.Address, range => memory.Read(range.Address, range.Length).ToImmutableArray()).Add(CameraAddress, memory.Read(CameraAddress, 176).ToImmutableArray());
        NativeBattleEffect54Resources result = new(mainTim, new("effect54-camera", fronts[CameraAddress]), new("effect54-source", source), sprites, fronts);
        result.Validate();
        return result;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect54Resources? nativeEffect54Resources;

    public NativeBattleEffect54Resources ReadNativeEffect54Resources()
    {
        if (nativeEffect54Resources is null)
        {
            byte[] executable = ReadNativeExecutable();
            using IFlArchive magic = OpenArchive("magic");
            nativeEffect54Resources = NativeBattleEffect54ResourcesReader.Read(executable, new("mag053.tim", Read(magic, "mag053.tim").ToImmutableArray()));
        }
        return nativeEffect54Resources;
    }
}
