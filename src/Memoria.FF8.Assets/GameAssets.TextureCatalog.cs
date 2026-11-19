using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Albeoris.Games.FF8.Imaging.Mim;
using Albeoris.Games.FF8.Imaging.Tex;
using Albeoris.Games.FF8.Imaging.Tim;
using Memoria.FF8.Resources;
using System.Buffers.Binary;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    /// <summary>Discovers source textures without loading scenes or registering resident textures. The caller owns the source and must serialize its use.</summary>
    public IEnumerable<TextureCatalogEntry> EnumerateTextureCatalog(CancellationToken cancellationToken = default, Action<TextureCatalogDiagnostic>? diagnostic = null)
    {
        HashSet<TextureTarget> seen = [];
        foreach (ModelCatalogEntry model in EnumerateModelCatalog())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (model.LoadError is not null)
            {
                diagnostic?.Invoke(new(model.ResourcePath, "Discovering models", model.LoadError));
                continue;
            }
            if (model.Category == "World static")
            {
                // The complete shared material set is enumerated once from section42 below.
                continue;
            }
            IReadOnlyList<TextureBuildContext> contexts;
            try
            {
                contexts = ReadCatalogModelTextures(model);
            }
            catch (Exception exception) when (CatalogError(exception))
            {
                diagnostic?.Invoke(new(model.ResourcePath, "Discovering model textures", exception.Message));
                continue;
            }
            for (int index = 0; index < contexts.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TextureBuildContext context = contexts[index];
                TextureTarget target = TextureTarget.FromContext(context);
                if (seen.Add(target))
                {
                    yield return Entry(context, $"{model.Name} · texture {index}", model.Category) with { Reader = "model", Model = model, Index = index };
                }
            }
        }
        foreach (string field in fieldNames.Where(name => !string.IsNullOrWhiteSpace(name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<TextureBuildContext> contexts;
            try
            {
                contexts = ReadCatalogBackground(field);
            }
            catch (Exception exception) when (CatalogError(exception))
            {
                diagnostic?.Invoke(new($"field/{field}/{field}.mim", "Discovering field background", exception.Message));
                continue;
            }
            for (int index = 0; index < contexts.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return Entry(contexts[index], $"{field} · tile {index}", "Field background") with { Reader = "field", Index = index };
            }
        }
        foreach (string archiveName in new[] { "menu", "main" })
        {
            IFlArchive? archive = null;
            try
            {
                archive = OpenArchive(archiveName);
            }
            catch (Exception exception) when (CatalogError(exception))
            {
                diagnostic?.Invoke(new(archiveName, "Discovering artwork", exception.Message));
            }
            using (archive)
            {
                if (archive is null)
                {
                    continue;
                }
                foreach (IFlArchiveEntry source in archive.Entries.Where(entry => entry.RelativePath.EndsWith(".tex", StringComparison.OrdinalIgnoreCase)).OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ResourceKey owner = new(archiveName + "/" + Path.GetFileName(source.RelativePath.Replace('\\', '/')));
                    IReadOnlyList<TextureBuildContext> contexts;
                    try
                    {
                        contexts = ReadCatalogTex(owner);
                    }
                    catch (Exception exception) when (CatalogError(exception))
                    {
                        diagnostic?.Invoke(new(owner.Path, "Discovering TEX palettes", exception.Message));
                        continue;
                    }
                    foreach (TextureBuildContext context in contexts)
                    {
                        yield return Entry(context, $"{Path.GetFileName(owner.Path)} · palette {context.Key.Palette}", archiveName == "main" ? "System artwork" : "Menu and cards") with { Reader = "tex" };
                    }
                }
            }
        }
        foreach (int section in new[] { 38, 39, 40, 42 })
        {
            IReadOnlyList<TextureBuildContext> contexts;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                contexts = ReadCatalogWorldSection(section);
            }
            catch (Exception exception) when (CatalogError(exception))
            {
                diagnostic?.Invoke(new(NativeResourceNames.WorldSettings(resources.ResourceLanguage).Path, $"Discovering world section {section}", exception.Message));
                continue;
            }
            foreach (TextureBuildContext context in contexts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (seen.Add(TextureTarget.FromContext(context)))
                {
                    string name = section == 42 ? $"World static shape {64 + int.Parse(context.Key.Subresource.Split('/')[^1], System.Globalization.CultureInfo.InvariantCulture)} · palette {context.Key.Palette}" : $"World {context.Key.Subresource} · palette {context.Key.Palette}";
                    yield return Entry(context, name, section == 42 ? "World static" : "World textures") with { Reader = "world-section", Section = section };
                }
            }
        }
        IReadOnlyList<TextureBuildContext> menuSlots;
        try
        {
            menuSlots = ReadCatalogMenuSlots();
        }
        catch (Exception exception) when (CatalogError(exception))
        {
            diagnostic?.Invoke(new("menu/mngrp.bin", "Discovering tutorial artwork", exception.Message));
            menuSlots = [];
        }
        foreach (TextureBuildContext context in menuSlots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return Entry(context, $"Tutorial {context.Key.Subresource} · palette {context.Key.Palette}", "Tutorial artwork") with { Reader = "menu-slots" };
        }
        for (int page = 0; page < 20; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<TextureBuildContext> contexts;
            try
            {
                contexts = ReadCatalogTexl(page);
            }
            catch (Exception exception) when (CatalogError(exception))
            {
                diagnostic?.Invoke(new("world/texl.obj", $"Discovering terrain page {page}", exception.Message));
                continue;
            }
            foreach (TextureBuildContext context in contexts)
            {
                yield return Entry(context, $"Terrain page {page} · palette {context.Key.Palette}", "World terrain") with { Reader = "texl", Index = page };
            }
        }
        for (int index = 4; index <= 33; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ResourceKey owner = new("main/" + NativeStartupArtwork.ResourceName(index));
            TextureBuildContext? context = null;
            try
            {
                ResourceContent content = resources.Get(owner);
                context = new(new(owner, "atlas"), resources.Source, content.Revision, "native-artwork", NativeStartupArtwork.Width, NativeStartupArtwork.Height, () => NativeStartupArtwork.Decode(resources.Read(owner)), encodedSource: content);
            }
            catch (Exception exception) when (CatalogError(exception))
            {
                diagnostic?.Invoke(new(owner.Path, "Discovering startup artwork", exception.Message));
            }
            if (context is not null)
            {
                yield return Entry(context, Path.GetFileName(owner.Path), "Startup artwork") with { Reader = "startup" };
            }
        }
        foreach (TextureCatalogEntry entry in EnumerateEffectTextureCatalog(cancellationToken, diagnostic))
        {
            if (seen.Add(entry.Target))
            {
                yield return entry;
            }
        }
        IReadOnlyList<TextureBuildContext> cards;
        try
        {
            cards = ReadCatalogCardArtwork();
        }
        catch (Exception exception) when (CatalogError(exception))
        {
            diagnostic?.Invoke(new("cards/cards", "Discovering native card artwork", exception.Message));
            cards = [];
        }
        foreach (TextureBuildContext context in cards)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return Entry(context, $"Triple Triad {context.Key.Subresource}", "Triple Triad") with { Reader = "cards" };
        }
    }

    /// <summary>Reopens one catalog unit; no registration, gameplay initialization, or GPU readback occurs.</summary>
    public TextureBuildContext LoadCatalogTexture(TextureCatalogEntry entry)
    {
        return LoadCatalogTextureUnit(entry).Single(context => TextureTarget.FromContext(context) == entry.Target);
    }

    public IReadOnlyList<TextureBuildContext> LoadCatalogTextureUnit(TextureCatalogEntry entry)
    {
        ResourceKey owner = new(entry.Target.Owner);
        return entry.Reader switch
        {
            "model" => ReadCatalogModelTextures(entry.Model ?? throw new InvalidDataException("The model catalog locator is missing.")),
            "field" => ReadCatalogBackground(owner.Path.Split('/')[1]),
            "tex" => ReadCatalogTex(owner),
            "world-section" => ReadCatalogWorldSection(entry.Section),
            "texl" => ReadCatalogTexl(entry.Index),
            "menu-slots" => ReadCatalogMenuSlots(),
            "standalone-tim" => ReadCatalogStandaloneTim(owner),
            "cinematic" => ReadCatalogCinematicTextures(entry.Section),
            "common-page" => ReadCatalogCommonEffectPages(entry.Index),
            "cards" => ReadCatalogCardArtwork(),
            "startup" => [Context(new(owner, "atlas"), NativeStartupArtwork.Decode(resources.Read(owner)), "native-artwork")],
            _ => throw new NotSupportedException($"Texture {entry.Name} has no source reader.")
        };
    }

    private static TextureCatalogEntry Entry(TextureBuildContext context, string name, string category) => new(TextureTarget.FromContext(context), name, category, context.Layout, context.LogicalWidth, context.LogicalHeight);

    private TextureBuildContext Context(TextureKey key, PixelImage image, string layout, ResourceSourceDescriptor? source = null, TextureSourceRegion? region = null, TextureSourceAddress? nativeSheet = null, TextureSourceAddress? nativeAlias = null) => new(key, source ?? resources.Source, resources.Get(key.Owner).Revision, layout, image.Width, image.Height, () => image, region, encodedSource: resources.Get(key.Owner)) { NativeSheet = nativeSheet, NativeAlias = nativeAlias };

    private IReadOnlyList<TextureBuildContext> ReadCatalogModelTextures(ModelCatalogEntry entry)
    {
        ResourceKey owner = new(entry.ResourcePath);
        if (entry.Category == "Field")
        {
            if (resources.GetSource<IFieldModelSource>() is null)
            {
                return ReadClassicCatalogFieldTextures(entry);
            }
            FieldModel model = ReadFieldModelCatalogSource(entry);
            string prefix = model.TextureResourceName is { } name ? $"model/{entry.ModelIndex}/{(model.MainCharacterTextures ? "main" : "second")}/{name}/texture" : $"model/{entry.ModelIndex}/texture";
            string layout = model.TextureResourceName is null ? "classic-model" : "classic-field-model";
            return model.Textures.Select((image, index) => Context(new(owner, $"{prefix}/{index}"), image, layout, model.TextureSourceDescriptor, nativeSheet: NativeFieldModelTextureSheets.Address(model, index))).ToArray();
        }
        if (entry.Category is "Battle model" or "Battle enemy" or "Battle weapon" && resources.GetSource<IBattleModelSource>() is null)
        {
            return ReadClassicCatalogBattleTextures(entry);
        }
        EditableModel decoded = LoadCatalogModel(entry);
        string texturePrefix = entry.Category == "World character" ? $"model/{entry.ModelIndex}/texture" : entry.Category == "World static" ? $"section/42/texture/{entry.ModelIndex - 64}" : "texture";
        string contract = entry.Category.StartsWith("World", StringComparison.Ordinal) ? entry.Category == "World static" ? "classic-world-section" : "classic-model" : "classic-battle";
        return decoded.Textures.Select((image, index) => Context(new(owner, entry.Category == "World static" ? texturePrefix : $"{texturePrefix}/{index}"), image, contract)).ToArray();
    }

    private IReadOnlyList<TextureBuildContext> ReadCatalogBackground(string field)
    {
        ResourceKey owner = new($"field/{field}/{field}.mim");
        MimFile mim = MimFile.Read(resources.Read(owner));
        MapFile map = MapFile.Read(resources.Read(new($"field/{field}/{field}.map")));
        IReadOnlyList<TextureSourceAddress> atlas = FieldBackgroundTileDecoder.AtlasAddresses(owner, map.Tiles);
        return map.Tiles.Select((tile, index) => new TextureBuildContext(new(owner, $"tile/{index}", tile.PaletteId), resources.Source, resources.Get(owner).Revision, "classic-field-tile", MimFile.TileSize, MimFile.TileSize, () => FieldBackgroundTileDecoder.Decode(mim, tile), new(tile.TextureId * 64, 256, tile.Uses8BitIndices ? 8 : 4), encodedSource: resources.Get(owner)) { NativeAlias = FieldBackgroundTileDecoder.Address(owner, tile), NativeSheet = atlas[index] }).ToArray();
    }

    private IReadOnlyList<TextureBuildContext> ReadCatalogTex(ResourceKey owner)
    {
        TexFile tex = TexFile.Read(resources.Read(owner));
        return Enumerable.Range(0, Math.Max(1, tex.Palettes.Count)).Select(palette => new TextureBuildContext(new(owner, "atlas", palette), resources.Source, resources.Get(owner).Revision, "native-artwork", tex.Header.Width, tex.Header.Height, () => tex.Decode(palette), paletteImage: tex.Decode, paletteCount: Math.Max(1, tex.Palettes.Count), encodedSource: resources.Get(owner))).ToArray();
    }

    private IReadOnlyList<TextureBuildContext> ReadCatalogCardArtwork()
    {
        NativeTripleTriadArtwork cards = NativeTripleTriadArtwork.Read(ReadNativeMemory());
        ResourceKey owner = new("cards/cards");
        TextureBuildContext Source(string member, PixelImage image) => new(new(owner, member), resources.Source, 1, "native-artwork", image.Width, image.Height, () => image);
        List<TextureBuildContext> result = [Source("back", cards.Back), Source("background", cards.Background), Source("board-background", cards.BoardBackground)];
        for (int index = 0; index < CardArtwork.CardCount; index++)
        {
            result.Add(Source($"front/{index}", cards.GetFront(index)));
        }
        for (int index = 0; index < 8; index++)
        {
            result.Add(Source($"element/{index}", cards.GetElement((Memoria.FF8.Runtime.TripleTriadElement)(index + 1))));
        }
        for (int index = 0; index < 3; index++)
        {
            result.Add(Source($"outcome/{index}", cards.GetOutcomeBanner(index)));
            result.Add(Source($"capture/{index}", cards.GetCaptureBanner(index)));
        }
        return result;
    }

    private IReadOnlyList<TextureBuildContext> ReadCatalogWorldSection(int section)
    {
        ResourceKey owner = NativeResourceNames.WorldSettings(resources.ResourceLanguage);
        byte[] bytes = resources.Read(owner);
        int[] sections = WorldAssetReader.ReadSections(bytes);
        int start = sections[section - 1];
        int[] pointers = WorldAssetReader.InnerPointers(bytes, start);
        List<TextureBuildContext> result = [];
        for (int member = 0; member < pointers.Length; member++)
        {
            TimImage tim = WorldAssetReader.ReadTim(bytes, start + pointers[member]).Image;
            result.AddRange(TimContexts(owner, $"section/{section}/texture/{member}", tim, "classic-world-section"));
        }
        return result;
    }

    private IReadOnlyList<TextureBuildContext> ReadCatalogTexl(int page)
    {
        ResourceKey owner = new("world/texl.obj");
        byte[] bytes = resources.Read(owner);
        TimImage tim = WorldAssetReader.ReadTim(bytes, checked(page * 0x12800)).Image;
        return TimContexts(owner, $"texl/{page}", tim, "classic-world-texl");
    }

    private IReadOnlyList<TextureBuildContext> ReadCatalogMenuSlots()
    {
        ResourceKey owner = new("menu/mngrp.bin");
        byte[] header = resources.Read(new("menu/mngrphd.bin"));
        byte[] content = resources.Read(owner);
        if (header.Length % 8 != 0)
        {
            throw new InvalidDataException("Tutorial artwork directory requires complete offset/length records.");
        }
        List<TextureBuildContext> result = [];
        for (int slot = 0; slot < header.Length / 8; slot++)
        {
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(slot * 8));
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(slot * 8 + 4));
            if (offset is 0 or uint.MaxValue || size < 8)
            {
                continue;
            }
            if ((ulong)offset - 1 + size > (ulong)content.Length)
            {
                throw new InvalidDataException("Tutorial artwork section is outside its source resource.");
            }
            ReadOnlySpan<byte> bytes = content.AsSpan(checked((int)offset - 1), checked((int)size));
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x10 || BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]) is not (2 or 3 or 8 or 9))
            {
                continue;
            }
            TimImage tim = WorldAssetReader.ReadTim(bytes.ToArray(), 0).Image;
            result.AddRange(TimContexts(owner, $"slot{slot:000}", tim, "native-artwork"));
        }
        return result;
    }

    private IReadOnlyList<TextureBuildContext> TimContexts(ResourceKey owner, string subresource, TimImage tim, string layout)
    {
        int bits = tim.PixelMode == TimPixelMode.Indexed4 ? 4 : tim.PixelMode == TimPixelMode.Indexed8 ? 8 : 16;
        return Enumerable.Range(0, Math.Max(1, tim.Palettes.Count)).Select(palette => new TextureBuildContext(new(owner, subresource, palette), resources.Source, resources.Get(owner).Revision, layout, tim.PixelWidth, tim.Height, () => tim.Decode(palette), new(tim.X, tim.Y, bits), tim.Decode, Math.Max(1, tim.Palettes.Count), resources.Get(owner))).ToArray();
    }
}
