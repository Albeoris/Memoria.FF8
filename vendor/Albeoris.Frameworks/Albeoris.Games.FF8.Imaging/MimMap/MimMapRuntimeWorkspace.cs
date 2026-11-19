using Albeoris.Games.FF8.Imaging.Map;
using Albeoris.Games.FF8.Imaging.Workspace;
using Albeoris.Games.FF8.Imaging.Workspace.Internal;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Albeoris.Games.FF8.Imaging.MimMap;

public sealed record MimMapRuntimeTile(Int32 Index, Int32 Palette, PixelImage Image);

/// <summary>Texture-only exchange retaining physical pixels and native MAP composition. The caller verifies source ownership and fingerprints.</summary>
public static class MimMapRuntimeWorkspace
{
    public static void Export(MimMapFile file, WorkspaceExportSpec spec, Func<Int32, PixelImage>? tileResolver = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(spec);
        if (spec.Destination.Format != WorkspaceContainerFormat.OpenRaster)
        {
            throw new ArgumentException("Field artwork requires an OpenRaster workspace.", nameof(spec));
        }
        using IWorkspaceWriter output = WorkspaceStorage.CreateWriter(spec.Destination);
        Byte[] mim = file.Mim.Write();
        Byte[] map = file.Map.Write();
        MimMapLayout layout = MimMapLayout.Read(file.Mim, map);
        MimMapAtlas atlas = new(file.Mim);
        Dictionary<Int32, PixelImage> resolved = [];
        Int32 scale = 1;
        Int64 retained = 0;
        if (tileResolver is not null)
        {
            foreach (MimMapTile tile in layout.Groups.SelectMany(group => group.Tiles))
            {
                cancellationToken.ThrowIfCancellationRequested();
                PixelImage image = tileResolver(tile.Index);
                if (image.Width < 16 || image.Width != image.Height || image.Width % 16 != 0)
                {
                    throw new InvalidDataException($"Resolved field tile {tile.Index} requires a uniform integer enlargement of 16x16.");
                }
                retained += image.Pixels.Length;
                if (retained > PixelImage.MaximumPixelCount)
                {
                    throw new InvalidDataException("Resolved field artwork exceeds the 64 million pixel workspace budget.");
                }
                resolved.Add(tile.Index, image);
                scale = Math.Max(scale, image.Width / 16);
            }
        }
        Int32 width = checked(layout.Width * scale);
        Int32 height = checked(layout.Height * scale);
        PixelImage preview = new(width, height);
        WriteBytes(output, MimMapWorkspacePaths.SourceMim, mim);
        WriteBytes(output, MimMapWorkspacePaths.SourceMap, map);
        WriteImage(output, MimMapWorkspacePaths.SourceMimLayer, OpenRasterBinaryPayload.Encode(mim));
        WriteImage(output, MimMapWorkspacePaths.SourceMapLayer, OpenRasterBinaryPayload.Encode(map));
        WriteImage(output, MimMapWorkspacePaths.Palette, MimMapPaletteCodec.CreateImage(file.Mim));
        MimMapTextureRenderer renderer = new(layout, atlas);
        foreach (MimMapTextureGroup group in layout.Groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MimMapRenderedTexture ownership = renderer.Render(group);
            PixelImage layer = new(width, height);
            for (Int32 y = 0; y < height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (Int32 x = 0; x < width; x++)
                {
                    Int32 index = y / scale * layout.Width + x / scale;
                    Int32 owner = ownership.Owners[index];
                    if (owner < 0)
                    {
                        continue;
                    }
                    Rgba32 pixel;
                    if (resolved.TryGetValue(owner, out PixelImage? image))
                    {
                        MimMapTile tile = layout.Tiles[owner];
                        Int32 localX = x - (tile.X + layout.OffsetX) * scale;
                        Int32 localY = y - (tile.Y + layout.OffsetY) * scale;
                        pixel = image[localX * image.Width / (16 * scale), localY * image.Height / (16 * scale)];
                    }
                    else
                    {
                        pixel = ownership.Image.Pixels[index];
                    }
                    layer[x, y] = pixel.A == 128 ? new(pixel.R, pixel.G, pixel.B, 255) : pixel;
                }
            }
            WriteImage(output, MimMapWorkspacePaths.Texture(group.PupuId), layer);
            MimMapTile first = group.Tiles[0];
            if (first.AnimationId == Byte.MaxValue || first.AnimationState == 0)
            {
                MimMapPreviewRenderer.Blend(preview, layer, first.BlendMode);
            }
        }
        WriteImage(output, MimMapWorkspacePaths.MergedImage, preview);
        WriteImage(output, MimMapWorkspacePaths.Thumbnail, MimMapPreviewRenderer.CreateThumbnail(preview));
        OpenRasterStack.Write(output, layout, scale);
        WorkspaceJson.Write(output, MimMapWorkspacePaths.Manifest, new MimMapWorkspaceManifest());
    }

    /// <summary>Reads one layer at a time. Hidden binary/palette layers are not trusted after an editor resize.</summary>
    public static IReadOnlyList<MimMapRuntimeTile> Import(WorkspaceImportSpec spec, MimMapFile verifiedSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(verifiedSource);
        using IWorkspaceReader input = WorkspaceStorage.CreateReader(spec.Source);
        if (!input.Read("mimetype").AsSpan().SequenceEqual(Encoding.ASCII.GetBytes("image/openraster")))
        {
            throw new InvalidDataException("Field artwork must be an OpenRaster document.");
        }
        MimMapLayout layout = MimMapLayout.Read(verifiedSource.Mim, verifiedSource.Map.Write());
        XDocument stack = ReadStack(input.Read("stack.xml"));
        XElement root = stack.Root ?? throw new InvalidDataException("OpenRaster stack has no image root.");
        foreach (XElement layer in stack.Descendants("layer"))
        {
            String name = (String?)layer.Attribute("name") ?? "";
            if (name.StartsWith("Pupu ", StringComparison.Ordinal) && (name.Length < 13 || !UInt32.TryParse(name.AsSpan(5, 8), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _)))
            {
                throw new InvalidDataException($"Invalid Pupu layer identity '{name}'. Restore the original eight-digit exported token.");
            }
            if (!name.StartsWith("Pupu ", StringComparison.Ordinal) && name is not (OpenRasterStack.PaletteLayerName or OpenRasterStack.SourceMimLayerName or OpenRasterStack.SourceMapLayerName))
            {
                throw new InvalidDataException($"Unknown field layer '{name}'. Paint inside an exported Pupu layer; additional composition layers have no native texture ownership.");
            }
        }
        Int32 width = ReadInteger(root, "w");
        Int32 height = ReadInteger(root, "h");
        if (width <= 0 || height <= 0 || width % layout.Width != 0 || height % layout.Height != 0 || width / layout.Width != height / layout.Height || width < layout.Width || width > PixelImage.MaximumDimension || height > PixelImage.MaximumDimension || (Int64)width * height > PixelImage.MaximumPixelCount)
        {
            throw new InvalidDataException($"Field canvas must be a uniform integer enlargement of {layout.Width}x{layout.Height}, within supported image limits.");
        }
        Int32 scale = width / layout.Width;
        Int64 tilePixels = (Int64)layout.Groups.Sum(group => group.Tiles.Count) * 256 * scale * scale;
        if (tilePixels > PixelImage.MaximumPixelCount)
        {
            throw new InvalidDataException("Compiled field artwork exceeds the 64 million pixel workspace budget. Reduce the enlargement.");
        }
        OpenRasterLayerSet references = OpenRasterStack.Read(input);
        HashSet<UInt32> expected = layout.Groups.Select(group => group.PupuId).ToHashSet();
        if (!expected.SetEquals(references.Textures.Keys))
        {
            throw new InvalidDataException("Field artwork has missing or unknown Pupu layers. Retain every exported Pupu identity; repaint transparently instead of deleting layers.");
        }
        MimMapAtlas atlas = new(verifiedSource.Mim);
        MimMapTextureRenderer renderer = new(layout, atlas);
        List<MimMapRuntimeTile> result = [];
        foreach (MimMapTextureGroup group in layout.Groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenRasterLayerReference reference = references.Textures[group.PupuId];
            XElement declaration = FindLayer(stack, group.PupuId);
            ValidateComposition(declaration, group.Tiles[0]);
            PixelImage layer = PngCodec.Read(input.Read(reference.Path)).ToPixelImage();
            if (reference.X < 0 || reference.Y < 0 || (Int64)reference.X + layer.Width > width || (Int64)reference.Y + layer.Height > height)
            {
                throw new InvalidDataException($"Pupu {group.PupuId:X8} lies outside its enlarged canvas. Undo layer movement or cropping.");
            }
            MimMapRenderedTexture original = renderer.Render(group);
            for (Int32 y = 0; y < layer.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (Int32 x = 0; x < layer.Width; x++)
                {
                    Int32 logical = (y + reference.Y) / scale * layout.Width + (x + reference.X) / scale;
                    if (original.Owners[logical] < 0 && layer[x, y].A != 0)
                    {
                        throw new InvalidDataException($"Pupu {group.PupuId:X8} paints outside MAP ownership at ({x + reference.X},{y + reference.Y}). Undo structural movement; paint only the exported tile areas.");
                    }
                }
            }
            foreach (MimMapTile tile in group.Tiles)
            {
                Int32 startX = checked((tile.X + layout.OffsetX) * scale);
                Int32 startY = checked((tile.Y + layout.OffsetY) * scale);
                PixelImage image = new(16 * scale, 16 * scale);
                for (Int32 y = 0; y < image.Height; y++)
                {
                    for (Int32 x = 0; x < image.Width; x++)
                    {
                        Rgba32 native = atlas.ReadNativePixel(tile, x / scale, y / scale);
                        Int32 owner = original.Owners[(startY / scale + y / scale) * layout.Width + startX / scale + x / scale];
                        Int32 editedX = startX + x - reference.X;
                        Int32 editedY = startY + y - reference.Y;
                        Rgba32 pixel = owner != tile.Index ? native : (UInt32)editedX < layer.Width && (UInt32)editedY < layer.Height ? layer[editedX, editedY] : default;
                        if (pixel.A == 255 && native.A == 128)
                        {
                            pixel = new(pixel.R, pixel.G, pixel.B, 128);
                        }
                        image[x, y] = pixel;
                    }
                }
                result.Add(new(tile.Index, tile.PaletteId, image));
            }
        }
        return result;
    }

    private static XElement FindLayer(XDocument document, UInt32 id)
    {
        String prefix = $"Pupu {id:X8}";
        XElement[] layers = document.Descendants("layer").Where(layer => ((String?)layer.Attribute("name"))?.StartsWith(prefix, StringComparison.Ordinal) == true).ToArray();
        return layers.Length == 1 ? layers[0] : throw new InvalidDataException($"Duplicate or missing Pupu {id:X8} layer identity.");
    }

    private static void WriteBytes(IWorkspaceWriter output, String path, Byte[] bytes) => output.Write(path, stream => stream.Write(bytes));

    private static void WriteImage(IWorkspaceWriter output, String path, PixelImage image) => output.Write(path, stream => PngCodec.WriteRgba(stream, image));

    private static void ValidateComposition(XElement layer, MimMapTile tile)
    {
        Double expectedOpacity = tile.BlendMode switch { MapBlendMode.Average => 0.5, MapBlendMode.QuarterAdd => 0.25, _ => 1.0 };
        String expectedBlend = tile.BlendMode is MapBlendMode.Add or MapBlendMode.Average or MapBlendMode.QuarterAdd ? "svg:plus" : "svg:src-over";
        Double opacity = ReadOpacity(layer);
        String blend = (String?)layer.Attribute("composite-op") ?? "svg:src-over";
        if (opacity != expectedOpacity || blend != expectedBlend || layer.Ancestors("stack").Any(stack => ReadOptionalCoordinate(stack, "x") != 0 || ReadOptionalCoordinate(stack, "y") != 0 || ReadOpacity(stack) != 1 || stack.Attribute("composite-op") is XAttribute operation && operation.Value != "svg:src-over"))
        {
            throw new InvalidDataException($"Pupu layer {tile.Index} changes native opacity, blend or group transforms. Restore exported composition; texture import changes artwork only.");
        }
    }

    private static Double ReadOpacity(XElement element)
    {
        String? value = (String?)element.Attribute("opacity");
        if (value is null)
        {
            return 1;
        }
        return Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out Double opacity) && Double.IsFinite(opacity) ? opacity : throw new InvalidDataException("OpenRaster opacity must be a finite number.");
    }

    private static Int32 ReadOptionalCoordinate(XElement element, String name) => element.Attribute(name) is null ? 0 : ReadInteger(element, name);

    private static Int32 ReadInteger(XElement element, String name) => Int32.TryParse((String?)element.Attribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 value) ? value : throw new InvalidDataException($"OpenRaster has invalid {name}.");

    private static XDocument ReadStack(Byte[] content)
    {
        using MemoryStream stream = new(content, false);
        using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, MaxCharactersInDocument = 4 * 1024 * 1024 });
        return XDocument.Load(reader);
    }
}
