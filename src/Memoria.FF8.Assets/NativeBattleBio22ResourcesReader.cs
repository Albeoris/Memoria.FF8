using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleBio22ResourcesReader
{
    public static NativeBattleBio22Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(0x162601A, 2)) != 0x918)
        {
            throw new InvalidDataException("Native Bio animation leaves its authenticated complete program extent.");
        }
        NativeBattleEffectHeapAllocation pointers = Allocation(0x16189CC, 0x1C);
        NativeBattleEffectHeapAllocation secondPointers = Allocation(0x1626000, 0x14);
        NativeBattleBio22Resources resources = new(Allocation(0x1626014, 0x918), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x1615CE4u), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x1617E3Cu), ReadSources(pointers), pointers, Allocation(0x16146E4, 4), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x1619888u), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x161F750u), [Allocation(0x16189E8, 0xEA0), Allocation(0x161E8B0, 0xEA0)], secondPointers, ReadSources(secondPointers), new uint[] { 0x1614F74, 0x1615120, 0x16152CC, 0x1615440, 0x16156F8, 0x1615858, 0x16155B4 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadSprite(memory, address)).ToImmutableArray())
        {
            OverlayProgram = Allocation(0x162693C, 0x10),
            PlanarCounts = Allocation(0x162694C, 0x15),
            BillboardCounts = Allocation(0x162696C, 0x15),
            LaterPlanarCounts = Allocation(0x162698C, 0x19),
            LaterBillboardCounts = Allocation(0x16269AC, 0x15),
            PaletteBaseline = Palette(0x16146E8),
            PalettePointers = Allocation(0x1614F68, 12),
            PaletteSources = new uint[] { 0x1614D48, 0x1614B28, 0x1614908 }.Select(Palette).ToImmutableArray()
        };
        resources.Validate();
        return resources;

        ImmutableArray<NativeBattleEffectHeapAllocation> ReadSources(NativeBattleEffectHeapAllocation directory)
        {
            ImmutableArray<NativeBattleEffectHeapAllocation>.Builder sources = ImmutableArray.CreateBuilder<NativeBattleEffectHeapAllocation>();
            for (int index = 0; index < directory.Data.Length / 4; index++)
            {
                uint address = BinaryPrimitives.ReadUInt32LittleEndian(directory.Data.AsSpan().Slice(4 * index));
                uint length = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
                if (length is not 0x2E0 and not 0x158 and not 0x818)
                {
                    throw new InvalidDataException("Native Bio source leaves its complete original vector-bank extent.");
                }
                sources.Add(Allocation(address, checked((int)length)));
            }
            return sources.ToImmutable();
        }

        NativeBattleEffectHeapAllocation Palette(uint address)
        {
            NativeBattleEffectHeapAllocation palette = Allocation(address, 0x220);
            NativeBattleBio22Palette.ValidateTim(palette.Data);
            return palette;
        }

        NativeBattleEffectHeapAllocation Allocation(uint address, int length) => new($"FF8_EN:{address:X}", NativeBattleEffectStaticResourcesReader.Read(memory, address, length));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleBio22Resources? nativeBio22Resources;

    public NativeBattleBio22Resources ReadNativeBio22Resources() => nativeBio22Resources ??= NativeBattleBio22ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
