using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging.Tex;

namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal static class TexWorkspaceCodec
{
    public const Int32 BitsPerByte = 8;
    public const Int32 IndexedBitsPerPixel = 8;
    public const Int32 IndexedBytesPerPixel = 1;

    public static TexWorkspaceManifest Export(TexFile file, WorkspaceExportSpec spec, IWorkspaceWriter output)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(output);

        Int32? selectedPaletteIndex = null;
        if (file.Palettes.Count > 0)
        {
            selectedPaletteIndex = spec.GetPaletteIndex(0);
            ValidatePaletteIndex(file, selectedPaletteIndex.Value);
            WriteIndexedImage(output, file, selectedPaletteIndex.Value);
            WritePaletteSheet(output, file);
            WritePalettes(output, file);
            if (spec.GeneratePreviews)
                WritePreviews(output, file);
        }
        else
        {
            ValidateDirectPixelFormat(file.Header);
            output.Write(TexWorkspacePaths.Image, stream => PngCodec.WriteRgba(stream, file.DecodeRaw()));
            if (spec.GeneratePreviews && file.Header.ColorKeyIndex is not null)
            {
                output.Write(
                    TexWorkspacePaths.Preview(0),
                    stream => PngCodec.WriteRgba(stream, file.Decode()));
            }
        }

        return new TexWorkspaceManifest
        {
            Header = TexHeaderSettings.From(file.Header),
            SelectedPaletteIndex = selectedPaletteIndex,
            PaletteColorKeyIndices = DecodeColorKeys(file.ColorKeys),
        };
    }

    public static TexFile Import(TexWorkspaceManifest manifest, WorkspaceImportSpec spec, IWorkspaceReader input)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(input);

        DecodedPng image = PngCodec.Read(input.Read(TexWorkspacePaths.Image));
        if (!input.Exists(TexWorkspacePaths.Palettes))
        {
            if (manifest.SelectedPaletteIndex is not null)
                throw new InvalidDataException("A TEX workspace without palettes must not select a palette.");
            if (manifest.PaletteColorKeyIndices is not null)
                throw new InvalidDataException("A TEX workspace without palettes cannot contain palette color keys.");

            TexHeader directHeader = manifest.Header.Create(image.Width, image.Height, 0, 0, hasColorKeys: false);
            Byte[] directPixels = EncodeDirectPixels(image.Pixels, directHeader);
            return new TexFile(directHeader, [], directPixels);
        }

        PixelImage paletteSheet = PngCodec.Read(input.Read(TexWorkspacePaths.Palettes)).ToPixelImage();
        if (paletteSheet.Width > Byte.MaxValue + 1)
            throw new InvalidDataException($"A TEX palette contains {paletteSheet.Width} colors; at most 256 are supported.");

        UInt32[][] palettes = ReadPalettes(paletteSheet);
        Byte[]? colorKeys = EncodeColorKeys(
            manifest.PaletteColorKeyIndices,
            palettes.Length,
            paletteSheet.Width);
        TexHeader indexedHeader = manifest.Header.Create(
            image.Width,
            image.Height,
            palettes.Length,
            paletteSheet.Width,
            colorKeys is not null);
        Int32 selectedPaletteIndex = manifest.SelectedPaletteIndex
            ?? throw new InvalidDataException("An indexed TEX workspace must select the palette embedded in image.png.");
        if (selectedPaletteIndex < 0 || selectedPaletteIndex >= palettes.Length)
        {
            throw new InvalidDataException(
                $"The TEX workspace selects palette {selectedPaletteIndex}, but palettes.png contains {palettes.Length} palettes.");
        }

        Byte[] emptyPixels = new Byte[checked(image.Width * image.Height)];
        TexFile paletteSource = new(indexedHeader, palettes, emptyPixels, colorKeys);
        Rgba32[] selectedPalette = paletteSource.GetEffectivePalette(selectedPaletteIndex);
        Byte[] indices = ReadIndices(image, selectedPalette, spec);
        return new TexFile(indexedHeader, palettes, indices, colorKeys);
    }

    private static void WriteIndexedImage(IWorkspaceWriter output, TexFile file, Int32 paletteIndex) =>
        output.Write(
            TexWorkspacePaths.Image,
            stream => PngCodec.WriteIndexed(
                stream,
                file.Header.Width,
                file.Header.Height,
                file.Pixels,
                file.GetEffectivePalette(paletteIndex)));

    private static void WritePaletteSheet(IWorkspaceWriter output, TexFile file)
    {
        Rgba32[] colors = new Rgba32[checked(file.Header.ColorsPerPalette * file.Palettes.Count)];
        for (Int32 paletteIndex = 0; paletteIndex < file.Palettes.Count; paletteIndex++)
            file.GetRawPalette(paletteIndex).CopyTo(colors, paletteIndex * file.Header.ColorsPerPalette);
        PixelImage sheet = new(file.Header.ColorsPerPalette, file.Palettes.Count, colors);
        output.Write(TexWorkspacePaths.Palettes, stream => PngCodec.WriteRgba(stream, sheet));
    }

    private static void WritePalettes(IWorkspaceWriter output, TexFile file)
    {
        for (Int32 paletteIndex = 0; paletteIndex < file.Palettes.Count; paletteIndex++)
        {
            Rgba32[] colors = file.GetRawPalette(paletteIndex);
            PixelImage palette = new(colors.Length, 1, colors);
            output.Write(WorkspacePaths.Palette(0, paletteIndex), stream => PngCodec.WriteRgba(stream, palette));
        }
    }

    private static void WritePreviews(IWorkspaceWriter output, TexFile file)
    {
        for (Int32 paletteIndex = 0; paletteIndex < file.Palettes.Count; paletteIndex++)
        {
            Int32 capturedPaletteIndex = paletteIndex;
            output.Write(
                TexWorkspacePaths.Preview(paletteIndex),
                stream => PngCodec.WriteRgba(stream, file.Decode(capturedPaletteIndex)));
        }
    }

    private static UInt32[][] ReadPalettes(PixelImage sheet)
    {
        UInt32[][] palettes = new UInt32[sheet.Height][];
        for (Int32 paletteIndex = 0; paletteIndex < palettes.Length; paletteIndex++)
        {
            UInt32[] palette = new UInt32[sheet.Width];
            for (Int32 colorIndex = 0; colorIndex < palette.Length; colorIndex++)
            {
                palette[colorIndex] = TexFile.EncodeBgra(
                    sheet.Pixels[paletteIndex * sheet.Width + colorIndex]);
            }
            palettes[paletteIndex] = palette;
        }
        return palettes;
    }

    private static Byte[] ReadIndices(
        DecodedPng image,
        IReadOnlyList<Rgba32> targetPalette,
        WorkspaceImportSpec spec)
    {
        PaletteColorMapper mapper = new(targetPalette, spec);
        Byte[] result = new Byte[image.Pixels.Length];
        if (!image.IsIndexed)
        {
            for (Int32 pixelIndex = 0; pixelIndex < result.Length; pixelIndex++)
                result[pixelIndex] = mapper.Map(image.Pixels[pixelIndex], $"TEX pixel {pixelIndex}");
            return result;
        }

        Byte[] sourceIndices = image.Indices
            ?? throw new InvalidDataException("The indexed PNG decoder did not return pixel indices.");
        Rgba32[] sourcePalette = image.Palette
            ?? throw new InvalidDataException("The indexed PNG decoder did not return a palette.");
        for (Int32 pixelIndex = 0; pixelIndex < result.Length; pixelIndex++)
        {
            Byte sourceIndex = sourceIndices[pixelIndex];
            Rgba32 sourceColor = sourcePalette[sourceIndex];
            result[pixelIndex] = sourceIndex < targetPalette.Count && sourceColor == targetPalette[sourceIndex]
                ? sourceIndex
                : mapper.Map(sourceColor, $"TEX pixel {pixelIndex}");
        }
        return result;
    }

    private static Byte[] EncodeDirectPixels(ReadOnlySpan<Rgba32> pixels, TexHeader header)
    {
        ValidateDirectPixelFormat(header);
        Byte[] result = new Byte[checked(pixels.Length * header.BytesPerPixel)];
        for (Int32 index = 0; index < pixels.Length; index++)
        {
            Rgba32 color = pixels[index];
            UInt32 value = EncodeComponent(color.R, header.RedMask, header.RedShift, header.RedMaximum, header.RedBits, 0, "red", index)
                | EncodeComponent(color.G, header.GreenMask, header.GreenShift, header.GreenMaximum, header.GreenBits, 0, "green", index)
                | EncodeComponent(color.B, header.BlueMask, header.BlueShift, header.BlueMaximum, header.BlueBits, 0, "blue", index)
                | EncodeComponent(color.A, header.AlphaMask, header.AlphaShift, header.AlphaMaximum, header.AlphaBits, Byte.MaxValue, "alpha", index);
            WritePixel(result.AsSpan(index * header.BytesPerPixel), value, header.BytesPerPixel);
        }
        return result;
    }

    private static void ValidateDirectPixelFormat(TexHeader header)
    {
        UInt32 storageMask = header.BytesPerPixel == sizeof(UInt32)
            ? UInt32.MaxValue
            : (1u << checked(header.BytesPerPixel * BitsPerByte)) - 1;
        UInt32[] masks =
        [
            unchecked((UInt32)header.RedMask),
            unchecked((UInt32)header.GreenMask),
            unchecked((UInt32)header.BlueMask),
            unchecked((UInt32)header.AlphaMask),
        ];
        UInt32 combinedMask = 0;
        foreach (UInt32 mask in masks)
        {
            if ((combinedMask & mask) != 0)
                throw new InvalidDataException("The TEX direct-color channel masks overlap.");
            combinedMask |= mask;
        }
        if (combinedMask != storageMask)
        {
            throw new InvalidDataException(
                $"The TEX channel masks 0x{combinedMask:X8} do not describe all {header.BytesPerPixel} pixel bytes.");
        }

        ValidateChannel("red", header.RedBits, header.RedMask, header.RedShift, header.RedMaximum);
        ValidateChannel("green", header.GreenBits, header.GreenMask, header.GreenShift, header.GreenMaximum);
        ValidateChannel("blue", header.BlueBits, header.BlueMask, header.BlueShift, header.BlueMaximum);
        ValidateChannel("alpha", header.AlphaBits, header.AlphaMask, header.AlphaShift, header.AlphaMaximum);
    }

    private static void ValidateChannel(String name, Int32 bits, Int32 mask, Int32 shift, Int32 maximum)
    {
        if (bits == 0 && mask == 0)
            return;
        if (bits is < 1 or > BitsPerByte || shift is < 0 or > 31)
        {
            throw new InvalidDataException(
                $"The TEX {name} channel cannot be represented losslessly by an eight-bit PNG channel.");
        }

        UInt32 expectedMaximum = maximum > 0 ? checked((UInt32)maximum) : (1u << bits) - 1;
        UInt32 expectedMask = expectedMaximum << shift;
        if (unchecked((UInt32)mask) != expectedMask)
            throw new InvalidDataException($"The TEX {name} maximum and shift do not match its channel mask.");
    }

    private static UInt32 EncodeComponent(
        Byte value,
        Int32 mask,
        Int32 shift,
        Int32 maximum,
        Int32 bits,
        Byte missingValue,
        String name,
        Int32 pixelIndex)
    {
        if (bits == 0 || mask == 0)
        {
            if (value != missingValue)
            {
                throw new InvalidDataException(
                    $"TEX pixel {pixelIndex} has {name} value {value}, but its pixel format has no {name} channel.");
            }
            return 0;
        }
        if (bits is < 1 or > 31 || shift is < 0 or > 31)
            throw new InvalidDataException($"The TEX {name} channel layout is invalid.");

        UInt32 max = maximum > 0 ? checked((UInt32)maximum) : (1u << bits) - 1;
        UInt32 component = ((UInt32)value * max + Byte.MaxValue / 2) / Byte.MaxValue;
        UInt32 packed = component << shift;
        UInt32 channelMask = unchecked((UInt32)mask);
        if ((packed & ~channelMask) != 0)
            throw new InvalidDataException($"The TEX {name} maximum does not fit its channel mask.");
        return packed;
    }

    private static void WritePixel(Span<Byte> destination, UInt32 value, Int32 byteCount)
    {
        switch (byteCount)
        {
            case 1:
                destination[0] = checked((Byte)value);
                break;
            case 2:
                BinaryPrimitives.WriteUInt16LittleEndian(destination, checked((UInt16)value));
                break;
            case 3:
                destination[0] = (Byte)value;
                destination[1] = (Byte)(value >> 8);
                destination[2] = (Byte)(value >> 16);
                break;
            case 4:
                BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
                break;
            default:
                throw new InvalidDataException($"Unsupported TEX byte depth {byteCount}.");
        }
    }

    private static Int32?[]? DecodeColorKeys(Byte[]? colorKeys)
    {
        if (colorKeys is null)
            return null;
        Int32?[] result = new Int32?[colorKeys.Length];
        for (Int32 index = 0; index < result.Length; index++)
            result[index] = colorKeys[index] == 0 ? null : colorKeys[index] - 1;
        return result;
    }

    private static Byte[]? EncodeColorKeys(Int32?[]? colorKeys, Int32 paletteCount, Int32 colorsPerPalette)
    {
        if (colorKeys is null)
            return null;
        if (colorKeys.Length != paletteCount)
        {
            throw new InvalidDataException(
                $"The workspace contains {colorKeys.Length} palette color keys for {paletteCount} palettes.");
        }

        Byte[] result = new Byte[colorKeys.Length];
        for (Int32 index = 0; index < result.Length; index++)
        {
            if (colorKeys[index] is not Int32 colorKey)
                continue;
            if (colorKey < 0 || colorKey >= colorsPerPalette || colorKey >= Byte.MaxValue)
            {
                throw new InvalidDataException(
                    $"Palette {index} color key {colorKey} is outside its representable color range.");
            }
            result[index] = checked((Byte)(colorKey + 1));
        }
        return result;
    }

    private static void ValidatePaletteIndex(TexFile file, Int32 paletteIndex)
    {
        if (paletteIndex < 0 || paletteIndex >= file.Palettes.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(paletteIndex),
                $"The TEX selects palette {paletteIndex}, but provides {file.Palettes.Count} palettes.");
        }
    }
}
