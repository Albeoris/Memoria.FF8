using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public static class Ff7MateriaResources
{
    public static byte[] LoadTypes(Ff7Installation installation)
    {
        return LoadRecords(installation).Select(record => record.Span[13]).ToArray();
    }

    public static IReadOnlyList<ReadOnlyMemory<byte>> LoadRecords(Ff7Installation installation)
    {
        IReadOnlyList<KernelSection> sections = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        if (sections.Count <= 8 || sections[8].Data.Length == 0 || sections[8].Data.Length % 20 != 0)
        {
            throw new InvalidDataException("FFVII materia requires its original kernel records.");
        }
        ReadOnlyMemory<byte> data = sections[8].Data;
        return Enumerable.Range(0, data.Length / 20).Select(index => data.Slice(index * 20, 20)).ToArray();
    }
}
