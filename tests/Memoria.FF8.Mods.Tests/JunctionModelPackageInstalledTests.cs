using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class JunctionModelPackageInstalledTests
{
    [Fact]
    public void OptionalSelectedModelPackageTexturesResolveAgainstInstalledNativeConsumers()
    {
        string? packagePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_MODEL_PACKAGE");
        string? optionsPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_MODEL_OPTIONS");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(packagePath) && File.Exists(optionsPath) && Directory.Exists(game), "Configure the optional model package, option selections and original game data.");
        Dictionary<string, int>[] selections = JsonSerializer.Deserialize<Dictionary<string, int>[]>(File.ReadAllText(optionsPath!))!;
        Assert.NotEmpty(selections);
        using ClassicGameDataSource source = new(game!);
        Dictionary<string, IReadOnlyList<TextureBuildContext>> fieldContexts = new(StringComparer.Ordinal);
        Dictionary<string, IReadOnlyList<TextureBuildContext>> otherContexts = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> decoded = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> validatedModels = new(StringComparer.Ordinal);
        HashSet<string> checkedBindings = new(StringComparer.Ordinal);
        List<Observation> observations = [];
        List<string> failures = [];
        for (int selection = 0; selection < selections.Length; selection++)
        {
            JunctionModPackage package = JunctionModImporter.Open(packagePath!, selections[selection]);
            ModFile[] files = package.Files.Select(file => ToFile(file)).ToArray();
            ModCatalogSnapshot catalog = new(selection, [], files);
            Assert.True(JunctionEncodedResources.TryPrepare(catalog, out var encoded, out var encodedDiagnostics), string.Join(Environment.NewLine, encodedDiagnostics));
            foreach (EncodedResource resource in encoded)
            {
                if (validatedModels.Add(resource.Fingerprint))
                {
                    GameAssets.ValidateMainCharacterResource(resource.Content);
                }
            }
            foreach (JunctionReplacement replacement in package.Files.Where(IsTexture))
            {
                try
                {
                    ModFile file = ToFile(replacement);
                    if (decoded.Add(replacement.SourcePath))
                    {
                        DecodedModImage image = ModImageDecoder.Decode(file);
                        Assert.True(image.Image.Width > 0 && image.Image.Height > 0);
                    }
                    string target = replacement.RelativePath[14..^Path.GetExtension(replacement.RelativePath).Length].ToLowerInvariant();
                    Match fieldTarget = Regex.Match(target, @"^field/model/main_chr/([a-z][0-9]{3})-", RegexOptions.CultureInvariant);
                    string modelSignature = fieldTarget.Success ? encoded.SingleOrDefault(resource => resource.Key.Path == $"field/main_chr/{fieldTarget.Groups[1].Value}.mch")?.Fingerprint ?? "installed" : "native";
                    string binding = replacement.SourcePath + "|" + modelSignature;
                    if (!checkedBindings.Add(binding))
                    {
                        continue;
                    }
                    DemasterInspection inspection = FfnxTextureResolver.Inspect(source.Descriptor, new(selection, [], [file]), source);
                    if (inspection.Diagnostics.Count > 0)
                    {
                        ModDiagnostic diagnostic = Assert.Single(inspection.Diagnostics);
                        Assert.Equal("ffnx-unused-native-target", diagnostic.Code);
                        observations.Add(new(selection, replacement.SourcePath, replacement.RelativePath, "unused", diagnostic.Message, []));
                        continue;
                    }
                    IReadOnlyList<TextureBuildContext> native = Contexts(target, modelSignature, encoded, source, game!, fieldContexts, otherContexts);
                    Assert.NotEmpty(native);
                    FfnxTextureResolver resolver = new(new(selection, [], [file]));
                    List<string> consumers = [];
                    foreach (TextureBuildContext context in native)
                    {
                        if (resolver.Resolve(context) is { } resolved)
                        {
                            Assert.Contains(replacement.RelativePath, resolved.Provenance, StringComparison.OrdinalIgnoreCase);
                            Assert.True(resolved.Image.Width > 0 && resolved.Image.Height > 0);
                            consumers.Add(context.Key.Owner.Path + ":" + context.Key.Subresource);
                        }
                    }
                    Assert.NotEmpty(consumers);
                    observations.Add(new(selection, replacement.SourcePath, replacement.RelativePath, "consumed", "actual-native-context", consumers.Distinct().ToArray()));
                }
                catch (Exception exception)
                {
                    string failure = replacement.SourcePath + ": " + exception.Message;
                    failures.Add(failure);
                    observations.Add(new(selection, replacement.SourcePath, replacement.RelativePath, "failed", exception.ToString(), []));
                }
            }
        }
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_MODEL_EVIDENCE");
        if (!string.IsNullOrWhiteSpace(evidence))
        {
            Directory.CreateDirectory(evidence);
            File.WriteAllText(Path.Combine(evidence, "texture-native-consumers.json"), JsonSerializer.Serialize(new { SelectionCount = selections.Length, DecodedPayloads = decoded.Count, CheckedBindings = checkedBindings.Count, Failures = failures, Observations = observations }, new JsonSerializerOptions { WriteIndented = true }));
        }
        Console.WriteLine($"model-package-textures selections={selections.Length} decoded={decoded.Count} bindings={checkedBindings.Count} consumed={observations.Count(item => item.Status == "consumed")} unused={observations.Count(item => item.Status == "unused")} failed={failures.Count}");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        Assert.NotEmpty(decoded);
    }

    private static IReadOnlyList<TextureBuildContext> Contexts(string target, string signature, IReadOnlyList<EncodedResource> encoded, ClassicGameDataSource source, string game, Dictionary<string, IReadOnlyList<TextureBuildContext>> fieldContexts, Dictionary<string, IReadOnlyList<TextureBuildContext>> otherContexts)
    {
        Match field = Regex.Match(target, @"^field/model/main_chr/([a-z][0-9]{3})-([0-9]+)_[0-9]{2}$", RegexOptions.CultureInvariant);
        if (field.Success)
        {
            string name = field.Groups[1].Value;
            string key = signature + "|" + name;
            if (!fieldContexts.TryGetValue(key, out IReadOnlyList<TextureBuildContext>? cached))
            {
                List<TextureBuildContext> captured = [];
                ResourceService resources = new(source);
                resources.Prepare(new Recorder(captured), encodedResources: encoded).Commit();
                using GameAssets assets = new(game, resources: resources);
                var textures = assets.LoadFieldMainCharacterTextures(name);
                for (int texture = 0; texture < textures.Count; texture++)
                {
                    _ = textures[texture];
                }
                cached = captured.Where(context => context.Key.Owner.Path == $"field/main_chr/{name}.mch").ToArray();
                fieldContexts.Add(key, cached);
            }
            return cached;
        }
        int separator = target.LastIndexOf('-');
        Assert.True(separator > 0, target);
        string owner = target[..separator];
        if (otherContexts.TryGetValue(owner, out IReadOnlyList<TextureBuildContext>? existing))
        {
            return existing;
        }
        List<TextureBuildContext> contexts = [];
        ResourceService nativeResources = new(source);
        nativeResources.Prepare(new Recorder(contexts)).Commit();
        if (owner.StartsWith("battle/", StringComparison.Ordinal))
        {
            string model = owner[7..];
            using BattleAssets assets = new(game, nativeResources);
            if (Regex.IsMatch(model, @"^d[0-9a-f]w[0-9]{3}\.dat$", RegexOptions.CultureInvariant))
            {
                int character = int.Parse(model.AsSpan(1, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                assets.LoadWeapon(character, int.Parse(model.AsSpan(3, 3), CultureInfo.InvariantCulture), assets.LoadPlayer(character));
            }
            else
            {
                assets.LoadModel(model);
            }
            contexts = contexts.Where(context => context.Key.Owner.Path == owner).ToList();
        }
        else
        {
            Match world = Regex.Match(owner, @"^world/esk/chara_one/model([0-9]+)$", RegexOptions.CultureInvariant);
            Assert.True(world.Success, $"No native consumer loader is available for '{target}'.");
            using WorldAssets assets = new(game, nativeResources);
            _ = int.Parse(world.Groups[1].Value, CultureInfo.InvariantCulture) switch
            {
                0 => assets.GetFootModel(0),
                1 => assets.GetRagnarokModel(),
                4 => assets.GetFootModel(1),
                5 => assets.GetFootModel(2),
                6 => assets.GetFootModel(6),
                _ => throw new NotSupportedException($"World model '{owner}' has no implemented native producer.")
            };
            contexts = contexts.Where(context => context.Key.Owner.Path == "world/chara.one").ToList();
        }
        otherContexts.Add(owner, contexts);
        return contexts;
    }

    private static bool IsTexture(JunctionReplacement file)
    {
        string extension = Path.GetExtension(file.RelativePath);
        return file.RelativePath.StartsWith("mods/textures/", StringComparison.OrdinalIgnoreCase) && (extension.Equals(".dds", StringComparison.OrdinalIgnoreCase) || extension.Equals(".png", StringComparison.OrdinalIgnoreCase));
    }

    private static ModFile ToFile(JunctionReplacement file) => new("installed-model-package", ModFormat.Junction, file.RelativePath, 1, () =>
    {
        using MemoryStream output = new();
        file.CopyTo(output);
        return output.ToArray();
    });

    private sealed class Recorder(List<TextureBuildContext> contexts) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            contexts.Add(context);
            return null;
        }
    }

    private sealed record Observation(int Selection, string SourcePath, string RelativePath, string Status, string Proof, IReadOnlyList<string> Consumers);
}
