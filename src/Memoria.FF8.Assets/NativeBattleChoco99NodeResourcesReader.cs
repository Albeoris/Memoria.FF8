using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco99NodeResourcesReader
{
    public static ImmutableArray<NativeBattleChoco97NodeResource> Read(byte[] executable, NativeBattleChoco99IoResources io)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleEffectFileAsset[] loaded = io.Files.Where(file => file.FileId == 479).ToArray();
        if (loaded.Length != 1 || loaded[0].Data.IsDefault || loaded[0].Data.Length != NativeBattleChoco99Nodes.LoadedLength)
        {
            throw new InvalidDataException("Native Choco nodes require the authenticated complete file479 IO source.");
        }
        NativeBattleChoco97NodeResource first = new(NativeBattleChoco99Nodes.StaticAddress, memory.Read(NativeBattleChoco99Nodes.StaticAddress, NativeBattleChoco99Nodes.StaticLength).ToImmutableArray());
        NativeBattleChoco97NodeResource second = new(NativeBattleChoco99Nodes.LoadedAddress, loaded[0].Data);
        NativeBattleChoco97NodeResourcesReader.Validate(first);
        NativeBattleChoco97NodeResourcesReader.Validate(second);
        return [first, second];
    }
}

public sealed partial class GameAssets
{
    private ImmutableArray<NativeBattleChoco97NodeResource> nativeChoco99Nodes;

    public ImmutableArray<NativeBattleChoco97NodeResource> ReadNativeChoco99NodeResources()
    {
        if (nativeChoco99Nodes.IsDefault)
        {
            nativeChoco99Nodes = NativeBattleChoco99NodeResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources), ReadNativeChoco99IoResources());
        }
        return nativeChoco99Nodes;
    }
}
