using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using ImageMagick;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class JunctionBattlePackageTests
{
    [Fact]
    public void FieldCopyFramesUseFirstSeenRectangleOrderAndRetainItAcrossSnapshots()
    {
        NativeFieldRegisteredTexture texture = new(1, 896, 0, 128, 128, 512, 242, 142, 15520);
        NativeFieldTextureAnimations animations = NativeFieldTextureAnimations.Register(new string('0', 64), [texture]);
        Assert.Equal(-1, Assert.Single(animations.Textures).CurrentFrame);
        animations = animations.Copy(904, 90, 6, 4, 904, 10);
        Assert.Equal(0, Assert.Single(animations.Textures).CurrentFrame);
        animations = animations.Copy(904, 100, 6, 4, 904, 10);
        Assert.Equal(1, Assert.Single(animations.Textures).CurrentFrame);
        NativeFieldTextureAnimations restored = JsonSerializer.Deserialize<NativeFieldTextureAnimations>(JsonSerializer.Serialize(animations))!;
        animations = animations.Copy(904, 90, 6, 4, 904, 10);
        restored = restored.Copy(904, 90, 6, 4, 904, 10);
        Assert.Equal(0, Assert.Single(animations.Textures).CurrentFrame);
        Assert.Equal(2, Assert.Single(animations.Textures).Frames.Length);
        Assert.Equal(JsonSerializer.Serialize(animations), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void FieldPageLookupRequiresExactMembershipFrameAndClutAndRejectsUnknownHistory()
    {
        using MagickImage image = new(new MagickColor(12, 34, 56, 255), 512, 512);
        byte[] png = image.ToByteArray(MagickFormat.Png);
        ModFile file = new("page", ModFormat.Junction, "mods/textures/field/model/main_chr/a001-0_f0_b002-1_f1_14_512_242.png", 1, () => png);
        FfnxTextureResolver resolver = new(new(1, [], [file]));
        TexturePageMember[] members = [new("main", "a001", 0, 896, 0, 32, 64, 8, 0), new("second", "b002", 1, 928, 0, 32, 64, 8, 1)];
        TextureSourcePageAddress page = new(14, 0, 0, 64, 64, 512, 242, members);
        TextureBuildContext Context(TextureSourcePageAddress address) => new(new(new("field/sample/chara.one"), "model/0/main/a001/registered/texture/0"), new("test", GameDataEdition.Classic, ["classic-ffnx"], "en"), 1, "classic-field-model", 64, 64, () => new(64, 64)) { NativePage = address };
        TextureReplacement result = Assert.IsType<TextureReplacement>(resolver.Resolve(Context(page)));
        Assert.Equal(128, result.Image.Width);
        Assert.Equal(64, result.LogicalWidth);
        Assert.Equal(new Rgba32(12, 34, 56), result.Image[0, 0]);
        Assert.Null(resolver.Resolve(Context(page with { PaletteY = 243 })));
        Assert.Null(resolver.Resolve(Context(page with { Members = members.Reverse().ToArray() })));
        Assert.Null(resolver.Resolve(Context(page with { Members = [members[0] with { Frame = 1 }, members[1]] })));
        ModTextureLoadException unavailable = Assert.Throws<ModTextureLoadException>(() => resolver.Resolve(Context(page with { Members = members.Select(member => member with { Frame = -2 }).ToArray() })));
        Assert.IsType<NotSupportedException>(unavailable.InnerException);
    }

    [Fact]
    public void FieldCopyHistorySurvivesBattleCaptureAndActiveSessionValidation()
    {
        NativeFieldRegisteredTexture texture = new(1, 896, 0, 128, 128, 512, 242, 142, 15520);
        NativeFieldTextureAnimations animations = NativeFieldTextureAnimations.Register(new string('0', 64), [texture]).Copy(904, 90, 6, 4, 904, 10);
        NativeBattleRetainedMemory retained = NativeBattleRetainedMemory.FromColdProcess() with { FieldTextureAnimations = animations };
        BattleDefinition definition = new()
        {
            NativeRetainedMemory = retained,
            Combatants = [new() { Id = "party", IsPlayer = true, Name = "Party", MaximumHp = 1000, CurrentHp = 1000 }, new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000 }]
        };
        BattleSnapshot snapshot = new BattleSession(definition, BattleAtbMode.Normal).Capture();
        NativeBattleRetainedMemory observed = NativeBattleRetainedMemory.Capture(snapshot, retained.ColdExecutableSha256);
        Assert.Same(animations, observed.FieldTextureAnimations);
        GameSession session = new(new GameState { Scene = SceneKind.Field, Battle = snapshot, NativeBattleMemory = observed });
        Assert.Same(animations, session.State.NativeBattleMemory!.FieldTextureAnimations);
    }

    [Theory]
    [InlineData("junction-battle-stages")]
    [InlineData("junction-battle-characters")]
    [InlineData("junction-battle-enemies")]
    public void OptionalOriginalPackagesValidateEveryDeclaredOptionCombination(string role)
    {
        (string archive, string game) = Fixture(role);
        using ClassicGameDataSource source = new(game);
        int combinations = 0;
        foreach (Dictionary<string, int> options in Selections(archive))
        {
            JunctionModPackage package = JunctionModImporter.Open(archive, options);
            Assert.Empty(package.Diagnostics);
            ModFile[] files = Files(role, package);
            DemasterInspection inspection = FfnxTextureResolver.Inspect(source.Descriptor, new(1, [], files), source);
            Assert.All(inspection.Diagnostics, diagnostic => Assert.Equal("ffnx-unused-native-target", diagnostic.Code));
            if (files.Length > inspection.Diagnostics.Count)
            {
                Assert.Contains(role, inspection.UsablePackageIds);
            }
            combinations++;
        }
        Assert.True(combinations > 1);
        Console.WriteLine($"junction-option-combinations={combinations} fixture={role}");
    }

    [Theory]
    [InlineData("junction-battle-stages")]
    [InlineData("junction-battle-characters")]
    [InlineData("junction-battle-enemies")]
    public void OptionalOriginalBattlePayloadsResolveAgainstActualNativeConsumerContexts(string role)
    {
        (string archive, string game) = Fixture(role);
        using ClassicGameDataSource source = new(game);
        Dictionary<string, JunctionReplacement> unique = new(StringComparer.OrdinalIgnoreCase);
        foreach (Dictionary<string, int> options in Selections(archive))
        {
            JunctionModPackage package = JunctionModImporter.Open(archive, options);
            foreach (JunctionReplacement file in package.Files.Where(file => file.RelativePath.StartsWith("mods/textures/battle/", StringComparison.OrdinalIgnoreCase)))
            {
                unique.TryAdd(file.SourcePath, file);
            }
        }
        Assert.NotEmpty(unique);
        Dictionary<string, IReadOnlyList<TextureBuildContext>> contexts = new(StringComparer.OrdinalIgnoreCase);
        int consumed = 0;
        int unused = 0;
        List<string> failures = [];
        foreach (JunctionReplacement file in unique.Values)
        {
            try
            {
                ModFile replacement = File(role, file);
                DemasterInspection inspection = FfnxTextureResolver.Inspect(source.Descriptor, new(1, [], [replacement]), source);
                if (inspection.Diagnostics.Count > 0)
                {
                    Assert.Equal("ffnx-unused-native-target", Assert.Single(inspection.Diagnostics).Code);
                    Console.WriteLine($"junction-unused={file.SourcePath}");
                    unused++;
                    continue;
                }
                string target = Path.GetFileNameWithoutExtension(file.RelativePath);
                int separator = target.LastIndexOf('-');
                Assert.True(separator > 0, target);
                string owner = target[..separator].ToLowerInvariant();
                if (!contexts.TryGetValue(owner, out IReadOnlyList<TextureBuildContext>? native))
                {
                    List<TextureBuildContext> captured = [];
                    ResourceService resources = new(source);
                    resources.Prepare(new Recorder(captured)).Commit();
                    using BattleAssets assets = new(game, resources);
                    if (owner.EndsWith(".x", StringComparison.Ordinal))
                    {
                        assets.LoadStage(int.Parse(owner.AsSpan(5, 3), CultureInfo.InvariantCulture));
                    }
                    else if (Regex.IsMatch(owner, @"^d[0-9a-f]w[0-9]{3}\.dat$"))
                    {
                        int character = int.Parse(owner.AsSpan(1, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        assets.LoadWeapon(character, int.Parse(owner.AsSpan(3, 3), CultureInfo.InvariantCulture), assets.LoadPlayer(character));
                    }
                    else
                    {
                        assets.LoadModel(owner);
                    }
                    native = captured.Where(context => context.Key.Owner.Path.Equals("battle/" + owner, StringComparison.OrdinalIgnoreCase)).ToArray();
                    Assert.NotEmpty(native);
                    contexts.Add(owner, native);
                }
                FfnxTextureResolver resolver = new(new(1, [], [replacement]));
                bool reached = false;
                foreach (TextureBuildContext context in native)
                {
                    if (resolver.Resolve(context) is { } resolved)
                    {
                        Assert.Contains(file.RelativePath, resolved.Provenance, StringComparison.OrdinalIgnoreCase);
                        Assert.True(resolved.Image.Width > 0 && resolved.Image.Height > 0);
                        reached = true;
                    }
                }
                Assert.True(reached, $"No actual native consumer context resolved '{file.SourcePath}'.");
                consumed++;
            }
            catch (Exception exception)
            {
                string failure = $"{file.SourcePath}: {exception.Message}";
                failures.Add(failure);
                Console.WriteLine($"junction-native-failure={failure}");
            }
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        Assert.True(consumed > 0);
        Console.WriteLine($"junction-battle-native-consumed={consumed} unused={unused} owners={contexts.Count} fixture={role}");
    }

    [Fact]
    public void OptionalOriginalFieldPagesFollowActualRegistrationAndCopyHistory()
    {
        (string archive, string game) = Fixture("junction-battle-enemies");
        JunctionModPackage package = JunctionModImporter.Open(archive);
        JunctionReplacement[] pageFiles = package.Files.Where(file => Regex.IsMatch(file.RelativePath, @"/field/model/(main|second)_chr/(?:[a-z0-9]{1,4}-[0-9]+_(?:f[0-9]+_)?){2,7}[0-9]+_[0-9]+_[0-9]+\.dds$", RegexOptions.IgnoreCase)).ToArray();
        Assert.NotEmpty(pageFiles);
        HashSet<string> names = pageFiles.SelectMany(file => Regex.Matches(Path.GetFileName(file.RelativePath), @"([a-z0-9]{1,4})-[0-9]+", RegexOptions.IgnoreCase).Select(match => match.Groups[1].Value)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        using ClassicGameDataSource source = new(game);
        ResourceService resources = new(source);
        ModFile[] replacements = Files("junction-battle-enemies", package);
        resources.Prepare(new FfnxTextureResolver(new(1, [], replacements))).Commit();
        List<Exception> failures = [];
        resources.ReplacementFailed += (_, exception) => failures.Add(exception);
        using GameAssets assets = new(game, resources: resources);
        ModelCatalogEntry[] catalog = assets.EnumerateModelCatalog().Where(entry => entry.Category == "Field" && entry.LoadError is null).ToArray();
        IGrouping<int?, ModelCatalogEntry> selected = catalog.GroupBy(entry => entry.FieldId).First(group => names.All(name => group.Any(entry => entry.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase))));
        FieldAssets field = assets.LoadField(selected.Key!.Value);
        int[] models = Enumerable.Range(0, field.Models.Count).Where(index => names.Contains(field.Models[index].TextureResourceName ?? "")).ToArray();
        Assert.Equal(names.Count, models.Length);
        int groups = field.Jsm[0] + field.Jsm[1] + field.Jsm[2] + field.Jsm[3];
        ushort[] scripts = Enumerable.Range(0, groups).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(field.Jsm.AsSpan(8 + index * 2))).OrderBy(group => group >> 7).ToArray();
        NativeFieldRetainedActor[] actors = models.Select((model, index) => new NativeFieldRetainedActor(index, model, scripts[index] >> 7, (scripts[index] >> 7) + (scripts[index] & 127) + 1)).ToArray();
        NativeFieldRegistrationStep registered = NativeFieldRegistrationMachine.ReloadKind3(field.NativeRegistration!, actors, NativeBattleRetainedMemory.FromColdProcess());
        NativeBattleRetainedMemory memory = registered.Memory;
        HashSet<string> resolved = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> rendered = new(StringComparer.OrdinalIgnoreCase);
        for (int first = 0; first < models.Length; first++)
        {
            for (int second = 0; second < models.Length; second++)
            {
                foreach ((int actor, uint mode) in new[] { (first, 0x100000u), (first, 0x200000u), (second, 0x100000u), (second, 0x200000u) })
                {
                    memory = NativeFieldRegistrationMachine.SetEyes532340(registered.State, memory, actor, mode, null, null).Memory;
                    FieldAssets visible = assets.BindNativeRegisteredTextures(field, registered.State, memory.EffectMemory!.Textures!, memory.FieldTextureAnimations);
                    foreach (int model in models)
                    {
                        ResourceTextureList textures = Assert.IsType<ResourceTextureList>(visible.Models[model].Textures);
                        for (int texture = 0; texture < textures.Count; texture++)
                        {
                            string provenance = textures.GetTexture(texture).Provenance;
                            foreach (JunctionReplacement file in pageFiles.Where(file => provenance.Contains(file.RelativePath, StringComparison.OrdinalIgnoreCase)))
                            {
                                resolved.Add(file.RelativePath);
                                string key = file.RelativePath + "|" + model + "|" + texture;
                                rendered.TryAdd(key, RenderRegisteredTexture(visible.Models[model], textures, texture));
                            }
                        }
                    }
                }
            }
        }
        Console.WriteLine($"junction-field-pages={resolved.Count}/{pageFiles.Length} field={field.Id} models={string.Join(',', models)}");
        Assert.Empty(failures);
        Assert.Equal(pageFiles.Length, resolved.Count);
        Assert.Contains(rendered, frame => frame.Key.Contains("_f0_", StringComparison.Ordinal) && rendered.TryGetValue(frame.Key.Replace("_f0_", "_f1_", StringComparison.Ordinal), out string? next) && frame.Value != next);
        NativeBattleRetainedMemory restored = JsonSerializer.Deserialize<NativeBattleRetainedMemory>(JsonSerializer.Serialize(memory))!;
        NativeBattleFieldResourceReloadResult continued = NativeFieldRegistrationMachine.SetEyes532340(registered.State, memory, 0, 0x100000, null, null);
        NativeBattleFieldResourceReloadResult independent = NativeFieldRegistrationMachine.SetEyes532340(registered.State, restored, 0, 0x100000, null, null);
        Assert.Equal(JsonSerializer.Serialize(continued.Memory.FieldTextureAnimations), JsonSerializer.Serialize(independent.Memory.FieldTextureAnimations));
    }

    private static string RenderRegisteredTexture(FieldModel source, IReadOnlyList<PixelImage> textures, int texture)
    {
        Vector2 size = ResourceTextureList.LogicalSize(textures, texture);
        Vector2 origin = Vector2.Zero;
        if (texture == 0 && source.NativeEyes?.Copy(false) is { } eyes)
        {
            origin = new(eyes.DestinationXWords * 2, eyes.DestinationY);
            size = new(eyes.WidthWords * 2, eyes.Height);
        }
        FieldModel quad = new("native-page-projection", [new(-1, 0)], [new(new Vector3(0, -300, -300), 0), new(new Vector3(0, -300, 300), 0), new(new Vector3(0, 300, -300), 0), new(new Vector3(0, 300, 300), 0)], [new([0, 1, 2, 3], [origin, origin + new Vector2(0, size.Y - 1), origin + new Vector2(size.X - 1, 0), origin + size - Vector2.One], texture, true)], source.Textures, [new([new(Vector3.Zero, [Vector3.Zero])])]);
        FieldAssets projection = new(0, "native-page-projection", [], [], [], new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100), new FieldWalkmesh([]), [quad]);
        FieldRenderer renderer = new(projection, modelTextureSampling: ModelTextureSampling.Nearest);
        ActorVisual visual = new(0, 0, 0, 0, 100, 192, 0, 0, true, PolygonColor: new Vector3(128), RegisteredTextures: textures);
        return Convert.ToHexString(SHA256.HashData(renderer.Render(new Dictionary<int, int>(), [visual], TimeSpan.Zero, backgroundVisible: false)));
    }

    private sealed class Recorder(List<TextureBuildContext> contexts) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            contexts.Add(context);
            return null;
        }
    }

    private static ModFile[] Files(string role, JunctionModPackage package) => package.Files.Where(file => file.RelativePath.StartsWith("mods/textures/", StringComparison.OrdinalIgnoreCase)).Select(file => File(role, file)).ToArray();

    private static ModFile File(string role, JunctionReplacement replacement) => new(role, ModFormat.Junction, replacement.RelativePath, 1, () =>
    {
        using MemoryStream output = new();
        replacement.CopyTo(output);
        return output.ToArray();
    });

    private static IEnumerable<Dictionary<string, int>> Selections(string archive)
    {
        JunctionArchive input = new(archive);
        using Stream manifest = input.Entries.Single(entry => entry.RelativePath.Equals("mod.xml", StringComparison.OrdinalIgnoreCase)).OpenRead();
        XElement root = XDocument.Load(manifest).Root!;
        IEnumerable<Dictionary<string, int>> combinations = [new(StringComparer.OrdinalIgnoreCase)];
        foreach (XElement option in root.Elements("ConfigOption"))
        {
            string id = option.Element("ID")!.Value;
            int[] values = option.Elements("Option").Select(value => int.Parse(value.Attribute("Value")!.Value, CultureInfo.InvariantCulture)).ToArray();
            combinations = combinations.SelectMany(previous => values.Select(value => new Dictionary<string, int>(previous, StringComparer.OrdinalIgnoreCase) { [id] = value })).ToArray();
        }
        return combinations;
    }

    private static (string Archive, string Game) Fixture(string role)
    {
        string? catalogPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_FIXTURE_CATALOG");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(System.IO.File.Exists(catalogPath) && Directory.Exists(game), "Configure optional external fixture catalog and original game data.");
        Dictionary<string, string> catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(System.IO.File.ReadAllText(catalogPath!))!;
        Assert.SkipUnless(catalog.TryGetValue(role, out string? configured), $"External fixture catalog has no '{role}' archive.");
        string fixtureRoot = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_FIXTURES") ?? Path.GetDirectoryName(Path.GetFullPath(catalogPath!))!;
        string archive = Path.GetFullPath(catalog[role], fixtureRoot);
        Assert.True(System.IO.File.Exists(archive), "Configured optional archive is unavailable.");
        return (archive, game!);
    }
}

