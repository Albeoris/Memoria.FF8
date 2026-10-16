namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal sealed class MimMapTextureRenderer
{
    private readonly MimMapLayout _layout;
    private readonly MimMapAtlas _atlas;

    public MimMapTextureRenderer(MimMapLayout layout, MimMapAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(atlas);
        _layout = layout;
        _atlas = atlas;
    }

    public MimMapRenderedTexture Render(MimMapTextureGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        PixelImage image = new(_layout.Width, _layout.Height);
        Int32[] owners = new Int32[image.Pixels.Length];
        Array.Fill(owners, -1);

        foreach (MimMapTile tile in group.Tiles)
        {
            Int32 destinationX = checked(tile.X + _layout.OffsetX);
            Int32 destinationY = checked(tile.Y + _layout.OffsetY);
            for (Int32 y = 0; y < MimMapTile.PixelSize; y++)
            {
                for (Int32 x = 0; x < MimMapTile.PixelSize; x++)
                {
                    Int32 outputX = destinationX + x;
                    Int32 outputY = destinationY + y;
                    if ((UInt32)outputX >= image.Width || (UInt32)outputY >= image.Height)
                        throw new InvalidDataException($"MAP tile {tile.Index} lies outside the calculated {_layout.Width}x{_layout.Height} canvas.");
                    Int32 outputIndex = outputY * image.Width + outputX;
                    Rgba32 color = _atlas.ReadPixel(tile, x, y);
                    if (color.A == 0)
                    {
                        if (image.Pixels[outputIndex].A == 0)
                            owners[outputIndex] = tile.Index;
                        continue;
                    }
                    image.Pixels[outputIndex] = color;
                    owners[outputIndex] = tile.Index;
                }
            }
        }
        return new MimMapRenderedTexture(image, owners);
    }
}

internal sealed record MimMapRenderedTexture(PixelImage Image, Int32[] Owners);
