using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleDefensiveResourceReader
{
    public static ImmutableArray<NativeBattleDefensiveResource> Read(byte[] executable)
    {
        return Read(new NativeExecutableMemory(executable));
    }

    public static ImmutableArray<NativeBattleDefensiveResource> Read(IGameNativeMemorySource memory)
    {
        ArgumentNullException.ThrowIfNull(memory);
        if (!NativeFieldRuntimeProfile.IsSupportedImage(memory.SourceHash))
        {
            throw new NotSupportedException("Native defensive meshes require an authenticated English runtime image.");
        }
        ImmutableArray<NativeBattleDefensiveResource>.Builder result = ImmutableArray.CreateBuilder<NativeBattleDefensiveResource>(3);
        for (byte variant = 0; variant < 3; variant++)
        {
            uint address = U32(0xC81698 + variant * 4u);
            int cursor = checked((int)U32(address));
            int vertices = checked((int)U32(address + 4));
            if (cursor != 8 + vertices * 8)
            {
                throw new InvalidDataException("Native defensive mesh vertex directory differs from its original WORD vectors.");
            }
            int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
            for (int group = 0; group < strides.Length; group++)
            {
                uint count = U32(address + (uint)cursor);
                if (count > 4096 || count != 0 && group is not (4 or 5))
                {
                    throw new NotSupportedException("Native defensive mesh requires an unsupported original primitive group.");
                }
                cursor = checked(cursor + 4 + (int)count * strides[group]);
            }
            result.Add(new(variant, address, Bytes(address, cursor).ToArray().ToImmutableArray()));
        }
        return result.MoveToImmutable();

        ReadOnlySpan<byte> Bytes(uint address, int count)
        {
            return memory.Read(address, count);
        }

        uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Bytes(address, 4));
    }
}

public sealed partial class GameAssets
{
    private ImmutableArray<NativeBattleDefensiveResource> nativeBattleDefensiveResources;

    public ImmutableArray<NativeBattleDefensiveResource> ReadNativeBattleDefensiveResources()
    {
        if (nativeBattleDefensiveResources.IsDefault)
        {
            _ = GameDataResources.RequireNativeRuntimeProfile(resources);
            nativeBattleDefensiveResources = NativeBattleDefensiveResourceReader.Read(ReadNativeMemory());
        }
        return nativeBattleDefensiveResources;
    }
}
