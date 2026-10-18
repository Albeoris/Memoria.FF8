using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleAeroResourcesReader
{
    public static NativeBattleAeroResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleGfAnimationSource first = ReadSource(memory, 0x1287DBC, 0x4354, 0x3488, [(108, 1), (92, 3), (64, 1), (44, 1), (64, 1), (20, 1)]);
        NativeBattleGfAnimationSource second = ReadSource(memory, 0x128C110, 0x149C, 0xFD0, [(84, 2), (33, 1)]);
        return new(new("FF8_EN:128D5B0", NativeBattleEffectStaticResourcesReader.Read(memory, 0x128D5B0, 0xD14)), first, second, NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x1287C44), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x128D5AC, 4)));
    }

    private static NativeBattleGfAnimationSource ReadSource(IGameNativeMemorySource memory, uint address, int length, int table, (int Vertices, int Shapes)[] geometries)
    {
        ImmutableArray<byte> bytes = NativeBattleEffectStaticResourcesReader.Read(memory, address, length);
        if (U32(0) != geometries.Length || U32(4) != table)
        {
            throw new InvalidDataException("Native Aero animation requires its original inline geometry table and bounded animation directory.");
        }
        for (int index = 0; index < geometries.Length; index++)
        {
            int start = checked((int)U32(8 + index * 4));
            (int vertices, int shapes) = geometries[index];
            int directory = checked(12 + vertices * shapes * 8);
            if (U32(start) != directory || U32(start + 4) != vertices || U32(start + 8) != shapes)
            {
                throw new InvalidDataException("Native Aero geometry requires its original12-byte header and complete shape vertex banks.");
            }
            int cursor = checked(start + directory);
            foreach (int stride in new int[] { 12, 12, 20, 24, 20, 24, 28, 36 })
            {
                uint count = U32(cursor);
                if (count > 4096)
                {
                    throw new InvalidDataException("Native Aero primitive directory exceeds its bounded resource.");
                }
                cursor = checked(cursor + 4 + (int)count * stride);
            }
            int limit = index + 1 < geometries.Length ? checked((int)U32(12 + index * 4)) : table;
            if (cursor > limit)
            {
                throw new InvalidDataException("Native Aero geometry overlaps the following original resource owner.");
            }
        }
        return new(address, bytes);

        uint U32(int offset)
        {
            if (offset < 0 || offset > bytes.Length - 4)
            {
                throw new InvalidDataException("Native Aero resource directory leaves its complete source bytes.");
            }
            return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleAeroResources? nativeAeroResources;
    public NativeBattleAeroResources ReadNativeAeroResources() => nativeAeroResources ??= NativeBattleAeroResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
