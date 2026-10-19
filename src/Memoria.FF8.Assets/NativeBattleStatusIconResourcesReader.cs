using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleStatusIconResourcesReader
{
    public static NativeBattleStatusIconResources Read(byte[] executable)
    {
        return Read(new NativeExecutableMemory(executable));
    }

    public static NativeBattleStatusIconResources Read(IGameNativeMemorySource memory)
    {
        ImmutableArray<byte> pointers = NativeBattleEffectStaticResourcesReader.Read(memory, 0xC7EF54, 24);
        ImmutableArray<byte> placements = NativeBattleEffectStaticResourcesReader.Read(memory, 0xC7EF70, 48);
        ImmutableArray<byte> dispatch = NativeBattleEffectStaticResourcesReader.Read(memory, 0xC7EFC8, 8);
        ImmutableArray<byte> selectors = NativeBattleEffectStaticResourcesReader.Read(memory, 0x571220, 32);
        uint[] expectedSelectors = [0x5711D3, 0x5711DA, 0x5711E1, 0x5711E8, 0x5711F6, 0x5711F6, 0x5711F6, 0x5711EF];
        for (int index = 0; index < expectedSelectors.Length; index++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(selectors.AsSpan().Slice(index * 4)) != expectedSelectors[index])
            {
                throw new InvalidDataException("Native5711C0 selector jump table differs from the original status mapping.");
            }
        }
        if (BinaryPrimitives.ReadUInt32LittleEndian(dispatch.AsSpan()) != 0x5710D0 || BinaryPrimitives.ReadUInt32LittleEndian(dispatch.AsSpan().Slice(4)) != 0x5710D0)
        {
            throw new InvalidDataException("Native5711C0 lacks its original5710D0 callback bindings.");
        }
        uint defaultAddress = BinaryPrimitives.ReadUInt32LittleEndian(pointers.AsSpan());
        uint defaultHeader = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(memory, defaultAddress, 4).AsSpan());
        byte[] bits = [5, 6, 7, 8, 12];
        ImmutableArray<NativeBattleStatusIconResource>.Builder entries = ImmutableArray.CreateBuilder<NativeBattleStatusIconResource>(5);
        for (int index = 0; index < bits.Length; index++)
        {
            int row = index + 1;
            uint address = BinaryPrimitives.ReadUInt32LittleEndian(pointers.AsSpan().Slice(row * 4));
            short x = BinaryPrimitives.ReadInt16LittleEndian(placements.AsSpan().Slice(row * 8));
            short y = BinaryPrimitives.ReadInt16LittleEndian(placements.AsSpan().Slice(row * 8 + 2));
            short scale = BinaryPrimitives.ReadInt16LittleEndian(placements.AsSpan().Slice(row * 8 + 6));
            entries.Add(new(bits[index], x, y, scale, NativeBattleEffectStaticResourcesReader.ReadSprite(memory, address)));
        }
        NativeBattleStatusIconResources result = new(defaultHeader, entries.MoveToImmutable());
        result.Validate();
        return result;
    }
}
