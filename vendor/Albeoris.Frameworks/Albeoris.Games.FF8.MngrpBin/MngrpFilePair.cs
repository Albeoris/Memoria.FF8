namespace Albeoris.Games.FF8.MngrpBin;

/// <summary>The in-memory contents of a <c>mngrp.bin</c>/<c>mngrphd.bin</c> file pair.</summary>
public sealed class MngrpFilePair
{
    public MngrpFilePair(Byte[] content, Byte[] header)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(header);
        Content = content;
        Header = header;
    }

    /// <summary>The contents of <c>mngrp.bin</c>: the section data.</summary>
    public Byte[] Content { get; }

    /// <summary>The contents of <c>mngrphd.bin</c>: the 256-slot section directory.</summary>
    public Byte[] Header { get; }
}
