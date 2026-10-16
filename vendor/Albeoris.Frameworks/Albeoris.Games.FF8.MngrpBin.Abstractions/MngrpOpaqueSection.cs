namespace Albeoris.Games.FF8.MngrpBin.Abstractions;

/// <summary>
/// A section whose internal layout is not (yet) reverse-engineered. Its bytes, including the
/// original sector padding, are preserved exactly as read, so round-tripping is always lossless.
/// </summary>
public sealed class MngrpOpaqueSection : MngrpSection
{
    private Byte[] _content;

    public MngrpOpaqueSection(Int32 slotIndex, Byte[] content)
        : base(slotIndex)
    {
        ArgumentNullException.ThrowIfNull(content);
        _content = content;
    }

    public override MngrpSectionLayout Layout => MngrpSectionLayout.Opaque;

    /// <summary>The section's raw bytes; the length must stay a multiple of the 2048-byte sector size.</summary>
    public Byte[] Content
    {
        get => _content;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _content = value;
        }
    }
}
