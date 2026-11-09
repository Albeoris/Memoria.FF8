using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public static class Ff7InitialGameData
{
    public static KernelInitialData Load(Ff7Installation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        IReadOnlyList<KernelSection> sections = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        if (sections.Count <= 3)
        {
            throw new InvalidDataException("FF7 kernel initial game section is absent.");
        }
        return KernelInitialData.Read(sections[3].Data.Span);
    }
}
