using System.Text;
using Albeoris.Games.FF8.MngrpBin.Abstractions;
using Albeoris.Games.FF8.MngrpBin.Internal;

namespace Albeoris.Games.FF8.MngrpBin;

/// <summary>
/// The full contents of a <c>mngrp.bin</c>/<c>mngrphd.bin</c> file pair, held in memory as parsed
/// sections. Use <see cref="Read(ReadOnlySpan{Byte}, ReadOnlySpan{Byte}, Encoding)"/> to parse
/// existing files, edit the sections' texts in place, and <see cref="Write"/> to serialize them
/// back; every stored offset and size is recalculated from the sections, and an unedited archive
/// serializes back byte-for-byte.
/// </summary>
public sealed class MngrpArchive
{
    private readonly List<IMngrpSection> _sections;

    internal MngrpArchive(List<IMngrpSection> sections, MngrpSlotDirectory slotDirectory, Encoding encoding)
    {
        _sections = sections;
        SlotDirectory = slotDirectory;
        Encoding = encoding;
    }

    /// <summary>
    /// The text encoding the archive's strings are stored with — European or Japanese,
    /// matching the file's localization.
    /// </summary>
    public Encoding Encoding { get; }

    /// <summary>
    /// The archive's sections in slot order. A <see cref="MngrpTextRecordSection"/> represents
    /// both its record slot and its companion text slot.
    /// </summary>
    public IReadOnlyList<IMngrpSection> Sections => _sections;

    /// <summary>The header slot table; kept to reproduce vacant slots verbatim.</summary>
    internal MngrpSlotDirectory SlotDirectory { get; }

    /// <summary>Parses a <c>mngrp.bin</c>/<c>mngrphd.bin</c> pair.</summary>
    public static MngrpArchive Read(ReadOnlySpan<Byte> content, ReadOnlySpan<Byte> header, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        return MngrpArchiveReader.Read(content, header, encoding);
    }

    /// <summary>Parses the pair returned by <see cref="Write"/>.</summary>
    public static MngrpArchive Read(MngrpFilePair files, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(files);
        return Read(files.Content, files.Header, encoding);
    }

    /// <summary>Serializes the archive back into a <c>mngrp.bin</c>/<c>mngrphd.bin</c> pair.</summary>
    public MngrpFilePair Write() => MngrpArchiveWriter.Write(this);

    /// <summary>Returns the section occupying <paramref name="slotIndex"/> as <typeparamref name="TSection"/>.</summary>
    public TSection GetSection<TSection>(Int32 slotIndex) where TSection : class, IMngrpSection
    {
        IMngrpSection section = _sections.FirstOrDefault(section => section.SlotIndex == slotIndex)
            ?? throw new KeyNotFoundException($"The archive has no section in slot {slotIndex}.");
        return section as TSection
            ?? throw new InvalidCastException($"The section in slot {slotIndex} is a {section.GetType().Name}, not a {typeof(TSection).Name}.");
    }

    /// <summary>Enumerates the sections of the requested type, in slot order.</summary>
    public IEnumerable<TSection> SectionsOfType<TSection>() where TSection : class, IMngrpSection
    {
        return _sections.OfType<TSection>();
    }
}
