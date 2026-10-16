using Albeoris.Games.FF8.Imaging.Mim;

namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal static class MimMapPaletteSynthesizer
{
    private const Int32 FourBitColorCount = 16;

    public static void AddMissingColors(MimFile mim, MimMapLayout layout, MimMapAtlas sourceAtlas, IReadOnlyDictionary<UInt32, MimMapRenderedTexture> originals, IReadOnlyDictionary<UInt32, PixelImage> edited, WorkspaceImportSpec spec)
    {
        ArgumentNullException.ThrowIfNull(mim);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(sourceAtlas);
        ArgumentNullException.ThrowIfNull(originals);
        ArgumentNullException.ThrowIfNull(edited);
        ArgumentNullException.ThrowIfNull(spec);
        Boolean[,] used = FindUsedIndices(layout, sourceAtlas);
        foreach (MimMapTextureGroup group in layout.Groups)
        {
            MimMapRenderedTexture original = originals[group.PupuId];
            PixelImage changed = edited[group.PupuId];
            for (Int32 index = 0; index < changed.Pixels.Length; index++)
            {
                if (changed.Pixels[index] == original.Image.Pixels[index])
                    continue;
                Int32 owner = original.Owners[index];
                if (owner < 0)
                    continue;
                MimMapTile tile = layout.Tiles[owner];
                if (tile.BitsPerPixel is not (4 or 8))
                    continue;
                AddColor(mim, tile, changed.Pixels[index], used, spec);
            }
        }
    }

    private static Boolean[,] FindUsedIndices(MimMapLayout layout, MimMapAtlas atlas)
    {
        Boolean[,] used = new Boolean[MimFile.PaletteCount, MimFile.ColorsPerPalette];
        foreach (MimMapTile tile in layout.Tiles)
        {
            if (tile.BitsPerPixel is not (4 or 8))
                continue;
            for (Int32 y = 0; y < MimMapTile.PixelSize; y++)
            {
                for (Int32 x = 0; x < MimMapTile.PixelSize; x++)
                    used[tile.PaletteId, atlas.ReadPaletteIndex(tile, x, y)] = true;
            }
        }
        return used;
    }

    private static void AddColor(MimFile mim, MimMapTile tile, Rgba32 color, Boolean[,] used, WorkspaceImportSpec spec)
    {
        UInt16 encoded = color.ToPsxBgr555();
        Rgba32 normalized = Rgba32.FromPsxBgr555Display(encoded);
        Int32 colorCount = tile.BitsPerPixel == 4 ? FourBitColorCount : MimFile.ColorsPerPalette;
        for (Int32 colorIndex = 0; colorIndex < colorCount; colorIndex++)
        {
            if (mim.GetDisplayColor(tile.PaletteId, colorIndex) == normalized)
                return;
        }
        for (Int32 colorIndex = 0; colorIndex < colorCount; colorIndex++)
        {
            if (used[tile.PaletteId, colorIndex])
                continue;
            mim.Colors[tile.PaletteId * MimFile.ColorsPerPalette + colorIndex] = encoded;
            used[tile.PaletteId, colorIndex] = true;
            return;
        }
        if (spec.MissingPaletteColor == PaletteColorHandling.RequireExactMatch)
            throw new InvalidDataException($"Palette {tile.PaletteId} has no free {tile.BitsPerPixel}-bpp index for color #{normalized.R:X2}{normalized.G:X2}{normalized.B:X2}{normalized.A:X2} after the palette layer was removed.");
    }
}
