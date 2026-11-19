using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed record NativeTextureVariant(TextureTarget Target, string PackageId, string RelativePath);

/// <summary>Validated immutable native texture contributions in catalog precedence order.</summary>
public sealed class NativeTextureMods : ITextureReplacementResolver
{
    private sealed record Entry(NativeTextureVariant Variant, byte[] Bytes, TextureTargetContract? Contract, TextureNativeCoverage? Coverage);
    private readonly IReadOnlyList<Entry> entries;
    private readonly IReadOnlyDictionary<TextureTarget, Entry> effective;
    private readonly IReadOnlyDictionary<TextureTarget, IReadOnlyDictionary<string, Entry>> companionEntries;
    private readonly TextureCache cache;

    private NativeTextureMods(IReadOnlyList<Entry> entries, TextureCache? cache = null)
    {
        this.entries = entries;
        effective = entries.GroupBy(entry => entry.Variant.Target).ToDictionary(group => group.Key, group => group.First());
        companionEntries = effective.Values.Where(entry => entry.Variant.Target.Variant.Length != 0).GroupBy(entry => entry.Variant.Target with { Variant = "" }).ToDictionary(group => group.Key, group => (IReadOnlyDictionary<string, Entry>)group.ToDictionary(entry => entry.Variant.Target.Variant, StringComparer.Ordinal));
        this.cache = cache ?? new();
        Variants = Array.AsReadOnly(entries.Select(entry => entry.Variant).ToArray());
    }

    public static NativeTextureMods Empty { get; } = new([]);
    public IReadOnlyList<NativeTextureVariant> Variants { get; }

    public static bool TryPrepare(ModCatalogSnapshot catalog, out NativeTextureMods result, out IReadOnlyList<ModDiagnostic> diagnostics, NativeTextureMods? previous = null)
    {
        List<Entry> prepared = [];
        List<ModDiagnostic> errors = [];
        List<ModDiagnostic> notices = [];
        long encodedBytes = 0;
        long metadataBytes = 0;
        TextureCache cache = previous?.cache ?? new();
        Dictionary<NativeTextureVariant, Entry> prior = previous?.entries.ToDictionary(entry => entry.Variant) ?? [];
        foreach (IGrouping<string, ModFile> package in catalog.Files.Where(file => file.Format == ModFormat.Memoria && file.RelativePath.StartsWith("textures/", StringComparison.OrdinalIgnoreCase)).GroupBy(file => file.PackageId, StringComparer.OrdinalIgnoreCase))
        {
            Dictionary<string, ModFile> images = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, TextureModMapping> mappings = new(StringComparer.OrdinalIgnoreCase);
            HashSet<TextureTarget> targets = [];
            foreach (ModFile file in package)
            {
                try
                {
                    if (TextureModPaths.IsImage(file.RelativePath))
                    {
                        if (!images.TryAdd(file.RelativePath, file))
                        {
                            throw new InvalidDataException("Duplicate normalized texture image path.");
                        }
                    }
                    else if (file.RelativePath.EndsWith(TextureModMapping.Extension, StringComparison.OrdinalIgnoreCase))
                    {
                        byte[] metadata = file.ReadBytes();
                        metadataBytes = checked(metadataBytes + metadata.Length);
                        if (metadataBytes > 512L * 1024 * 1024)
                        {
                            throw new InvalidDataException("Native texture mappings exceed their bounded metadata budget.");
                        }
                        TextureModMapping mapping;
                        using (System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(metadata))
                        {
                            if (document.RootElement.TryGetProperty("image", out _))
                            {
                                mapping = TextureModMapping.Parse(metadata);
                            }
                            else
                            {
                                TextureExchangeMetadata authoring = TextureExchangeMetadata.Parse(metadata);
                                string imageName = Path.GetFileName(file.RelativePath[..^TextureModMapping.Extension.Length]);
                                mapping = new(authoring.SchemaVersion, authoring.Target, imageName, authoring.Contract)
                                {
                                    Extensions = authoring.NativeFlagsBase64 is not { } flags ? null : new() { ["extensions"] = System.Text.Json.JsonSerializer.SerializeToElement(new { nativeCoverage = new { width = authoring.NativeWidth ?? authoring.Contract.LogicalWidth, height = authoring.NativeHeight ?? authoring.Contract.LogicalHeight, alpha = flags } }) }
                                };
                                mapping = TextureModMapping.Parse(mapping.ToBytes());
                            }
                        }
                        string directory = file.RelativePath[..(file.RelativePath.LastIndexOf('/') + 1)];
                        string image = directory + mapping.Image;
                        if (!mappings.TryAdd(image, mapping))
                        {
                            throw new InvalidDataException("Multiple mappings identify the same texture image.");
                        }
                    }
                }
                catch (Exception exception) when (IsValidationError(exception))
                {
                    errors.Add(new("Memoria.InvalidTexture", exception.Message, package.Key, file.RelativePath));
                }
            }
            foreach ((string path, TextureModMapping _) in mappings)
            {
                if (!images.ContainsKey(path))
                {
                    errors.Add(new("Memoria.InvalidTexture", "Texture mapping references a missing image.", package.Key, path));
                }
            }
            foreach ((string path, ModFile file) in images)
            {
                try
                {
                    TextureModPaths.ValidateRelative(path);
                    byte[] bytes = file.ReadBytes();
                    encodedBytes = checked(encodedBytes + bytes.Length);
                    if (bytes.Length > 64 * 1024 * 1024 || encodedBytes > 512L * 1024 * 1024)
                    {
                        throw new InvalidDataException("Native texture contributions exceed the bounded encoded image budget.");
                    }
                    bool canonical = TextureTarget.TryParsePath(path, out TextureTarget? named);
                    if (!canonical)
                    {
                        canonical = TextureTarget.TryParsePath("textures/" + Path.GetFileName(path), out named);
                    }
                    mappings.TryGetValue(path, out TextureModMapping? mapping);
                    Memoria.FF8.Resources.TextureExchangeMetadata? embedded = Memoria.FF8.Assets.TextureExchange.TryReadEmbeddedMetadata(bytes);
                    if (embedded is not null)
                    {
                        if (mapping is not null && (mapping.Target != embedded.Target || mapping.Contract is not null && mapping.Contract != embedded.Contract))
                        {
                            throw new InvalidDataException("Embedded texture metadata conflicts with its sidecar; repair or remap the image.");
                        }
                        mapping ??= new(embedded.SchemaVersion, embedded.Target, Path.GetFileName(path), embedded.Contract)
                        {
                            Extensions = embedded.NativeFlagsBase64 is not { } flags ? null : new() { ["extensions"] = System.Text.Json.JsonSerializer.SerializeToElement(new { nativeCoverage = new { width = embedded.NativeWidth ?? embedded.Contract.LogicalWidth, height = embedded.NativeHeight ?? embedded.Contract.LogicalHeight, alpha = flags } }) }
                        };
                        mapping = TextureModMapping.Parse(mapping.ToBytes());
                    }
                    if (mapping is not null && canonical && mapping.Target != named)
                    {
                        throw new InvalidDataException("Texture mapping conflicts with its canonical filename; remap or rename the image.");
                    }
                    TextureTarget target = mapping?.Target ?? (canonical ? named! : throw new InvalidDataException("Unrecognized texture image; select a target in the importer."));
                    target.Validate();
                    if (!targets.Add(target))
                    {
                        throw new InvalidDataException("Multiple images in one package replace the same texture target.");
                    }
                    NativeTextureVariant variant = new(target, package.Key, path);
                    TextureNativeCoverage? coverage = mapping?.NativeCoverage;
                    if (prior.TryGetValue(variant, out Entry? unchanged) && unchanged.Contract == mapping?.Contract && unchanged.Coverage == coverage && unchanged.Bytes.AsSpan().SequenceEqual(bytes))
                    {
                        prepared.Add(unchanged);
                    }
                    else
                    {
                        Entry entry = new(variant, bytes, mapping?.Contract, coverage);
                        _ = cache.Get(entry, () => TextureImageDecoder.Decode(bytes, path), decoded => PixelBytes(decoded.MipLevels));
                        prepared.Add(entry);
                    }
                }
                catch (Exception exception) when (IsValidationError(exception))
                {
                    errors.Add(new("Memoria.InvalidTexture", exception.Message, package.Key, path));
                }
            }
        }
        foreach (IGrouping<TextureTarget, Entry> contributions in prepared.GroupBy(entry => entry.Variant.Target))
        {
            Entry winner = contributions.First();
            foreach (Entry masked in contributions.Skip(1).Where(entry => !entry.Variant.PackageId.Equals(winner.Variant.PackageId, StringComparison.OrdinalIgnoreCase)))
            {
                notices.Add(new("Memoria.TextureOverridden", $"Texture {masked.Variant.Target.Owner} / {masked.Variant.Target.Subresource} / palette {masked.Variant.Target.Palette} / variant '{masked.Variant.Target.Variant}' is overridden by {winner.Variant.PackageId} ({winner.Variant.RelativePath}). Change enablement or priority in Mods to select a different owner.", masked.Variant.PackageId, masked.Variant.RelativePath));
            }
        }
        diagnostics = Array.AsReadOnly(errors.Concat(notices).ToArray());
        result = errors.Count == 0 ? new(Array.AsReadOnly(prepared.ToArray()), cache) : Empty;
        return errors.Count == 0;
    }

    public TextureReplacement? Resolve(TextureBuildContext context)
    {
        TextureReplacement? direct = ResolveFamily(context, TextureTarget.FromContext(context, context.Game));
        if (direct is not null)
        {
            return direct;
        }
        if (context.NativeAlias is { } alias)
        {
            TextureReplacement? aliased = ResolveFamily(context, AddressTarget(context, alias), alias);
            if (aliased is not null)
            {
                return aliased;
            }
        }
        if (context.NativeSheet is { CanvasWidth: 128, CanvasHeight: 128 } sheet && context.Layout is "classic-field-model" or "classic-model")
        {
            return ResolveFamily(context, AddressTarget(context, sheet), sheet, sheet: true);
        }
        return null;
    }

    private static TextureTarget AddressTarget(TextureBuildContext context, TextureSourceAddress address) => TextureTarget.FromKey(new(address.Owner, address.Subresource, address.Palette), context.RequestedVariant ?? "", context.Game);

    private TextureReplacement? ResolveFamily(TextureBuildContext context, TextureTarget target, TextureSourceAddress? address = null, bool sheet = false)
    {
        if (string.IsNullOrEmpty(context.RequestedVariant) && TryResolveCompanions(context, target, address, sheet, out TextureReplacement? composed))
        {
            return composed;
        }
        return effective.TryGetValue(target, out Entry? entry) ? ResolveDirect(entry, context, address, sheet) : null;
    }

    private TextureReplacement ResolveDirect(Entry entry, TextureBuildContext context, TextureSourceAddress? address = null, bool sheet = false)
    {
        if (address is not null)
        {
            AliasResolveKey aliasKey = new(entry, context.Key, context.Revision, context.Source.Id, context.Source.Edition, context.Source.Language, context.Layout, context.LogicalWidth, context.LogicalHeight, context.RequestedVariant, address, sheet);
            return cache.Get(aliasKey, () => ResolveAlias(entry, context, address, sheet), ReplacementBytes);
        }
        ResolveKey key = ContextKey(entry, context);
        return cache.Get(key, () => Resolve(entry, context), ReplacementBytes);
    }

    private bool TryResolveCompanions(TextureBuildContext context, TextureTarget target, TextureSourceAddress? address, bool sheet, out TextureReplacement? replacement)
    {
        replacement = null;
        effective.TryGetValue(target, out Entry? baseEntry);
        companionEntries.TryGetValue(target, out IReadOnlyDictionary<string, Entry>? siblings);
        if ((baseEntry is null && siblings is null) || context.ReadInstalledOriginal is null)
        {
            return false;
        }
        TextureValue installed = context.ReadInstalledOriginal();
        Entry[] selected = installed.Variants?.Keys.Where(name => siblings?.ContainsKey(name) == true).Order(StringComparer.Ordinal).Select(name => siblings![name]).ToArray() ?? [];
        if ((baseEntry is null && selected.Length == 0) || (installed.Variants is null && installed.Animation is null))
        {
            return false;
        }
        ComposedResolveKey key = new(ContextKey(baseEntry ?? selected[0], context), installed, new(selected), address, sheet);
        replacement = cache.Get(key, () =>
        {
            TextureReplacement open = baseEntry is not null ? ResolveDirect(baseEntry, context, address, sheet) : new(installed.Image, installed.Provenance, installed.LogicalWidth, installed.LogicalHeight, installed.MipLevels);
            Dictionary<string, Albeoris.Games.FF8.Imaging.PixelImage> variants = installed.Variants is null ? new(StringComparer.Ordinal) : new(installed.Variants, StringComparer.Ordinal);
            HashSet<string> explicitVariants = [];
            List<string> provenance = [open.Provenance];
            foreach (Entry sibling in selected)
            {
                string name = sibling.Variant.Target.Variant;
                Albeoris.Games.FF8.Imaging.PixelImage original = variants[name];
                TextureBuildContext variantContext = new(context.Key, context.Source, context.Revision, context.Layout, context.LogicalWidth, context.LogicalHeight, () => original, context.Region, context.PaletteImage, context.PaletteCount, context.EncodedSource)
                {
                    Game = context.Game,
                    RequestedVariant = name,
                    NativeAlias = context.NativeAlias,
                    NativeSheet = context.NativeSheet,
                    NativePage = context.NativePage,
                    NativeUpload = context.NativeUpload,
                    NativeAtlas = context.NativeAtlas
                };
                TextureReplacement variant = ResolveDirect(sibling, variantContext, address, sheet);
                variants[name] = variant.Image;
                explicitVariants.Add(name);
                provenance.Add($"variant {name}: {variant.Provenance}");
            }
            if (variants.Values.Any(image => (long)image.Width * open.Image.Height != (long)image.Height * open.Image.Width))
            {
                throw new InvalidDataException("Native companion variants must retain the default texture's logical aspect. Import compatible default and companion canvases together.");
            }
            int width = variants.Values.Select(image => image.Width).Append(open.Image.Width).Max();
            int height = variants.Values.Select(image => image.Height).Append(open.Image.Height).Max();
            Albeoris.Games.FF8.Imaging.PixelImage baseImage = ProjectCompanion(open.Image, width, height);
            foreach (string name in variants.Keys.ToArray())
            {
                Albeoris.Games.FF8.Imaging.PixelImage variant = !explicitVariants.Contains(name) && !installed.IsReplacement && context.ProjectNativeVariant is { } project ? project(baseImage, name) : variants[name];
                variants[name] = ProjectCompanion(variant, width, height);
            }
            return open with { Image = baseImage, MipLevels = ReferenceEquals(baseImage, open.Image) ? open.MipLevels : null, Variants = variants.Count == 0 ? null : new System.Collections.ObjectModel.ReadOnlyDictionary<string, Albeoris.Games.FF8.Imaging.PixelImage>(variants), Animation = installed.Animation, Provenance = string.Join("; ", provenance) };
        }, ReplacementBytes);
        return true;
    }

    /// <summary>Runtime-only nearest expansion keeps source pixels and the shared logical canvas; authored files are untouched.</summary>
    private static Albeoris.Games.FF8.Imaging.PixelImage ProjectCompanion(Albeoris.Games.FF8.Imaging.PixelImage source, int width, int height)
    {
        if (source.Width == width && source.Height == height)
        {
            return source;
        }
        if (width < source.Width || height < source.Height || (long)source.Width * height != (long)source.Height * width)
        {
            throw new InvalidDataException("Native companion variants must retain the default texture's logical aspect. Import compatible default and companion canvases together.");
        }
        Albeoris.Games.FF8.Imaging.PixelImage projected = new(width, height);
        for (int y = 0; y < height; y++)
        {
            int sourceY = (int)((long)y * source.Height / height);
            for (int x = 0; x < width; x++)
            {
                projected[x, y] = source[(int)((long)x * source.Width / width), sourceY];
            }
        }
        return projected;
    }

    private static ResolveKey ContextKey(Entry entry, TextureBuildContext context) => new(entry, context.Key, context.Revision, context.Source.Id, context.Source.Edition, context.Source.Language, context.Layout, context.LogicalWidth, context.LogicalHeight, context.RequestedVariant);

    private TextureReplacement ResolveAlias(Entry entry, TextureBuildContext context, TextureSourceAddress address, bool sheet = false)
    {
        if (address.CanvasWidth <= 0 || address.CanvasHeight <= 0 || address.X < 0 || address.Y < 0 || address.Width <= 0 || address.Height <= 0 || (long)address.X + address.Width > address.CanvasWidth || (long)address.Y + address.Height > address.CanvasHeight)
        {
            throw new InvalidDataException("The native texture source publishes an invalid crop address.");
        }
        DecodedTextureImage decoded = cache.Get(entry, () => TextureImageDecoder.Decode(entry.Bytes, entry.Variant.RelativePath), decoded => PixelBytes(decoded.MipLevels));
        if (sheet && (decoded.Image.Width != decoded.Image.Height || decoded.Image.Width % 128 != 0))
        {
            throw new InvalidDataException("Native field model sheets require a square uniform integer enlargement of the published 128x128 canvas.");
        }
        string layout = sheet ? "native-field-model-sheet" : context.Layout == "native-effect-texture" && entry.Contract?.Layout == "native-effect-source" ? "native-effect-source" : context.Layout;
        TextureBuildContext canvas = new(entry.Variant.Target.Key, context.Source, context.Revision, layout, address.CanvasWidth, address.CanvasHeight, () => throw new InvalidOperationException("Full native source pixels are not needed for alias validation.")) { Game = context.Game };
        (entry.Contract ?? TextureTargetContract.FromContext(canvas)).Validate(canvas, decoded.Image);
        List<Albeoris.Games.FF8.Imaging.PixelImage> levels = [];
        foreach (Albeoris.Games.FF8.Imaging.PixelImage level in decoded.MipLevels)
        {
            if ((long)address.X * level.Width % address.CanvasWidth != 0 || (long)address.Y * level.Height % address.CanvasHeight != 0 || (long)address.Width * level.Width % address.CanvasWidth != 0 || (long)address.Height * level.Height % address.CanvasHeight != 0)
            {
                break;
            }
            Albeoris.Games.FF8.Imaging.PixelImage source = level;
            if (entry.Coverage is { } coverage)
            {
                bool uniform = coverage.Width >= address.CanvasWidth && coverage.Height >= address.CanvasHeight && coverage.Width % address.CanvasWidth == 0 && coverage.Height % address.CanvasHeight == 0 && coverage.Width / address.CanvasWidth == coverage.Height / address.CanvasHeight;
                if (!uniform)
                {
                    throw new InvalidDataException("Native alias coverage must preserve the full source canvas at an integral scale.");
                }
                source = TexturePixelExchange.RestoreNativeFlags(source, coverage.Width, coverage.Height, coverage.Alpha);
            }
            int x = checked((int)((long)address.X * level.Width / address.CanvasWidth));
            int y = checked((int)((long)address.Y * level.Height / address.CanvasHeight));
            int width = checked((int)((long)address.Width * level.Width / address.CanvasWidth));
            int height = checked((int)((long)address.Height * level.Height / address.CanvasHeight));
            Albeoris.Games.FF8.Imaging.PixelImage crop = new(width, height);
            for (int row = 0; row < height; row++)
            {
                source.Pixels.AsSpan((y + row) * source.Width + x, width).CopyTo(crop.Pixels.AsSpan(row * width, width));
            }
            if (entry.Coverage is null && TexturePixelExchange.UsesNativeFlags(context))
            {
                crop = TexturePixelExchange.RestoreNativeFlags(crop, context.Original.Width, context.Original.Height, TexturePixelExchange.CaptureNativeFlags(context.Original));
            }
            if (context.Layout == "native-effect-texture")
            {
                foreach (ref Albeoris.Games.FF8.Imaging.Rgba32 pixel in crop.Pixels.AsSpan())
                {
                    pixel = pixel with { A = pixel.A >= 127 ? (byte)127 : (byte)0 };
                }
            }
            levels.Add(crop);
        }
        if (levels.Count == 0)
        {
            throw new InvalidDataException("Replacement dimensions do not retain the source crop boundaries; use a uniform integer enlargement.");
        }
        return new(levels[0], entry.Variant.PackageId + "/" + entry.Variant.RelativePath, context.LogicalWidth, context.LogicalHeight, levels.AsReadOnly());
    }

    private TextureReplacement Resolve(Entry entry, TextureBuildContext context)
    {
        DecodedTextureImage decoded = cache.Get(entry, () => TextureImageDecoder.Decode(entry.Bytes, entry.Variant.RelativePath), decoded => PixelBytes(decoded.MipLevels));
        TextureTargetContract contract = entry.Contract ?? TextureTargetContract.FromContext(context);
        contract.Validate(context, decoded.Image);
        Albeoris.Games.FF8.Imaging.PixelImage image = decoded.Image;
        IReadOnlyList<Albeoris.Games.FF8.Imaging.PixelImage> mips = decoded.MipLevels;
        TextureNativeCoverage? nativeCoverage = entry.Coverage;
        if (nativeCoverage is null && TexturePixelExchange.UsesNativeFlags(context))
        {
            nativeCoverage = new(context.Original.Width, context.Original.Height, TexturePixelExchange.CaptureNativeFlags(context.Original));
        }
        if (nativeCoverage is { } coverage)
        {
            bool sourceDimensions = coverage.Width == context.Original.Width && coverage.Height == context.Original.Height;
            bool exactTexels = context.Layout.Contains("tile", StringComparison.Ordinal) || context.Layout.Contains("page", StringComparison.Ordinal) || context.Layout.Contains("vram", StringComparison.Ordinal) || context.Layout.Contains("atlas", StringComparison.Ordinal);
            bool compatibleCanvas = !exactTexels || coverage.Width >= context.LogicalWidth && coverage.Height >= context.LogicalHeight && coverage.Width % context.LogicalWidth == 0 && coverage.Height % context.LogicalHeight == 0 && coverage.Width / context.LogicalWidth == coverage.Height / context.LogicalHeight;
            if (!sourceDimensions && (entry.Contract is null || !compatibleCanvas))
            {
                throw new InvalidDataException("Native texture coverage dimensions differ from the selected source; re-export this texture.");
            }
            image = TexturePixelExchange.RestoreNativeFlags(image, coverage.Width, coverage.Height, coverage.Alpha);
            mips = decoded.MipLevels.Select(mip => TexturePixelExchange.RestoreNativeFlags(mip, coverage.Width, coverage.Height, coverage.Alpha)).ToArray();
        }
        return new(image, entry.Variant.PackageId + "/" + entry.Variant.RelativePath, context.LogicalWidth, context.LogicalHeight, mips);
    }

    private static long PixelBytes(IEnumerable<Albeoris.Games.FF8.Imaging.PixelImage> images) => images.Sum(image => (long)image.Width * image.Height * 4);
    private static long ReplacementBytes(TextureReplacement replacement) => PixelBytes(replacement.MipLevels ?? [replacement.Image]) + PixelBytes(replacement.Variants?.Values ?? []) + PixelBytes(replacement.Animation?.Frames ?? []);

    private sealed record ResolveKey(Entry Entry, TextureKey Key, long Revision, string SourceId, GameDataEdition Edition, string Language, string Layout, int Width, int Height, string? Variant);
    private sealed record AliasResolveKey(Entry Entry, TextureKey Key, long Revision, string SourceId, GameDataEdition Edition, string Language, string Layout, int Width, int Height, string? Variant, TextureSourceAddress Address, bool Sheet);
    private sealed record ComposedResolveKey(ResolveKey Context, TextureValue Installed, CompanionSet Companions, TextureSourceAddress? Address, bool Sheet);

    private sealed class CompanionSet : IEquatable<CompanionSet>
    {
        private readonly Entry[] entries;

        public CompanionSet(Entry[] entries)
        {
            this.entries = entries;
        }

        public bool Equals(CompanionSet? other) => other is not null && entries.Length == other.entries.Length && entries.Select((entry, index) => ReferenceEquals(entry, other.entries[index])).All(same => same);
        public override bool Equals(object? other) => other is CompanionSet companions && Equals(companions);
        public override int GetHashCode()
        {
            HashCode hash = new();
            foreach (Entry entry in entries)
            {
                hash.Add(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(entry));
            }
            return hash.ToHashCode();
        }
    }

    private sealed class TextureCache
    {
        private const long MaximumBytes = 128L * 1024 * 1024;
        private const int MaximumEntries = 256;
        private readonly object gate = new();
        private readonly Dictionary<object, (object Value, long Bytes, LinkedListNode<object> Node)> values = [];
        private readonly LinkedList<object> order = [];
        private long bytes;

        public T Get<T>(object key, Func<T> create, Func<T, long> measure) where T : class
        {
            lock (gate)
            {
                if (values.TryGetValue(key, out var cached))
                {
                    order.Remove(cached.Node);
                    order.AddLast(cached.Node);
                    return (T)cached.Value;
                }
                T value = create();
                long size = measure(value);
                if (size > MaximumBytes)
                {
                    return value;
                }
                while (values.Count >= MaximumEntries || bytes + size > MaximumBytes)
                {
                    object oldest = order.First!.Value;
                    bytes -= values[oldest].Bytes;
                    values.Remove(oldest);
                    order.RemoveFirst();
                }
                LinkedListNode<object> node = order.AddLast(key);
                values.Add(key, (value, size, node));
                bytes += size;
                return value;
            }
        }
    }

    private static bool IsValidationError(Exception exception) => exception is IOException or InvalidDataException or NotSupportedException or ArgumentException or OverflowException or System.Text.Json.JsonException;
}
