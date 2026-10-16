namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal sealed record DecodedPng(
    Int32 Width,
    Int32 Height,
    Rgba32[] Pixels,
    Byte[]? Indices,
    Rgba32[]? Palette)
{
    public Boolean IsIndexed => Indices is not null;

    public PixelImage ToPixelImage() => new(Width, Height, Pixels);
}