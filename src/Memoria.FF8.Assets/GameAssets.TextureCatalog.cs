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
    private (ResourceKey Key, long Revision, byte[] Bytes)? textureCatalogFieldBytes;
    private (ResourceKey Key, long Revision, byte[] Bytes)? textureCatalogLibraryBytes;
    private static bool TextureCatalogError(Exception exception) => CatalogError(exception) || exception is NotImplementedException;
    private static string TextureCatalogOutcome(Exception exception) => exception is FileNotFoundException or DirectoryNotFoundException ? "skipped" : exception is NotSupportedException or NotImplementedException ? "unsupported" : "failed";
    /// <summary>Discovers source textures without loading scenes or registering resident textures. The caller owns the source and must serialize its use.</summary>
    public IEnumerable<TextureCatalogEntry> EnumerateTextureCatalog(CancellationToken cancellationToken = default, Action<TextureCatalogDiagnostic>? diagnostic = null, bool includeFieldTiles = false)
    {
        HashSet<TextureTarget> seen = [];
        Dictionary<TextureTarget, List<string>> usageAliases = [];
        foreach (ModelCatalogEntry model in EnumerateModelCatalog(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (model.LoadError is not null)
            {
                diagnostic?.Invoke(new(model.ResourcePath, "Discovering models", model.LoadError, "skipped"));
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
                contexts = ReadAuthoringModelTextures(model);
            }
            catch (Exception exception) when (TextureCatalogError(exception))
            {
                diagnostic?.Invoke(new(model.ResourcePath, "Discovering model textures", exception.Message, TextureCatalogOutcome(exception)));
                continue;
            }
            for (int index = 0; index < contexts.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TextureBuildContext context = contexts[index];
                TextureTarget target = TextureTarget.FromContext(context);
                string usage = model.ResourcePath + "#model/" + model.ModelIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (seen.Add(target))
                {
                    List<string> aliases = [usage];
                    usageAliases[target] = aliases;
                    string name = context.Layout == "native-field-model-sheet" ? $"{Path.GetFileName(target.Owner)} / {target.Subresource}{(target.Variant.Length == 0 ? "" : " / " + target.Variant)}" : $"{model.Name} / texture {index}{(target.Variant.Length == 0 ? "" : " / " + target.Variant)}";
                    yield return Entry(context, name, model.Category) with { Reader = "model", Model = model, Index = index, UsedBy = aliases.AsReadOnly() };
                }
                else if (usageAliases.TryGetValue(target, out List<string>? aliases) && !aliases.Contains(usage, StringComparer.Ordinal))
                {
                    aliases.Add(usage);
                }
            }
        }
        foreach (string field in fieldNames.Where(name => !string.IsNullOrWhiteSpace(name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!includeFieldTiles)
            {
                TextureCatalogEntry? workspace = null;
                try
                {
                    ResourceKey owner = new($"field/{field}/{field}.mim");
                    _ = resources.Get(owner);
                    MapFile map = MapFile.Read(ReadTextureSource(new($"field/{field}/{field}.map")));
                    if (map.Tiles.Count > 0)
                    {
                        int width = Math.Abs(map.Tiles.Min(tile => (int)tile.X)) + Math.Abs(map.Tiles.Max(tile => (int)tile.X)) + 16;
                        int height = Math.Abs(map.Tiles.Min(tile => (int)tile.Y)) + Math.Abs(map.Tiles.Max(tile => (int)tile.Y)) + 16;
                        workspace = new(TextureTarget.FromKey(new(owner, "workspace")), field + " / layered background", "Field background", "field-workspace", width, height) { Reader = "field-workspace" };
                    }
                }
                catch (Exception exception) when (TextureCatalogError(exception))
                {
                    diagnostic?.Invoke(new($"field/{field}/{field}.mim", "Discovering field workspace", exception.Message, TextureCatalogOutcome(exception)));
                }
                if (workspace is not null)
                {
                    yield return workspace;
                }
                continue;
            }
            IReadOnlyList<TextureBuildContext> contexts;
            try
            {
                contexts = ReadCatalogBackground(field);
            }
            catch (Exception exception) when (TextureCatalogError(exception))
            {
                diagnostic?.Invoke(new($"field/{field}/{field}.mim", "Discovering field background", exception.Message, TextureCatalogOutcome(exception)));
                continue;
            }
            for (int index = 0; index < contexts.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return Entry(contexts[index], $"{field} / tile {index}", "Field background") with { Reader = "field", Index = index };
            }
        }
        foreach (string archiveName in new[] { "menu", "main" })
        {
            IFlArchive? archive = null;
            try
            {
                archive = OpenArchive(archiveName);
            }
            catch (Exception exception) when (TextureCatalogError(exception))
            {
                diagnostic?.Invoke(new(archiveName, "Discovering artwork", exception.Message, TextureCatalogOutcome(exception)));
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
                    catch (Exception exception) when (TextureCatalogError(exception))
                    {
                        diagnostic?.Invoke(new(owner.Path, "Discovering TEX palettes", exception.Message, TextureCatalogOutcome(exception)));
                        continue;
                    }
                    foreach (TextureBuildContext context in contexts)
                    {
                        string name = owner.Path == "menu/magita.tex" ? "Magazine paper palette (mean colour only)" : $"{Path.GetFileName(owner.Path)} / palette {context.Key.Palette}";
                        yield return Entry(context, name, archiveName == "main" ? "System artwork" : "Menu and cards") with { Reader = "tex" };
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
            catch (Exception exception) when (TextureCatalogError(exception))
            {
                diagnostic?.Invoke(new(NativeResourceNames.WorldSettings(resources.ResourceLanguage).Path, $"Discovering world section {section}", exception.Message, TextureCatalogOutcome(exception)));
                continue;
            }
            foreach (TextureBuildContext context in contexts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (seen.Add(TextureTarget.FromContext(context)))
                {
                    string name = section == 42 ? $"World static shape {64 + int.Parse(context.Key.Subresource.Split('/')[^1], System.Globalization.CultureInfo.InvariantCulture)} / palette {context.Key.Palette}" : $"World {context.Key.Subresource} / palette {context.Key.Palette}";
                    yield return Entry(context, name, section == 42 ? "World static" : "World textures") with { Reader = "world-section", Section = section };
                }
            }
        }
        IReadOnlyList<TextureBuildContext> menuSlots;
        try
        {
            menuSlots = ReadCatalogMenuSlots();
        }
        catch (Exception exception) when (TextureCatalogError(exception))
        {
            diagnostic?.Invoke(new("menu/mngrp.bin", "Discovering tutorial artwork", exception.Message, TextureCatalogOutcome(exception)));
            menuSlots = [];
        }
        foreach (TextureBuildContext context in menuSlots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return Entry(context, $"Tutorial {context.Key.Subresource} / palette {context.Key.Palette}", "Tutorial artwork") with { Reader = "menu-slots" };
        }
        for (int page = 0; page < 20; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<TextureBuildContext> contexts;
            try
            {
                contexts = ReadCatalogTexl(page);
            }
            catch (Exception exception) when (TextureCatalogError(exception))
            {
                diagnostic?.Invoke(new("world/texl.obj", $"Discovering terrain page {page}", exception.Message, TextureCatalogOutcome(exception)));
                continue;
            }
            foreach (TextureBuildContext context in contexts)
            {
                yield return Entry(context, $"Terrain page {page} / palette {context.Key.Palette}", "World terrain") with { Reader = "texl", Index = page };
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
                context = new(new(owner, "atlas"), resources.Source, content.Revision, "native-artwork", NativeStartupArtwork.Width, NativeStartupArtwork.Height, () => NativeStartupArtwork.Decode(ReadTextureSource(owner)), encodedSource: content);
            }
            catch (Exception exception) when (TextureCatalogError(exception))
            {
                diagnostic?.Invoke(new(owner.Path, "Discovering startup artwork", exception.Message, TextureCatalogOutcome(exception)));
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
        catch (Exception exception) when (TextureCatalogError(exception))
        {
            diagnostic?.Invoke(new("cards/cards", "Discovering native card artwork", exception.Message, TextureCatalogOutcome(exception)));
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
        if (entry.Layout == "field-workspace")
        {
            throw new NotSupportedException("A layered field is a compound workspace. Export it as ORA or expand its native tile targets.");
        }
        return LoadCatalogTextureUnit(entry).Single(context => TextureTarget.FromContext(context) == entry.Target);
    }

    public IEnumerable<TextureCatalogEntry> ExpandFieldTextureCatalog(TextureCatalogEntry field, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string[] parts = field.Target.Owner.Split('/');
        if (parts.Length != 3 || parts[0] != "field" || !fieldNames.Contains(parts[1], StringComparer.Ordinal) || parts[2] != parts[1] + ".mim")
        {
            throw new ArgumentException("Select a field background from the installed catalog.", nameof(field));
        }
        foreach (TextureBuildContext context in ReadCatalogBackground(parts[1]))
        {
            cancellationToken.ThrowIfCancellationRequested();
            int index = int.Parse(context.Key.Subresource.AsSpan(5), System.Globalization.CultureInfo.InvariantCulture);
            yield return Entry(context, $"{parts[1]} / tile {index}", "Field background") with { Reader = "field", Index = index };
        }
    }

    public TextureCatalogEntry? FindCatalogTextureTarget(TextureTarget target, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string[] parts = target.Owner.Split('/');
        if (target.Game == "ff8" && parts.Length == 4 && parts[0] == "field" && parts[1] == "model")
        {
            return FindGlobalFieldSheetTarget(target, cancellationToken);
        }
        if (target.Game != "ff8" || parts.Length != 3 || parts[0] != "field" || !fieldNames.Contains(parts[1], StringComparer.Ordinal))
        {
            return null;
        }
        if (parts[2] == "chara.one" && target.Subresource.StartsWith("model/", StringComparison.Ordinal))
        {
            return FindRegisteredFieldTextureTarget(target, parts[1]);
        }
        if (target.Variant.Length != 0 || parts[2] != parts[1] + ".mim" || !target.Subresource.StartsWith("tile/", StringComparison.Ordinal))
        {
            return null;
        }
        TextureCatalogEntry field = new(TextureTarget.FromKey(new(new(target.Owner), "workspace")), parts[1], "Field background", "field-workspace", 0, 0);
        return ExpandFieldTextureCatalog(field, cancellationToken).FirstOrDefault(entry => entry.Target == target);
    }

    /// <summary>Reads a single addressable texture source unit without scanning unrelated archives.</summary>
    public IReadOnlyList<TextureBuildContext>? LoadAddressableCatalogTextureUnit(TextureTarget target)
    {
        if (target.Game != "ff8")
        {
            return null;
        }
        string[] member = target.Subresource.Split('/');
        if (target.Owner.StartsWith("world/wmset", StringComparison.Ordinal) && target.Owner == NativeResourceNames.WorldSettings(resources.ResourceLanguage).Path && member is ["section", _, "texture", _] && int.TryParse(member[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int section) && section is 38 or 39 or 40 or 42)
        {
            return ReadCatalogWorldSection(section);
        }
        if (target.Owner == "world/texl.obj" && member is ["texl", _] && int.TryParse(member[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int page) && page is >= 0 and < 20)
        {
            return ReadCatalogTexl(page);
        }
        if (target.Owner is "cards/cards")
        {
            return ReadCatalogCardArtwork();
        }
        if ((target.Owner.StartsWith("menu/", StringComparison.Ordinal) || target.Owner.StartsWith("main/", StringComparison.Ordinal)) && target.Owner.EndsWith(".tex", StringComparison.Ordinal) && target.Subresource == "atlas")
        {
            return ReadCatalogTex(new(target.Owner));
        }
        string[] owner = target.Owner.Split('/');
        if (owner is ["field", _, _] && owner[2] == owner[1] + ".mim" && fieldNames.Contains(owner[1], StringComparer.Ordinal))
        {
            return ReadCatalogBackground(owner[1]);
        }
        return null;
    }

    /// <summary>Reads model materials using their runtime identities before grouping them into authoring sheets.</summary>
    public IReadOnlyList<TextureBuildContext> LoadCatalogModelMaterialUnit(ModelCatalogEntry model) => ReadCatalogModelTextures(model);

    public int FindCatalogFieldId(string fieldName) => Array.IndexOf(fieldNames, fieldName);

    public IReadOnlyList<TextureBuildContext> LoadCatalogTextureUnit(TextureCatalogEntry entry)
    {
        ResourceKey owner = new(entry.Target.Owner);
        return entry.Reader switch
        {
            "model" => ReadAuthoringModelTextures(entry.Model ?? throw new InvalidDataException("The model catalog locator is missing.")),
            "field-registered" => [ReadRegisteredFieldTexture(entry)],
            "main-sheet" => ReadMainCharacterAuthoringSheets(Path.GetFileName(entry.Target.Owner), entry.Target.Subresource.EndsWith("/alpha", StringComparison.Ordinal)),
            "field" or "field-workspace" => ReadCatalogBackground(owner.Path.Split('/')[1]),
            "tex" => ReadCatalogTex(owner),
            "world-section" => ReadCatalogWorldSection(entry.Section),
            "texl" => ReadCatalogTexl(entry.Index),
            "menu-slots" => ReadCatalogMenuSlots(),
            "standalone-tim" => ReadCatalogStandaloneTim(resources, owner),
            "cinematic" => ReadCatalogCinematicTextures(entry.Section),
            "common-page" => ReadCatalogCommonEffectPages(resources, entry.Index),
            "cards" => ReadCatalogCardArtwork(),
            "startup" => [Context(new(owner, "atlas"), NativeStartupArtwork.Decode(ReadTextureSource(owner)), "native-artwork")],
            _ => throw new NotSupportedException($"Texture {entry.Name} has no source reader.")
        };
    }

    private static TextureCatalogEntry Entry(TextureBuildContext context, string name, string category) => new(TextureTarget.FromContext(context), name, category, context.Layout, context.LogicalWidth, context.LogicalHeight);

    private byte[] ReadTextureSource(ResourceKey key)
    {
        if (resources.GetSource<ITextureAuthoringResourceSource>() is not { IsTextureAuthoringSource: true })
        {
            return resources.Read(key);
        }
        ResourceContent content = resources.Get(key);
        bool field = key.Path.EndsWith("/chara.one", StringComparison.Ordinal);
        bool library = key.Path.StartsWith("field/main_chr/", StringComparison.Ordinal);
        var cached = field ? textureCatalogFieldBytes : library ? textureCatalogLibraryBytes : null;
        if (cached is { } previous && previous.Key == key && previous.Revision == content.Revision)
        {
            return previous.Bytes;
        }
        byte[] Retain(byte[] bytes)
        {
            if (field)
            {
                textureCatalogFieldBytes = (key, content.Revision, bytes);
            }
            else if (library)
            {
                textureCatalogLibraryBytes = (key, content.Revision, bytes);
            }
            return bytes;
        }
        using Stream input = content.OpenRead();
        if (input is MemoryStream memory && memory.TryGetBuffer(out ArraySegment<byte> buffer) && buffer.Offset == 0 && buffer.Count == buffer.Array!.Length)
        {
            return Retain(buffer.Array!);
        }
        using MemoryStream output = new();
        input.CopyTo(output);
        return Retain(output.ToArray());
    }

    private TextureBuildContext Context(TextureKey key, PixelImage image, string layout, ResourceSourceDescriptor? source = null, TextureSourceRegion? region = null, TextureSourceAddress? nativeSheet = null, TextureSourceAddress? nativeAlias = null, Func<IReadOnlyDictionary<string, PixelImage>>? nativeVariants = null, Func<PixelImage, string, PixelImage>? projectNativeVariant = null) => new(key, source ?? resources.Source, resources.Get(key.Owner).Revision, layout, image.Width, image.Height, () => image, region, encodedSource: resources.Get(key.Owner)) { NativeSheet = nativeSheet, NativeAlias = nativeAlias, NativeVariants = nativeVariants, ProjectNativeVariant = projectNativeVariant };

    private IReadOnlyList<TextureBuildContext> ReadCatalogModelTextures(ModelCatalogEntry entry)
    {
        ResourceKey owner = new(entry.ResourcePath);
        if (entry.Category == "Field")
        {
            if (resources.GetSource<IFieldModelSource>() is null)
            {
                return ReadClassicCatalogFieldTextures(entry);
            }
            FieldModel model = resources.GetSource<IFieldModelSource>()!.ReadModel(owner, ReadTextureSource(owner), name => ReadTextureSource(new($"field/main_chr/{name}.mch")), entry.ModelIndex);
            string prefix = model.TextureResourceName is { } name ? $"model/{entry.ModelIndex}/{(model.MainCharacterTextures ? "main" : "second")}/{name}/texture" : $"model/{entry.ModelIndex}/texture";
            string layout = model.TextureResourceName is null ? "classic-model" : "classic-field-model";
            bool nativeVariants = FieldEyeMaterial.HasNativeVariants(model);
            Func<IReadOnlyDictionary<string, PixelImage>>? variants = nativeVariants ? FieldEyeMaterial.VariantFactory(model.NativeEyes!, model.Textures[0]) : null;
            Func<PixelImage, string, PixelImage>? projector = nativeVariants ? FieldEyeMaterial.VariantProjector(model.NativeEyes!) : null;
            return model.Textures.Select((image, index) => Context(new(owner, $"{prefix}/{index}"), image, layout, model.TextureSourceDescriptor, nativeSheet: NativeFieldModelTextureSheets.Address(model, index), nativeVariants: index == 0 ? variants : null, projectNativeVariant: index == 0 ? projector : null)).ToArray();
        }
        if (entry.Category is "Battle model" or "Battle enemy" or "Battle weapon" && resources.GetSource<IBattleModelSource>() is null)
        {
            return ReadClassicCatalogBattleTextures(entry);
        }
        if (entry.Category is "Battle model" or "Battle enemy" or "Battle weapon")
        {
            IBattleModelSource source = resources.GetSource<IBattleModelSource>()!;
            BattleModel model;
            if (entry.Category == "Battle weapon")
            {
                if (!TryPartyAddress(entry.ResourcePath, out int character, out bool linked) || !linked)
                {
                    throw new NotSupportedException("The weapon has no native character material owner binding.");
                }
                string bodyName = Path.GetFileName(ModelAssetDiscoveryBindings.ResolvePartyFile(ReadNativeMemory(), character, false, 0));
                ResourceKey bodyOwner = new("battle/" + bodyName);
                BattleModel body = source.ReadModel(bodyOwner, ReadTextureSource(bodyOwner));
                model = source.ReadWeapon(owner, ReadTextureSource(owner), body);
            }
            else
            {
                model = source.ReadModel(owner, ReadTextureSource(owner));
            }
            return model.Textures.Select((image, index) =>
            {
                TextureSourceRegion? region = index < model.TextureRegions.Count ? model.TextureRegions[index] : null;
                return Context(new(owner, $"texture/{index}"), image, "classic-battle", model.TextureSourceDescriptor, region, region is null ? null : BattleAssets.BattleModelSheetAddress(owner, region, model.Textures.Count), index < model.TextureSourceAddresses.Count ? model.TextureSourceAddresses[index] : null);
            }).ToArray();
        }
        if (entry.Category == "Battle stage")
        {
            int stageId = int.Parse(Path.GetFileName(entry.ResourcePath).AsSpan(5, 3), System.Globalization.CultureInfo.InvariantCulture);
            BattleStage stage = BattleResourceReader.ReadStage(stageId, ReadTextureSource(owner));
            return stage.Textures.Select((image, index) => Context(new(owner, $"texture/{index}"), image, "classic-battle", region: index < stage.TextureRegions.Count ? stage.TextureRegions[index] : null, nativeAlias: index < stage.TextureSourceAddresses.Count ? stage.TextureSourceAddresses[index] : null)).ToArray();
        }
        if (entry.Category == "World character")
        {
            FieldModel model = WorldAssetReader.ReadWorldCharacter(ReadTextureSource(owner), entry.ModelIndex);
            return model.Textures.Select((image, index) => Context(new(owner, $"model/{entry.ModelIndex}/texture/{index}"), image, "classic-model")).ToArray();
        }
        EditableModel decoded = LoadCatalogModel(entry);
        string texturePrefix = entry.Category == "World character" ? $"model/{entry.ModelIndex}/texture" : entry.Category == "World static" ? $"section/42/texture/{entry.ModelIndex - 64}" : "texture";
        string contract = entry.Category.StartsWith("World", StringComparison.Ordinal) ? entry.Category == "World static" ? "classic-world-section" : "classic-model" : "classic-battle";
        return decoded.Textures.Select((image, index) => Context(new(owner, entry.Category == "World static" ? texturePrefix : $"{texturePrefix}/{index}"), image, contract)).ToArray();
    }

    private IReadOnlyList<TextureBuildContext> ReadCatalogBackground(string field)
    {
        ResourceKey owner = new($"field/{field}/{field}.mim");
        MimFile mim = MimFile.Read(ReadTextureSource(owner));
        MapFile map = MapFile.Read(ReadTextureSource(new($"field/{field}/{field}.map")));
        IReadOnlyList<TextureSourceAddress> atlas = FieldBackgroundTileDecoder.AtlasAddresses(owner, map.Tiles);
        return map.Tiles.Select((tile, index) => new TextureBuildContext(new(owner, $"tile/{index}", tile.PaletteId), resources.Source, resources.Get(owner).Revision, "classic-field-tile", MimFile.TileSize, MimFile.TileSize, () => FieldBackgroundTileDecoder.Decode(mim, tile), new(tile.TextureId * 64, 256, tile.Uses8BitIndices ? 8 : 4), encodedSource: resources.Get(owner)) { NativeAlias = FieldBackgroundTileDecoder.Address(owner, tile), NativeSheet = atlas[index] }).ToArray();
    }

    private IReadOnlyList<TextureBuildContext> ReadCatalogTex(ResourceKey owner)
    {
        TexFile tex = TexFile.Read(ReadTextureSource(owner));
        Func<int, PixelImage> decode = NativeMenuArtworkDataReader.IsPortraitAtlas(owner) ? _ => NativeMenuArtworkDataReader.DecodePortraitAtlas(tex) : tex.Decode;
        int paletteCount = Math.Max(1, tex.Palettes.Count);
        ResourceContent content = resources.Get(owner);
        return Enumerable.Range(0, paletteCount).Select(palette => new TextureBuildContext(new(owner, "atlas", palette), resources.Source, content.Revision, "native-artwork", tex.Header.Width, tex.Header.Height, () => decode(palette), paletteImage: decode, paletteCount: paletteCount, encodedSource: content)).ToArray();
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
        byte[] bytes = ReadTextureSource(owner);
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
        byte[] bytes = ReadTextureSource(owner);
        TimImage tim = WorldAssetReader.ReadTim(bytes, checked(page * 0x12800)).Image;
        return TimContexts(owner, $"texl/{page}", tim, "classic-world-texl");
    }

    private IReadOnlyList<TextureBuildContext> ReadCatalogMenuSlots()
    {
        ResourceKey owner = new("menu/mngrp.bin");
        byte[] header = ReadTextureSource(new("menu/mngrphd.bin"));
        byte[] content = ReadTextureSource(owner);
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
        return Enumerable.Range(0, Math.Max(1, tim.Palettes.Count)).Select(palette => new TextureBuildContext(new(owner, subresource, palette), resources.Source, resources.Get(owner).Revision, layout, tim.PixelWidth, tim.Height, () => tim.Decode(palette), layout == "classic-world-section" ? WorldAssets.SectionTextureRegion(tim, palette) : new(tim.X, tim.Y, bits), tim.Decode, Math.Max(1, tim.Palettes.Count), resources.Get(owner))).ToArray();
    }
}
