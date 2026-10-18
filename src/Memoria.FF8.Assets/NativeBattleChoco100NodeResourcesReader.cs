using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco100NodeResourcesReader
{
    public static ImmutableArray<NativeBattleChoco97NodeResource> Read(byte[] executable, NativeBattleChoco100IoResources io)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleChoco97NodeResource first = new(NativeBattleChoco100Nodes.StaticAddress, memory.Read(NativeBattleChoco100Nodes.StaticAddress, NativeBattleChoco100Nodes.StaticLength).ToImmutableArray());
        NativeBattleChoco97NodeResource second = Loaded(495, NativeBattleChoco100Nodes.Loaded495Address, NativeBattleChoco100Nodes.Loaded495Length);
        NativeBattleChoco97NodeResource third = Loaded(501, NativeBattleChoco100Nodes.Loaded501Address, NativeBattleChoco100Nodes.Loaded501Length);
        NativeBattleChoco97NodeResourcesReader.Validate(first);
        NativeBattleChoco97NodeResourcesReader.Validate(second);
        NativeBattleChoco97NodeResourcesReader.Validate(third);
        return [first, second, third];

        NativeBattleChoco97NodeResource Loaded(short fileId, uint address, int length)
        {
            NativeBattleEffectFileAsset[] files = io.Files.Where(file => file.FileId == fileId).ToArray();
            if (files.Length != 1 || files[0].Data.IsDefault || files[0].Data.Length != length)
            {
                throw new InvalidDataException("Native Choco nodes require their authenticated complete IO source.");
            }
            return new(address, files[0].Data);
        }
    }
}

public sealed partial class GameAssets
{
    private ImmutableArray<NativeBattleChoco97NodeResource> nativeChoco100Nodes;

    public ImmutableArray<NativeBattleChoco97NodeResource> ReadNativeChoco100NodeResources()
    {
        if (nativeChoco100Nodes.IsDefault)
        {
            nativeChoco100Nodes = NativeBattleChoco100NodeResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources), ReadNativeChoco100IoResources());
        }
        return nativeChoco100Nodes;
    }
}
