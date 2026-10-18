using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleWaterShapeSelection
{
    public static uint ResolveB657E0(NativeBattleWaterExecutionFrame frame, NativeBattleWater222Resources resources, NativeBattleEffectHeapAllocation map, short index) => Resolve(frame, resources, map, index).Selected;

    public static (NativeBattleWaterExecutionFrame Frame, uint Address) SelectB66230(NativeBattleWaterExecutionFrame frame, NativeBattleWater222Resources resources, NativeBattleEffectHeapAllocation map, short index)
    {
        (uint directory, uint selected) = Resolve(frame, resources, map, index);
        return (frame.WriteTemporaryDword(0xFC, directory), selected);
    }

    private static (uint Directory, uint Selected) Resolve(NativeBattleWaterExecutionFrame frame, NativeBattleWater222Resources resources, NativeBattleEffectHeapAllocation map, short index)
    {
        if (map.Data.IsDefault || map.Data.Length != 16 || CoreDword(frame, CoreDword(frame, 0x27973EC) + 0xAC) != 0x186C8CC)
        {
            throw new NotSupportedException("Native shape selection requires its actual bounded resource map.");
        }
        if (index < 0 || index >= map.Data.Length)
        {
            throw new NotSupportedException("Native shape selection leaves its actual resource-map owner.");
        }
        int resource = map.Data[index];
        if (resource >= resources.Files.Length)
        {
            throw new NotSupportedException("Native shape mapping leaves its supplied current files.");
        }
        uint basis = CoreDword(frame, 0x2798A68 + 4u * (uint)resource);
        int length = resources.Files[resource].Data.Length;
        Require(basis, 12, 4, length);
        uint directory = unchecked(basis + Dword(unchecked(basis + 12)));
        uint entry = unchecked(directory + 4u + (uint)(4 * index));
        Require(basis, unchecked(entry - basis), 4, length);
        return (directory, unchecked(directory + Dword(entry)));

        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
    }

    private static void Require(uint basis, uint relative, int count, int length)
    {
        if (relative > length || count > length - relative)
        {
            throw new InvalidDataException("Native shape directory read leaves its current resource owner.");
        }
        _ = ArenaOffset(unchecked(basis + relative), count);
    }
}
