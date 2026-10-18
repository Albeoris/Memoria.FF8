using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco98NodeResourcesReader
{
    public static NativeBattleChoco97NodeResource Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleChoco97NodeResource resource = new(NativeBattleChoco98Nodes.StaticAddress, memory.Read(NativeBattleChoco98Nodes.StaticAddress, NativeBattleChoco98Nodes.StaticLength).ToImmutableArray());
        NativeBattleChoco97NodeResourcesReader.Validate(resource);
        return resource;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleChoco97NodeResource? nativeChoco98Node;

    public NativeBattleChoco97NodeResource ReadNativeChoco98NodeResource() => nativeChoco98Node ??= NativeBattleChoco98NodeResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
