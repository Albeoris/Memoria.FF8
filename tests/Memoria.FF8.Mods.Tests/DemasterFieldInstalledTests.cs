using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Tests;

namespace Memoria.FF8.Mods.Tests;

public sealed class DemasterFieldInstalledTests
{
    private static readonly Regex Target = new(@"\Atextures/field_bg/(?<prefix>[a-z0-9_]{2})/(?<map>[a-z0-9_]{2,})/\k<map>_(?<page>0|[1-9][0-9]*)(?:_(?<palette>0|[1-9][0-9]*))?\.(?:dds|png)\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [Fact]
    public void InstalledFieldPagesDecodeAndResolveAgainstOriginalMapContexts()
    {
        string[] packages = OptionalPaths("MEMORIA_FF8_FIELD_TEXTURE_PACKAGES");
        using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
        HashSet<string> names = Encoding.ASCII.GetString(Read(source, "field/mapdata/maplist")).Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        ResourceSourceDescriptor descriptor = source.Descriptor with { Layouts = source.Descriptor.Layouts.Add("native-field-background-page") };
        List<object> evidence = [];
        int decodedCount = 0;
        for (int packageIndex = 0; packageIndex < packages.Length; packageIndex++)
        {
            string directory = packages[packageIndex];
            Assert.True(Directory.Exists(directory), "A configured field package directory is unavailable.");
            string packageId = $"Demaster/Fixture{packageIndex}";
            List<object> files = [];
            Dictionary<string, int> unused = new(StringComparer.Ordinal);
            int selectedCount = 0;
            long contextCount = 0;
            string? currentName = null;
            MapFile? currentMap = null;
            foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase))
            {
                string relative = Path.GetRelativePath(directory, path).Replace('\\', '/');
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension == ".map")
                {
                    string companionName = Path.GetFileNameWithoutExtension(path);
                    Assert.Contains(companionName, names);
                    Assert.Equal(Read(source, $"field/{companionName}/{companionName}.map"), File.ReadAllBytes(path));
                    files.Add(new { path = relative, role = "identical-source-map-companion" });
                    continue;
                }
                if (extension is not (".dds" or ".png"))
                {
                    continue;
                }
                ModFile file = new(packageId, ModFormat.Demaster, relative, 1, () => File.ReadAllBytes(path));
                DecodedModImage decoded = ModImageDecoder.Decode(file);
                Assert.True(decoded.Image.Width > 0);
                Assert.Equal(decoded.Image.Width, decoded.Image.Height);
                Assert.Equal(0, decoded.Image.Width % 256);
                decodedCount++;
                if (decodedCount % 500 == 0)
                {
                    Console.WriteLine($"Decoded {decodedCount} configured field images.");
                }
                Match match = Target.Match(relative);
                string? reason = null;
                string name = match.Success ? match.Groups["map"].Value : "";
                int page = match.Success ? int.Parse(match.Groups["page"].Value) : -1;
                int? palette = match.Success && match.Groups["palette"].Success ? int.Parse(match.Groups["palette"].Value) : null;
                if (!match.Success || !name.StartsWith(match.Groups["prefix"].Value, StringComparison.OrdinalIgnoreCase))
                {
                    reason = "not-native-field-page-filename";
                }
                else if (page > 15 || palette > 15)
                {
                    reason = "outside-native-page-or-palette-domain";
                }
                else if (!names.Contains(name))
                {
                    reason = "field-absent-from-source-maplist";
                }
                else
                {
                    if (!StringComparer.OrdinalIgnoreCase.Equals(currentName, name))
                    {
                        currentName = name;
                        currentMap = null;
                        ResourceContent? mapContent = source.Find(new($"field/{name}/{name}.map"));
                        if (mapContent is not null)
                        {
                            using Stream input = mapContent.OpenRead();
                            using MemoryStream output = new();
                            input.CopyTo(output);
                            currentMap = MapFile.Read(output.ToArray());
                        }
                    }
                    if (currentMap is null)
                    {
                        reason = "source-map-resource-absent";
                    }
                }
                (MapTile tile, int index)[] contexts = reason is null ? currentMap!.Tiles.Select((tile, index) => (tile, index)).Where(item => item.tile.TextureId == page && (palette is null || item.tile.PaletteId == palette)).ToArray() : [];
                if (reason is null && contexts.Length == 0)
                {
                    reason = "no-source-map-page-palette-context";
                }
                if (reason is not null)
                {
                    unused[reason] = unused.GetValueOrDefault(reason) + 1;
                    files.Add(new { path = relative, role = "unused", reason, sourceField = name, page, palette });
                    GC.KeepAlive(decoded);
                    continue;
                }
                (MapTile selectedTile, int selectedIndex) = contexts[0];
                ResourceKey owner = new($"field/{name}/{name}.mim");
                TextureBuildContext context = new(new(owner, $"tile/{selectedIndex}", selectedTile.PaletteId), descriptor, 1, "classic-field-tile", 16, 16, () => throw new InvalidOperationException("Native page lookup must not decode original pixels."), new(BitsPerPixel: selectedTile.Uses8BitIndices ? 8 : 4))
                {
                    NativeAlias = FieldBackgroundTileDecoder.Address(owner, selectedTile)
                };
                DemasterTextureResolver resolver = new(new(1, [], [file]));
                TextureReplacement replacement = Assert.IsType<TextureReplacement>(resolver.Resolve(context));
                Assert.Equal($"{packageId}/{relative}@1", replacement.Provenance);
                Assert.Equal(16, replacement.LogicalWidth);
                Assert.Equal(16, replacement.LogicalHeight);
                int scale = decoded.Image.Width / 256;
                Assert.Equal(16 * scale, replacement.Image.Width);
                Assert.Equal(16 * scale, replacement.Image.Height);
                for (int y = 0; y < replacement.Image.Height; y++)
                {
                    for (int x = 0; x < replacement.Image.Width; x++)
                    {
                        Assert.Equal(decoded.Image[selectedTile.SourceX * scale + x, selectedTile.SourceY * scale + y], replacement.Image[x, y]);
                    }
                }
                selectedCount++;
                contextCount += contexts.Length;
                files.Add(new { path = relative, role = "selected", sourceField = name, page, palette, sourceContextCount = contexts.Length, tileIndex = selectedIndex, sourceU = selectedTile.SourceX, sourceV = selectedTile.SourceY, tileX = selectedTile.X, tileY = selectedTile.Y, sourcePalette = selectedTile.PaletteId });
                GC.KeepAlive(decoded);
            }
            Assert.True(files.Count > 0, "The configured package contains no images or MAP companions.");
            evidence.Add(new { packageId, selectedCount, sourceContextCount = contextCount, unusedReasons = unused, files });
        }
        WriteEvidence("field-installed-coverage.json", new { sourceEdition = source.Descriptor.Edition.ToString(), sourceLanguage = source.Descriptor.Language, decodedCount, packages = evidence });
        Assert.True(decodedCount > 0);
    }

    [Fact]
    public void OptionalExtractedFieldImagesDecodeIndependently()
    {
        string[] directories = OptionalPaths("MEMORIA_FF8_FIELD_TEXTURE_OPTIONAL_IMAGES");
        List<object> images = [];
        foreach (string directory in directories)
        {
            Assert.True(Directory.Exists(directory));
            foreach (string path in Directory.EnumerateFiles(directory, "*.png", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase))
            {
                DecodedModImage decoded = ModImageDecoder.Decode(File.ReadAllBytes(path), Path.GetFileName(path));
                Assert.True(decoded.Image.Width > 0);
                Assert.Equal(decoded.Image.Width, decoded.Image.Height);
                Assert.Equal(0, decoded.Image.Width % 256);
                images.Add(new { path = Path.GetRelativePath(directory, path).Replace('\\', '/'), width = decoded.Image.Width, height = decoded.Image.Height });
            }
        }
        Assert.NotEmpty(images);
        WriteEvidence("field-optional-image-coverage.json", images);
    }

    private static string[] OptionalPaths(string variable)
    {
        string? catalog = Environment.GetEnvironmentVariable(variable);
        Assert.SkipUnless(File.Exists(catalog), $"Set {variable} to an optional JSON array of external directory paths.");
        string[] paths = JsonSerializer.Deserialize<string[]>(File.ReadAllText(catalog!)) ?? throw new InvalidDataException("The external directory catalog must be a JSON string array.");
        Assert.NotEmpty(paths);
        string parent = Path.GetDirectoryName(Path.GetFullPath(catalog!))!;
        return paths.Select(path => Path.GetFullPath(path, parent)).ToArray();
    }

    private static byte[] Read(IResourceSource source, string key)
    {
        ResourceContent content = source.Find(new(key)) ?? throw new InvalidDataException($"The configured original source has no '{key}'.");
        using Stream input = content.OpenRead();
        using MemoryStream output = new();
        input.CopyTo(output);
        return output.ToArray();
    }

    private static void WriteEvidence(string name, object value)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, name), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}



