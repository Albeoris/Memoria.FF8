using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging.Tim;

namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal static class TimWorkspaceCodec
{
    public static WorkspaceManifest Export(TimFile file, WorkspaceExportSpec spec, IWorkspaceWriter output, String format)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(spec);
        List<WorkspaceImageEntry> entries = new(file.Images.Count);

        for (Int32 imageIndex = 0; imageIndex < file.Images.Count; imageIndex++)
        {
            TimImage image = file.Images[imageIndex];
            Boolean indexed = image.PixelMode is TimPixelMode.Indexed4 or TimPixelMode.Indexed8;
            Int32? paletteIndex = indexed ? spec.GetPaletteIndex(imageIndex) : null;
            Int32 normalBlockSize = checked(TimBlockHeader.Size + image.FilePixelByteCount);
            Int32? declaredBlockSizeOverride = image.DeclaredBlockSize == normalBlockSize
                ? null
                : image.DeclaredBlockSize;

            Int16? clutHeightOverride = null;
            UInt16[]? clutTrailingColors = null;
            if (image.Clut is not null)
            {
                Int32 actualHeight = image.Clut.Colors.Length / image.Clut.Width;
                clutHeightOverride = image.Clut.Height == actualHeight ? null : image.Clut.Height;
                Int32 exportedColorCount = image.Palettes.Count * GetPaletteColorCount(image.PixelMode);
                clutTrailingColors = exportedColorCount == image.Clut.Colors.Length ? null : image.Clut.Colors[exportedColorCount..];
                WriteClut(output, imageIndex, image);
                WritePalettes(output, imageIndex, image);
            }

            if (!image.IsEmpty)
            {
                if (!image.CanDecode)
                    throw new InvalidDataException($"TIM image {imageIndex} pixel data does not match its dimensions.");
                WriteImage(output, imageIndex, image, paletteIndex);
                if (spec.GeneratePreviews && indexed)
                    WritePreviews(output, imageIndex, image);
            }

            entries.Add(new WorkspaceImageEntry
            {
                PixelMode = image.PixelMode,
                X = image.X,
                Y = image.Y,
                AdditionalFlags = image.Flags & ~TimFlags.HasClut,
                PaletteIndex = paletteIndex,
                DeclaredBlockSizeOverride = declaredBlockSizeOverride,
                EmptyWidthInWords = image.IsEmpty ? image.WidthInWords : null,
                ClutX = image.Clut?.X ?? 0,
                ClutY = image.Clut?.Y ?? 0,
                ClutWidth = image.Clut?.Width,
                ClutTrailingColors = clutTrailingColors,
                ClutDeclaredHeightOverride = clutHeightOverride,
            });
        }

        return new WorkspaceManifest { Format = format, Images = entries };
    }

    public static TimFile Import(WorkspaceManifest manifest, WorkspaceImportSpec spec, IWorkspaceReader input)
    {
        List<TimImage> images = new(manifest.Images.Count);
        for (Int32 imageIndex = 0; imageIndex < manifest.Images.Count; imageIndex++)
            images.Add(ImportImage(manifest.Images[imageIndex], imageIndex, spec, input));
        return new TimFile(images);
    }

    private static void WriteImage(
        IWorkspaceWriter output,
        Int32 imageIndex,
        TimImage image,
        Int32? paletteIndex)
    {
        output.Write(WorkspacePaths.Image(imageIndex), stream =>
        {
            if (paletteIndex is Int32 selectedPalette)
            {
                ValidatePaletteIndex(image, imageIndex, selectedPalette);
                PngCodec.WriteIndexed(stream, image.PixelWidth, image.Height, image.Pixels, image.Palettes[selectedPalette]);
            }
            else
            {
                PngCodec.WriteRgba(stream, image.Decode());
            }
        });
    }

    private static void WriteClut(IWorkspaceWriter output, Int32 imageIndex, TimImage image)
    {
        IReadOnlyList<TimPalette> palettes = image.Palettes;
        if (palettes.Count == 0)
            throw new InvalidDataException($"TIM image {imageIndex} has no complete palettes for {image.PixelMode}.");
        Int32 paletteWidth = palettes[0].Count;
        PixelImage paletteSheet = new(paletteWidth, palettes.Count);
        for (Int32 paletteIndex = 0; paletteIndex < palettes.Count; paletteIndex++)
            palettes[paletteIndex].Colors.CopyTo(paletteSheet.Pixels, paletteIndex * paletteWidth);
        output.Write(WorkspacePaths.Clut(imageIndex), stream => PngCodec.WriteRgba(stream, paletteSheet));
    }

    private static void WritePalettes(IWorkspaceWriter output, Int32 imageIndex, TimImage image)
    {
        for (Int32 paletteIndex = 0; paletteIndex < image.Palettes.Count; paletteIndex++)
        {
            TimPalette palette = image.Palettes[paletteIndex];
            PixelImage paletteImage = new(palette.Count, 1, palette.Colors);
            output.Write(WorkspacePaths.Palette(imageIndex, paletteIndex), stream => PngCodec.WriteRgba(stream, paletteImage));
        }
    }

    private static void WritePreviews(IWorkspaceWriter output, Int32 imageIndex, TimImage image)
    {
        PixelImage previews = new(image.PixelWidth, checked(image.Height * image.Palettes.Count));
        for (Int32 paletteIndex = 0; paletteIndex < image.Palettes.Count; paletteIndex++)
        {
            PixelImage preview = image.Decode(paletteIndex);
            preview.Pixels.CopyTo(previews.Pixels, paletteIndex * preview.Pixels.Length);
        }
        output.Write(WorkspacePaths.Preview(imageIndex), stream => PngCodec.WriteRgba(stream, previews));
    }

    private static TimImage ImportImage(
        WorkspaceImageEntry entry,
        Int32 imageIndex,
        WorkspaceImportSpec spec,
        IWorkspaceReader input)
    {
        if ((entry.AdditionalFlags & TimFlags.HasClut) == TimFlags.HasClut)
            throw new InvalidDataException($"TIM image {imageIndex} additionalFlags must not contain HasClut; CLUT presence comes from its CLUT file.");

        TimClut? clut = ReadClut(entry, imageIndex, input);
        TimFlags flags = clut is null ? entry.AdditionalFlags : entry.AdditionalFlags | TimFlags.HasClut;
        String imagePath = WorkspacePaths.Image(imageIndex);
        Int16 widthInWords;
        Int16 height;
        Byte[] pixels;

        if (entry.EmptyWidthInWords is Int16 emptyWidth)
        {
            if (input.Exists(imagePath))
                throw new InvalidDataException($"Empty TIM image {imageIndex} must not also have '{imagePath}'.");
            widthInWords = emptyWidth;
            height = 0;
            pixels = [];
        }
        else
        {
            Byte[] png = input.Read(imagePath);
            (Int32 width, Int32 pngHeight, Byte[] importedPixels) = entry.PixelMode switch
            {
                TimPixelMode.Indexed4 or TimPixelMode.Indexed8 => ReadIndexedImage(
                    png, entry, clut, imageIndex, spec),
                TimPixelMode.Direct16 => ReadDirect16Image(png),
                TimPixelMode.Direct24 => ReadDirect24Image(png),
                _ => throw new InvalidDataException($"Unsupported TIM pixel mode {(Int32)entry.PixelMode}."),
            };
            widthInWords = GetWidthInWords(entry.PixelMode, width, imageIndex);
            height = checked((Int16)pngHeight);
            pixels = importedPixels;
        }

        Int32 normalBlockSize = checked(TimBlockHeader.Size + widthInWords * sizeof(UInt16) * height);
        Int32 declaredBlockSize = entry.DeclaredBlockSizeOverride ?? normalBlockSize;
        return new TimImage(
            entry.PixelMode,
            entry.X,
            entry.Y,
            widthInWords,
            height,
            pixels,
            clut,
            flags,
            declaredBlockSize);
    }

    private static TimClut? ReadClut(WorkspaceImageEntry entry, Int32 imageIndex, IWorkspaceReader input)
    {
        String path = WorkspacePaths.Clut(imageIndex);
        if (!input.Exists(path))
            return null;

        PixelImage image = PngCodec.Read(input.Read(path)).ToPixelImage();
        if (entry.PixelMode is not (TimPixelMode.Indexed4 or TimPixelMode.Indexed8))
            throw new InvalidDataException($"Direct-color TIM image {imageIndex} must not have '{path}'.");
        Int32 expectedPaletteWidth = GetPaletteColorCount(entry.PixelMode);
        if (image.Width != expectedPaletteWidth)
            throw new InvalidDataException($"TIM image {imageIndex} palette rows contain {image.Width} colors; expected {expectedPaletteWidth} for {entry.PixelMode}.");
        UInt16[] trailingColors = entry.ClutTrailingColors ?? [];
        if (trailingColors.Length >= expectedPaletteWidth)
            throw new InvalidDataException($"TIM image {imageIndex} has {trailingColors.Length} trailing CLUT colors; expected fewer than {expectedPaletteWidth}.");
        Int16 width = entry.ClutWidth is Int16 clutWidth && clutWidth > 0
            ? clutWidth
            : throw new InvalidDataException($"TIM image {imageIndex} has no valid clutWidth in the manifest.");
        Int32 colorCount = checked(image.Pixels.Length + trailingColors.Length);
        if (colorCount % width != 0)
            throw new InvalidDataException($"TIM image {imageIndex} palettes cannot be arranged into CLUT rows of {width} colors.");
        Int16 height = entry.ClutDeclaredHeightOverride ?? checked((Int16)(colorCount / width));
        UInt16[] colors = new UInt16[colorCount];
        for (Int32 index = 0; index < image.Pixels.Length; index++)
            colors[index] = image.Pixels[index].ToPsxBgr555();
        trailingColors.CopyTo(colors, image.Pixels.Length);
        return new TimClut(entry.ClutX, entry.ClutY, width, height, colors);
    }

    private static (Int32 Width, Int32 Height, Byte[] Pixels) ReadIndexedImage(
        Byte[] png,
        WorkspaceImageEntry entry,
        TimClut? clut,
        Int32 imageIndex,
        WorkspaceImportSpec spec)
    {
        if (clut is null)
            throw new InvalidDataException($"Indexed TIM image {imageIndex} has no '{WorkspacePaths.Clut(imageIndex)}'.");
        Int32 paletteIndex = entry.PaletteIndex
            ?? throw new InvalidDataException($"Indexed TIM image {imageIndex} has no paletteIndex in the manifest.");
        IReadOnlyList<TimPalette> palettes = clut.GetPalettes(entry.PixelMode);
        if (paletteIndex < 0 || paletteIndex >= palettes.Count)
        {
            throw new InvalidDataException(
                $"TIM image {imageIndex} selects palette {paletteIndex}, but its CLUT provides {palettes.Count} palettes.");
        }
        TimPalette target = palettes[paletteIndex];
        PaletteColorMapper mapper = new(target, spec);

        DecodedPng source = PngCodec.Read(png);
        if (source.IsIndexed)
        {
            Byte[] sourceIndices = source.Indices
                ?? throw new InvalidDataException("The indexed PNG decoder did not return pixel indices.");
            Rgba32[] sourcePalette = source.Palette
                ?? throw new InvalidDataException("The indexed PNG decoder did not return a palette.");
            Byte[] result = new Byte[sourceIndices.Length];
            for (Int32 pixelIndex = 0; pixelIndex < result.Length; pixelIndex++)
            {
                Byte sourceIndex = sourceIndices[pixelIndex];
                Rgba32 sourceColor = sourcePalette[sourceIndex];
                result[pixelIndex] = sourceIndex < target.Count && sourceColor == target[sourceIndex]
                    ? sourceIndex
                    : mapper.Map(sourceColor, $"TIM image {imageIndex} pixel {pixelIndex}");
            }
            return (source.Width, source.Height, result);
        }

        PixelImage rgba = source.ToPixelImage();
        Byte[] indices = new Byte[rgba.Pixels.Length];
        for (Int32 pixelIndex = 0; pixelIndex < indices.Length; pixelIndex++)
            indices[pixelIndex] = mapper.Map(rgba.Pixels[pixelIndex], $"TIM image {imageIndex} pixel {pixelIndex}");
        return (rgba.Width, rgba.Height, indices);
    }

    private static (Int32 Width, Int32 Height, Byte[] Pixels) ReadDirect16Image(Byte[] png)
    {
        PixelImage image = PngCodec.Read(png).ToPixelImage();
        Byte[] pixels = new Byte[checked(image.Pixels.Length * sizeof(UInt16))];
        for (Int32 index = 0; index < image.Pixels.Length; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan(index * sizeof(UInt16)), image.Pixels[index].ToPsxBgr555());
        return (image.Width, image.Height, pixels);
    }

    private static (Int32 Width, Int32 Height, Byte[] Pixels) ReadDirect24Image(Byte[] png)
    {
        PixelImage image = PngCodec.Read(png).ToPixelImage();
        Byte[] pixels = new Byte[checked(image.Pixels.Length * 3)];
        for (Int32 index = 0; index < image.Pixels.Length; index++)
        {
            Rgba32 color = image.Pixels[index];
            if (color.A != Byte.MaxValue)
                throw new InvalidDataException($"A 24-bit TIM cannot represent alpha {color.A} at pixel {index}.");
            pixels[index * 3] = color.B;
            pixels[index * 3 + 1] = color.G;
            pixels[index * 3 + 2] = color.R;
        }
        return (image.Width, image.Height, pixels);
    }

    private static Int16 GetWidthInWords(TimPixelMode mode, Int32 pixelWidth, Int32 imageIndex)
    {
        Int32 widthInWords = mode switch
        {
            TimPixelMode.Indexed4 when pixelWidth % 4 == 0 => pixelWidth / 4,
            TimPixelMode.Indexed8 when pixelWidth % 2 == 0 => pixelWidth / 2,
            TimPixelMode.Direct16 => pixelWidth,
            TimPixelMode.Direct24 when pixelWidth * 3 % 2 == 0 => pixelWidth * 3 / 2,
            TimPixelMode.Indexed4 => throw new InvalidDataException($"TIM image {imageIndex} width {pixelWidth} is not divisible by four for 4bpp."),
            TimPixelMode.Indexed8 => throw new InvalidDataException($"TIM image {imageIndex} width {pixelWidth} is not divisible by two for 8bpp."),
            TimPixelMode.Direct24 => throw new InvalidDataException($"TIM image {imageIndex} width {pixelWidth} cannot form complete 16-bit words at 24bpp."),
            _ => throw new InvalidDataException($"Unsupported TIM pixel mode {(Int32)mode}."),
        };
        return checked((Int16)widthInWords);
    }

    private static Int32 GetPaletteColorCount(TimPixelMode mode) => mode switch
    {
        TimPixelMode.Indexed4 => 16,
        TimPixelMode.Indexed8 => 256,
        _ => throw new ArgumentException($"Pixel mode {mode} does not use palettes.", nameof(mode)),
    };

    private static void ValidatePaletteIndex(TimImage image, Int32 imageIndex, Int32 paletteIndex)
    {
        if (paletteIndex < 0 || paletteIndex >= image.Palettes.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(paletteIndex),
                $"TIM image {imageIndex} selects palette {paletteIndex}, but provides {image.Palettes.Count} palettes.");
        }
    }
}
