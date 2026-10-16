using System.Text;
using Albeoris.Games.FF8.Msd.Internal;

namespace Albeoris.Games.FF8.Msd;

/// <summary>
/// An offset-indexed string table stored in a Final Fantasy VIII <c>.msd</c> file.
/// Offsets are resolved while reading and recalculated whenever the file is written.
/// </summary>
public sealed class MsdFile
{
    /// <summary>Creates an empty MSD file using <paramref name="encoding"/>.</summary>
    public MsdFile(Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        Encoding = encoding;
    }

    internal MsdFile(List<String> texts, Encoding encoding)
        : this(encoding)
    {
        Texts.AddRange(texts);
    }

    /// <summary>The localization-specific FF8 encoding used by this file.</summary>
    public Encoding Encoding { get; }

    /// <summary>The decoded texts, in the order in which their offsets are stored.</summary>
    public List<String> Texts { get; } = [];

    /// <summary>Reads a complete MSD file held in memory.</summary>
    /// <exception cref="InvalidDataException">The offset table is malformed.</exception>
    public static MsdFile Read(ReadOnlySpan<Byte> content, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        return MsdFileReader.Read(content, encoding);
    }

    /// <summary>Writes the complete MSD file and recalculates every string offset.</summary>
    public Byte[] Write() => MsdFileWriter.Write(this);
}
