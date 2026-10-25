using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Tests;

namespace Memoria.FF8.Mods.Tests;

public sealed class DemasterFieldModelInstalledTests
{
    private static readonly Regex FieldTarget = new(@"\Atextures/field\.fs/(?<directory>field_hd_new|field_hd)/(?<name>[dnop][0-9]{3})_(?<row>0|[1-9][0-9]*)(?<alpha>_alpha)?\.(?:png|dds)\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex BattleTarget = new(@"\Atextures/battle\.fs/hd_new/(?<name>c0m[0-9]{3}|d[0-9a-f][cw][0-9]{3})_(?<row>0|[1-9][0-9]*)\.(?:png|dds)\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [Fact]
    public void InstalledFieldAndCompanionBattleSheetsReachOriginalSourceContexts()
    {
        string[] packages = Packages();
        string game = TestResources.GameDirectory;
        using IResourceSource source = GameDataSourceFactory.Open(game);
        ModFile[][] packageFiles = packages.Select((directory, index) =>
        {
            Assert.True(Directory.Exists(directory), "A configured model package directory is unavailable.");
            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Where(path => Path.GetExtension(path).ToLowerInvariant() is ".png" or ".dds").Order(StringComparer.OrdinalIgnoreCase).Select(path => new ModFile($"Demaster/Fixture{index}", ModFormat.Demaster, Path.GetRelativePath(directory, path).Replace('\\', '/'), 1, () => File.ReadAllBytes(path))).ToArray();
        }).ToArray();
        Assert.All(packageFiles, files => Assert.NotEmpty(files));
        // Catalog order is base then addon; later packages own matching virtual targets.
        ModFile[] combinedFiles = packageFiles.Reverse().SelectMany(files => files).ToArray();
        Dictionary<string, List<TextureBuildContext>> fieldContexts = CaptureFieldContexts(game, source, combinedFiles);
        Dictionary<string, TextureBuildContext[]> battleContexts = new(StringComparer.OrdinalIgnoreCase);
        DemasterTextureResolver combined = new(new(1, [], combinedFiles));
        List<object> reports = [];
        List<string> failures = [];
        int decodedCount = 0;
        int consumedCount = 0;
        int unusedCount = 0;
        int combinedSelectedCount = 0;
        int shadowedCount = 0;
        foreach (ModFile[] files in packageFiles)
        {
            List<object> images = [];
            foreach (ModFile file in files)
            {
                try
                {
                    DecodedModImage decoded = ModImageDecoder.Decode(file);
                    Assert.True(decoded.Image.Width > 0 && decoded.Image.Height > 0);
                    decodedCount++;
                    Match field = FieldTarget.Match(file.RelativePath);
                    Match battle = BattleTarget.Match(file.RelativePath);
                    if (!field.Success && !battle.Success)
                    {
                        unusedCount++;
                        images.Add(new { path = file.RelativePath, role = "unused", reason = "not-native-model-sheet-target", width = decoded.Image.Width, height = decoded.Image.Height });
                        continue;
                    }
                    bool isField = field.Success;
                    Match target = isField ? field : battle;
                    string stem = Stem(target);
                    IReadOnlyList<TextureBuildContext> contexts;
                    if (isField)
                    {
                        Assert.True(fieldContexts.TryGetValue(stem, out List<TextureBuildContext>? actual), $"No original field model material publishes '{stem}'.");
                        contexts = actual!;
                    }
                    else
                    {
                        string owner = target.Groups["name"].Value.ToLowerInvariant() + ".dat";
                        if (!battleContexts.TryGetValue(owner, out TextureBuildContext[]? actual))
                        {
                            actual = CaptureBattleContexts(game, source, owner);
                            battleContexts.Add(owner, actual);
                        }
                        contexts = actual.Where(context => context.NativeSheet?.Subresource == "row/" + target.Groups["row"].Value).ToArray();
                    }
                    Assert.NotEmpty(contexts);
                    ModFile[] isolated = [file, .. files.Where(candidate => Companion(file, candidate))];
                    DemasterTextureResolver resolver = new(new(1, [], isolated));
                    List<object> consumers = [];
                    bool selected = false;
                    foreach (TextureBuildContext context in contexts)
                    {
                        TextureSourceAddress address = Assert.IsType<TextureSourceAddress>(context.NativeSheet);
                        TextureReplacement replacement = Assert.IsType<TextureReplacement>(resolver.Resolve(context));
                        Assert.Equal(Provenance(file), replacement.Provenance);
                        Assert.Equal(context.Key.Owner, Assert.IsType<ResourceContent>(context.EncodedSource).Key);
                        Assert.Equal(context.LogicalWidth, replacement.LogicalWidth);
                        Assert.Equal(context.LogicalHeight, replacement.LogicalHeight);
                        bool body = !isField && target.Groups["name"].Value[0] == 'd' && char.ToLowerInvariant(target.Groups["name"].Value[2]) == 'c';
                        int scale = Scale(decoded.Image, address, isField, body, isolated);
                        VerifyPixels(replacement.Image, decoded.Image, address, scale, 0);
                        bool closed = (isField || body) && decoded.Image.Width == address.CanvasWidth * scale * 2;
                        if (closed)
                        {
                            Assert.NotNull(replacement.Variants);
                            Assert.True(replacement.Variants.TryGetValue("eyes-closed", out PixelImage? plane));
                            VerifyPixels(Assert.IsType<PixelImage>(plane), decoded.Image, address, scale, address.CanvasWidth * scale);
                        }
                        else
                        {
                            Assert.False(replacement.Variants?.ContainsKey("eyes-closed") == true);
                        }
                        TextureReplacement winning = Assert.IsType<TextureReplacement>(combined.Resolve(context));
                        ModFile expected = Winner(combinedFiles, target, isField);
                        Assert.Equal(Provenance(expected), winning.Provenance);
                        selected |= winning.Provenance == Provenance(file);
                        consumers.Add(new { sourceOwner = context.Key.Owner.Path, sourceMaterial = context.Key.Subresource, sheetOwner = address.Owner.Path, sheetRow = address.Subresource, address.Width, address.Height, scale, closed, replacement.Provenance, combinedProvenance = winning.Provenance });
                    }
                    consumedCount++;
                    if (selected)
                    {
                        combinedSelectedCount++;
                    }
                    else
                    {
                        shadowedCount++;
                    }
                    images.Add(new { path = file.RelativePath, role = "consumed", combinedRole = selected ? "selected" : "shadowed", width = decoded.Image.Width, height = decoded.Image.Height, consumers });
                }
                catch (Exception exception)
                {
                    string failure = $"{file.PackageId}/{file.RelativePath}: {exception.Message}";
                    failures.Add(failure);
                    images.Add(new { path = file.RelativePath, role = "unresolved", reason = exception.Message });
                    Console.WriteLine(failure);
                }
            }
            reports.Add(new { packageId = files[0].PackageId, imageCount = files.Length, files = images });
        }
        Evidence(new { sourceEdition = source.Descriptor.Edition.ToString(), decodedCount, consumedCount, unusedCount, combinedSelectedCount, shadowedCount, failures, packages = reports });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        Assert.True(consumedCount > 0);
        Assert.Equal(decodedCount, consumedCount + unusedCount);
        Assert.Equal(consumedCount, combinedSelectedCount + shadowedCount);
        Console.WriteLine($"model-images decoded={decodedCount} consumed={consumedCount} unused={unusedCount} selected={combinedSelectedCount} shadowed={shadowedCount}");
    }

    private static Dictionary<string, List<TextureBuildContext>> CaptureFieldContexts(string game, IResourceSource source, ModFile[] files)
    {
        HashSet<string> wanted = files.Select(file => FieldTarget.Match(file.RelativePath)).Where(match => match.Success).Select(match => match.Groups["name"].Value.ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<TextureBuildContext>> result = new(StringComparer.OrdinalIgnoreCase);
        List<TextureBuildContext> captured = [];
        ResourceService resources = new(source);
        resources.Prepare(new Recorder(captured)).Commit();
        ModelCatalogEntry[] entries;
        using (GameAssets inventory = new(game, resources: resources))
        {
            int libraryMaterials = 0;
            foreach (string name in wanted.Where(name => name[0] == 'd').Order(StringComparer.Ordinal))
            {
                captured.Clear();
                IReadOnlyList<PixelImage> textures = inventory.LoadFieldMainCharacterTextures(name);
                foreach (PixelImage image in textures)
                {
                    Assert.True(image.Width > 0 && image.Height > 0);
                }
                libraryMaterials += textures.Count;
                RetainFieldContexts(result, captured);
            }
            Console.WriteLine($"Original main-character library materials={libraryMaterials}; field placement inventory follows independently.");
            entries = inventory.EnumerateFieldModelSources().Where(entry => entry.LoadError is null && entry.Name.Length >= 4 && wanted.Contains(ExternalName(entry.Name[..4]))).ToArray();
        }
        foreach (IGrouping<int?, ModelCatalogEntry> field in entries.GroupBy(entry => entry.FieldId))
        {
            using GameAssets assets = new(game, resources: resources);
            foreach (ModelCatalogEntry entry in field)
            {
                captured.Clear();
                FieldModel model = assets.LoadFieldModelSource(entry);
                foreach (PixelImage image in model.Textures)
                {
                    Assert.True(image.Width > 0 && image.Height > 0);
                }
                RetainFieldContexts(result, captured);
            }
        }
        return result;
    }

    private static void RetainFieldContexts(Dictionary<string, List<TextureBuildContext>> result, List<TextureBuildContext> captured)
    {
        foreach (TextureBuildContext context in captured.Where(context => context.Layout == "classic-field-model" && context.NativeSheet is not null))
        {
            TextureSourceAddress address = context.NativeSheet!;
            string name = ExternalName(address.Owner.Path.Split('/')[^1]);
            string[] row = address.Subresource.Split('/');
            Assert.True(row.Length is 2 or 3 && row[0] == "row");
            string stem = name + "_" + row[1] + (row.Length == 3 ? "_" + row[2] : "");
            if (!result.TryGetValue(stem, out List<TextureBuildContext>? found))
            {
                found = [];
                result.Add(stem, found);
            }
            bool library = context.Key.Owner.Path.EndsWith(".mch", StringComparison.Ordinal);
            // Keep actual library and placed ONE ownership separately; repeated placements share the same crop contract.
            if (!found.Any(previous => previous.NativeSheet == address && previous.LogicalWidth == context.LogicalWidth && previous.LogicalHeight == context.LogicalHeight && previous.Key.Owner.Path.EndsWith(".mch", StringComparison.Ordinal) == library))
            {
                found.Add(context);
            }
        }
    }
    private static TextureBuildContext[] CaptureBattleContexts(string game, IResourceSource source, string owner)
    {
        List<TextureBuildContext> captured = [];
        ResourceService resources = new(source);
        resources.Prepare(new Recorder(captured)).Commit();
        using BattleAssets assets = new(game, resources);
        BattleModel model = owner[0] == 'd' && owner[2] == 'w' ? assets.LoadWeapon(int.Parse(owner.AsSpan(1, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture), int.Parse(owner.AsSpan(3, 3), CultureInfo.InvariantCulture), assets.LoadPlayer(int.Parse(owner.AsSpan(1, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture))) : assets.LoadModel(owner);
        foreach (PixelImage image in model.Textures)
        {
            Assert.True(image.Width > 0 && image.Height > 0);
        }
        return captured.Where(context => context.Key.Owner.Path.Equals("battle/" + owner, StringComparison.OrdinalIgnoreCase)).DistinctBy(context => context.Key).ToArray();
    }

    private static string ExternalName(string name) => char.ToLowerInvariant(name[0]) == 'c' ? "n" + name[1..].ToLowerInvariant() : name.ToLowerInvariant();
    private static string Stem(Match target) => target.Groups["name"].Value.ToLowerInvariant() + "_" + target.Groups["row"].Value + (target.Groups["alpha"].Success ? "_alpha" : "");
    private static string Provenance(ModFile file) => $"{file.PackageId}/{file.RelativePath}@{file.Revision}";

    private static bool Companion(ModFile original, ModFile candidate)
    {
        Match first = FieldTarget.Match(original.RelativePath);
        Match second = FieldTarget.Match(candidate.RelativePath);
        if (!first.Success || !second.Success)
        {
            first = BattleTarget.Match(original.RelativePath);
            second = BattleTarget.Match(candidate.RelativePath);
        }
        return first.Success && second.Success && first.Groups["name"].Value.Equals(second.Groups["name"].Value, StringComparison.OrdinalIgnoreCase) && first.Groups["row"].Value != second.Groups["row"].Value;
    }

    private static ModFile Winner(ModFile[] files, Match target, bool field)
    {
        string stem = Stem(target);
        string[] prefixes = field ? ["textures/field.fs/field_hd_new/", "textures/field.fs/field_hd/"] : ["textures/battle.fs/hd_new/"];
        foreach (string prefix in prefixes)
        {
            foreach (string extension in new[] { ".dds", ".png" })
            {
                ModFile? file = files.FirstOrDefault(file => file.RelativePath.Equals(prefix + stem + extension, StringComparison.OrdinalIgnoreCase));
                if (file is not null)
                {
                    return file;
                }
            }
        }
        throw new InvalidDataException("A resolved source material has no native candidate path.");
    }

    private static int Scale(PixelImage image, TextureSourceAddress address, bool field, bool body, ModFile[] files)
    {
        if (field)
        {
            int divisor = image.Width > image.Height ? address.CanvasWidth * 2 : address.CanvasWidth;
            Assert.Equal(0, image.Width % divisor);
            return image.Width / divisor;
        }
        if (body)
        {
            PixelImage anchor = files.Select(file => ModImageDecoder.Decode(file).Image).First(candidate => candidate.Width == candidate.Height && candidate.Width % address.CanvasWidth == 0);
            return anchor.Width / address.CanvasWidth;
        }
        Assert.Equal(0, image.Width % address.CanvasWidth);
        int scale = image.Width / address.CanvasWidth;
        Assert.Equal(scale * address.CanvasHeight, image.Height);
        return scale;
    }

    private static void VerifyPixels(PixelImage actual, PixelImage encoded, TextureSourceAddress address, int scale, int planeX)
    {
        Assert.True(scale > 0);
        Assert.Equal(address.Width * scale, actual.Width);
        Assert.Equal(address.Height * scale, actual.Height);
        for (int y = 0; y < actual.Height; y++)
        {
            int sourceY = address.Y * scale + y;
            int sourceX = planeX + address.X * scale;
            ReadOnlySpan<Rgba32> row = actual.Pixels.AsSpan(y * actual.Width, actual.Width);
            Assert.True(sourceY < encoded.Height ? row.SequenceEqual(encoded.Pixels.AsSpan(sourceY * encoded.Width + sourceX, actual.Width)) : row.ToArray().All(pixel => pixel == default), $"Native crop differs at physical row {y}.");
        }
    }

    private static string[] Packages()
    {
        string? catalog = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_MODEL_TEXTURE_PACKAGES");
        Assert.SkipUnless(File.Exists(catalog), "Set MEMORIA_FF8_FIELD_MODEL_TEXTURE_PACKAGES to an optional JSON array of external package directories, base then addon.");
        string[] paths = JsonSerializer.Deserialize<string[]>(File.ReadAllText(catalog!)) ?? throw new InvalidDataException("Expected an external JSON directory array.");
        Assert.NotEmpty(paths);
        string parent = Path.GetDirectoryName(Path.GetFullPath(catalog!))!;
        return paths.Select(path => Path.GetFullPath(path, parent)).ToArray();
    }

    private static void Evidence(object report)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "field-model-installed-coverage.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private sealed class Recorder(List<TextureBuildContext> captured) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            captured.Add(context);
            return null;
        }
    }
}
