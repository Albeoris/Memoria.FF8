namespace Memoria.FF8.Resources;

/// <summary>A source-published complete native atlas and the sampled logical rectangle. The replacement image replaces the complete bound texture.</summary>
public sealed record TextureSourceAtlasAddress(string Name, int FirstImage, int ImageCount, int CanvasWidth, int CanvasHeight, int X, int Y, int Width, int Height)
{
    public string Variant { get; init; } = "";
    public TextureSourceAtlasReplacementMode ReplacementMode { get; init; } = TextureSourceAtlasReplacementMode.WholeTexture;
    public int SampleXOffset { get; init; }
    public TextureSourceAtlasAddressMode HorizontalAddressMode { get; init; } = TextureSourceAtlasAddressMode.Clamp;
    public string? UnsupportedReason { get; init; }

    public void Validate()
    {
        if (string.IsNullOrEmpty(Name) || Name.Contains('/') || Name.Contains('\\') || FirstImage < 0 || (FirstImage & 1) != 0 || ImageCount is < 1 or > 2 || CanvasWidth <= 0 || CanvasHeight <= 0 || (CanvasHeight & 1) != 0 || X < 0 || Y < 0 || Width <= 0 || Height <= 0 || X + (long)Width > CanvasWidth || Y + (long)Height > CanvasHeight || SampleXOffset < 0 || SampleXOffset >= CanvasWidth || HorizontalAddressMode != TextureSourceAtlasAddressMode.Clamp || Variant is not ("" or "_jp") || ReplacementMode != TextureSourceAtlasReplacementMode.WholeTexture)
        {
            throw new InvalidDataException("Native atlas addressing requires a source-published image selector and a bounded positive logical sampling rectangle.");
        }
    }
}

public enum TextureSourceAtlasReplacementMode
{
    WholeTexture
}

public enum TextureSourceAtlasAddressMode
{
    Clamp
}
