using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public static partial class ModPreviewResources
{
    public static ModPreviewComparison? CompareTexture(string gameDirectory, string? modelSourceDirectory, string? language, ModCatalogSnapshot catalog, string relativePath, CancellationToken cancellationToken, Func<FieldAssets, PixelImage>? renderField = null, string? textureMember = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        relativePath = ModCatalogSnapshot.NormalizePath(relativePath);
        ModFile? file = catalog.Files.FirstOrDefault(candidate => candidate.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
        if (file is null || !TextureModPaths.IsImage(file.RelativePath))
        {
            return null;
        }

        IReadOnlyList<NativeTextureVariant> nativeTargets = [];
        LegacyTextureTarget? legacyTarget = null;
        ModCatalogSnapshot package = new(catalog.Generation, catalog.Packages.Where(candidate => candidate.Id.Equals(file.PackageId, StringComparison.OrdinalIgnoreCase)), catalog.Files.Where(candidate => candidate.PackageId.Equals(file.PackageId, StringComparison.OrdinalIgnoreCase) && candidate.Format == file.Format));
        ITextureReplacementResolver replacement;
        if (file.Format == ModFormat.Memoria && file.RelativePath.StartsWith("textures/", StringComparison.OrdinalIgnoreCase))
        {
            ModCatalogSnapshot selected = new(package.Generation, package.Packages, SelectNativePreviewFiles(package, file, cancellationToken));
            if (!NativeTextureMods.TryPrepare(selected, out NativeTextureMods native, out IReadOnlyList<ModDiagnostic> diagnostics, cancellationToken))
            {
                throw new InvalidDataException(diagnostics.First().Message);
            }
            nativeTargets = native.Variants.Where(variant => variant.Target.Game == "ff8" && variant.RelativePath.Equals(file.RelativePath, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (nativeTargets.Count == 0)
            {
                return null;
            }
            replacement = native;
        }
        else if (file.Format == ModFormat.Junction && FindJunctionTextureTarget(file.RelativePath) is { } target)
        {
            legacyTarget = target;
            replacement = new FfnxTextureResolver(package);
        }
        else if (file.Format == ModFormat.Demaster && FindDemasterTextureTarget(file.RelativePath) is { } demaster)
        {
            legacyTarget = demaster;
            replacement = new DemasterTextureResolver(package);
        }
        else
        {
            return null;
        }

        using IResourceSource source = GameDataSourceFactory.OpenTextureAuthoring(gameDirectory, modelSourceDirectory, language);
        ResourceService resources = new(source, language);
        resources.Prepare(replacement).Commit();
        using GameAssets assets = new(gameDirectory, resources: resources);

        foreach (NativeTextureVariant native in nativeTargets)
        {
            if (assets.LoadAddressableCatalogTextureUnit(native.Target) is { } unit)
            {
                TextureBuildContext? context = unit.FirstOrDefault(native.Target.Matches);
                return context is null ? null : CompareContext(context, DescribeTextureContext(context));
            }
            TextureCatalogEntry? direct = assets.FindCatalogTextureTarget(native.Target, cancellationToken);
            if (direct is not null && CompareEntry(direct) is { } comparison)
            {
                return comparison;
            }
        }

        if (legacyTarget is not null && legacyTarget.Address(resources.ResourceLanguage) is { } address)
        {
            if (assets.LoadAddressableCatalogTextureUnit(address) is { } unit)
            {
                foreach (TextureBuildContext context in unit)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if ((textureMember is null || context.Key.Subresource == textureMember) && legacyTarget.Matches(TextureTarget.FromContext(context), context.Layout)
                        && (legacyTarget.NativeMember is null || context.NativeAlias?.Subresource == legacyTarget.NativeMember)
                        && CompareContext(context, textureMember is null ? DescribeTextureContext(context) : DescribeTextureAtlasMember(textureMember)) is { } comparison)
                    {
                        return comparison;
                    }
                }
            }
            return null;
        }

        if (legacyTarget is not null && (legacyTarget.Model is not null || legacyTarget.Layout is "classic-battle" or "classic-model"))
        {
            foreach (ModelCatalogEntry model in assets.EnumerateModelCatalog(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (model.LoadError is not null || !model.ResourcePath.StartsWith(legacyTarget.Owner, StringComparison.Ordinal) || legacyTarget.Model is { } modelName && !model.Name.StartsWith(modelName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                foreach (TextureBuildContext context in assets.LoadCatalogModelMaterialUnit(model))
                {
                    if (legacyTarget.Matches(TextureTarget.FromContext(context), context.Layout) && SelectModelPreviewPalette(context, legacyTarget.ModelPalette) is { } material && CompareContext(material, $"{model.Name} / {material.Key.Subresource}") is { } comparison)
                    {
                        return comparison;
                    }
                }
            }
            return null;
        }

        foreach (TextureCatalogEntry entry in assets.EnumerateTextureCatalog(cancellationToken))
        {
            if (entry.Layout == "field-workspace" || (legacyTarget is not null ? !legacyTarget.Matches(entry.Target, entry.Layout) : !nativeTargets.Any(target => target.Target == entry.Target)))
            {
                continue;
            }
            if (CompareEntry(entry) is { } comparison)
            {
                return comparison;
            }
        }
        return null;

        ModPreviewComparison? CompareEntry(TextureCatalogEntry entry) => CompareContext(assets.LoadCatalogTexture(entry), entry.Name);

        ModPreviewComparison? CompareContext(TextureBuildContext context, string name)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TextureValue after = resources.ResolveForExport(context);
            string provenance = $"{file.PackageId}/{file.RelativePath}";
            if (!after.IsReplacement || !after.Provenance.Split(';', StringSplitOptions.TrimEntries).Any(value => MatchesTextureProvenance(value, provenance)))
            {
                return null;
            }
            if (renderField is not null && context.Layout == "classic-field-tile" && CompareField(context.Key.Owner.Path.Split('/')[1], renderField) is { } field)
            {
                return field;
            }
            TextureValue before = resources.ResolveOriginalForExport(context);
            bool nativeFlags = TexturePixelExchange.UsesNativeFlags(context, before);
            PixelImage beforeImage = nativeFlags ? TexturePixelExchange.ToEditable(before.Image) : before.Image;
            PixelImage afterImage = nativeFlags ? TexturePixelExchange.ToEditable(after.Image) : after.Image;
            return new(new($"{name} · {beforeImage.Width} × {beforeImage.Height}", beforeImage), new($"{name} · {afterImage.Width} × {afterImage.Height}", afterImage));
        }

        ModPreviewComparison? CompareField(string name, Func<FieldAssets, PixelImage> render)
        {
            int fieldId = assets.FindCatalogFieldId(name);
            if (fieldId < 0)
            {
                return null;
            }
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using IResourceSource originalSource = GameDataSourceFactory.Open(gameDirectory, modelSourceDirectory, language);
                using GameAssets originalAssets = new(gameDirectory, resources: new(originalSource, language));
                PixelImage original = render(originalAssets.LoadField(fieldId));
                cancellationToken.ThrowIfCancellationRequested();
                PixelImage applied = render(assets.LoadField(fieldId));
                return new(new(name, original), new(name, applied));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException or NotImplementedException or ArgumentException)
            {
                return null;
            }
        }
    }

    internal static (string Category, string Group, string Name)? DescribeTexturePath(ModFile file)
    {
        if (!TextureModPaths.IsImage(file.RelativePath))
        {
            return null;
        }
        if (file.Format == ModFormat.Memoria && TextureTarget.TryParsePath(file.RelativePath, out TextureTarget? native) && native is not null)
        {
            string[] owner = native.Owner.Split('/');
            string member = $"{native.Subresource} · palette {native.Palette}{(native.Variant.Length == 0 ? "" : " · " + native.Variant)}";
            return owner switch
            {
                ["field", _, _, ..] when owner[2] == owner[1] + ".mim" => ("Fields", owner[1], member),
                ["field", _, ..] => ("Textures", "Field models", $"{owner[^1]} / {member}"),
                ["world", _, ..] => ("World map", WorldTextureGroup(native.Subresource), $"{owner[^1]} / {member}"),
                ["battle", _, ..] => ("Textures", "Battle", $"{owner[^1]} / {member}"),
                _ => ("Textures", "Artwork", $"{owner[^1]} / {member}")
            };
        }
        LegacyTextureTarget? legacy = file.Format switch
        {
            ModFormat.Junction => FindJunctionTextureTarget(file.RelativePath),
            ModFormat.Demaster => FindDemasterTextureTarget(file.RelativePath),
            _ => null
        };
        if (legacy is not null)
        {
            string name = Path.GetFileNameWithoutExtension(file.RelativePath);
            if (legacy.Field is { } field)
            {
                return ("Fields", field, name);
            }
            if (legacy.Owner.StartsWith("world/", StringComparison.Ordinal))
            {
                return ("World map", WorldTextureGroup(legacy.Member), name);
            }
            if (legacy.Model is not null)
            {
                return ("Textures", "Field models", name);
            }
            if (legacy.Owner.StartsWith("battle/", StringComparison.Ordinal) || legacy.Layout == "native-effect-texture")
            {
                return ("Textures", "Battle", name);
            }
            return ("Textures", legacy.Owner == "cards/cards" ? "Triple Triad" : "Menu artwork", name);
        }
        return null;

        static string WorldTextureGroup(string? member) => member?.Split('/') switch
        {
            ["texl", ..] => "Terrain",
            ["model", ..] => "Characters and vehicles",
            ["section", "38", ..] => "Water",
            ["section", "39", ..] => "Roads",
            ["section", "42", ..] => "Landmarks",
            _ => "World textures"
        };
    }

    private static IEnumerable<ModFile> SelectNativePreviewFiles(ModCatalogSnapshot package, ModFile image, CancellationToken cancellationToken)
    {
        yield return image;
        string directory = image.RelativePath[..(image.RelativePath.LastIndexOf('/') + 1)];
        foreach (ModFile file in package.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!file.RelativePath.EndsWith(TextureModMapping.Extension, StringComparison.OrdinalIgnoreCase) || file.RelativePath.LastIndexOf('/') != directory.Length - 1 || !file.RelativePath.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (file.RelativePath.Equals(image.RelativePath + TextureModMapping.Extension, StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
                continue;
            }
            string? name = null;
            try
            {
                using JsonDocument document = JsonDocument.Parse(file.ReadBytes());
                if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("image", out JsonElement mapped) && mapped.ValueKind == JsonValueKind.String)
                {
                    name = mapped.GetString();
                }
            }
            catch (JsonException)
            {
                // A malformed sidecar elsewhere cannot identify the selected image.
            }
            if (name is not null && image.RelativePath.Equals(directory + name, StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }
    }

    private static bool MatchesTextureProvenance(string value, string file)
    {
        int separator = value.IndexOf(": ", StringComparison.Ordinal);
        if (value.StartsWith("variant ", StringComparison.Ordinal) && separator >= 0)
        {
            value = value[(separator + 2)..];
        }
        return value.Equals(file, StringComparison.OrdinalIgnoreCase) || value.StartsWith(file + "@", StringComparison.OrdinalIgnoreCase);
    }

    private static TextureBuildContext? SelectModelPreviewPalette(TextureBuildContext context, int? requested)
    {
        if (requested is not int palette || palette == context.Key.Palette)
        {
            return context;
        }
        if (context.PaletteImage is not { } read || palette >= context.PaletteCount)
        {
            return null;
        }
        return new(new(context.Key.Owner, context.Key.Subresource, palette), context.Source, context.Revision, context.Layout, context.LogicalWidth, context.LogicalHeight, () => read(palette), context.Region, read, context.PaletteCount, context.EncodedSource)
        {
            NativeAlias = context.NativeAlias is { } alias ? alias with { Palette = palette } : null,
            NativeSheet = context.NativeSheet,
            NativePage = context.NativePage
        };
    }

    private static string DescribeTextureContext(TextureBuildContext context) => $"{Path.GetFileNameWithoutExtension(context.Key.Owner.Path)} / {context.Key.Subresource} / palette {context.Key.Palette}";

    private sealed record LegacyTextureTarget(string Owner, string? Member = null, int? Palette = null, string? Field = null, string? Model = null, string? Layout = null, int? ModelPalette = null, IReadOnlyList<string>? Members = null, string? NativeMember = null)
    {
        public TextureTarget? Address(string language)
        {
            if (Field is not null)
            {
                return new("ff8", Owner, "workspace");
            }
            if (Owner == "world/wmset" && Member is not null)
            {
                return new("ff8", NativeResourceNames.WorldSettings(language).Path, Member, Palette ?? 0);
            }
            return Owner is "world/texl.obj" or "cards/cards" || Owner.StartsWith("menu/", StringComparison.Ordinal) ? new("ff8", Owner, Member ?? "atlas", Palette ?? 0) : null;
        }

        public bool Matches(TextureTarget target, string layout)
        {
            return target.Owner.StartsWith(Owner, StringComparison.Ordinal) && (Layout is null || layout == Layout) && (Palette is null || target.Palette == Palette)
                && (Members is not null ? Members.Contains(target.Subresource) : Member is null || (Member.EndsWith('/') ? target.Subresource.StartsWith(Member, StringComparison.Ordinal) : target.Subresource == Member))
                && (Model is null || target.Subresource.Contains("/" + Model + "/", StringComparison.Ordinal));
        }
    }

    private static LegacyTextureTarget? FindJunctionTextureTarget(string path)
    {
        string stem = path[..^Path.GetExtension(path).Length].ToLowerInvariant();
        if (stem.StartsWith("mods/textures/", StringComparison.Ordinal))
        {
            stem = stem[14..];
            if (stem.StartsWith("eng/", StringComparison.Ordinal))
            {
                stem = stem[4..];
            }
        }
        Match field = Regex.Match(stem, @"^field/mapdata/(?:(?<prefix>[a-z0-9_]{2})/)?(?<field>[a-z0-9_]+)/\k<field>(?:_[0-9]+)?$", RegexOptions.CultureInvariant);
        if (field.Success && (!field.Groups["prefix"].Success || FfnxTextureResolver.IsNativeFieldBackgroundPath(path)))
        {
            string name = field.Groups["field"].Value;
            return new($"field/{name}/{name}.mim", Field: name);
        }
        Match world = Regex.Match(stem, @"^world/dat/wmset/section(38|39|40|42)/texture([0-9]+)_([0-9]+)(_[0-9]+_[0-9]+)?$", RegexOptions.CultureInvariant);
        if (world.Success)
        {
            return new("world/wmset", $"section/{Numeric(world, 1)}/texture/{Numeric(world, 2)}", world.Groups[4].Success ? null : Numeric(world, 3));
        }
        Match terrain = Regex.Match(stem, @"^world/dat/texl/texture([0-9]+)_00$", RegexOptions.CultureInvariant);
        if (terrain.Success)
        {
            return new("world/texl.obj", $"texl/{Numeric(terrain, 1)}", 0);
        }
        Match character = Regex.Match(stem, @"^world/esk/chara_one/model([0-9]+)-([0-9]+)_00$", RegexOptions.CultureInvariant);
        if (character.Success)
        {
            return new("world/chara.one", $"model/{Numeric(character, 1)}/texture/{Numeric(character, 2)}", Layout: "classic-model");
        }
        Match battle = Regex.Match(stem, @"^(battle/(?:a0stg[0-9]{3}\.x|[cd][0-9a-f][mcw][0-9]{3}\.dat))-([0-9]+)_([0-9]+)$", RegexOptions.CultureInvariant);
        if (battle.Success)
        {
            return new(battle.Groups[1].Value, $"texture/{Numeric(battle, 2)}", Layout: "classic-battle", ModelPalette: Numeric(battle, 3));
        }
        Match actor = Regex.Match(stem, @"^field/model/(main|second)_chr/([a-z0-9]+)-([0-9]+)_", RegexOptions.CultureInvariant);
        if (actor.Success)
        {
            return new("field/", Model: actor.Groups[2].Value, Layout: "classic-field-model");
        }
        Match menu = Regex.Match(stem, @"^(?:data/eng/)?menu/(?:hires/)?(mc0[0-9]|mag[0-9]{2}\.tex|face_b0[01]|gf_big0[01]|icon(?:fl0[0-3])?)(?:_([0-9]+))?$", RegexOptions.CultureInvariant);
        if (menu.Success)
        {
            string name = menu.Groups[1].Value;
            if (name.StartsWith("iconfl", StringComparison.Ordinal))
            {
                name = "icon";
            }
            return new($"menu/{(name.EndsWith(".tex", StringComparison.Ordinal) ? name : name + ".tex")}", "atlas", Numeric(menu, 2));
        }
        if (stem.StartsWith("cardgame/", StringComparison.Ordinal))
        {
            return stem[9..].Split('_')[0] switch
            {
                "cards" or "icons" => new("cards/cards"),
                "intro" => new("cards/cards", "background"),
                "game" => new("cards/cards", "board-background"),
                _ => null
            };
        }
        if (stem.StartsWith("magic/", StringComparison.Ordinal) || stem.StartsWith("battle/", StringComparison.Ordinal))
        {
            return new("", Layout: "native-effect-texture");
        }
        return null;

        static int? Numeric(Match match, int group) => int.TryParse(match.Groups[group].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out int value) ? value : null;
    }
}
