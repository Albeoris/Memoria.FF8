using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class JunctionMainCharacterResourceInstalledTests
{
    [Fact]
    public void SelectedLibraryReplacementsReachActualFieldSourcesMaterialsCatalogAndNativeRegistration()
    {
        using PackageFixture fixture = new();
        using GameResourceHost host = fixture.Open();
        using GameAssets assets = new(TestResources.GameDirectory, resources: host.Resources);
        Assert.DoesNotContain(host.Mods.Diagnostics, item => item.Code == "Junction.InvalidEncodedResource");
        Dictionary<string, ModelCatalogEntry> sources = [];
        foreach (ModelCatalogEntry entry in assets.EnumerateFieldModelSources())
        {
            string name = entry.Name.Split(' ')[0];
            if (entry.LoadError is null && name.Length >= 4 && fixture.Resources.ContainsKey(name[..4]))
            {
                if (!sources.TryGetValue(name[..4], out ModelCatalogEntry? previous) || entry.FieldId == 114 || previous.FieldId < 100 && entry.FieldId >= 100)
                {
                    sources[name[..4]] = entry;
                }
            }
        }
        Assert.NotEmpty(sources);
        string[] unreferenced = fixture.Resources.Keys.Except(sources.Keys).Order().ToArray();
        List<object> models = [];
        foreach ((string name, byte[] expected) in fixture.Resources)
        {
            ResourceKey key = new($"field/main_chr/{name}.mch");
            Assert.Equal(expected, host.Resources.Read(key));
            GameAssets.ValidateMainCharacterResource(host.Resources.Get(key));
            IReadOnlyList<Albeoris.Games.FF8.Imaging.PixelImage> materials = assets.LoadFieldMainCharacterTextures(name);
            Assert.NotEmpty(materials);
            if (!sources.ContainsKey(name))
            {
                models.Add(new { name, bytes = expected.Length, sha256 = Hash(expected), materials = materials.Count, geometryValidated = true, actualOneReference = false });
                continue;
            }
            FieldModel model = assets.LoadFieldModelSource(sources[name]);
            Assert.True(model.MainCharacterTextures);
            Assert.Equal(name, model.TextureResourceName);
            Assert.NotEmpty(model.Vertices);
            Assert.NotEmpty(model.Faces);
            Assert.NotEmpty(model.Animations);
            Assert.All(model.Animations.SelectMany(animation => animation.Frames), frame => Assert.Equal(model.Bones.Count, frame.Rotations.Count));
            Assert.Equal(model.Textures.Count, materials.Count);
            List<object> materialConsumers = [];
            for (int index = 0; index < materials.Count; index++)
            {
                TextureValue placedMaterial = Assert.IsType<ResourceTextureList>(model.Textures).GetTexture(index);
                TextureValue libraryMaterial = Assert.IsType<ResourceTextureList>(materials).GetTexture(index);
                Assert.True(model.Textures[index].Pixels.AsSpan().SequenceEqual(materials[index].Pixels), $"{name}/{index}: placed {placedMaterial.Provenance} ({placedMaterial.Image.Width}x{placedMaterial.Image.Height}) differs from library {libraryMaterial.Provenance} ({libraryMaterial.Image.Width}x{libraryMaterial.Image.Height}).");
                materialConsumers.Add(new { index, placed = placedMaterial.Provenance, library = libraryMaterial.Provenance, placedMaterial.LogicalWidth, placedMaterial.LogicalHeight, width = placedMaterial.Image.Width, height = placedMaterial.Image.Height });
            }
            EditableModel catalog = assets.LoadCatalogModel(sources[name]);
            Assert.Equal(model.Textures.Count, catalog.Textures.Count);
            Assert.NotEmpty(catalog.Meshes);
            Assert.NotEmpty(catalog.Animations);
            FieldAssets scene = assets.LoadField(sources[name].FieldId!.Value);
            FieldModel placed = scene.Models[sources[name].ModelIndex];
            Assert.Equal(Geometry(model), Geometry(placed));
            NativeFieldRegistrationResources registration = Assert.IsType<NativeFieldRegistrationResources>(scene.NativeRegistration);
            registration.Validate();
            NativeFieldModelSource retained = Assert.Single(registration.MainCharacters, source => source.Member == key.Path);
            Assert.Equal(expected, retained.Data.ToArray());
            Assert.Equal(Hash(expected), retained.Sha256);
            Assert.Equal(Hash(host.Resources.Read(new(sources[name].ResourcePath))), registration.One.Sha256);
            Assert.Equal(Hash(expected), model.NativeParts!.SourceHash);
            models.Add(new { name, source = sources[name], bytes = expected.Length, sha256 = Hash(expected), actualOneReference = true, vertices = model.Vertices.Count, faces = model.Faces.Count, bones = model.Bones.Count, animations = model.Animations.Count, materials = materials.Count, materialConsumers, one = registration.One.Sha256, geometry = Geometry(model) });
        }
        ModelCatalogEntry selected = sources.OrderBy(pair => pair.Key).First().Value;
        FieldAssets selectedScene = assets.LoadField(selected.FieldId!.Value);
        NativeFieldRegistrationResources native = selectedScene.NativeRegistration!;
        ushort group = BinaryPrimitives.ReadUInt16LittleEndian(selectedScene.Jsm.AsSpan(8, 2));
        NativeFieldRegistrationStep staged = NativeFieldRegistrationMachine.ReloadKind3(native, [new(0, selected.ModelIndex, group >> 7, (group >> 7) + (group & 127) + 1)], NativeBattleRetainedMemory.FromColdProcess());
        staged.State.Validate(staged.Memory);
        NativeFieldRegisteredResource resource = Assert.Single(staged.State.Resources);
        Assert.Equal(selectedScene.Models[selected.ModelIndex].NativeParts!.SourceHash, resource.SourceHash);
        Assert.Equal(selectedScene.Models[selected.ModelIndex].NativeParts!.SourceOffset, resource.SourceOffset);
        long generation = host.Resources.Generation;
        Assert.True(host.Mods.SetEnabled(fixture.PackageId, false).Success);
        Assert.False(host.Pump(true));
        Assert.Equal(generation, host.Resources.Generation);
        Assert.Contains(host.Mods.Diagnostics, item => item.Code == "resource-reload-required");
        foreach ((string name, byte[] expected) in fixture.Resources)
        {
            Assert.Equal(expected, host.Resources.Read(new($"field/main_chr/{name}.mch")));
        }
        using GameResourceHost disabledHost = fixture.Open();
        using GameAssets disabledAssets = new(TestResources.GameDirectory, resources: disabledHost.Resources);
        FieldModel original = disabledAssets.LoadFieldModelSource(selected);
        FieldModel enabled = assets.LoadFieldModelSource(selected);
        Assert.NotEqual(Geometry(original), Geometry(enabled));
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        var baseline = Capture(original, "original", gpu);
        var replacement = Capture(enabled, "enabled", gpu);
        Assert.NotEqual(baseline.Cpu, replacement.Cpu);
        Assert.NotEqual(baseline.Gpu, replacement.Gpu);
        using GameResourceHost disabledAgainHost = fixture.Open();
        using GameAssets disabledAgainAssets = new(TestResources.GameDirectory, resources: disabledAgainHost.Resources);
        Assert.Equal(baseline, Capture(disabledAgainAssets.LoadFieldModelSource(selected), "fresh-host-disabled", gpu));
        Assert.True(disabledHost.Mods.SetEnabled(fixture.PackageId, true).Success);
        Assert.False(disabledHost.Pump(true));
        using GameResourceHost restoredHost = fixture.Open();
        using GameAssets restoredAssets = new(TestResources.GameDirectory, resources: restoredHost.Resources);
        Assert.Equal(replacement, Capture(restoredAssets.LoadFieldModelSource(selected), "fresh-host-restored", gpu));
        Evidence("main-character-installed.json", new { edition = host.Resources.Source.Edition.ToString(), count = models.Count, fixture.PhysicalModelCount, fixture.OptionOverrides, fixture.SelectedFiles, actualOneReferences = sources.Count, unreferenced, models, selected, nativeStaging = new { resource.SourceHash, resource.SourceOffset, validated = true }, frozenAfterRead = true, original = baseline, enabled = replacement, freshHostDisabled = true, freshHostRestored = true, scope = "Fresh actual ONE placement and native cold registration components; no saved-state migration or gameplay continuation." });
    }

    [Fact]
    public void CorruptSelectedLibraryWinnerRejectsTheEntireInitialEncodedPublication()
    {
        using PackageFixture fixture = new();
        string name = fixture.Resources.Keys.First();
        File.WriteAllBytes(fixture.ResourcePaths[name], new byte[32]);
        using GameResourceHost host = fixture.Open();
        Assert.Contains(host.Mods.Diagnostics, item => item.Code == "Junction.InvalidEncodedResource");
        using IResourceSource original = GameDataSourceFactory.Open(TestResources.GameDirectory);
        ResourceService baseResources = new(original);
        foreach (string owner in fixture.Resources.Keys)
        {
            ResourceKey key = new($"field/main_chr/{owner}.mch");
            Assert.Equal(baseResources.Read(key), host.Resources.Read(key));
        }
        Evidence("main-character-corrupt-winner.json", new { rejected = name, baseFallbackCount = fixture.Resources.Count, partialPublication = false, diagnostics = host.Mods.Diagnostics });
    }

    private static (string Cpu, string Gpu) Capture(FieldModel model, string phase, GpuTriangleRenderer gpu)
    {
        Vector3[] pose = model.Pose(0, 0);
        Vector3 minimum = pose.Aggregate(Vector3.Min);
        Vector3 maximum = pose.Aggregate(Vector3.Max);
        Vector3 center = (minimum + maximum) * 0.5f;
        float radius = Vector3.Distance(minimum, maximum) * 0.5f;
        Assert.True(float.IsFinite(radius) && radius > 0);
        FieldCamera camera = new(Vector3.UnitX, -Vector3.UnitZ, Vector3.UnitY, new(-center.X, center.Z, radius * 3.4f - center.Y), Vector2.Zero, 256);
        FieldAssets field = new(100, "model-component", [], [], [], camera, new FieldWalkmesh([]), [model]);
        ActorVisual[] actors = [new(0, 0, 0, 0, 0, 0, 0, 0, true, PolygonColor: new Vector3(128))];
        FieldRenderer cpu = new(field, 640, 480);
        byte[] pixels = cpu.Render(new Dictionary<int, int>(), actors, TimeSpan.Zero).ToArray();
        FieldRenderer hardware = new(field, 640, 480) { TriangleRenderer = gpu };
        hardware.Render(new Dictionary<int, int>(), actors, TimeSpan.Zero);
        byte[] hardwarePixels = GpuTestDevice.Read(gpu.Target).SelectMany(pixel => new[] { pixel.R, pixel.G, pixel.B, pixel.A }).ToArray();
        if (EvidenceDirectory() is { } directory)
        {
            using var bitmap = GameUi.BattlePreviewCanvas.BitmapFromRgba(pixels, 640, 480);
            bitmap.Save(Path.Combine(directory, phase + "-cpu.png"));
            using FileStream output = File.Create(Path.Combine(directory, phase + "-gpu.png"));
            gpu.Target.SaveAsPng(output, 640, 480);
        }
        return (Hash(pixels), Hash(hardwarePixels));
    }

    private static string Geometry(FieldModel model) => Hash(JsonSerializer.SerializeToUtf8Bytes(new { model.Bones, model.Vertices, model.Faces }, new JsonSerializerOptions { IncludeFields = true }));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static string? EvidenceDirectory()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }
        string directory = Path.Combine(root, "MainCharacterResources");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void Evidence(string filename, object value)
    {
        if (EvidenceDirectory() is { } directory)
        {
            File.WriteAllText(Path.Combine(directory, filename), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
        }
    }

    private sealed class PackageFixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-library-resource-" + Guid.NewGuid().ToString("N"));
        public string PackageId { get; }
        public Dictionary<string, byte[]> Resources { get; } = [];
        public Dictionary<string, string> ResourcePaths { get; } = [];
        public Dictionary<string, int> OptionOverrides { get; } = [];
        public int PhysicalModelCount { get; private set; }
        public List<object> SelectedFiles { get; } = [];

        public PackageFixture()
        {
            string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_MAIN_CHARACTER_RESOURCE_PACKAGE");
            Assert.SkipUnless(Directory.Exists(input), "Set MEMORIA_FF8_MAIN_CHARACTER_RESOURCE_PACKAGE to an optional extracted Junction package directory.");
            string packageName = Path.GetFileName(Path.TrimEndingDirectorySeparator(input!));
            PackageId = "Junction/" + packageName;
            string destination = Path.Combine(root, "Junction", packageName);
            foreach (string path in Directory.EnumerateFiles(input!, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(input!, path);
                string copy = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.Copy(path, copy);
                if (Path.GetExtension(copy).Equals(".mch", StringComparison.OrdinalIgnoreCase))
                {
                    PhysicalModelCount++;
                }
            }
            string? overrides = Environment.GetEnvironmentVariable("MEMORIA_FF8_MAIN_CHARACTER_RESOURCE_OPTIONS");
            if (!string.IsNullOrWhiteSpace(overrides))
            {
                OptionOverrides = JsonSerializer.Deserialize<Dictionary<string, int>>(File.Exists(overrides) ? File.ReadAllText(overrides) : overrides) ?? throw new InvalidDataException("Expected a JSON object of declared option IDs and integer values.");
            }
            File.WriteAllLines(Path.Combine(destination, "mod.ini"), new[] { "enabled = 1" }.Concat(OptionOverrides.Select(option => $"option.{option.Key} = {option.Value}")));
            JunctionModPackage imported = JunctionModImporter.Open(destination, OptionOverrides);
            foreach (JunctionReplacement file in imported.Files)
            {
                if (JunctionEncodedResources.Address(file.RelativePath) is not ResourceKey key)
                {
                    continue;
                }
                string name = Path.GetFileNameWithoutExtension(key.Path);
                if (Resources.ContainsKey(name))
                {
                    continue;
                }
                using Stream inputStream = file.OpenRead();
                using MemoryStream bytes = new();
                inputStream.CopyTo(bytes);
                Resources.Add(name, bytes.ToArray());
                ResourcePaths.Add(name, Path.Combine(destination, file.SourcePath.Replace('/', Path.DirectorySeparatorChar)));
                SelectedFiles.Add(new { name, file.RelativePath, file.SourcePath, sha256 = Hash(bytes.ToArray()) });
            }
            Assert.NotEmpty(Resources);
        }

        public GameResourceHost Open() => new(TestResources.GameDirectory, watch: false, modsDirectory: root);

        public void Dispose() => Directory.Delete(root, true);
    }
}
