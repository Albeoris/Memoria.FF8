using System.Buffers.Binary;
using System.Reflection.PortableExecutable;

namespace Memoria.FF7.Resources;

public sealed partial class Ff7PcExecutableData
{
    public static IReadOnlyList<Ff7WorldBridgeDefinition> LoadWorldBridges(Ff7Installation installation)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        byte[] bytes = ReadVirtual(stream, pe, 0x969CA8, 3 * 20);
        (int X, int Z)[] chunks = [(5, 18), (4, 17), (4, 14)];
        List<Ff7WorldBridgeDefinition> result = [];
        for (int index = 0; index < chunks.Length; index++)
        {
            ReadOnlySpan<byte> row = bytes.AsSpan(index * 20, 20);
            List<int> vertices = [];
            for (int vertex = 8; vertex < 20 && unchecked((sbyte)row[vertex]) >= 0; vertex++)
            {
                vertices.Add(row[vertex]);
            }
            result.Add(new(index, chunks[index].X, chunks[index].Z, BinaryPrimitives.ReadInt16LittleEndian(row), BinaryPrimitives.ReadInt16LittleEndian(row[2..]), BinaryPrimitives.ReadInt16LittleEndian(row[4..]), vertices.AsReadOnly()));
        }
        return result.AsReadOnly();
    }
}
