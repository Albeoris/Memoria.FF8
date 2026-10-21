using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleMeltingBubble43ResourcesReader
{
    public static NativeBattleMeltingBubble43Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x15C62AC, 11)) != "mag042.tim\0")
        {
            throw new InvalidDataException("Melting Bubble requires its original initializer texture name.");
        }
        uint[] addresses = [0x15C3684, 0x15C38B4, 0x15C3AE4, 0x15C3F44, 0x15C40F0, 0x15C429C, 0x15C4448, 0x15C45F4];
        NativeBattleMeltingBubble43Resources result = new(new("FF8_EN:15C6184", memory.Read(0x15C6184, 296).ToImmutableArray()), new(memory.Read(0x15C6080, 260).ToImmutableArray()) { ResourceAddress = 0x15C6080 }, new(0, 0x15C3684, 0, 0, memory.Read(0x15C3684, 10748).ToImmutableArray()), addresses.Select(address => new NativeBattleModuleResource(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray(), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x15C6180, 4)));
        result.Validate();
        int sources = 0;
        int sprites = 0;
        for (int index = 0; index < 16; index++)
        {
            uint sourceAddress = Pointer(0, index);
            uint spriteAddress = Pointer(1, index);
            if (Pointer(2, index) != 0 || Pointer(3, index) != 0)
            {
                throw new InvalidDataException("Melting Bubble has no original mesh or morph directory entries.");
            }
            if (spriteAddress != 0)
            {
                _ = result.ReadResource(spriteAddress);
                sprites++;
            }
            if (sourceAddress == 0)
            {
                continue;
            }
            sources++;
            ReadOnlySpan<byte> source = result.ReadStatic(sourceAddress, 0x158);
            int kind = unchecked((sbyte)source[0x16]);
            int mode = unchecked((sbyte)source[0x1E]);
            int lifetime = unchecked((sbyte)source[0x12]);
            if (source[0x11] is not (0 or 1) || source[0x27] != 0 || source[0x34] is not (0 or 1) || kind is not (0 or 1) || spriteAddress == 0 || lifetime < 1)
            {
                throw new InvalidDataException("Melting Bubble source leaves its actual sprite, anchor, motion or placement contract.");
            }
            int emissionSamples = Math.Min(lifetime, 40);
            ReadStaticChannel(0xC8, emissionSamples);
            if (source[0x34] == 1)
            {
                ReadStaticChannel(0xCC, emissionSamples * 2);
                ReadStaticChannel(0xD0, emissionSamples * 2);
                ReadStaticChannel(0xD4, emissionSamples * 2);
            }
            int geometrySamples = mode == 0 && kind == 0 ? BinaryPrimitives.ReadUInt16LittleEndian(result.ReadResource(spriteAddress).AsSpan().Slice(8)) - 1 : mode == 1 && kind == 1 ? Math.Max(unchecked((sbyte)source[0x1F]), unchecked((sbyte)source[0x20])) : -1;
            if (geometrySamples is < 1 or > 20)
            {
                throw new InvalidDataException("Melting Bubble geometry channel samples exceed their actual sprite lifetime.");
            }
            foreach (int field in new[] { 0xD8, 0xDC, 0xE0 })
            {
                uint channel = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(field));
                int length = geometrySamples * 2;
                if (!NativeBattleMeltingBubble43Profile.ChannelRegions.Any(region => channel >= region.Address && (ulong)channel + (uint)length <= (ulong)region.Address + (uint)region.Length))
                {
                    throw new InvalidDataException("Melting Bubble Euler channel leaves its original typed retained interval.");
                }
                if (memory.ReadLoader(channel, length).Any(value => value != 0))
                {
                    throw new InvalidDataException("Melting Bubble original channel storage contradicts its loader-zero producer.");
                }
            }
            for (int child = 0; child < 5; child++)
            {
                int trigger = child == 4 ? 0x30 : 0x28 + child;
                int selector = child == 4 ? 0x31 : 0x2C + child;
                int selected = unchecked((sbyte)source[selector]);
                if (source[trigger] == 1 && (selected is < 0 or >= 16 || Pointer(0, selected) == 0))
                {
                    throw new InvalidDataException("Melting Bubble child source leaves its actual absolute directory.");
                }
            }

            void ReadStaticChannel(int field, int length)
            {
                uint channel = BinaryPrimitives.ReadUInt32LittleEndian(result.ReadStatic(sourceAddress + (uint)field, 4));
                _ = result.ReadStatic(channel, length);
            }
        }
        if (sources != 10 || sprites != 10)
        {
            throw new InvalidDataException("Melting Bubble requires all ten original source and aliased sprite directory entries.");
        }
        return result;

        uint Pointer(int table, int index) => BinaryPrimitives.ReadUInt32LittleEndian(result.Executable.Data.AsSpan().Slice(4 + table * 64 + index * 4));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleMeltingBubble43Resources? nativeMeltingBubble43Resources;

    public NativeBattleMeltingBubble43Resources ReadNativeMeltingBubble43Resources()
    {
        return nativeMeltingBubble43Resources ??= NativeBattleMeltingBubble43ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }
}
