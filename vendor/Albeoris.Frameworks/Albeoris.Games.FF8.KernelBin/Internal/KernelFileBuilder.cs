using System.Buffers.Binary;

namespace Albeoris.Games.FF8.KernelBin.Internal;

/// <summary>
/// Assembles a complete kernel.bin file from its 56 sections, computing the header's section
/// offset table from the actual size of each section as it is added.
/// </summary>
internal sealed class KernelFileBuilder
{
    private readonly List<Byte[]> _sections = new(KernelSections.Count);

    public void AddSection(Byte[] sectionData)
    {
        ArgumentNullException.ThrowIfNull(sectionData);
        _sections.Add(sectionData);
    }

    public Byte[] Build()
    {
        if (_sections.Count != KernelSections.Count)
            throw new InvalidOperationException($"Expected {KernelSections.Count} kernel.bin sections, but {_sections.Count} were added.");

        Int32 headerSize = 4 + 4 * KernelSections.Count;
        Int32[] offsets = new Int32[KernelSections.Count];
        Int32 currentOffset = headerSize;
        for (Int32 i = 0; i < _sections.Count; i++)
        {
            offsets[i] = currentOffset;
            currentOffset += _sections[i].Length;
        }

        Byte[] result = new Byte[currentOffset];
        BinaryPrimitives.WriteInt32LittleEndian(result, KernelSections.Count);
        for (Int32 i = 0; i < offsets.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(4 + i * 4, 4), offsets[i]);

        for (Int32 i = 0; i < _sections.Count; i++)
            _sections[i].CopyTo(result, offsets[i]);

        return result;
    }
}
