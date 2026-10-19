using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleDuel274ResourcesReader
{
    public static NativeBattleDuel274Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleSearchSpriteResource> sprites = new uint[] { 0x10E778C, 0x10E7958, 0x10E7A34, 0x10E7ADC, 0x10E7C54 }.Select(address => new NativeBattleSearchSpriteResource(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        NativeBattleDuel274Resources result = new(memory.Read(NativeBattleDuel274Resources.Address, checked((int)(NativeBattleDuel274Resources.EndAddress - NativeBattleDuel274Resources.Address))).ToImmutableArray(), sprites);
        result.Validate();
        return result;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleDuel274Resources? nativeDuel274Resources;

    public NativeBattleDuel274Resources ReadNativeDuel274Resources() => nativeDuel274Resources ??= NativeBattleDuel274ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
