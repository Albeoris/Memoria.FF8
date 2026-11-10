using System.Collections.ObjectModel;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed class Ff7KernelTextResources
{
    private Ff7KernelTextResources(Dictionary<int, string> names, Dictionary<int, string> descriptions)
    {
        ItemNames = new ReadOnlyDictionary<int, string>(names);
        ItemDescriptions = new ReadOnlyDictionary<int, string>(descriptions);
    }

    public IReadOnlyDictionary<int, string> ItemNames { get; }
    public IReadOnlyDictionary<int, string> ItemDescriptions { get; }

    public static Ff7KernelTextResources Load(Ff7Installation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        if (!StringComparer.OrdinalIgnoreCase.Equals(installation.Language, "en"))
        {
            throw new NotSupportedException("This kernel menu text profile requires the original English string tables.");
        }
        IReadOnlyList<ReadOnlyMemory<byte>> sections = KernelTextArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel2).Path));
        Dictionary<int, string> names = new();
        Dictionary<int, string> descriptions = new();
        Add(0, 128, 10, 2);
        Add(128, 128, 11, 3);
        Add(256, 32, 12, 4);
        Add(288, 32, 13, 5);
        return new(names, descriptions);

        void Add(int first, int count, int nameSection, int descriptionSection)
        {
            IReadOnlyList<string> nativeNames = KernelTextArchive.ReadPlainStrings(sections[nameSection].Span, count);
            IReadOnlyList<string> nativeDescriptions = KernelTextArchive.ReadPlainStrings(sections[descriptionSection].Span, count);
            for (int index = 0; index < count; index++)
            {
                names.Add(first + index, nativeNames[index]);
                descriptions.Add(first + index, nativeDescriptions[index]);
            }
        }
    }
}
