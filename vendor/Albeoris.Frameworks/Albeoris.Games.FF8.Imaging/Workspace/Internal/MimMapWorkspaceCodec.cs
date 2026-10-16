using Albeoris.Games.FF8.Imaging.MimMap;
using System.Text;

namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal static class MimMapWorkspaceCodec
{
    public static void Export(MimMapFile file, IWorkspaceWriter output, IWorkspaceExportScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(scheduler);
        Byte[] mimContent = file.Mim.Write();
        Byte[] mapContent = file.Map.Write();
        MimMapLayout layout = MimMapLayout.Read(file.Mim, mapContent);
        MimMapTextureRenderer renderer = new(layout, new MimMapAtlas(file.Mim));
        Task<Byte[]> mimPayload = scheduler.Schedule(() => EncodePng(OpenRasterBinaryPayload.Encode(mimContent)));
        Task<Byte[]> mapPayload = scheduler.Schedule(() => EncodePng(OpenRasterBinaryPayload.Encode(mapContent)));
        Task<Byte[]> palette = scheduler.Schedule(() => EncodePng(MimMapPaletteCodec.CreateImage(file.Mim)));
        List<Task<EncodedTexture>> textureTasks = layout.Groups.Select(group => scheduler.Schedule(() => EncodeTexture(renderer, group))).ToList();

        output.Write(MimMapWorkspacePaths.SourceMim, stream => stream.Write(mimContent));
        output.Write(MimMapWorkspacePaths.SourceMap, stream => stream.Write(mapContent));
        Task.WhenAll(textureTasks.Cast<Task>().Append(mimPayload).Append(mapPayload).Append(palette)).GetAwaiter().GetResult();
        output.Write(MimMapWorkspacePaths.SourceMimLayer, stream => stream.Write(mimPayload.Result));
        output.Write(MimMapWorkspacePaths.SourceMapLayer, stream => stream.Write(mapPayload.Result));
        output.Write(MimMapWorkspacePaths.Palette, stream => stream.Write(palette.Result));
        Dictionary<UInt32, MimMapRenderedTexture> textures = [];
        foreach (Task<EncodedTexture> task in textureTasks)
        {
            EncodedTexture texture = task.Result;
            textures.Add(texture.PupuId, texture.Texture);
            output.Write(MimMapWorkspacePaths.Texture(texture.PupuId), stream => stream.Write(texture.Png));
        }
        PixelImage preview = MimMapPreviewRenderer.Render(layout, textures);
        Task<Byte[]> mergedImagePng = scheduler.Schedule(() => EncodePng(preview));
        Task<Byte[]> thumbnailPng = scheduler.Schedule(() => EncodePng(MimMapPreviewRenderer.CreateThumbnail(preview)));
        Task.WhenAll(mergedImagePng, thumbnailPng).GetAwaiter().GetResult();
        output.Write(MimMapWorkspacePaths.MergedImage, stream => stream.Write(mergedImagePng.Result));
        output.Write(MimMapWorkspacePaths.Thumbnail, stream => stream.Write(thumbnailPng.Result));
        OpenRasterStack.Write(output, layout);
        WorkspaceJson.Write(output, MimMapWorkspacePaths.Manifest, new MimMapWorkspaceManifest());
    }

    private static EncodedTexture EncodeTexture(MimMapTextureRenderer renderer, MimMapTextureGroup group)
    {
        MimMapRenderedTexture texture = renderer.Render(group);
        return new EncodedTexture(group.PupuId, texture, EncodePng(texture.Image));
    }

    private static Byte[] EncodePng(PixelImage image)
    {
        using MemoryStream output = new();
        PngCodec.WriteRgba(output, image);
        return output.ToArray();
    }

    public static MimMapFile Import(WorkspaceImportSpec spec, IWorkspaceReader input)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(input);
        ValidateOpenRaster(input);
        if (input.Exists(MimMapWorkspacePaths.Manifest))
            ValidateManifest(WorkspaceJson.Read<MimMapWorkspaceManifest>(input, MimMapWorkspacePaths.Manifest));
        OpenRasterLayerSet layers = OpenRasterStack.Read(input);
        Byte[] mimContent = ReadSource(input, MimMapWorkspacePaths.SourceMim, layers.SourceMim, "source MIM");
        Byte[] mapContent = ReadSource(input, MimMapWorkspacePaths.SourceMap, layers.SourceMap, "source MAP");
        MimMapFile file = MimMapFile.Read(mimContent, mapContent);
        MimMapLayout layout = MimMapLayout.Read(file.Mim, mapContent);
        MimMapAtlas sourceAtlas = new(file.Mim);
        MimMapTextureRenderer renderer = new(layout, sourceAtlas);
        Dictionary<UInt32, MimMapRenderedTexture> originals = [];
        Dictionary<UInt32, PixelImage> edited = [];

        foreach (MimMapTextureGroup group in layout.Groups)
        {
            if (!layers.Textures.TryGetValue(group.PupuId, out OpenRasterLayerReference? layer))
                throw new FileNotFoundException($"The OpenRaster document has no layer for Pupu texture 0x{group.PupuId:X8}.");
            PixelImage changed = ReadLayer(input, layer, layout.Width, layout.Height);
            MimMapRenderedTexture original = renderer.Render(group);
            ValidateDimensions(layer.Path, changed, original.Image);
            originals.Add(group.PupuId, original);
            edited.Add(group.PupuId, changed);
        }
        if (layers.Palette is not null && input.Exists(layers.Palette.Path))
            MimMapPaletteCodec.Apply(file.Mim, ReadLayer(input, layers.Palette, MimMapPaletteCodec.Width, MimMapPaletteCodec.Height));
        else
            MimMapPaletteSynthesizer.AddMissingColors(file.Mim, layout, sourceAtlas, originals, edited, spec);
        MimMapAtlas atlas = new(file.Mim, spec);
        foreach (MimMapTextureGroup group in layout.Groups)
            ApplyChanges(group, layout, atlas, originals[group.PupuId], edited[group.PupuId]);
        return file;
    }

    private static void ValidateOpenRaster(IWorkspaceReader input)
    {
        Byte[] expected = Encoding.ASCII.GetBytes("image/openraster");
        if (!input.Read(MimMapWorkspacePaths.MimeType).AsSpan().SequenceEqual(expected))
            throw new InvalidDataException("The workspace is not an OpenRaster document.");
    }

    private static Byte[] ReadSource(IWorkspaceReader input, String binaryPath, OpenRasterLayerReference? layer, String description)
    {
        if (input.Exists(binaryPath))
            return input.Read(binaryPath);
        if (layer is null || !input.Exists(layer.Path))
            throw new FileNotFoundException($"The OpenRaster workspace has no {description} data.", binaryPath);
        return OpenRasterBinaryPayload.Decode(PngCodec.Read(input.Read(layer.Path)).ToPixelImage(), description);
    }

    private static PixelImage ReadLayer(IWorkspaceReader input, OpenRasterLayerReference layer, Int32 width, Int32 height)
    {
        PixelImage source = PngCodec.Read(input.Read(layer.Path)).ToPixelImage();
        if (source.Width == width && source.Height == height && layer.X == 0 && layer.Y == 0)
            return source;
        PixelImage result = new(width, height);
        for (Int32 y = 0; y < source.Height; y++)
        {
            Int32 destinationY = checked(layer.Y + y);
            if ((UInt32)destinationY >= result.Height)
                continue;
            for (Int32 x = 0; x < source.Width; x++)
            {
                Int32 destinationX = checked(layer.X + x);
                if ((UInt32)destinationX < result.Width)
                    result[destinationX, destinationY] = source[x, y];
            }
        }
        return result;
    }

    private static void ApplyChanges(MimMapTextureGroup group, MimMapLayout layout, MimMapAtlas atlas, MimMapRenderedTexture original, PixelImage edited)
    {
        for (Int32 index = 0; index < edited.Pixels.Length; index++)
        {
            if (edited.Pixels[index] == original.Image.Pixels[index])
                continue;
            Int32 owner = original.Owners[index];
            if (owner < 0)
                throw new InvalidDataException($"Texture 0x{group.PupuId:X8} changes canvas pixel ({index % edited.Width}, {index / edited.Width}) that is not covered by a MAP tile.");
            MimMapTile tile = layout.Tiles[owner];
            Int32 x = index % edited.Width - tile.X - layout.OffsetX;
            Int32 y = index / edited.Width - tile.Y - layout.OffsetY;
            atlas.WritePixel(tile, x, y, edited.Pixels[index]);
        }
    }

    private static void ValidateManifest(MimMapWorkspaceManifest manifest)
    {
        if (!String.Equals(manifest.Format, MimMapWorkspaceManifest.FormatName, StringComparison.Ordinal))
            throw new InvalidDataException($"Workspace format '{manifest.Format}' is not '{MimMapWorkspaceManifest.FormatName}'.");
        if (manifest.Version != MimMapWorkspaceManifest.CurrentVersion)
            throw new InvalidDataException($"Unsupported MIM/MAP workspace version {manifest.Version}.");
    }

    private static void ValidateDimensions(String path, PixelImage actual, PixelImage expected)
    {
        if (actual.Width != expected.Width || actual.Height != expected.Height)
            throw new InvalidDataException($"MIM/MAP texture '{path}' is {actual.Width}x{actual.Height}; expected {expected.Width}x{expected.Height}.");
    }

    private sealed record EncodedTexture(UInt32 PupuId, MimMapRenderedTexture Texture, Byte[] Png);
}
