using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Tests;

namespace Memoria.FF8.Mods.Tests;

public sealed class DemasterBattleModelInstalledTests
{
    private static readonly Regex Target = new(@"\Atextures/battle\.fs/hd_new/(?<owner>c0m[0-9]{3}|d[0-9a-f][cw][0-9]{3})_(?<sheet>0|[1-9][0-9]*)\.(?:png|dds)\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [Fact]
    public void InstalledModelSheetsResolveAgainstEveryOriginalConsumer()
    {
        string[] packages = OptionalPackages();
        string game = TestResources.GameDirectory;
        using IResourceSource source = GameDataSourceFactory.Open(game);
        Dictionary<string, TextureBuildContext[]> owners = new(StringComparer.OrdinalIgnoreCase);
        List<object> evidence = [];
        List<string> failures = [];
        int decodedCount = 0;
        int consumedCount = 0;
        int unusedCount = 0;
        for (int packageIndex = 0; packageIndex < packages.Length; packageIndex++)
        {
            string directory = packages[packageIndex];
            Assert.True(Directory.Exists(directory), "A configured battle model package directory is unavailable.");
            string packageId = $"Demaster/Fixture{packageIndex}";
            List<object> files = [];
            int imageCount = 0;
            string[] packagePaths = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (string path in packagePaths)
            {
                if (Path.GetExtension(path).ToLowerInvariant() is not (".png" or ".dds"))
                {
                    continue;
                }
                imageCount++;
                string relative = Path.GetRelativePath(directory, path).Replace('\\', '/');
                try
                {
                    ModFile file = new(packageId, ModFormat.Demaster, relative, 1, () => File.ReadAllBytes(path));
                    DecodedModImage decoded = ModImageDecoder.Decode(file);
                    Assert.True(decoded.Image.Width > 0 && decoded.Image.Height > 0);
                    decodedCount++;
                    Match match = Target.Match(relative);
                    if (!match.Success)
                    {
                        unusedCount++;
                        files.Add(new { path = relative, role = "unused", reason = "not-native-model-sheet-target", width = decoded.Image.Width, height = decoded.Image.Height });
                        continue;
                    }
                    string owner = match.Groups["owner"].Value.ToLowerInvariant() + ".dat";
                    if (!owners.TryGetValue(owner, out TextureBuildContext[]? contexts))
                    {
                        List<TextureBuildContext> captured = [];
                        ResourceService resources = new(source);
                        resources.Prepare(new Recorder(captured)).Commit();
                        using BattleAssets assets = new(game, resources);
                        BattleModel model;
                        if (owner[0] == 'd' && owner[2] == 'w')
                        {
                            int character = int.Parse(owner.AsSpan(1, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                            int weapon = int.Parse(owner.AsSpan(3, 3), CultureInfo.InvariantCulture);
                            model = assets.LoadWeapon(character, weapon, assets.LoadPlayer(character));
                        }
                        else
                        {
                            model = assets.LoadModel(owner);
                        }
                        foreach (PixelImage image in model.Textures)
                        {
                            Assert.True(image.Width > 0 && image.Height > 0);
                        }
                        contexts = captured.Where(context => context.Key.Owner.Path.Equals("battle/" + owner, StringComparison.OrdinalIgnoreCase)).DistinctBy(context => context.Key).ToArray();
                        Assert.NotEmpty(contexts);
                        owners.Add(owner, contexts);
                    }
                    List<ModFile> candidates = [file];
                    bool body = owner[0] == 'd' && owner[2] == 'c';
                    PixelImage? anchor = null;
                    if (body)
                    {
                        foreach (string companionPath in packagePaths)
                        {
                            string companionRelative = Path.GetRelativePath(directory, companionPath).Replace('\\', '/');
                            Match companionMatch = Target.Match(companionRelative);
                            if (!companionMatch.Success || !companionMatch.Groups["owner"].Value.Equals(match.Groups["owner"].Value, StringComparison.OrdinalIgnoreCase) || companionMatch.Groups["sheet"].Value == match.Groups["sheet"].Value)
                            {
                                continue;
                            }
                            candidates.Add(new(packageId, ModFormat.Demaster, companionRelative, 1, () => File.ReadAllBytes(companionPath)));
                        }
                        foreach (ModFile candidate in candidates)
                        {
                            PixelImage image = ModImageDecoder.Decode(candidate).Image;
                            if (image.Width == image.Height && image.Width > 0 && image.Width % 128 == 0)
                            {
                                anchor = image;
                                break;
                            }
                        }
                        Assert.NotNull(anchor);
                    }
                    DemasterTextureResolver resolver = new(new(1, [], candidates));
                    List<object> consumers = [];
                    string row = "row/" + match.Groups["sheet"].Value;
                    foreach (TextureBuildContext context in contexts)
                    {
                        TextureReplacement? resolved = resolver.Resolve(context);
                        if (context.NativeSheet?.Subresource != row)
                        {
                            if (resolved is not null)
                            {
                                Assert.NotEqual($"{packageId}/{relative}@1", resolved.Provenance);
                                Assert.Contains(candidates, candidate => resolved.Provenance == $"{candidate.PackageId}/{candidate.RelativePath}@{candidate.Revision}");
                            }
                            continue;
                        }
                        TextureReplacement replacement = Assert.IsType<TextureReplacement>(resolved);
                        TextureSourceAddress address = Assert.IsType<TextureSourceAddress>(context.NativeSheet);
                        Assert.Equal(context.Key.Owner, Assert.IsType<ResourceContent>(context.EncodedSource).Key);
                        Assert.Equal(context.LogicalWidth, context.Original.Width);
                        Assert.Equal(context.LogicalHeight, context.Original.Height);
                        Assert.Equal(context.Key.Owner, address.Owner);
                        Assert.Equal("classic-battle", context.Layout);
                        Assert.Contains("native-battle-model-sheet", context.Source.Layouts);
                        Assert.Equal($"{packageId}/{relative}@1", replacement.Provenance);
                        Assert.Equal(context.LogicalWidth, replacement.LogicalWidth);
                        Assert.Equal(context.LogicalHeight, replacement.LogicalHeight);
                        Assert.True(address.Width > 0 && address.Height > 0);
                        Assert.True(address.X >= 0 && address.Y >= 0);
                        Assert.True(address.X + address.Width <= address.CanvasWidth);
                        Assert.True(address.Y + address.Height <= address.CanvasHeight);
                        int scale;
                        if (body)
                        {
                            scale = anchor!.Width / 128;
                            Assert.True(decoded.Image.Width == 128 * scale || decoded.Image.Width == 256 * scale);
                            Assert.True(decoded.Image.Height <= 128 * scale);
                        }
                        else
                        {
                            Assert.Equal(0, decoded.Image.Width % address.CanvasWidth);
                            Assert.Equal(0, decoded.Image.Height % address.CanvasHeight);
                            scale = decoded.Image.Width / address.CanvasWidth;
                            Assert.Equal(scale, decoded.Image.Height / address.CanvasHeight);
                        }
                        Assert.True(scale > 0);
                        VerifyPixels(replacement.Image, decoded.Image, address, scale, 0);
                        bool closed = body && decoded.Image.Width == 256 * scale;
                        if (closed)
                        {
                            Assert.NotNull(replacement.Variants);
                            Assert.True(replacement.Variants.TryGetValue("eyes-closed", out PixelImage? closedImage));
                            VerifyPixels(Assert.IsType<PixelImage>(closedImage), decoded.Image, address, scale, 128 * scale);
                        }
                        else
                        {
                            Assert.False(replacement.Variants?.ContainsKey("eyes-closed") == true);
                        }
                        consumers.Add(new { sourceOwner = address.Owner.Path, context = context.Key.Subresource, sheetRow = address.Subresource, address.X, address.Y, address.Width, address.Height, address.CanvasWidth, address.CanvasHeight, context.LogicalWidth, context.LogicalHeight, scale, closed, replacement.Provenance });
                    }
                    Assert.NotEmpty(consumers);
                    consumedCount++;
                    files.Add(new { path = relative, role = "consumed", width = decoded.Image.Width, height = decoded.Image.Height, sourceContextCount = contexts.Length, consumers });
                }
                catch (Exception exception)
                {
                    string failure = $"{packageId}/{relative}: {exception.Message}";
                    failures.Add(failure);
                    files.Add(new { path = relative, role = "unresolved", reason = exception.Message });
                    Console.WriteLine(failure);
                }
            }
            Assert.True(imageCount > 0, "The configured package contains no PNG or DDS images.");
            evidence.Add(new { packageId, imageCount, files });
        }
        WriteEvidence(new { sourceEdition = source.Descriptor.Edition.ToString(), sourceLanguage = source.Descriptor.Language, decodedCount, consumedCount, unusedCount, failures, packages = evidence });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        Assert.True(consumedCount > 0);
        Assert.Equal(decodedCount, consumedCount + unusedCount);
    }

    private static void VerifyPixels(PixelImage actual, PixelImage encoded, TextureSourceAddress address, int scale, int planeX)
    {
        Assert.Equal(address.Width * scale, actual.Width);
        Assert.Equal(address.Height * scale, actual.Height);
        for (int y = 0; y < actual.Height; y++)
        {
            for (int x = 0; x < actual.Width; x++)
            {
                int sourceX = planeX + address.X * scale + x;
                int sourceY = address.Y * scale + y;
                Rgba32 expected = sourceY < encoded.Height ? encoded[sourceX, sourceY] : default;
                Assert.Equal(expected, actual[x, y]);
            }
        }
    }
    private static string[] OptionalPackages()
    {
        string? catalog = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_MODEL_TEXTURE_PACKAGES");
        Assert.SkipUnless(File.Exists(catalog), "Set MEMORIA_FF8_BATTLE_MODEL_TEXTURE_PACKAGES to an optional JSON array of external directory paths.");
        string[] paths = JsonSerializer.Deserialize<string[]>(File.ReadAllText(catalog!)) ?? throw new InvalidDataException("The external directory catalog must be a JSON string array.");
        Assert.NotEmpty(paths);
        string parent = Path.GetDirectoryName(Path.GetFullPath(catalog!))!;
        return paths.Select(path => Path.GetFullPath(path, parent)).ToArray();
    }

    private static void WriteEvidence(object value)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "battle-model-installed-coverage.json"), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private sealed class Recorder(List<TextureBuildContext> contexts) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            contexts.Add(context);
            return null;
        }
    }
}
