using System.Buffers.Binary;
using System.Reflection.PortableExecutable;

namespace Memoria.FF7.Resources;

public sealed record Ff7BattleCameraEntryScript(ReadOnlyMemory<byte> Position, ReadOnlyMemory<byte> Focus);

public sealed partial class Ff7PcExecutableData
{
    public static Ff7BattleCameraMath LoadBattleCameraMath(Ff7Installation installation)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        return new(BitConverter.ToSingle(ReadVirtual(stream, pe, 0x7B7A60, 4)), BitConverter.ToDouble(ReadVirtual(stream, pe, 0x7B7AD8, 8)), BitConverter.ToDouble(ReadVirtual(stream, pe, 0x7B7AE0, 8)), BitConverter.ToSingle(ReadVirtual(stream, pe, 0x7B7A70, 4)));
    }

    public static IReadOnlyList<Ff7BattleCameraEntryScript> LoadBattleCameraEntryScripts(Ff7Installation installation)
    {
        const int count = 103;
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        byte[] positions = ReadVirtual(stream, pe, 0x9010D0, count * sizeof(uint));
        byte[] focuses = ReadVirtual(stream, pe, 0x901270, count * sizeof(uint));
        byte[] scripts = ReadVirtual(stream, pe, 0x9001A0, 0x9010D0 - 0x9001A0);
        int[][] offsets = new int[2][];
        SortedSet<int> boundaries = [scripts.Length];
        for (int component = 0; component < 2; component++)
        {
            byte[] table = component == 0 ? positions : focuses;
            offsets[component] = new int[count];
            for (int index = 0; index < count; index++)
            {
                uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(index * sizeof(uint)));
                if (pointer < 0x9001A0 || pointer >= 0x9010D0)
                {
                    throw new InvalidDataException("FFVII battle entry camera script pointer crosses its original data area.");
                }
                offsets[component][index] = (int)(pointer - 0x9001A0);
                boundaries.Add(offsets[component][index]);
            }
        }

        int[] ordered = boundaries.ToArray();
        Ff7BattleCameraEntryScript[] result = new Ff7BattleCameraEntryScript[count];
        for (int index = 0; index < count; index++)
        {
            result[index] = new(Slice(offsets[0][index]), Slice(offsets[1][index]));
        }
        return Array.AsReadOnly(result);

        ReadOnlyMemory<byte> Slice(int start)
        {
            int boundary = Array.BinarySearch(ordered, start);
            return scripts.AsMemory(start, ordered[boundary + 1] - start);
        }
    }
}
