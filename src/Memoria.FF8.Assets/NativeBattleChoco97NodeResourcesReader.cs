using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco97NodeResourcesReader
{
    public static ImmutableArray<NativeBattleChoco97NodeResource> Read(byte[] executable, NativeBattleChoco97IoResources io)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleEffectFileAsset[] loaded = io.Files.Where(file => file.FileId == 448).ToArray();
        if (loaded.Length != 1 || loaded[0].Data.IsDefault || loaded[0].Data.Length != NativeBattleChoco97Nodes.LoadedLength)
        {
            throw new InvalidDataException("Native Choco nodes require the authenticated complete file448 IO source.");
        }
        NativeBattleChoco97NodeResource first = new(NativeBattleChoco97Nodes.StaticAddress, memory.Read(NativeBattleChoco97Nodes.StaticAddress, NativeBattleChoco97Nodes.StaticLength).ToImmutableArray());
        NativeBattleChoco97NodeResource second = new(NativeBattleChoco97Nodes.LoadedAddress, loaded[0].Data);
        Validate(first);
        Validate(second);
        return [first, second];
    }

    private static void Validate(NativeBattleChoco97NodeResource resource)
    {
        ImmutableArray<byte> data = resource.Data;
        if (U32(0) != 0)
        {
            throw new InvalidDataException("Native Choco node ingress requires its cold relative-pointer source.");
        }
        for (int table = 0; table < 4; table++)
        {
            for (int index = 0; index < 16; index++)
            {
                uint pointer = U32(4 + table * 64 + index * 4);
                if (pointer == 0)
                {
                    continue;
                }
                if (pointer >= data.Length || table == 0 && pointer > data.Length - 0x158)
                {
                    throw new InvalidDataException("Native Choco node directory leaves its supplied source extent.");
                }
                if (table == 0)
                {
                    for (int field = 0xC8; field <= 0x144; field += 4)
                    {
                        if (U32(checked((int)pointer + field)) >= data.Length)
                        {
                            throw new InvalidDataException("Native Choco node channel leaves its supplied source extent.");
                        }
                    }
                }
            }
        }
        uint U32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(offset));
    }
}

public sealed partial class GameAssets
{
    private ImmutableArray<NativeBattleChoco97NodeResource> nativeChoco97Nodes;

    public ImmutableArray<NativeBattleChoco97NodeResource> ReadNativeChoco97NodeResources()
    {
        if (nativeChoco97Nodes.IsDefault)
        {
            nativeChoco97Nodes = NativeBattleChoco97NodeResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources), ReadNativeChoco97IoResources());
        }
        return nativeChoco97Nodes;
    }
}
